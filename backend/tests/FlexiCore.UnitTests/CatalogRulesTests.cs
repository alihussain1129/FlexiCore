using FlexiCore.Catalog;
using Xunit;

namespace FlexiCore.UnitTests;

public class CatalogRulesTests
{
    [Fact]
    public void A_valid_unit_has_no_errors() =>
        Assert.Empty(CatalogRules.ValidateUnit("KG", "Kilogram"));

    [Theory]
    [InlineData("", "Kilogram", "code")]
    [InlineData("-KG", "Kilogram", "code")]
    [InlineData("K G", "Kilogram", "code")]
    [InlineData("ABCDEFGHIJKLMNOPQRSTU", "Kilogram", "code")]
    [InlineData("KG", "", "name")]
    public void An_invalid_unit_reports_the_field(string code, string name, string field) =>
        Assert.Contains(field, CatalogRules.ValidateUnit(code, name).Keys);

    [Fact]
    public void A_unit_name_over_160_characters_is_rejected() =>
        Assert.Contains("name", CatalogRules.ValidateUnit("KG", new string('x', 161)).Keys);

    [Fact]
    public void A_unit_name_of_exactly_160_characters_is_accepted() =>
        Assert.Empty(CatalogRules.ValidateUnit("KG", new string('x', 160)));

    [Fact]
    public void A_valid_product_has_no_errors() =>
        Assert.Empty(CatalogRules.ValidateProduct("SKU-1", "5901234123457", "Milk 1L", 1.25m, 0.9m));

    [Fact]
    public void A_product_without_a_barcode_is_valid() =>
        Assert.Empty(CatalogRules.ValidateProduct("SKU-1", null, "Milk 1L", 0m, 0m));

    [Theory]
    [InlineData("", "Milk", "sku")]
    [InlineData("bad sku", "Milk", "sku")]
    [InlineData("SKU-1", "", "name")]
    public void An_invalid_product_reports_the_field(string sku, string name, string field) =>
        Assert.Contains(field, CatalogRules.ValidateProduct(sku, null, name, 1m, 1m).Keys);

    [Theory]
    [InlineData("123")]
    [InlineData("has space 1234")]
    [InlineData("bad_underscore_1234")]
    public void A_bad_barcode_is_rejected(string barcode) =>
        Assert.Contains("barcode", CatalogRules.ValidateProduct("SKU-1", barcode, "Milk", 1m, 1m).Keys);

    [Fact]
    public void A_negative_price_is_rejected()
    {
        var errors = CatalogRules.ValidateProduct("SKU-1", null, "Milk", -0.01m, -5m);
        Assert.Contains("salePrice", errors.Keys);
        Assert.Contains("costPrice", errors.Keys);
    }

    [Fact]
    public void More_than_four_decimal_places_is_rejected() =>
        Assert.Contains("salePrice", CatalogRules.ValidateProduct("SKU-1", null, "Milk", 1.00001m, 1m).Keys);

    [Fact]
    public void A_price_beyond_the_column_range_is_rejected() =>
        Assert.Contains("costPrice", CatalogRules.ValidateProduct("SKU-1", null, "Milk", 1m, 100_000_000_000_000m).Keys);

    [Fact]
    public void Clean_trims_and_optional_blank_becomes_null()
    {
        Assert.Equal("abc", CatalogRules.Clean("  abc "));
        Assert.Equal(string.Empty, CatalogRules.Clean(null));
        Assert.Null(CatalogRules.CleanOptional("   "));
        Assert.Equal("1234", CatalogRules.CleanOptional(" 1234 "));
    }

    [Fact]
    public void Paging_defaults_are_applied()
    {
        var errors = CatalogRules.ValidatePaging(null, null, out var limit, out var offset);
        Assert.Empty(errors);
        Assert.Equal(CatalogRules.DefaultLimit, limit);
        Assert.Equal(0, offset);
    }

    [Theory]
    [InlineData(0, 0, "limit")]
    [InlineData(101, 0, "limit")]
    [InlineData(10, -1, "offset")]
    [InlineData(10, 1_000_001, "offset")]
    public void Out_of_range_paging_is_rejected(int limit, int offset, string field) =>
        Assert.Contains(field, CatalogRules.ValidatePaging(limit, offset, out _, out _).Keys);

    [Fact]
    public void The_maximum_limit_is_accepted() =>
        Assert.Empty(CatalogRules.ValidatePaging(CatalogRules.MaxLimit, 0, out _, out _));

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("milk", "%milk%")]
    [InlineData("100%", "%100\\%%")]
    [InlineData("a_b", "%a\\_b%")]
    [InlineData("a\\b", "%a\\\\b%")]
    public void Search_terms_are_escaped_for_ILIKE(string? input, string? expected) =>
        Assert.Equal(expected, CatalogRules.ToLikePattern(input));

    [Fact]
    public void A_very_long_search_term_is_cut()
    {
        var pattern = CatalogRules.ToLikePattern(new string('a', 500))!;
        Assert.Equal(CatalogRules.SearchMaxLength + 2, pattern.Length);
    }
}