namespace DentalLabOS.Domain.Common;

/// <summary>Value object for an amount in a currency with its rate to the tenant base currency.</summary>
public sealed class Money
{
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "EGP";
    public decimal ExchangeRate { get; set; } = 1m;

    public decimal BaseAmount => Math.Round(Amount * ExchangeRate, 2, MidpointRounding.AwayFromZero);
}
