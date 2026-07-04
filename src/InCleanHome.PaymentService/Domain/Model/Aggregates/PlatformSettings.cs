namespace InCleanHome.PaymentService.Domain.Model.Aggregates;

/// <summary>
/// Single-row aggregate that stores tunable platform-wide settings.
/// Currently only holds the commission percent applied to client payments,
/// but designed to be extensible (e.g. min booking amount, payout schedule).
/// </summary>
public class PlatformSettings
{
    public int Id { get; set; }                       // Always 1 (singleton row)
    public decimal CommissionPercent { get; set; }    // 0..100, e.g. 10 means 10%
    public DateTimeOffset CreatedDate { get; set; }
    public DateTimeOffset? UpdatedDate { get; set; }

    public PlatformSettings() { }

    public PlatformSettings(decimal commissionPercent)
    {
        Id = 1;
        CommissionPercent = commissionPercent;
    }

    public void UpdateCommission(decimal newPercent)
    {
        if (newPercent < 0 || newPercent > 100)
            throw new ArgumentOutOfRangeException(nameof(newPercent),
                "Commission percent must be between 0 and 100");
        CommissionPercent = newPercent;
    }
}
