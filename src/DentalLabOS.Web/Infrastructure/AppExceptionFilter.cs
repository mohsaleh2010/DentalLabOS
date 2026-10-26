using DentalLabOS.Application.Common;
using DentalLabOS.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace DentalLabOS.Web.Infrastructure;

/// <summary>Turns application exceptions that escape a page handler into the matching status page.</summary>
public sealed class AppExceptionFilter(ILogger<AppExceptionFilter> logger) : IAsyncPageFilter
{
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var executed = await next();
        if (executed.Exception is null || executed.ExceptionHandled)
        {
            return;
        }

        int? status = executed.Exception switch
        {
            ForbiddenException => StatusCodes.Status403Forbidden,
            NotFoundException => StatusCodes.Status404NotFound,
            TenantIsolationException => StatusCodes.Status403Forbidden,
            ConcurrencyException => StatusCodes.Status409Conflict,
            _ => null,
        };

        if (status is { } code)
        {
            if (executed.Exception is TenantIsolationException)
            {
                logger.LogWarning(executed.Exception, "Blocked cross-tenant operation");
            }

            executed.Result = new RedirectResult($"/error/{code}");
            executed.ExceptionHandled = true;
        }
    }
}
