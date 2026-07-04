using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace InCleanHome.PaymentService.Infrastructure.ExternalServices.IamService;

/// <summary>
/// HTTP client to talk to IAM Service. Used to fetch a user's email for
/// MercadoPago checkout (the payer email).
/// </summary>
public interface IIamServiceClient
{
    Task<string?> GetUserEmailAsync(int userId, string bearerToken);
}

public class IamServiceClient(
    HttpClient http,
    IConfiguration configuration,
    ILogger<IamServiceClient> logger) : IIamServiceClient
{
    private string BaseUrl => configuration["Dependencies:IamServiceUrl"] ?? "http://iam-service:5001";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<string?> GetUserEmailAsync(int userId, string bearerToken)
    {
        try
        {
            // /api/auth/me returns the CURRENT user (whoever owns the bearer).
            // For payment-flow purposes, the caller is always the booking client.
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/api/v1/auth/me");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            using var resp = await http.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
            var id = json.TryGetProperty("id", out var idEl) ? idEl.GetInt32() : 0;
            if (id != userId) return null;

            return json.TryGetProperty("email", out var emailEl) ? emailEl.GetString() : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GetUserEmailAsync failed for user {Id}", userId);
            return null;
        }
    }
}
