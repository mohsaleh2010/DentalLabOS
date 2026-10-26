namespace DentalLabOS.Application.Common;

/// <summary>Base for errors surfaced to users. Messages are localization keys.</summary>
public abstract class AppException(string messageKey) : Exception(messageKey)
{
    public string MessageKey { get; } = messageKey;
}

public sealed class ForbiddenException(string permission) : AppException("Errors.Forbidden")
{
    public string Permission { get; } = permission;
}

public sealed class NotFoundException(string entity) : AppException("Errors.NotFound")
{
    public string Entity { get; } = entity;
}

public sealed class ConcurrencyException() : AppException("Errors.Concurrency");

/// <summary>Field-level validation failures; keys are field names, values are localization keys.</summary>
public sealed class ValidationException(IReadOnlyDictionary<string, string[]> errors) : AppException("Errors.Validation")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;

    public static ValidationException For(string field, string messageKey) => new(new Dictionary<string, string[]> { [field] = [messageKey] });
}

/// <summary>Collects validation errors and throws once.</summary>
public sealed class Validator
{
    private readonly Dictionary<string, List<string>> _errors = new(StringComparer.Ordinal);

    public Validator Required(string field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Add(field, "Validation.Required");
        }

        return this;
    }

    public Validator MaxLength(string field, string? value, int max)
    {
        if (value is not null && value.Trim().Length > max)
        {
            Add(field, "Validation.TooLong");
        }

        return this;
    }

    public Validator Email(string field, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !System.Net.Mail.MailAddress.TryCreate(value.Trim(), out _))
        {
            Add(field, "Validation.Email");
        }

        return this;
    }

    public Validator Pattern(string field, string? value, string regex, string messageKey)
    {
        if (!string.IsNullOrWhiteSpace(value) && !System.Text.RegularExpressions.Regex.IsMatch(value.Trim(), regex))
        {
            Add(field, messageKey);
        }

        return this;
    }

    public Validator Range(string field, int value, int min, int max)
    {
        if (value < min || value > max)
        {
            Add(field, "Validation.Range");
        }

        return this;
    }

    public Validator Range(string field, decimal value, decimal min, decimal max)
    {
        if (value < min || value > max)
        {
            Add(field, "Validation.Range");
        }

        return this;
    }

    public Validator When(bool condition, string field, string messageKey)
    {
        if (condition)
        {
            Add(field, messageKey);
        }

        return this;
    }

    public void Add(string field, string messageKey)
    {
        if (!_errors.TryGetValue(field, out var list))
        {
            _errors[field] = list = [];
        }

        list.Add(messageKey);
    }

    public void ThrowIfInvalid()
    {
        if (_errors.Count > 0)
        {
            throw new ValidationException(_errors.ToDictionary(e => e.Key, e => e.Value.ToArray()));
        }
    }
}
