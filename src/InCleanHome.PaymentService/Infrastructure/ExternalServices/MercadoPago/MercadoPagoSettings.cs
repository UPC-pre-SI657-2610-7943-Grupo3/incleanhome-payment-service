namespace InCleanHome.PaymentService.Infrastructure.ExternalServices.MercadoPago;

/// <summary>
/// MercadoPago configuration. Read from Consul KV + env vars.
/// AccessToken is SECRET (env var only). PublicKey can live in Consul.
/// </summary>
public class MercadoPagoSettings
{
    public const string SectionName = "MercadoPago";

    public string AccessToken     { get; set; } = string.Empty;
    public string PublicKey       { get; set; } = string.Empty;
    public string FrontendBaseUrl { get; set; } = "http://localhost:5173";
    public string BaseApiUrl      { get; set; } = "https://api.mercadopago.com";

    public bool IsEnabled =>
        !string.IsNullOrWhiteSpace(AccessToken) && !string.IsNullOrWhiteSpace(PublicKey);
}
