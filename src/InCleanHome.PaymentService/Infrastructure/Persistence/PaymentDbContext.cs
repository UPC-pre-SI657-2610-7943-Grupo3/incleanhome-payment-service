using EntityFrameworkCore.CreatedUpdatedDate.Extensions;
using InCleanHome.PaymentService.Domain.Model.Aggregates;
using InCleanHome.PaymentService.Infrastructure.Persistence.Extensions;
using Microsoft.EntityFrameworkCore;

namespace InCleanHome.PaymentService.Infrastructure.Persistence;

public class PaymentDbContext(DbContextOptions<PaymentDbContext> options) : DbContext(options)
{
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<ServicePayment> ServicePayments => Set<ServicePayment>();

    protected override void OnConfiguring(DbContextOptionsBuilder builder)
    {
        builder.AddCreatedUpdatedInterceptor();
        base.OnConfiguring(builder);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<PaymentMethod>().HasKey(p => p.Id);
        builder.Entity<PaymentMethod>().Property(p => p.Id).IsRequired().ValueGeneratedOnAdd();
        builder.Entity<PaymentMethod>().Property(p => p.UserId).IsRequired();
        builder.Entity<PaymentMethod>().Property(p => p.Type).IsRequired().HasMaxLength(40);
        builder.Entity<PaymentMethod>().Property(p => p.Label).HasMaxLength(120);
        builder.Entity<PaymentMethod>().Property(p => p.Details).HasMaxLength(500);
        builder.Entity<PaymentMethod>().HasIndex(p => p.UserId);

        builder.Entity<ServicePayment>().HasKey(p => p.Id);
        builder.Entity<ServicePayment>().Property(p => p.Id).IsRequired().ValueGeneratedOnAdd();
        builder.Entity<ServicePayment>().Property(p => p.BookingId).IsRequired();
        builder.Entity<ServicePayment>().Property(p => p.ClientId).IsRequired();
        builder.Entity<ServicePayment>().Property(p => p.WorkerId).IsRequired();
        builder.Entity<ServicePayment>().Property(p => p.Amount).HasPrecision(10, 2);
        builder.Entity<ServicePayment>().Property(p => p.PlatformFee).HasPrecision(10, 2);
        builder.Entity<ServicePayment>().Property(p => p.WorkerEarning).HasPrecision(10, 2);
        builder.Entity<ServicePayment>().Property(p => p.Channel).IsRequired().HasMaxLength(40);
        builder.Entity<ServicePayment>().Property(p => p.PayoutStatus).IsRequired().HasMaxLength(20);
        builder.Entity<ServicePayment>().Property(p => p.MercadoPagoPaymentId).HasMaxLength(80);
        builder.Entity<ServicePayment>().Property(p => p.MercadoPagoPreferenceId).HasMaxLength(80);
        builder.Entity<ServicePayment>().HasIndex(p => p.BookingId).IsUnique();
        builder.Entity<ServicePayment>().HasIndex(p => p.WorkerId);

        builder.UseSnakeCaseNamingConvention();
    }
}
