using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using InCleanHome.PaymentService.Domain.Services.External;
using Microsoft.Extensions.Options;

namespace InCleanHome.PaymentService.Infrastructure.ExternalServices.MercadoPago;

/// <summary>
/// Concrete adapter for MercadoPago Perú implementing <see cref="IPaymentGatewayProvider"/>.
/// Only this class knows MP's REST API details: endpoints, payload shape, status mapping.
/// </summary>
public class MercadoPagoAdapter : IPaymentGatewayProvider
{
    private readonly HttpClient _httpClient;
    private readonly MercadoPagoSettings _settings;
    private readonly ILogger<MercadoPagoAdapter> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public MercadoPagoAdapter(
        HttpClient httpClient,
        IOptions<MercadoPagoSettings> settings,
        ILogger<MercadoPagoAdapter> logger)
    {
        _httpClient = httpClient;
        _settings   = settings.Value;
        _logger     = logger;

        if (!string.IsNullOrWhiteSpace(_settings.AccessToken))
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _settings.AccessToken);

        if (_httpClient.BaseAddress is null && !string.IsNullOrWhiteSpace(_settings.BaseApiUrl))
            _httpClient.BaseAddress = new Uri(_settings.BaseApiUrl);
    }

    public string GetPublicKey() => _settings.PublicKey;

    public async Task<CreatePaymentIntentResult> CreatePaymentIntentAsync(CreatePaymentIntentRequest request)
    {
        if (!_settings.IsEnabled)
            throw new InvalidOperationException(
                "Mercado Pago no está configurado. Verifica MERCADOPAGO_ACCESS_TOKEN y MERCADOPAGO_PUBLIC_KEY.");

        // auto_return = "approved" cannot be used with localhost back_urls.
        var isLocalhost = request.SuccessUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase)
                       || request.SuccessUrl.Contains("127.0.0.1");

        var payload = new Dictionary<string, object?>
        {
            ["items"] = new[]
            {
                new {
                    title       = request.Description,
                    quantity    = 1,
                    unit_price  = (double)request.Amount,
                    currency_id = "PEN",
                }
            },
            ["payer"] = new { email = request.PayerEmail },
            ["back_urls"] = new
            {
                success = request.SuccessUrl,
                failure = request.FailureUrl,
                pending = request.PendingUrl,
            },
            ["external_reference"]   = request.BookingId.ToString(),
            ["statement_descriptor"] = "InCleanHome",
        };
        if (!isLocalhost) payload["auto_return"] = "approved";

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/checkout/preferences", payload, JsonOpts);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogError("[MP] Preference creation failed ({StatusCode}): {Body}",
                    (int)response.StatusCode, errorBody);
                throw new InvalidOperationException(
                    $"Mercado Pago rechazó la preferencia ({(int)response.StatusCode}): {errorBody}");
            }
            var dto = await response.Content.ReadFromJsonAsync<MpPreferenceDto>(JsonOpts);
            if (dto is null || string.IsNullOrEmpty(dto.Id))
                throw new InvalidOperationException("Mercado Pago devolvió una preferencia inválida.");

            var isSandbox = _settings.AccessToken.StartsWith("TEST-", StringComparison.OrdinalIgnoreCase);
            var checkoutUrl = isSandbox ? (dto.SandboxInitPoint ?? dto.InitPoint) : (dto.InitPoint ?? dto.SandboxInitPoint);

            return new CreatePaymentIntentResult(dto.Id, checkoutUrl ?? string.Empty);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[MP] Error creando preferencia para booking {BookingId}", request.BookingId);
            throw new InvalidOperationException(
                "No se pudo iniciar el pago con Mercado Pago. Verifica que las credenciales sean válidas.", ex);
        }
    }

    public async Task<PaymentStatusResult> GetPaymentStatusAsync(string paymentId)
    {
        if (!_settings.IsEnabled)
            throw new InvalidOperationException("Mercado Pago no está configurado.");

        try
        {
            var response = await _httpClient.GetAsync($"/v1/payments/{paymentId}");
            response.EnsureSuccessStatusCode();
            var dto = await response.Content.ReadFromJsonAsync<MpPaymentDto>(JsonOpts);
            if (dto is null)
                throw new InvalidOperationException("Mercado Pago devolvió un pago inválido.");

            var normalized = dto.Status?.ToLowerInvariant() switch
            {
                "approved"     => "approved",
                "pending"      => "pending",
                "in_process"   => "pending",
                "authorized"   => "pending",
                "refunded"     => "refunded",
                "charged_back" => "refunded",
                _              => "rejected",
            };

            return new PaymentStatusResult(
                PaymentId: dto.Id?.ToString() ?? paymentId,
                Status:    normalized,
                Amount:    dto.TransactionAmount,
                ProviderRawStatus: dto.Status ?? "unknown");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[MP] Error consultando pago {PaymentId}", paymentId);
            throw new InvalidOperationException(
                "No se pudo consultar el estado del pago en Mercado Pago.", ex);
        }
    }

    public async Task<PaymentStatusResult?> FindApprovedPaymentByExternalReferenceAsync(string externalReference)
    {
        if (!_settings.IsEnabled)
            throw new InvalidOperationException("Mercado Pago no está configurado.");
        if (string.IsNullOrWhiteSpace(externalReference))
            return null;

        try
        {
            var url = $"/v1/payments/search?external_reference={Uri.EscapeDataString(externalReference)}";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("[MP] /payments/search returned {Status}: {Body}", (int)response.StatusCode, body);
                return null;
            }
            var dto = await response.Content.ReadFromJsonAsync<MpSearchDto>(JsonOpts);
            if (dto?.Results is null || dto.Results.Count == 0)
                return null;

            var approved = dto.Results.FirstOrDefault(p =>
                string.Equals(p.Status, "approved", StringComparison.OrdinalIgnoreCase));
            if (approved is null) return null;

            return new PaymentStatusResult(
                PaymentId: approved.Id?.ToString() ?? string.Empty,
                Status:    "approved",
                Amount:    approved.TransactionAmount,
                ProviderRawStatus: approved.Status ?? "approved");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[MP] Error buscando pagos por external_reference {Ref}", externalReference);
            return null;
        }
    }

    // Internal DTOs (only known by this adapter)
    private record MpPreferenceDto(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("init_point")] string? InitPoint,
        [property: JsonPropertyName("sandbox_init_point")] string? SandboxInitPoint);

    private record MpPaymentDto(
        [property: JsonPropertyName("id")] long? Id,
        [property: JsonPropertyName("status")] string? Status,
        [property: JsonPropertyName("transaction_amount")] decimal TransactionAmount);

    private record MpSearchDto(
        [property: JsonPropertyName("results")] List<MpPaymentDto>? Results);
}
