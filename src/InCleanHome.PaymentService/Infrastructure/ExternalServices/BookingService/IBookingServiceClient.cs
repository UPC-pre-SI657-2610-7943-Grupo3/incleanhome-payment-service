using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace InCleanHome.PaymentService.Infrastructure.ExternalServices.BookingService;

/// <summary>
/// HTTP client to talk to Booking Service. Used during payment confirmation
/// to validate the booking exists, belongs to the paying client, and is in
/// 'completed' status.
/// </summary>
public interface IBookingServiceClient
{
    Task<BookingSummary?> GetBookingAsync(int bookingId, string bearerToken);
}

public record BookingSummary(
    int Id,
    int ClientId,
    int WorkerId,
    decimal HourlyRate,
    decimal Hours,
    decimal TotalAmount,
    decimal PlatformFee,
    decimal WorkerEarning,
    string Status);

public class BookingServiceClient(
    HttpClient http,
    IConfiguration configuration,
    ILogger<BookingServiceClient> logger) : IBookingServiceClient
{
    private string BaseUrl => configuration["Dependencies:BookingServiceUrl"]
                              ?? "http://booking-service:5003";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<BookingSummary?> GetBookingAsync(int bookingId, string bearerToken)
    {
        try
        {
            // There is no GET /api/v1/bookings/{id} — only /receipt (which 400s for non-completed).
            // We get the receipt which works for completed bookings (the case that matters here).
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/api/v1/bookings/{bookingId}/receipt");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            using var resp = await http.SendAsync(req);

            if (!resp.IsSuccessStatusCode)
            {
                logger.LogWarning("GET booking {Id} -> {Status}", bookingId, resp.StatusCode);
                return null;
            }

            var json = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOptions);
            return new BookingSummary(
                Id:            json.TryGetProperty("id", out var id) ? id.GetInt32() : 0,
                ClientId:      json.TryGetProperty("clientId", out var cId) ? cId.GetInt32() : 0,
                WorkerId:      json.TryGetProperty("workerId", out var wId) ? wId.GetInt32() : 0,
                HourlyRate:    json.TryGetProperty("hourlyRate", out var hr) ? hr.GetDecimal() : 0m,
                Hours:         json.TryGetProperty("hours", out var hs) ? hs.GetDecimal() : 0m,
                TotalAmount:   json.TryGetProperty("totalAmount", out var amt) ? amt.GetDecimal() : 0m,
                PlatformFee:   json.TryGetProperty("platformFee", out var pf) ? pf.GetDecimal() : 0m,
                WorkerEarning: json.TryGetProperty("workerEarning", out var we) ? we.GetDecimal() : 0m,
                Status:        json.TryGetProperty("status", out var st) ? st.GetString() ?? "" : "");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GetBookingAsync failed for booking {Id}", bookingId);
            return null;
        }
    }
}
