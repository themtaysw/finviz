using Microsoft.AspNetCore.Diagnostics;

namespace Taxonomy.Api.Infrastructure;

/// <summary>
/// Keeps client disconnects out of the error logs: once the caller is gone there is nothing to respond to,
/// and the cancelled database query is expected, not a fault.
/// </summary>
internal sealed partial class RequestAbortedExceptionHandler(ILogger<RequestAbortedExceptionHandler> logger)
    : IExceptionHandler
{
    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not OperationCanceledException || !httpContext.RequestAborted.IsCancellationRequested)
        {
            return ValueTask.FromResult(false);
        }

        ClientCancelled(logger, httpContext.Request.Method, httpContext.Request.Path);
        httpContext.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;

        return ValueTask.FromResult(true);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Client cancelled {Method} {Path}.")]
    private static partial void ClientCancelled(ILogger logger, string method, PathString path);
}
