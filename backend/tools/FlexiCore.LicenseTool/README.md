# FlexiCore.LicenseTool (owner only)

This tool holds the power to issue licenses. It is never shipped, never installed on a customer machine, and
nothing in the product references it. The customer application contains public keys only.

## What it produces

- Licenses in the exact format `LicenseVerifier` accepts: ES256 (ECDSA P-256 + SHA-256), signature as raw 64-byte
  r||s, base64url without padding, inside the `schemaVersion / keyId / payload / signature` envelope. The signed bytes
  are exactly the payload bytes inside the file.
- Public key sets in the exact shape `ProductionKeyLoader` parses (DER SubjectPublicKeyInfo, standard base64).
- Every license is verified with the real `LicenseVerifier` before it is written. If that fails, nothing is written.

## Private key storage

Keys are passphrase-encrypted PKCS#8 files (PBES2, AES-256, PBKDF2-SHA256, 600,000 iterations). The passphrase is
typed at a prompt and never comes from an argument, environment variable or pipe. `keygen` refuses any folder inside a
git working tree and warns about cloud-synced folders.

Rules:

1. Generate keys on the machine you use for signing, into a folder outside every repository.
2. Immediately make two encrypted copies on separate media kept in different places.
3. Keep the passphrase in a password manager and on paper, stored apart from the media.
4. Never e-mail, sync, commit or paste a key file or its passphrase.

The signer sits behind `ILicenseSigner`, so a cloud KMS or a hardware token can replace the file key later without
changing the license format.

## Two keys: active and standby

Generate two keys with different ids (for example `fc-2026-a` and `fc-2026-b`), keep them in different storage, and
embed both public keys in the product. Issue only with the active key.

- Active key lost: issue with the standby key. Existing licenses keep verifying.
- Active key leaked: there is no revocation. Ship an update that no longer trusts that key id. Every license it signed
  stops verifying, so re-issue them with the standby key.
- A leaked standby key is equally dangerous, so it gets the same protection.

## Commands

    FlexiCore.LicenseTool keygen --key-id fc-2026-a --out-dir E:\signing-keys
    FlexiCore.LicenseTool keygen --key-id fc-2026-b --out-dir F:\signing-keys-standby
    FlexiCore.LicenseTool build-keyset --out production-public-keys.json E:\signing-keys\fc-2026-a.public.json F:\signing-keys-standby\fc-2026-b.public.json
    FlexiCore.LicenseTool issue --key E:\signing-keys\fc-2026-a.p8 --license-id LIC-0001 --licensee "Customer Name" --expires 2027-01-01 --installations 1 --out customer.lic --ledger E:\signing-keys\issued.jsonl
    FlexiCore.LicenseTool verify customer.lic --public-keys production-public-keys.json
    FlexiCore.LicenseTool make-test-vectors --out <folder>     (test vectors only: throwaway key, never persisted)

The key id is the key file name without its extension. `--expires never` is required to make a perpetual license; the
tool will not default to one.

## Production key set

`build-keyset` writes the file that is embedded as `backend/src/FlexiCore.Licensing/Keys/production-public-keys.json`.
It is public. It refuses ids that start with `interop-test`, which are reserved for test vectors. Embedding it
changes the "no key" behaviour of Local mode, so the test that expects `unavailable` must be updated in the same change.

## Issue ledger

`--ledger` appends one JSON line per license (id, licensee, key id, dates, SHA-256 of the file). With no revocation it is
your only record of what was signed. It names customers, so the tool refuses a ledger inside a git working tree. Back it
up with the keys.

## Limits

- Signatures are randomized, so signing the same payload twice gives different bytes. Both verify.
- A signed license is not secret and not device-bound. See DECISIONS.md for the known limitations.