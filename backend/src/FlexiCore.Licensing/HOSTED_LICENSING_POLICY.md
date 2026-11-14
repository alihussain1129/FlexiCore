# Hosted licensing and offline policy

Hosted authorization is online and fail-closed. Every protected operation checks the tenant's current database state, the license validity/grace window, the latest imported signed revocation snapshot, and the latest complete signed entitlement revision.

The hosted `/api/v1/organizations/{organizationId}/licensing/sync` route returns signed source artifacts for recovery and synchronization. A response is complete only when `isComplete` is true. Current response caps are 500 license records and 1,000 each for activation events, renewal records, and entitlement revisions. A client must not treat a partial bundle as a complete recovery state.

This client does not grant offline hosted ERP access. If PostgreSQL or the hosted API is unavailable, protected hosted operations are unavailable. Signed artifact export does not enable local offline authorization by itself. A future offline-capable client must verify each artifact locally and stop authorization at the earliest of license expiry plus grace, entitlement `validUntil`, or the signed revocation snapshot `nextUpdate`; it must also preserve monotonic snapshot/revision state against rollback. Those client-side verification and anti-rollback guarantees are not implemented here.

Local deployment remains governed by the existing local signed-license gate and local revocation snapshot policy. Local mode bypasses tenant database entitlement checks because local license verification is the active gate.
