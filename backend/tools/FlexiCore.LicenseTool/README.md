# FlexiCore.LicenseTool (owner only)

This tool holds the power to issue licenses. It is never shipped, never installed on a customer machine, and
nothing in the product references it. The customer application contains public keys only.

## What it produces

- Licenses in the exact format `LicenseVerifier` accepts: ES256 (ECDSA P-256 + SHA-256), signature as raw 64-byte
  r||s, base64url without padding, inside the `schemaVersion / keyId / payload / signature` envelope. The signed bytes
  are exactly the payload bytes inside the file.
- Public key sets in the exact shape `ProductionKeyLoader` parses (DER SubjectPublicKeyInfo, standard base64).
- Every issued license must match a public key in the supplied ERP key set and verify with the real `LicenseVerifier` before it is written. A local signer check alone does not establish ERP trust; if either check fails, nothing is written.

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
- Active key leaked: rotate the signing authority and ship an updated trust set according to the key-retirement policy.
  Signed license revocations are separate from signing-key retirement and do not make a compromised signing key safe.
- A leaked standby key is equally dangerous, so it gets the same protection.

## Commands

    FlexiCore.LicenseTool keygen --key-id fc-2026-a --out-dir E:\signing-keys
    FlexiCore.LicenseTool keygen --key-id fc-2026-b --out-dir F:\signing-keys-standby
    FlexiCore.LicenseTool build-keyset --out production-public-keys.json E:\signing-keys\fc-2026-a.public.json F:\signing-keys-standby\fc-2026-b.public.json
    FlexiCore.LicenseTool provision-key --key E:\recovered\fc-2026-a.p8 --public-keys production-public-keys.json --out-dir E:\owner-signing
    FlexiCore.LicenseTool revocation-snapshot --key E:\owner-signing\fc-2026-a.p8 --public-keys production-public-keys.json --out initial-revocations.json
    FlexiCore.LicenseTool issue --key E:\signing-keys\fc-2026-a.p8 --public-keys production-public-keys.json --license-id LIC-0001 --licensee "Customer Name" --expires 2027-01-01 --installations 1 --out customer.lic --ledger E:\signing-keys\issued.jsonl
    FlexiCore.LicenseTool renew --key E:\owner-signing\fc-2026-a.p8 --public-keys production-public-keys.json --license previous.lic --revocations current-revocations.json --ledger E:\signing-keys\issued.jsonl --license-id LIC-0002 --expires 2028-01-01 --out renewed.lic
    FlexiCore.LicenseTool verify customer.lic --public-keys production-public-keys.json
    FlexiCore.LicenseTool make-test-vectors --out <folder>     (test vectors only: throwaway key, never persisted)

The key id is the key file name without its extension. For `issue`, `--public-keys` must name the exact key set embedded in the target ERP build; the signer id and public-key bytes must match one entry in that file. Build and ship the ERP trust set before issuing licenses for it. `--expires never` is required to make a perpetual license; the tool will not default to one.

`provision-key` imports an existing encrypted PKCS#8 key only after its decrypted public key exactly matches one entry
in the supplied authorized trust set. It copies the encrypted bytes to a new owner-only file outside every git working
tree and refuses to overwrite an existing key. It does not generate or replace production keys or alter the trust set.
The passphrase is typed interactively. It appends a key ID, public-key fingerprint, operator, and UTC timestamp to
`key-provisioning-audit.jsonl` beside the provisioned key; no private material or passphrase is logged.

`revocation-snapshot` creates an empty signed initial snapshot or a cumulative update. Updates use `--previous`; adding
a revocation requires both `--revoke-id` and `--reason`. Prior revocations cannot be removed or changed. The output
must be delivered to Local API instances through the authorized `PUT /api/v1/license/revocations` route. Only run
these commands with the private signer authorized for the exact public trust set shipped by the target ERP. Snapshot
format and offline behavior are described in `backend/src/FlexiCore.Licensing/REVOCATION_POLICY.md`.

`renew` is an owner-operated renewal. It requires the previous license file to verify under the authorized key set,
match its customer-bound issue-ledger hash and installation, have a valid customer/request history row, and not appear
in a current signed revocation snapshot. Expired licenses are eligible; perpetual, unbound, revoked, unrecorded, or
already-renewed licenses are refused. The successor retains the licensee, installation, grace period, and installation
limit, receives a new license ID and validity period, and is verified before output. The ledger records its predecessor,
customer ID, and local OS operator. This command is a local owner approval workflow, not customer self-service or an
ERP-backed renewal request; the private-key passphrase is the signing authorization.

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
