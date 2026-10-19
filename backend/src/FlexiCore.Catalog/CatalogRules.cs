using System.Text;
using System.Text.RegularExpressions;

namespace FlexiCore.Catalog;

/// <summary>
/// Validation rules shared by the endpoints and the unit tests. The patterns mirror the CHECK constraints
/// in migration 0012, so the database is the last line of defence and these give friendly field errors first.
/// </summary>
public static class CatalogRules
{
    public const int NameMaxLength = 160;
    public const int DefaultLimit = 50;
    public const int MaxLimit = 100;
    public const int MaxOffset = 1_000_000;
    public const int SearchMaxLength = 100;
    public const decimal MaxPrice = 99_999_999_999_999.9999m;

    // \z (not $) so a trailing newline is not accepted.
    public static readonly Regex UnitCodePattern = new(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,19}\z", RegexOptions.CultureInvariant);
    public static readonly Regex SkuPattern = new(@"^[A-Za-z0-9][A-Za-z0-9._-]{0,39}\z", RegexOptions.CultureInvariant);
    public static readonly Regex BarcodePattern = new(@"^[A-Za-z0-9-]{4,64}\z", RegexOptions.CultureInvariant);

    public static string Clean(string? value) => value?.Trim() ?? string.Empty;

    public static string? CleanOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    public static Dictionary<string, string[]> ValidateUnit(string code, string name)
    {
        var errors = new Dictionary<string, List<string>>();
        if (code.Length == 0) Add(errors, "code", "Code is required.");
        else if (!UnitCodePattern.IsMatch(code)) Add(errors, "code", "Code must be 1 to 20 letters, digits, dots, dashes or underscores, starting with a letter or digit.");
        CheckName(errors, name);
        return Freeze(errors);
    }

    public static Dictionary<string, string[]> ValidateProduct(
        string sku, string? barcode, string name, decimal salePrice, decimal costPrice)
    {
        var errors = new Dictionary<string, List<string>>();
        if (sku.Length == 0) Add(errors, "sku", "SKU is required.");
        else if (!SkuPattern.IsMatch(sku)) Add(errors, "sku", "SKU must be 1 to 40 letters, digits, dots, dashes or underscores, starting with a letter or digit.");
        if (barcode is not null && !BarcodePattern.IsMatch(barcode)) Add(errors, "barcode", "Barcode must be 4 to 64 letters, digits or dashes.");
        CheckName(errors, name);
        CheckPrice(errors, "salePrice", salePrice);
        CheckPrice(errors, "costPrice", costPrice);
        return Freeze(errors);
    }

    public static Dictionary<string, string[]> ValidatePaging(
        int? limit, int? offset, out int effectiveLimit, out int effectiveOffset)
    {
        effectiveLimit = limit ?? DefaultLimit;
        effectiveOffset = offset ?? 0;
        var errors = new Dictionary<string, List<string>>();
        if (effectiveLimit < 1 || effectiveLimit > MaxLimit) Add(errors, "limit", $"Limit must be between 1 and {MaxLimit}.");
        if (effectiveOffset < 0 || effectiveOffset > MaxOffset) Add(errors, "offset", $"Offset must be between 0 and {MaxOffset}.");
        return Freeze(errors);
    }

    /// <summary>Turns a search term into an ILIKE pattern with LIKE wildcards escaped; null when the term is blank.</summary>
    public static string? ToLikePattern(string? search)
    {
        var term = search?.Trim();
        if (string.IsNullOrEmpty(term)) return null;
        if (term.Length > SearchMaxLength) term = term[..SearchMaxLength];

        var builder = new StringBuilder("%");
        foreach (var ch in term)
        {
            if (ch is '\\' or '%' or '_') builder.Append('\\');
            builder.Append(ch);
        }
        return builder.Append('%').ToString();
    }

    private static void CheckName(Dictionary<string, List<string>> errors, string name)
    {
        if (name.Length == 0) Add(errors, "name", "Name is required.");
        else if (name.Length > NameMaxLength) Add(errors, "name", $"Name must be at most {NameMaxLength} characters.");
    }

    private static void CheckPrice(Dictionary<string, List<string>> errors, string field, decimal value)
    {
        if (value < 0) Add(errors, field, "Price must not be negative.");
        else if (value > MaxPrice) Add(errors, field, "Price is too large.");
        else if (decimal.Round(value, 4) != value) Add(errors, field, "Price may have at most 4 decimal places.");
    }

    private static void Add(Dictionary<string, List<string>> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out var list)) errors[field] = list = [];
        list.Add(message);
    }

    private static Dictionary<string, string[]> Freeze(Dictionary<string, List<string>> errors) =>
        errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
}