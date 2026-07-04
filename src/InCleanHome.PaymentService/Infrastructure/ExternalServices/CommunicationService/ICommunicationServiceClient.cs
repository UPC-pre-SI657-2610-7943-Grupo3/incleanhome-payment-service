using System.Text;
using System.Text.Json;

namespace InCleanHome.PaymentService.Infrastructure.ExternalServices.CommunicationService;

/// <summary>
///   HTTP client that lets Payment Service create in-app + push notifications
///   directly in Communication Service. See the booking-service equivalent for
///   the full rationale — this is the HTTP fallback path for when the broker
///   layer drops or delays events.
/// </summary>
public interface ICommunicationServiceClient
{
    Task<bool> CreateNotificationAsync(
        int userId,
        string type,
        string title,
        string body,
        string? link,
        string idempotencyKey);
}

public class CommunicationServiceClient(
    HttpClient http,
    IConfiguration configuration,
    ILogger<CommunicationServiceClient> logger) : ICommunicationServiceClient
{
    private string BaseUrl => configuration["Dependencies:CommunicationServiceUrl"]
                              ?? "http://communication-service:5005";

    private string? InternalToken => Environment.GetEnvironmentVariable("INTERNAL_SERVICE_TOKEN")
                                     ?? configuration["Internal:ServiceToken"];

    public async Task<bool> CreateNotificationAsync(
        int userId,
        string type,
        string title,
        string body,
        string? link,
        string idempotencyKey)
    {
        try
        {
            using var req = new HttpRequestMessage(
                HttpMethod.Post,
                $"{BaseUrl}/api/v1/internal/notifications");

            var token = InternalToken;
            if (!string.IsNullOrEmpty(token))
                req.Headers.Add("X-Internal-Token", token);

            var payload = new { userId, type, title, body, link, idempotencyKey };
            req.Content = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json");

            using var resp = await http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                var respBody = await resp.Content.ReadAsStringAsync();
                logger.LogWarning(
                    "[CommunicationServiceClient] {Status} for user {UserId}: {Body}",
                    resp.StatusCode, userId, respBody);
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "[CommunicationServiceClient] HTTP call to notify user {UserId} failed",
                userId);
            return false;
        }
    }
}
