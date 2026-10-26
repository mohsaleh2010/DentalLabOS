using DentalLabOS.Application.Common;
using DentalLabOS.Contracts;
using DentalLabOS.Domain.Common;
using DentalLabOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;

namespace DentalLabOS.Api.Infrastructure;

/// <summary>Maps application exceptions to RFC 9457 problem details with a stable <c>code</c> and localized title.</summary>
public sealed class ApiExceptionHandler(IProblemDetailsService problems, IStringLocalizer localizer, ILogger<ApiExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, code, key) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, ErrorCodes.Validation, "Errors.Validation"),
            NotFoundException => (StatusCodes.Status404NotFound, ErrorCodes.NotFound, "Errors.NotFound"),
            ForbiddenException or TenantIsolationException => (StatusCodes.Status403Forbidden, ErrorCodes.Forbidden, "Errors.Forbidden"),
            ConcurrencyException => (StatusCodes.Status409Conflict, ErrorCodes.Conflict, "Errors.Concurrency"),
            AppException app => (StatusCodes.Status422UnprocessableEntity, ErrorCodes.BusinessRule, app.MessageKey),
            DomainException domain => (StatusCodes.Status422UnprocessableEntity, ErrorCodes.BusinessRule, domain.MessageKey),
            _ => (0, string.Empty, string.Empty),
        };

        if (status == 0)
        {
            return false;
        }

        if (exception is TenantIsolationException)
        {
            logger.LogWarning(exception, "Blocked cross-tenant operation");
        }

        var details = new ProblemDetails { Status = status, Title = localizer[key], Type = $"https://dentallabos.app/errors/{code}" };
        details.Extensions["code"] = code;
        details.Extensions["traceId"] = context.TraceIdentifier;
        if (exception is ValidationException v)
        {
            details.Extensions["errors"] = v.Errors.ToDictionary(e => e.Key, e => e.Value.Select(m => new { key = m, message = localizer[m].Value }).ToArray());
        }

        context.Response.StatusCode = status;
        return await problems.TryWriteAsync(new() { HttpContext = context, ProblemDetails = details, Exception = exception });
    }
}
