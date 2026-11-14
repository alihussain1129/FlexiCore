-- 0010: retain complete signed entitlement revisions so every authorization decision can be tied to its signed source.
CREATE TABLE licensing.entitlement_revisions (
    organization_id uuid NOT NULL,
    license_id text NOT NULL,
    revision integer NOT NULL,
    signing_key_id text NOT NULL,
    signed_entitlement bytea NOT NULL,
    signed_entitlement_hash text NOT NULL,
    imported_at timestamptz NOT NULL DEFAULT now(),
    imported_by uuid REFERENCES identity.users(id),
    PRIMARY KEY (organization_id, license_id, revision),
    UNIQUE (organization_id, license_id, signed_entitlement_hash),
    FOREIGN KEY (license_id, organization_id)
        REFERENCES licensing.licenses(license_id, organization_id),
    CHECK (revision > 0),
    CHECK (signing_key_id ~ '^[A-Za-z0-9._-]{1,64}$'),
    CHECK (signed_entitlement_hash ~ '^[0-9a-f]{64}$'),
    CHECK (octet_length(signed_entitlement) BETWEEN 1 AND 262144)
);

ALTER TABLE licensing.entitlements
    ADD CONSTRAINT fk_entitlements_signed_revision
    FOREIGN KEY (organization_id, license_id, revision)
    REFERENCES licensing.entitlement_revisions(organization_id, license_id, revision);

ALTER TABLE licensing.entitlement_revisions ENABLE ROW LEVEL SECURITY;
CREATE POLICY entitlement_revisions_read_member ON licensing.entitlement_revisions FOR SELECT USING (
    EXISTS (SELECT 1 FROM tenancy.organization_memberships om
        WHERE om.organization_id = entitlement_revisions.organization_id
          AND om.user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
          AND om.status = 'active'));
CREATE POLICY entitlement_revisions_insert_admin ON licensing.entitlement_revisions FOR INSERT WITH CHECK (
    EXISTS (SELECT 1 FROM tenancy.organization_memberships om
        WHERE om.organization_id = entitlement_revisions.organization_id
          AND om.user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
          AND om.status = 'active' AND om.role IN ('owner', 'admin')));
GRANT SELECT, INSERT ON licensing.entitlement_revisions TO flexicore_app;
REVOKE UPDATE, DELETE, TRUNCATE ON licensing.entitlement_revisions FROM flexicore_app, public;

COMMENT ON TABLE licensing.entitlement_revisions IS
    'Append-only complete signed entitlement documents; each revision replaces all grants from earlier revisions.';
