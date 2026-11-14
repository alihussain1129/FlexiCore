# Production signing key: status and recovery

## What the ERP trusts

The ERP trusts only the public keys embedded in `backend/src/FlexiCore.Licensing/Keys/production-public-keys.json`.
A license, revocation snapshot or signed entitlement is accepted only if it carries one of these key ids and the
signature verifies. Format: ES256 (ECDSA P-256, SHA-256), raw 64-byte signature, Base64URL without padding.

| Key id | Fingerprint (first 16 hex of SHA-256 over the SubjectPublicKeyInfo) |
|---|---|
| `fc-2026-a` | `53844eef91a95d10` |
| `fc-2026-b` | `6df7f3fa54f9cde0` |
| `flexicore-owner-2026-01` | `36d72e03d36f4516` |

These values are pinned by the test `Embedded_production_trust_keeps_the_three_known_public_keys`.

## Status (verified 2026-11-14)

- The private keys for these three ids were **not found** on the owner's Linux machine, on mounted drives, or in
  Documents, Downloads or Desktop.
- A key named `fc-2026-a` exists in the local Activation Engine registry on that machine. Its fingerprint is
  `3038babc36c3dc92`, so it is a **different key** from the trusted `fc-2026-a`. The ERP does not trust it.
  It is marked "pending ERP" and the Activation Engine refuses to issue with it
  (test `Local_key_with_a_production_id_but_another_fingerprint_cannot_issue`).
  Never use it for customer licenses. Never rename it to make it match.
- All test-key verification (end-to-end tests) uses throwaway keys. **No license has been signed with a
  production key as part of this work.**

## Recovery option A: find the original key

1. Look for the encrypted private key backup (`.p8`, PEM header `BEGIN ENCRYPTED PRIVATE KEY`) of one of the three ids on
   the machine or offline media where the keys were generated, and for its passphrase.
2. Import it with the LicenseTool `provision-key` command (see `backend/tools/FlexiCore.LicenseTool/README.md`),
   passing the production key set. The command requires an exact public-key match with the trusted entry and refuses
   to overwrite an existing key.
3. Before issuing anything, check that the imported key's fingerprint equals the table above.
4. Issue a first test license and verify it with the embedded production keys.

## Recovery option B: the original keys are gone

Licenses signed by a lost key can never be re-signed, and installed ERP builds trust only their embedded list.

1. Generate a new key (Activation Engine, Signing Keys page, or `LicenseTool keygen`) with a **new, unused** key id.
2. Add its public key to `production-public-keys.json` (maximum 8 keys), keeping the three existing entries so licenses
   already issued still verify. Update the pinned test deliberately, in its own reviewed commit.
3. Build and deploy a new ERP release to every customer **before** issuing licenses with the new key.
4. Keep an offline encrypted backup of the new private key and test a restore before first use.

## Rules

- Never replace or edit the trusted public keys silently. Every change is a reviewed commit.
- Never commit private keys or passphrases. A scan of the history on 2026-11-14 found no key files.
- Retire a key in the Activation Engine when it should stop signing. Licenses it already signed stay valid
  (test `Retiring_a_key_keeps_its_licenses_valid_and_the_standby_key_keeps_issuing`).
- Keep a second key (standby) in a different storage location from the first.
