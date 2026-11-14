-- 0013: retain signed revocations received before the associated license record.
-- This prevents import order from creating a window in which a revoked license can be registered.
CREATE TABLE licensing.pending_revocations (
    organization_id uuid NOT NULL,
    license_id text NOT NULL CHECK (license_id ~ '^[A-Za-z0-9._-]{1,64}$'),
    snapshot_id uuid NOT NULL,
    revoked_at timestamptz NOT NULL,
    reason text NOT NULL CHECK (char_length(btrim(reason)) BETWEEN 1 AND 1000),
    authorized_by uuid REFERENCES identity.users(id),
    recorded_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (organization_id, license_id),
    FOREIGN KEY (snapshot_id, organization_id)
        REFERENCES licensing.revocation_snapshots(id, organization_id)
);

ALTER TABLE licensing.pending_revocations ENABLE ROW LEVEL SECURITY;
CREATE POLICY pending_revocations_read_member ON licensing.pending_revocations FOR SELECT USING (
    EXISTS (SELECT 1 FROM tenancy.organization_memberships om
        WHERE om.organization_id = pending_revocations.organization_id
          AND om.user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
          AND om.status = 'active'));
CREATE POLICY pending_revocations_insert_admin ON licensing.pending_revocations FOR INSERT WITH CHECK (
    EXISTS (SELECT 1 FROM tenancy.organization_memberships om
        WHERE om.organization_id = pending_revocations.organization_id
          AND om.user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
          AND om.status = 'active' AND om.role IN ('owner', 'admin')));
GRANT SELECT, INSERT ON licensing.pending_revocations TO flexicore_app;
REVOKE UPDATE, DELETE, TRUNCATE ON licensing.pending_revocations FROM flexicore_app, public;

COMMENT ON TABLE licensing.pending_revocations IS
    'Append-only signed revocations for license IDs not yet registered in this organization.';
