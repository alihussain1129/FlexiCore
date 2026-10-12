namespace FlexiCore.SharedKernel;

/// <summary>Shared key under which the request correlation id is stored in HttpContext.Items.</summary>
public static class CorrelationContext
{
    public const string ItemKey = "FlexiCore.CorrelationId";
}