using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace CourtFlow.Application.Behaviors;

public sealed class RequestLoggingBehavior<TRequest, TResponse>(ILogger<RequestLoggingBehavior<TRequest, TResponse>> log)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var typeName = typeof(TRequest).Name;
        log.LogDebug("CourtFlow invoking {MediatrRequest}", typeName);
        var sw = Stopwatch.StartNew();
        try
        {
            return await next().ConfigureAwait(false);
        }
        finally
        {
            sw.Stop();
            log.LogDebug("CourtFlow completed {MediatrRequest} in {ElapsedMilliseconds} ms", typeName, sw.ElapsedMilliseconds);
        }
    }
}
