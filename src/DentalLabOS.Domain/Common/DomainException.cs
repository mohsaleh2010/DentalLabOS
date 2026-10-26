namespace DentalLabOS.Domain.Common;

/// <summary>A business rule violation. <see cref="Exception.Message"/> is a localization key.</summary>
public class DomainException(string messageKey) : Exception(messageKey)
{
    public string MessageKey { get; } = messageKey;
}
