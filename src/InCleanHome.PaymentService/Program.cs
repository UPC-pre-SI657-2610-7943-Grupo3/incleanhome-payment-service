using InCleanHome.PaymentService.Application.Internal.CommandServices;
using InCleanHome.PaymentService.Application.Internal.QueryServices;
using InCleanHome.PaymentService.Configuration;
using InCleanHome.PaymentService.Discovery;
using InCleanHome.PaymentService.Domain.Repositories;
using InCleanHome.PaymentService.Domain.Services;
using InCleanHome.PaymentService.Domain.Services.External;
using InCleanHome.PaymentService.Infrastructure.ExternalServices.BookingService;
using InCleanHome.PaymentService.Infrastructure.ExternalServices.IamService;
using InCleanHome.PaymentService.Infrastructure.ExternalServices.MercadoPago;
using InCleanHome.PaymentService.Infrastructure.Messaging.Consumers;
using InCleanHome.PaymentService.Infrastructure.Persistence;
using InCleanHome.PaymentService.Infrastructure.Persistence.Repositories;
using InCleanHome.PaymentService.Infrastructure.Pipeline;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().MinimumLevel.Information().CreateLogger();

try
{
    Log.Information("Starting InCleanHome Payment Service");

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    var consulAddress = Environment.GetEnvironmentVariable("CONSUL_HTTP_ADDR") ?? "http://consul:8500";
    var serviceName   = Environment.GetEnvironmentVariable("SERVICE_NAME") ?? "payment-service";
    var serviceHost   = Environment.GetEnvironmentVariable("SERVICE_HOST") ?? serviceName;
    var servicePort   = int.TryParse(Environment.GetEnvironmentVariable("SERVICE_PORT"), out var p) ? p : 5004;

    var dbConnection = Environment.GetEnvironmentVariable("PAYMENT_DB_CONNECTION")
                       ?? builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? throw new InvalidOperationException("PAYMENT_DB_CONNECTION env var is required.");

    var rabbitMqUrl = Environment.GetEnvironmentVariable("RABBITMQ_URL") ?? string.Empty;
    var rabbitMqEnabled = !string.IsNullOrWhiteSpace(rabbitMqUrl)
                         && !rabbitMqUrl.Contains("placeholder", StringComparison.OrdinalIgnoreCase);

    var mpAccessToken = Environment.GetEnvironmentVariable("MERCADOPAGO_ACCESS_TOKEN") ?? string.Empty;
    var mpPublicKey   = Environment.GetEnvironmentVariable("MERCADOPAGO_PUBLIC_KEY") ?? string.Empty;

    var loadedFromConsul = await ConsulConfigurationLoader.LoadFromConsulAsync(
        builder.Configuration, consulAddress, serviceName);

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddHttpContextAccessor();

    builder.Services.AddSwaggerGen(opts =>
    {
        opts.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "InCleanHome Payment Service", Version = "v1",
            Description = "Payments (ServicePayment + PaymentMethod) + MercadoPago gateway"
        });
        opts.EnableAnnotations();
        opts.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Description = "JWT Authorization header. Example: 'Bearer eyJhbGciOi...'",
            Name = "Authorization", In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey, Scheme = "Bearer"
        });
        opts.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            { new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }, Array.Empty<string>() }
        });
    });

    builder.Services.AddDbContext<PaymentDbContext>(opts => opts.UseNpgsql(dbConnection));

    builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
    builder.Services.AddScoped<IPaymentMethodRepository, PaymentMethodRepository>();
    builder.Services.AddScoped<IServicePaymentRepository, ServicePaymentRepository>();

    builder.Services.AddScoped<IPaymentMethodCommandService, PaymentMethodCommandService>();
    builder.Services.AddScoped<IPaymentMethodQueryService, PaymentMethodQueryService>();
    builder.Services.AddScoped<IServicePaymentCommandService, ServicePaymentCommandService>();
    builder.Services.AddScoped<IServicePaymentQueryService, ServicePaymentQueryService>();

    // MercadoPago — settings come from env vars (secret) + Consul (URLs)
    builder.Services.Configure<MercadoPagoSettings>(opts =>
    {
        builder.Configuration.GetSection("MercadoPago").Bind(opts);
        // Env vars override config (secrets)
        if (!string.IsNullOrWhiteSpace(mpAccessToken)) opts.AccessToken = mpAccessToken;
        if (!string.IsNullOrWhiteSpace(mpPublicKey))   opts.PublicKey   = mpPublicKey;
    });
    builder.Services.AddHttpClient<IPaymentGatewayProvider, MercadoPagoAdapter>(c =>
    {
        c.Timeout = TimeSpan.FromSeconds(20);
    });

    // External service HTTP clients
    builder.Services.AddHttpClient<IBookingServiceClient, BookingServiceClient>(c => c.Timeout = TimeSpan.FromSeconds(15));
    builder.Services.AddHttpClient<IIamServiceClient, IamServiceClient>(c => c.Timeout = TimeSpan.FromSeconds(15));

    // MassTransit + RabbitMQ
    builder.Services.AddMassTransit(x =>
    {
        x.AddConsumer<BookingCompletedConsumer>();

        if (rabbitMqEnabled)
            x.UsingRabbitMq((context, cfg) => { cfg.Host(new Uri(rabbitMqUrl)); cfg.ConfigureEndpoints(context); });
        else
            x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
    });

    var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? new[] { "http://localhost:8080" };
    builder.Services.AddCors(opts => opts.AddDefaultPolicy(
        p => p.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

    var registrationOptions = new ConsulRegistrationOptions
    {
        ConsulAddress = consulAddress, ServiceName = serviceName,
        ServiceId = $"{serviceName}-{Environment.MachineName}",
        Host = serviceHost, Port = servicePort,
        Tags = new[] { "payment", "dotnet" },
        HealthCheckUrl = $"http://{serviceHost}:{servicePort}/health"
    };
    builder.Services.AddSingleton(Options.Create(registrationOptions));
    builder.Services.AddHttpClient<ConsulServiceRegistration>(c => c.Timeout = TimeSpan.FromSeconds(10));
    builder.Services.AddHostedService<ConsulRegistrationHostedService>();

    builder.Services.AddHealthChecks().AddDbContextCheck<PaymentDbContext>("payment-db");

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        await db.Database.EnsureCreatedAsync();
        Log.Information("Database schema ensured.");
    }

    app.UseSerilogRequestLogging();
    app.UseCors();
    app.MapHealthChecks("/health");
    app.MapGet("/", () => Results.Ok(new
    {
        service = serviceName, status = "running",
        configSource = loadedFromConsul ? "consul" : "appsettings.json",
        broker = rabbitMqEnabled ? "configured" : "disabled",
        mercadoPago = !string.IsNullOrWhiteSpace(mpAccessToken) ? "configured" : "disabled"
    }));
    app.UseSwagger();
    app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", "Payment Service v1"); c.RoutePrefix = "swagger"; });
    app.UseJwtAuth();
    app.MapControllers();

    Log.Information("InCleanHome Payment Service ready on port {Port}", servicePort);
    await app.RunAsync();
}
catch (Exception ex) { Log.Fatal(ex, "Payment Service terminated unexpectedly"); throw; }
finally { Log.CloseAndFlush(); }
