using System.Security.Claims;
using FlexiCore.Catalog.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace FlexiCore.Catalog.Endpoints;

public sealed record UnitBody(Guid? Id, string? Code, string? Name, int? Version);

public sealed record ProductBody(
    Guid? Id, string? Sku, string? Barcode, string? Name, Guid? UnitId, decimal? SalePrice, decimal? CostPrice, int? Version);

/// <summary>
/// Catalog endpoints. Users only: the Bearer scheme is named explicitly, so a paired device cannot reach
/// them until the sync slice grants it access on purpose. Inputs are checked against CatalogRules, which
/// mirror the database CHECK constraints. The store returns outcomes that become 400, 403, 404 or 409.
/// </summary>
internal static class CatalogEndpoints
{
    private const string BearerScheme = "Bearer"; // the JWT scheme registered by Identity
    private const string CorrelationHeader = "X-Correlation-Id";

    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/companies/{companyId:guid}/catalog")
            .RequireAuthorization(policy => policy.AddAuthenticationSchemes(BearerScheme).RequireAuthenticatedUser());

        group.MapGet("/units", ListUnits);
        group.MapPost("/units", CreateUnit);
        group.MapGet("/units/{id:guid}", GetUnit);
        group.MapPut("/units/{id:guid}", UpdateUnit);
        group.MapDelete("/units/{id:guid}", DeleteUnit);

        group.MapGet("/products", ListProducts);
        group.MapPost("/products", CreateProduct);
        group.MapGet("/products/{id:guid}", GetProduct);
        group.MapPut("/products/{id:guid}", UpdateProduct);
        group.MapDelete("/products/{id:guid}", DeleteProduct);
    }

    // ---------------------------------------------------------------- units

    private static async Task<IResult> ListUnits(
        Guid companyId, string? q, int? limit, int? offset, HttpContext http, CatalogStore store, CancellationToken ct)
    {
        if (UserId(http) is not { } userId) return Results.Unauthorized();
        var errors = CatalogRules.ValidatePaging(limit, offset, out var effectiveLimit, out var effectiveOffset);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var result = await store.ListUnitsAsync(
            userId, companyId, CatalogRules.ToLikePattern(q), effectiveLimit, effectiveOffset, Correlation(http), ct);
        return result.Outcome == CatalogOutcome.Ok ? Results.Ok(result.Value) : Failure(result);
    }

    private static async Task<IResult> GetUnit(
        Guid companyId, Guid id, HttpContext http, CatalogStore store, CancellationToken ct)
    {
        if (UserId(http) is not { } userId) return Results.Unauthorized();
        var result = await store.GetUnitAsync(userId, companyId, id, Correlation(http), ct);
        return result.Outcome == CatalogOutcome.Ok ? Results.Ok(result.Value) : Failure(result);
    }

    private static async Task<IResult> CreateUnit(
        Guid companyId, UnitBody body, HttpContext http, CatalogStore store, CancellationToken ct)
    {
        if (UserId(http) is not { } userId) return Results.Unauthorized();
        var code = CatalogRules.Clean(body.Code);
        var name = CatalogRules.Clean(body.Name);
        var errors = CatalogRules.ValidateUnit(code, name);
        if (body.Id is null || body.Id == Guid.Empty) errors["id"] = new[] { "An id (UUID) is required." };
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var id = body.Id!.Value;
        var result = await store.CreateUnitAsync(userId, companyId, id, new UnitInput(code, name), Correlation(http), ct);
        return result.Outcome switch
        {
            CatalogOutcome.Created => Results.Created(UnitUrl(companyId, id), result.Value),
            CatalogOutcome.Ok => Results.Ok(result.Value),
            _ => Failure(result)
        };
    }

    private static async Task<IResult> UpdateUnit(
        Guid companyId, Guid id, UnitBody body, HttpContext http, CatalogStore store, CancellationToken ct)
    {
        if (UserId(http) is not { } userId) return Results.Unauthorized();
        var code = CatalogRules.Clean(body.Code);
        var name = CatalogRules.Clean(body.Name);
        var errors = CatalogRules.ValidateUnit(code, name);
        if (body.Version is null || body.Version < 1) errors["version"] = new[] { "The current version is required." };
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var result = await store.UpdateUnitAsync(
            userId, companyId, id, body.Version!.Value, new UnitInput(code, name), Correlation(http), ct);
        return result.Outcome == CatalogOutcome.Ok ? Results.Ok(result.Value) : Failure(result);
    }

    private static async Task<IResult> DeleteUnit(
        Guid companyId, Guid id, HttpContext http, CatalogStore store, CancellationToken ct)
    {
        if (UserId(http) is not { } userId) return Results.Unauthorized();
        var result = await store.DeleteUnitAsync(userId, companyId, id, Correlation(http), ct);
        return result.Outcome == CatalogOutcome.Ok ? Results.NoContent() : Failure(result);
    }

    // ------------------------------------------------------------- products

    private static async Task<IResult> ListProducts(
        Guid companyId, string? q, int? limit, int? offset, HttpContext http, CatalogStore store, CancellationToken ct)
    {
        if (UserId(http) is not { } userId) return Results.Unauthorized();
        var errors = CatalogRules.ValidatePaging(limit, offset, out var effectiveLimit, out var effectiveOffset);
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var result = await store.ListProductsAsync(
            userId, companyId, CatalogRules.ToLikePattern(q), effectiveLimit, effectiveOffset, Correlation(http), ct);
        return result.Outcome == CatalogOutcome.Ok ? Results.Ok(result.Value) : Failure(result);
    }

    private static async Task<IResult> GetProduct(
        Guid companyId, Guid id, HttpContext http, CatalogStore store, CancellationToken ct)
    {
        if (UserId(http) is not { } userId) return Results.Unauthorized();
        var result = await store.GetProductAsync(userId, companyId, id, Correlation(http), ct);
        return result.Outcome == CatalogOutcome.Ok ? Results.Ok(result.Value) : Failure(result);
    }

    private static async Task<IResult> CreateProduct(
        Guid companyId, ProductBody body, HttpContext http, CatalogStore store, CancellationToken ct)
    {
        if (UserId(http) is not { } userId) return Results.Unauthorized();
        var input = ToInput(body, out var errors);
        if (body.Id is null || body.Id == Guid.Empty) errors["id"] = new[] { "An id (UUID) is required." };
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var id = body.Id!.Value;
        var result = await store.CreateProductAsync(userId, companyId, id, input, Correlation(http), ct);
        return result.Outcome switch
        {
            CatalogOutcome.Created => Results.Created(ProductUrl(companyId, id), result.Value),
            CatalogOutcome.Ok => Results.Ok(result.Value),
            _ => Failure(result)
        };
    }

    private static async Task<IResult> UpdateProduct(
        Guid companyId, Guid id, ProductBody body, HttpContext http, CatalogStore store, CancellationToken ct)
    {
        if (UserId(http) is not { } userId) return Results.Unauthorized();
        var input = ToInput(body, out var errors);
        if (body.Version is null || body.Version < 1) errors["version"] = new[] { "The current version is required." };
        if (errors.Count > 0) return Results.ValidationProblem(errors);

        var result = await store.UpdateProductAsync(userId, companyId, id, body.Version!.Value, input, Correlation(http), ct);
        return result.Outcome == CatalogOutcome.Ok ? Results.Ok(result.Value) : Failure(result);
    }

    private static async Task<IResult> DeleteProduct(
        Guid companyId, Guid id, HttpContext http, CatalogStore store, CancellationToken ct)
    {
        if (UserId(http) is not { } userId) return Results.Unauthorized();
        var result = await store.DeleteProductAsync(userId, companyId, id, Correlation(http), ct);
        return result.Outcome == CatalogOutcome.Ok ? Results.NoContent() : Failure(result);
    }

    // -------------------------------------------------------------- helpers

    private static ProductInput ToInput(ProductBody body, out Dictionary<string, string[]> errors)
    {
        var sku = CatalogRules.Clean(body.Sku);
        var barcode = CatalogRules.CleanOptional(body.Barcode);
        var name = CatalogRules.Clean(body.Name);
        var sale = body.SalePrice ?? 0m;
        var cost = body.CostPrice ?? 0m;
        errors = CatalogRules.ValidateProduct(sku, barcode, name, sale, cost);
        return new ProductInput(sku, barcode, name, body.UnitId, sale, cost);
    }

    private static Guid? UserId(HttpContext http)
    {
        var value = http.User.FindFirst("sub")?.Value ?? http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }

    private static string? Correlation(HttpContext http)
    {
        var value = http.Request.Headers[CorrelationHeader].FirstOrDefault()
            ?? http.Response.Headers[CorrelationHeader].FirstOrDefault()
            ?? http.TraceIdentifier;
        return string.IsNullOrWhiteSpace(value) ? null : (value.Length > 128 ? value[..128] : value);
    }

    private static string UnitUrl(Guid companyId, Guid id) => $"/api/v1/companies/{companyId}/catalog/units/{id}";

    private static string ProductUrl(Guid companyId, Guid id) => $"/api/v1/companies/{companyId}/catalog/products/{id}";

    private static IResult Failure<T>(CatalogResult<T> result) where T : class => result.Outcome switch
    {
        CatalogOutcome.NotFound => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found."),
        CatalogOutcome.Forbidden => Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Not permitted."),
        CatalogOutcome.Conflict => Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict."),
        CatalogOutcome.VersionConflict => Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "The record was changed since you read it.",
            extensions: new Dictionary<string, object?> { ["current"] = result.Value }),
        CatalogOutcome.Invalid => Results.ValidationProblem(result.Errors ?? new Dictionary<string, string[]>()),
        _ => Results.Problem(statusCode: StatusCodes.Status500InternalServerError)
    };
}