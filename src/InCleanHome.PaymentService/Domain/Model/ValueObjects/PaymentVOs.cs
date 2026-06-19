namespace InCleanHome.PaymentService.Domain.Model.ValueObjects;

public static class PaymentChannel
{
    public const string MercadoPago = "mercadopago";
    public const string Yape        = "yape";
    public const string Plin        = "plin";
    public const string Bank        = "bank_transfer";

    public static readonly string[] All = { MercadoPago, Yape, Plin, Bank };
    public static bool IsValid(string c) => All.Contains(c);
    public static bool IsGatewayMediated(string c) => c == MercadoPago;
}

public static class PaymentMethodType
{
    public const string MercadoPago  = "mercadopago";
    public const string Yape         = "yape";
    public const string Plin         = "plin";
    public const string BankTransfer = "bank_transfer";

    public static readonly string[] All = { MercadoPago, Yape, Plin, BankTransfer };
    public static bool IsValid(string t) => All.Contains(t);
}

public static class PayoutStatus
{
    public const string NotApplicable = "not_applicable";
    public const string Pending       = "pending";
    public const string Completed     = "completed";

    public static readonly string[] All = { NotApplicable, Pending, Completed };
    public static bool IsValid(string s) => All.Contains(s);
}
