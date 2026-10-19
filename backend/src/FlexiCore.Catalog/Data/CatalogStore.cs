using FlexiCore.Tenancy;
using Npgsql;
using NpgsqlTypes;

namespace FlexiCore.Catalog.Data;

public enum CatalogOutcome { Ok, Created, NotFound, Forbidden, Conflict, VersionConflict, Invalid }

public sealed record CatalogResult<T>(
    CatalogOutcome Outcome, T? Value = default, Dictionary<string, string[]>? Errors = null) where T : class;

public sealed record UnitRecord(
    Guid Id, Guid CompanyId, string Code, string Name, int Version,
    DateTimeOffset UpdatedAt, DateTimeOffset? DeletedAt, long ChangeSeq);

public sealed record ProductRecord(
    Guid Id, Guid CompanyId, string Sku, string? Barcode, string Name, Guid? UnitId,
    decimal SalePrice, decimal CostPrice, int Version,
    DateTimeOffset UpdatedAt, DateTimeOffset? DeletedAt, long ChangeSeq);

public sealed record UnitInput(string Code, string Name);

public sealed record ProductInput(
    string Sku, string? Barcode, string Name, Guid? UnitId, decimal SalePrice, decimal CostPrice);

/// <summary>
/// Catalog reads and writes. Every call runs inside a TenantScope, so row-level security sees the caller.
/// A caller with no access to the company gets NotFound for everything (nothing leaks); a plain member who
/// writes gets Forbidden. Successful writes are audited in the same transaction. Refusals write nothing.
/// </summary>
public sealed class CatalogStore(ITenantConnectionFactory factory, CatalogAuditWriter audit)
{
    private const string UnitColumns = "id, company_id, code, name, version, updated_at, deleted_at, change_seq";
    private const string ProductColumns =
        "id, company_id, sku, barcode, name, unit_id, sale_price, cost_price, version, updated_at, deleted_at, change_seq";

    // ---------------------------------------------------------------- units

    public async Task<CatalogResult<IReadOnlyList<UnitRecord>>> ListUnitsAsync(
        Guid userId, Guid companyId, string? likePattern, int limit, int offset, string? correlationId, CancellationToken ct)
    {
        await using var scope = await OpenAsync(userId, companyId, correlationId, ct);
        if (await GateAsync<IReadOnlyList<UnitRecord>>(scope, write: false, ct) is { } denied) return denied;

        await using var command = Cmd(scope,
            $"select {UnitColumns} from catalog.units " +
            "where company_id = @company and deleted_at is null " +
            "and (@q::text is null or code ilike @q escape '\\' or name ilike @q escape '\\') " +
            "order by name, id limit @limit offset @offset",
            Param("company", NpgsqlDbType.Uuid, companyId), Param("q", NpgsqlDbType.Text, likePattern),
            Param("limit", NpgsqlDbType.Integer, limit), Param("offset", NpgsqlDbType.Integer, offset));

        var list = new List<UnitRecord>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) list.Add(ReadUnit(reader));
        return new(CatalogOutcome.Ok, list);
    }

    public async Task<CatalogResult<UnitRecord>> GetUnitAsync(
        Guid userId, Guid companyId, Guid id, string? correlationId, CancellationToken ct)
    {
        await using var scope = await OpenAsync(userId, companyId, correlationId, ct);
        if (await GateAsync<UnitRecord>(scope, write: false, ct) is { } denied) return denied;

        var unit = await FindUnitAsync(scope, companyId, id, ct);
        return unit is null || unit.DeletedAt is not null ? new(CatalogOutcome.NotFound) : new(CatalogOutcome.Ok, unit);
    }

    public async Task<CatalogResult<UnitRecord>> CreateUnitAsync(
        Guid userId, Guid companyId, Guid id, UnitInput input, string? correlationId, CancellationToken ct)
    {
        await using var scope = await OpenAsync(userId, companyId, correlationId, ct);
        if (await GateAsync<UnitRecord>(scope, write: true, ct) is { } denied) return denied;

        var existing = await FindUnitAsync(scope, companyId, id, ct);
        if (existing is not null)
        {
            var same = existing.DeletedAt is null && existing.Code == input.Code && existing.Name == input.Name;
            return same ? new(CatalogOutcome.Ok, existing) : new(CatalogOutcome.Conflict);
        }

        try
        {
            await using var command = Cmd(scope,
                $"insert into catalog.units (id, company_id, code, name) values (@id, @company, @code, @name) returning {UnitColumns}",
                Param("id", NpgsqlDbType.Uuid, id), Param("company", NpgsqlDbType.Uuid, companyId),
                Param("code", NpgsqlDbType.Text, input.Code), Param("name", NpgsqlDbType.Text, input.Name));
            var created = (await ReadOneUnitAsync(command, ct))!;

            await audit.WriteSuccessAsync(scope,
                new("catalog.unit.create", userId, companyId, "catalog.unit", id, correlationId, created.Version), ct);
            await scope.Transaction.CommitAsync(ct);
            return new(CatalogOutcome.Created, created);
        }
        catch (PostgresException ex) when (DbError<UnitRecord>(ex) is { } mapped) { return mapped; }
    }

    public async Task<CatalogResult<UnitRecord>> UpdateUnitAsync(
        Guid userId, Guid companyId, Guid id, int expectedVersion, UnitInput input, string? correlationId, CancellationToken ct)
    {
        await using var scope = await OpenAsync(userId, companyId, correlationId, ct);
        if (await GateAsync<UnitRecord>(scope, write: true, ct) is { } denied) return denied;

        try
        {
            await using var command = Cmd(scope,
                "update catalog.units set code = @code, name = @name " +
                $"where id = @id and company_id = @company and version = @version and deleted_at is null returning {UnitColumns}",
                Param("id", NpgsqlDbType.Uuid, id), Param("company", NpgsqlDbType.Uuid, companyId),
                Param("version", NpgsqlDbType.Integer, expectedVersion),
                Param("code", NpgsqlDbType.Text, input.Code), Param("name", NpgsqlDbType.Text, input.Name));
            var updated = await ReadOneUnitAsync(command, ct);

            if (updated is null)
            {
                var current = await FindUnitAsync(scope, companyId, id, ct);
                return current is null || current.DeletedAt is not null
                    ? new(CatalogOutcome.NotFound)
                    : new(CatalogOutcome.VersionConflict, current);
            }

            await audit.WriteSuccessAsync(scope,
                new("catalog.unit.update", userId, companyId, "catalog.unit", id, correlationId, updated.Version), ct);
            await scope.Transaction.CommitAsync(ct);
            return new(CatalogOutcome.Ok, updated);
        }
        catch (PostgresException ex) when (DbError<UnitRecord>(ex) is { } mapped) { return mapped; }
    }

    public async Task<CatalogResult<UnitRecord>> DeleteUnitAsync(
        Guid userId, Guid companyId, Guid id, string? correlationId, CancellationToken ct)
    {
        await using var scope = await OpenAsync(userId, companyId, correlationId, ct);
        if (await GateAsync<UnitRecord>(scope, write: true, ct) is { } denied) return denied;

        var existing = await FindUnitAsync(scope, companyId, id, ct);
        if (existing is null) return new(CatalogOutcome.NotFound);
        if (existing.DeletedAt is not null) return new(CatalogOutcome.Ok, existing); // already deleted: nothing to write or audit

        await using (var inUse = Cmd(scope,
            "select 1 from catalog.products where company_id = @company and unit_id = @id and deleted_at is null limit 1",
            Param("company", NpgsqlDbType.Uuid, companyId), Param("id", NpgsqlDbType.Uuid, id)))
        {
            if (await inUse.ExecuteScalarAsync(ct) is not null) return new(CatalogOutcome.Conflict);
        }

        try
        {
            await using var command = Cmd(scope,
                "update catalog.units set deleted_at = now() " +
                $"where id = @id and company_id = @company and deleted_at is null returning {UnitColumns}",
                Param("id", NpgsqlDbType.Uuid, id), Param("company", NpgsqlDbType.Uuid, companyId));
            var deleted = await ReadOneUnitAsync(command, ct);
            if (deleted is null) return new(CatalogOutcome.NotFound);

            await audit.WriteSuccessAsync(scope,
                new("catalog.unit.delete", userId, companyId, "catalog.unit", id, correlationId, deleted.Version), ct);
            await scope.Transaction.CommitAsync(ct);
            return new(CatalogOutcome.Ok, deleted);
        }
        catch (PostgresException ex) when (DbError<UnitRecord>(ex) is { } mapped) { return mapped; }
    }

    // ------------------------------------------------------------- products

    public async Task<CatalogResult<IReadOnlyList<ProductRecord>>> ListProductsAsync(
        Guid userId, Guid companyId, string? likePattern, int limit, int offset, string? correlationId, CancellationToken ct)
    {
        await using var scope = await OpenAsync(userId, companyId, correlationId, ct);
        if (await GateAsync<IReadOnlyList<ProductRecord>>(scope, write: false, ct) is { } denied) return denied;

        await using var command = Cmd(scope,
            $"select {ProductColumns} from catalog.products " +
            "where company_id = @company and deleted_at is null " +
            "and (@q::text is null or sku ilike @q escape '\\' or name ilike @q escape '\\' or barcode ilike @q escape '\\') " +
            "order by name, id limit @limit offset @offset",
            Param("company", NpgsqlDbType.Uuid, companyId), Param("q", NpgsqlDbType.Text, likePattern),
            Param("limit", NpgsqlDbType.Integer, limit), Param("offset", NpgsqlDbType.Integer, offset));

        var list = new List<ProductRecord>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct)) list.Add(ReadProduct(reader));
        return new(CatalogOutcome.Ok, list);
    }

    public async Task<CatalogResult<ProductRecord>> GetProductAsync(
        Guid userId, Guid companyId, Guid id, string? correlationId, CancellationToken ct)
    {
        await using var scope = await OpenAsync(userId, companyId, correlationId, ct);
        if (await GateAsync<ProductRecord>(scope, write: false, ct) is { } denied) return denied;

        var product = await FindProductAsync(scope, companyId, id, ct);
        return product is null || product.DeletedAt is not null ? new(CatalogOutcome.NotFound) : new(CatalogOutcome.Ok, product);
    }

    public async Task<CatalogResult<ProductRecord>> CreateProductAsync(
        Guid userId, Guid companyId, Guid id, ProductInput input, string? correlationId, CancellationToken ct)
    {
        await using var scope = await OpenAsync(userId, companyId, correlationId, ct);
        if (await GateAsync<ProductRecord>(scope, write: true, ct) is { } denied) return denied;

        var existing = await FindProductAsync(scope, companyId, id, ct);
        if (existing is not null)
        {
            var same = existing.DeletedAt is null && existing.Sku == input.Sku && existing.Barcode == input.Barcode
                && existing.Name == input.Name && existing.UnitId == input.UnitId
                && existing.SalePrice == input.SalePrice && existing.CostPrice == input.CostPrice;
            return same ? new(CatalogOutcome.Ok, existing) : new(CatalogOutcome.Conflict);
        }

        if (input.UnitId is { } unitId && !await UnitExistsAsync(scope, companyId, unitId, ct)) return UnitNotFound();

        try
        {
            await using var command = Cmd(scope,
                "insert into catalog.products (id, company_id, sku, barcode, name, unit_id, sale_price, cost_price) " +
                $"values (@id, @company, @sku, @barcode, @name, @unit, @sale, @cost) returning {ProductColumns}",
                Param("id", NpgsqlDbType.Uuid, id), Param("company", NpgsqlDbType.Uuid, companyId),
                Param("sku", NpgsqlDbType.Text, input.Sku), Param("barcode", NpgsqlDbType.Text, input.Barcode),
                Param("name", NpgsqlDbType.Text, input.Name), Param("unit", NpgsqlDbType.Uuid, input.UnitId),
                Param("sale", NpgsqlDbType.Numeric, input.SalePrice), Param("cost", NpgsqlDbType.Numeric, input.CostPrice));
            var created = (await ReadOneProductAsync(command, ct))!;

            await audit.WriteSuccessAsync(scope,
                new("catalog.product.create", userId, companyId, "catalog.product", id, correlationId, created.Version), ct);
            await scope.Transaction.CommitAsync(ct);
            return new(CatalogOutcome.Created, created);
        }
        catch (PostgresException ex) when (DbError<ProductRecord>(ex) is { } mapped) { return mapped; }
    }

    public async Task<CatalogResult<ProductRecord>> UpdateProductAsync(
        Guid userId, Guid companyId, Guid id, int expectedVersion, ProductInput input, string? correlationId, CancellationToken ct)
    {
        await using var scope = await OpenAsync(userId, companyId, correlationId, ct);
        if (await GateAsync<ProductRecord>(scope, write: true, ct) is { } denied) return denied;

        if (input.UnitId is { } unitId && !await UnitExistsAsync(scope, companyId, unitId, ct)) return UnitNotFound();

        try
        {
            await using var command = Cmd(scope,
                "update catalog.products set sku = @sku, barcode = @barcode, name = @name, unit_id = @unit, " +
                "sale_price = @sale, cost_price = @cost " +
                $"where id = @id and company_id = @company and version = @version and deleted_at is null returning {ProductColumns}",
                Param("id", NpgsqlDbType.Uuid, id), Param("company", NpgsqlDbType.Uuid, companyId),
                Param("version", NpgsqlDbType.Integer, expectedVersion),
                Param("sku", NpgsqlDbType.Text, input.Sku), Param("barcode", NpgsqlDbType.Text, input.Barcode),
                Param("name", NpgsqlDbType.Text, input.Name), Param("unit", NpgsqlDbType.Uuid, input.UnitId),
                Param("sale", NpgsqlDbType.Numeric, input.SalePrice), Param("cost", NpgsqlDbType.Numeric, input.CostPrice));
            var updated = await ReadOneProductAsync(command, ct);

            if (updated is null)
            {
                var current = await FindProductAsync(scope, companyId, id, ct);
                return current is null || current.DeletedAt is not null
                    ? new(CatalogOutcome.NotFound)
                    : new(CatalogOutcome.VersionConflict, current);
            }

            await audit.WriteSuccessAsync(scope,
                new("catalog.product.update", userId, companyId, "catalog.product", id, correlationId, updated.Version), ct);
            await scope.Transaction.CommitAsync(ct);
            return new(CatalogOutcome.Ok, updated);
        }
        catch (PostgresException ex) when (DbError<ProductRecord>(ex) is { } mapped) { return mapped; }
    }

    public async Task<CatalogResult<ProductRecord>> DeleteProductAsync(
        Guid userId, Guid companyId, Guid id, string? correlationId, CancellationToken ct)
    {
        await using var scope = await OpenAsync(userId, companyId, correlationId, ct);
        if (await GateAsync<ProductRecord>(scope, write: true, ct) is { } denied) return denied;

        var existing = await FindProductAsync(scope, companyId, id, ct);
        if (existing is null) return new(CatalogOutcome.NotFound);
        if (existing.DeletedAt is not null) return new(CatalogOutcome.Ok, existing);

        try
        {
            await using var command = Cmd(scope,
                "update catalog.products set deleted_at = now() " +
                $"where id = @id and company_id = @company and deleted_at is null returning {ProductColumns}",
                Param("id", NpgsqlDbType.Uuid, id), Param("company", NpgsqlDbType.Uuid, companyId));
            var deleted = await ReadOneProductAsync(command, ct);
            if (deleted is null) return new(CatalogOutcome.NotFound);

            await audit.WriteSuccessAsync(scope,
                new("catalog.product.delete", userId, companyId, "catalog.product", id, correlationId, deleted.Version), ct);
            await scope.Transaction.CommitAsync(ct);
            return new(CatalogOutcome.Ok, deleted);
        }
        catch (PostgresException ex) when (DbError<ProductRecord>(ex) is { } mapped) { return mapped; }
    }

    // -------------------------------------------------------------- helpers

    private Task<TenantScope> OpenAsync(Guid userId, Guid companyId, string? correlationId, CancellationToken ct) =>
        factory.OpenAsync(CatalogTenantContext.Create(userId, companyId, correlationId), ct);

    private static NpgsqlParameter Param(string name, NpgsqlDbType type, object? value) =>
        new(name, type) { Value = value ?? DBNull.Value };

    private static NpgsqlCommand Cmd(TenantScope scope, string sql, params NpgsqlParameter[] parameters)
    {
        var command = new NpgsqlCommand(sql, scope.Connection, scope.Transaction);
        command.Parameters.AddRange(parameters);
        return command;
    }

    /// <summary>Null when allowed. NotFound when the caller has no active access to the company; Forbidden for a plain member who writes.</summary>
    private static async Task<CatalogResult<T>?> GateAsync<T>(TenantScope scope, bool write, CancellationToken ct) where T : class
    {
        await using var command = Cmd(scope, "select catalog.caller_role()");
        var role = await command.ExecuteScalarAsync(ct) as string;
        if (role is null) return new(CatalogOutcome.NotFound);
        if (write && role is not ("owner" or "admin")) return new(CatalogOutcome.Forbidden);
        return null;
    }

    private static CatalogResult<T>? DbError<T>(PostgresException ex) where T : class => ex.SqlState switch
    {
        PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.ForeignKeyViolation => new(CatalogOutcome.Conflict),
        PostgresErrorCodes.InsufficientPrivilege => new(CatalogOutcome.Forbidden),
        PostgresErrorCodes.CheckViolation => new(CatalogOutcome.Invalid, null,
            new Dictionary<string, string[]> { ["body"] = new[] { "A value was rejected by the database rules." } }),
        _ => null
    };

    private static CatalogResult<ProductRecord> UnitNotFound() =>
        new(CatalogOutcome.Invalid, null, new Dictionary<string, string[]> { ["unitId"] = new[] { "Unit not found." } });

    private static async Task<bool> UnitExistsAsync(TenantScope scope, Guid companyId, Guid unitId, CancellationToken ct)
    {
        await using var command = Cmd(scope,
            "select 1 from catalog.units where company_id = @company and id = @id and deleted_at is null",
            Param("company", NpgsqlDbType.Uuid, companyId), Param("id", NpgsqlDbType.Uuid, unitId));
        return await command.ExecuteScalarAsync(ct) is not null;
    }

    private static async Task<UnitRecord?> FindUnitAsync(TenantScope scope, Guid companyId, Guid id, CancellationToken ct)
    {
        await using var command = Cmd(scope,
            $"select {UnitColumns} from catalog.units where company_id = @company and id = @id",
            Param("company", NpgsqlDbType.Uuid, companyId), Param("id", NpgsqlDbType.Uuid, id));
        return await ReadOneUnitAsync(command, ct);
    }

    private static async Task<ProductRecord?> FindProductAsync(TenantScope scope, Guid companyId, Guid id, CancellationToken ct)
    {
        await using var command = Cmd(scope,
            $"select {ProductColumns} from catalog.products where company_id = @company and id = @id",
            Param("company", NpgsqlDbType.Uuid, companyId), Param("id", NpgsqlDbType.Uuid, id));
        return await ReadOneProductAsync(command, ct);
    }

    private static async Task<UnitRecord?> ReadOneUnitAsync(NpgsqlCommand command, CancellationToken ct)
    {
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadUnit(reader) : null;
    }

    private static async Task<ProductRecord?> ReadOneProductAsync(NpgsqlCommand command, CancellationToken ct)
    {
        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? ReadProduct(reader) : null;
    }

    private static UnitRecord ReadUnit(NpgsqlDataReader r) => new(
        r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.GetString(3), r.GetInt32(4),
        r.GetFieldValue<DateTimeOffset>(5), r.IsDBNull(6) ? null : r.GetFieldValue<DateTimeOffset>(6), r.GetInt64(7));

    private static ProductRecord ReadProduct(NpgsqlDataReader r) => new(
        r.GetGuid(0), r.GetGuid(1), r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3), r.GetString(4),
        r.IsDBNull(5) ? null : r.GetGuid(5), r.GetDecimal(6), r.GetDecimal(7), r.GetInt32(8),
        r.GetFieldValue<DateTimeOffset>(9), r.IsDBNull(10) ? null : r.GetFieldValue<DateTimeOffset>(10), r.GetInt64(11));
}