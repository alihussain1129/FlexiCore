# License interoperability vectors (TEST ONLY)

Public test data. There is no private key here: the signing key (`interop-test-1`) existed only in memory while the
files were generated and was discarded. `.gitattributes` turns off line-ending conversion so the bytes never change.
The key id `interop-test-1` is reserved for tests and must never appear in a production key set.

Generated once with:

    dotnet run --project tools/FlexiCore.LicenseTool -c Release -- make-test-vectors --out <this folder>

Signatures are randomized, so regenerating gives different bytes. `InteropVectorHashes.cs` pins every file, so a
change fails the tests until someone updates the hashes on purpose.

| File | Meaning | Status at 2026-06-01T00:00:00Z |
|---|---|---|
| interop-test-public-keys.json | public key set, the exact production loader format | n/a |
| valid.lic | LIC-VECTOR-VALID, 2026-01-01 to 2027-01-01 | Valid |
| expired.lic | LIC-VECTOR-EXPIRED, expires 2026-05-01 | Expired |
| wrong-product.lic | product is OtherProduct | WrongProduct |
| tampered.lic | valid.lic with graceDays changed 0 to 300, original signature | InvalidSignature |

## Independent check (optional, not part of the test run)

This verifies the signatures with a non-.NET implementation. Run it once from this folder after `pip install cryptography`.
Expected: valid, expired and wrong-product print "signature OK"; tampered prints "signature INVALID".

    import base64, json
    from cryptography.hazmat.primitives import hashes, serialization
    from cryptography.hazmat.primitives.asymmetric import ec
    from cryptography.hazmat.primitives.asymmetric.utils import encode_dss_signature

    def b64url(s): return base64.urlsafe_b64decode(s + "=" * (-len(s) % 4))

    keys = json.load(open("interop-test-public-keys.json"))["keys"]
    pub = serialization.load_der_public_key(base64.b64decode(keys[0]["publicKey"]))
    for name in ["valid", "expired", "wrong-product", "tampered"]:
        env = json.load(open(name + ".lic"))
        sig = b64url(env["signature"])
        assert len(sig) == 64
        der = encode_dss_signature(int.from_bytes(sig[:32], "big"), int.from_bytes(sig[32:], "big"))
        try:
            pub.verify(der, b64url(env["payload"]), ec.ECDSA(hashes.SHA256()))
            print(name, "signature OK")
        except Exception:
            print(name, "signature INVALID")