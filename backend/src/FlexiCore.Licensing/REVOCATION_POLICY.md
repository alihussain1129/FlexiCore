# Local license revocation policy

## Snapshot format and trust

The local API accepts a detached signed snapshot at `PUT /api/v1/license/revocations` in Local deployment mode. The caller must pass the existing local activation authorization policy and import rate limit. The body is a JSON ES256 envelope (`schemaVersion`, `keyId`, base64url `payload`, base64url raw 64-byte `signature`). The signed payload is version 1 and contains `listId`, UTC `issuedAt`, UTC `nextUpdate`, and `revokedLicenses` entries (`licenseId`, UTC `revokedAt`, `reason`). The signature is checked against the same embedded production public-key set as license files. Unknown keys, malformed input, duplicate IDs/properties, invalid signatures, and unsupported versions are rejected.

The issuer must keep every prior revoked license ID and its original timestamp/reason in each newer snapshot. A snapshot that removes or edits a prior entry is rejected. A different snapshot with an equal or earlier `issuedAt` is rejected. Exact re-import is idempotent. These rules prevent ordinary replay and rollback through the API while the current snapshot remains intact.

The API writes the snapshot through a same-directory temporary file, flushes it, atomically replaces the stored snapshot, and creates it with Unix mode `0600`. Existing license bytes are not rewritten.

## Offline behavior

The installed signed snapshot is evaluated locally without a network request. A matching revoked license is denied at status checks and license import. Once `nextUpdate` is reached, status becomes `unavailable`, and the Local API license gate denies protected requests until a fresh trusted snapshot is installed. This bounds the time a disconnected instance can go without learning newly issued revocations.

A deployment with no snapshot yet has no revocation knowledge; for backward compatibility this currently reports clear. Operators must provision a trusted initial snapshot before treating revocation enforcement as active. The code does not claim immediate revocation on a disconnected machine.

The local filesystem is not a hardware-backed monotonic store. A privileged machine owner can restore an older copy of the snapshot or patch the application. API-level timestamp checks do not prevent an administrator from restoring both files/backups outside the API. Deployments needing rollback resistance against the host administrator require protected monotonic storage or a hosted authority, neither of which is implemented here.

## Operations and limits

- Snapshots may be valid for at most 31 days and contain at most 20,000 entries.
- Generate and sign snapshots only with the authorized signing key that matches the embedded production keyset. This repository does not contain or generate the KMSONS production private key.
- Deliver snapshots over the authorized local API or an approved administrative process; protect transfer and retain audit records at the signing authority.
- A corrupt installed snapshot fails closed and must be repaired with an authorized operational recovery procedure. There is no automatic deletion or reset path.
- No revocation database, hosted synchronization scheduler, renewal workflow, or module entitlement feed is part of this implementation.
