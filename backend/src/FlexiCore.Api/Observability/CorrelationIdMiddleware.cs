using FlexiCore.SharedKernel;

namespace FlexiCore.Api.Observability;

public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].ToString();
        // Accept a caller-supplied id only if it is short and plain; otherwise a client
        // could inject arbitrary text into our logs.
        var id = IsAcceptable(incoming) ? incoming : Guid.NewGuid().ToString("N");

        // Made available to modules (for example audit events) without them depending on the Api project.
        context.Items[CorrelationContext.ItemKey] = id;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = id;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = id }))
        {
            await next(context);
        }
    }

    private static bool IsAcceptable(string value) =>
        value.Length is > 0 and <= 64 &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}