namespace FlexiCore.UnitTests.LicensingTests;

// Pinned once, right after the vectors were generated. Regenerating the vectors changes these hashes on purpose:
// a person must update this file, so the vectors never change by accident.
internal static class InteropVectorHashes
{
    public static readonly IReadOnlyDictionary<string, string> Expected = new Dictionary<string, string>
    {
        ["interop-test-public-keys.json"] = "1de2fe72f51d6b332f365612eb3c4c2ff185c3b4c75c556a31b727124a7c7d93",
        ["valid.lic"] = "35d6933e87e236f358f0e9ffbdb7d7ef167dba7631243363eb650fcd606f834e",
        ["expired.lic"] = "f28f95998c046070a1d3a1ece79575720030960ae172ce1e735954e4e4cbc213",
        ["wrong-product.lic"] = "6b3a3cdb2e60703cdd6d6fa72f5479abc8267591cc058f75f62ee5d948dfb68c",
        ["tampered.lic"] = "397bd56d0ce4f44870087d5929c951a008082aaeab7230b486663dc6cfbb79aa",
    };
}
