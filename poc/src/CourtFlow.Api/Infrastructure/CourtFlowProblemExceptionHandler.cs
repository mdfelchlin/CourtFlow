using CourtFlow.Application.Scheduling;
using CourtFlow.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CourtFlow.Api.Infrastructure;

/// <summary>Maps common application exceptions into RFC 7807 problem responses.</summary>
public sealed class CourtFlowProblemExceptionHandler(ILogger<CourtFlowProblemExceptionHandler> log) : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        if (exception is ValidationException vx)
        {
            var dict = vx.Errors
                .GroupBy(e => string.IsNullOrWhiteSpace(e.PropertyName) ? "request" : e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct().ToArray());

            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            httpContext.Response.ContentType = "application/problem+json";
            var body = new ValidationProblemDetails(dict)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
                Detail = vx.Message
            };
            return Finish(httpContext, body, ct);
        }

        if (exception is EntityNotFoundException notFound)
            return PlainProblem(httpContext, StatusCodes.Status404NotFound, notFound, ct);

        if (exception is LifecycleViolationException lifecycle)
            return PlainProblem(httpContext, StatusCodes.Status409Conflict, lifecycle, ct);

        if (exception is ConcurrencyConflictException concurrency)
            return PlainProblem(httpContext, StatusCodes.Status409Conflict, concurrency, ct);

        if (exception is ForbiddenDomainException forbidden)
            return PlainProblem(httpContext, StatusCodes.Status403Forbidden, forbidden, ct);

        if (exception is StaleScheduleApplyException stale)
            return PlainProblem(httpContext, StatusCodes.Status409Conflict, stale, ct);

        if (exception is ScheduleApplyTokenInvalidException badTok)
            return PlainProblem(httpContext, StatusCodes.Status400BadRequest, badTok, ct);

        if (exception is ScheduleConflictAfterPreviewException cx)
            return PlainProblem(httpContext, StatusCodes.Status422UnprocessableEntity, cx, ct);

        log.LogDebug(exception, "No CourtFlow ProblemDetails mapping — falling through.");
        return new ValueTask<bool>(false);
    }

    private static async ValueTask<bool> PlainProblem(HttpContext ctx, int status, Exception ex, CancellationToken ct)
    {
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/problem+json";
        await ctx.Response.WriteAsJsonAsync(
            new ProblemDetails { Status = status, Title = ex.GetType().Name, Detail = ex.Message },
            cancellationToken: ct);
        return true;
    }

    private static async ValueTask<bool> Finish(HttpContext ctx, ProblemDetails pd, CancellationToken ct)
    {
        await ctx.Response.WriteAsJsonAsync(pd, cancellationToken: ct);
        return true;
    }
}
