# Licensing: release, operation and deployment

## Model

FlexiCore works offline. A signed license file is verified locally by the ERP API in Local mode. There is no
mandatory cloud login. In Local mode every request outside health, license and sign-in routes is answered with
403 unless the installed license is `valid` or `validInGrace`. The check runs on every request.

## Owner: issuing a license

1. The customer sends the request code shown by the ERP (`GET /api/v1/license/request`) or the license screen.
2. In the Activation Engine pick a trusted, non-retired signing key, enter customer, term and the request code
   (installation binding), and issue. The tool refuses keys that are not in the ERP trust file, retired keys,
   and repeated license ids. Every issuance is written to an append-only ledger.
3. Send the `.lic` file, or the activation code, to the customer.

## Customer: activating

1. Open the license screen and paste the license file or activation code, or `PUT /api/v1/license` with the file.
2. The ERP verifies the envelope, signature, trusted key id, product, installation binding, dates and revocation
   state, then stores the file atomically. A license that fails any check never replaces the installed one.
3. The state becomes `valid`. It survives restarts and works offline.

Refusals have exact reasons, for example `wrong_installation`, `not_bound`, expired, invalid signature, unknown key,
revoked, malformed.

## Renewal

Use the LicenseTool/Activation Engine renewal. A renewal keeps the same customer and installation and records its
predecessor. A revoked license cannot be renewed and a predecessor cannot have two successors. The customer imports the
new file; an older license cannot replace a newer one.

## Revocation

The owner issues a signed, cumulative revocation snapshot (LicenseTool `revocation-snapshot`). The customer ERP imports it
(`PUT /api/v1/license/revocations`). Afterwards the license state is `revoked`, business routes return 403, and the license
cannot be installed again. A snapshot with a bad signature is refused (422); a snapshot that drops earlier revocations or is
older is refused (409). Snapshots expire after their validity period; an expired snapshot makes the status unavailable
rather than silently trusting the license.

## Offline and clock policy

A valid installed license keeps working with no network. A monotonic clock file next to the license prevents rolling the
system clock back to revive an expired license.

## Deployment settings (Local mode)

`Deployment__Mode=Local`, `License__Directory=<persistent directory>`, and
`License__LocalActivationAllowedRemoteAddresses=<docker gateway ip>` if anonymous activation from the host through Docker is
wanted. **The license directory must be on a persistent volume**, otherwise recreating the container removes the license
and the revocation snapshot.

## What has been verified (2026-11-14)

- Test keys only, local throwaway databases: issue, import, activation code, restart, forgery rejection, wrong or unbound
  machine, expiry, clock rollback, enforcement 403, revocation, revocation rollback refusal, key rotation. Integration suite
  241/241, unit 243, architecture 36, ActivationEngine 199, LicenseTool 81, Flutter 76 plus analyze clean.
- Windows CI (build of WPF and Avalonia apps, unit, ActivationEngine and LicenseTool tests) green on commit `95916ff`.
- Android project restores and builds in Release (`android-arm64`).

## Not verified

- Signing with the production owner keys (private keys unavailable, see PRODUCTION-KEY-RECOVERY.md).
- A production-signed Android APK, and any test on a physical device.
- Running the Windows apps and issuing a license on Windows (CI only builds and tests).
- Integration tests and Flutter tests in CI.
