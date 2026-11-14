-- 0009_licensing: additive licensing persistence. Signed license bytes and prior lifecycle events are immutable.
CREATE SCHEMA licensing;
GRANT USAGE ON SCHEMA licensing TO flexicore_app;

CREATE TABLE licensing.licenses (
    license_id text PRIMARY KEY,
    organization_id uuid NOT NULL REFERENCES tenancy.organizations(id),
    product text NOT NULL,
    licensee text NOT NULL,
    signing_key_id text NOT NULL,
    signed_license bytea NOT NULL,
    signed_license_sha256 text NOT NULL,
    issued_at timestamptz NOT NULL,
    not_before timestamptz NOT NULL,
    expires_at timestamptz,
    grace_days integer NOT NULL DEFAULT 0,
    installation_id text,
    max_installations integer NOT NULL,
    lifecycle_state text NOT NULL DEFAULT 'active',
    predecessor_license_id text,
    recorded_at timestamptz NOT NULL DEFAULT now(),
    recorded_by uuid REFERENCES identity.users(id),
    CONSTRAINT uq_licenses_id_organization UNIQUE (license_id, organization_id),
    CONSTRAINT fk_licenses_predecessor FOREIGN KEY (predecessor_license_id, organization_id)
        REFERENCES licensing.licenses(license_id, organization_id),
    CONSTRAINT ck_licenses_id CHECK (license_id ~ '^[A-Za-z0-9._-]{1,64}$'),
    CONSTRAINT ck_licenses_product CHECK (char_length(btrim(product)) BETWEEN 1 AND 64),
    CONSTRAINT ck_licenses_licensee CHECK (char_length(btrim(licensee)) BETWEEN 1 AND 200),
    CONSTRAINT ck_licenses_key_id CHECK (signing_key_id ~ '^[A-Za-z0-9._-]{1,64}$'),
    CONSTRAINT ck_licenses_hash CHECK (signed_license_sha256 ~ '^[0-9a-f]{64}$'),
    CONSTRAINT ck_licenses_dates CHECK (expires_at IS NULL OR expires_at > not_before),
    CONSTRAINT ck_licenses_grace CHECK (grace_days BETWEEN 0 AND 365),
    CONSTRAINT ck_licenses_installation CHECK (installation_id IS NULL OR installation_id ~ '^[0-9a-f]{64}$'),
    CONSTRAINT ck_licenses_installations CHECK (max_installations BETWEEN 1 AND 100000),
    CONSTRAINT ck_licenses_state CHECK (lifecycle_state IN ('active', 'suspended', 'superseded', 'revoked')),
    CONSTRAINT ck_licenses_not_self_predecessor CHECK (predecessor_license_id IS NULL OR predecessor_license_id <> license_id)
);

CREATE INDEX ix_licenses_organization_state ON licensing.licenses(organization_id, lifecycle_state, issued_at DESC);
CREATE INDEX ix_licenses_expiry ON licensing.licenses(expires_at) WHERE lifecycle_state = 'active' AND expires_at IS NOT NULL;
CREATE UNIQUE INDEX uq_licenses_one_successor ON licensing.licenses(predecessor_license_id) WHERE predecessor_license_id IS NOT NULL;

CREATE TABLE licensing.activation_events (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id uuid NOT NULL REFERENCES tenancy.organizations(id),
    license_id text NOT NULL,
    installation_id text NOT NULL,
    event_type text NOT NULL,
    occurred_at timestamptz NOT NULL,
    authorized_by uuid REFERENCES identity.users(id),
    request_id uuid,
    previous_installation_id text,
    metadata jsonb NOT NULL DEFAULT '{}'::jsonb,
    CONSTRAINT fk_activation_events_license FOREIGN KEY (license_id, organization_id)
        REFERENCES licensing.licenses(license_id, organization_id),
    CONSTRAINT uq_activation_events_request UNIQUE (organization_id, request_id),
    CONSTRAINT ck_activation_events_installation CHECK (installation_id ~ '^[0-9a-f]{64}$'),
    CONSTRAINT ck_activation_events_previous_installation CHECK (previous_installation_id IS NULL OR previous_installation_id ~ '^[0-9a-f]{64}$'),
    CONSTRAINT ck_activation_events_type CHECK (event_type IN ('activated', 'reactivated', 'transferred', 'deactivated'))
);
CREATE INDEX ix_activation_events_license_time ON licensing.activation_events(organization_id, license_id, occurred_at DESC);
CREATE INDEX ix_activation_events_installation ON licensing.activation_events(organization_id, installation_id, occurred_at DESC);

CREATE TABLE licensing.renewals (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id uuid NOT NULL REFERENCES tenancy.organizations(id),
    previous_license_id text NOT NULL,
    renewed_license_id text NOT NULL,
    authorized_by uuid NOT NULL REFERENCES identity.users(id),
    authorized_at timestamptz NOT NULL,
    request_id uuid NOT NULL,
    reason text NOT NULL,
    CONSTRAINT fk_renewals_previous FOREIGN KEY (previous_license_id, organization_id)
        REFERENCES licensing.licenses(license_id, organization_id),
    CONSTRAINT fk_renewals_new FOREIGN KEY (renewed_license_id, organization_id)
        REFERENCES licensing.licenses(license_id, organization_id),
    CONSTRAINT uq_renewals_previous UNIQUE (organization_id, previous_license_id),
    CONSTRAINT uq_renewals_new UNIQUE (organization_id, renewed_license_id),
    CONSTRAINT uq_renewals_request UNIQUE (organization_id, request_id),
    CONSTRAINT ck_renewals_not_self CHECK (previous_license_id <> renewed_license_id),
    CONSTRAINT ck_renewals_reason CHECK (char_length(btrim(reason)) BETWEEN 1 AND 1000)
);
CREATE INDEX ix_renewals_organization_time ON licensing.renewals(organization_id, authorized_at DESC);

CREATE TABLE licensing.revocation_snapshots (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id uuid NOT NULL REFERENCES tenancy.organizations(id),
    list_id uuid NOT NULL,
    signing_key_id text NOT NULL,
    issued_at timestamptz NOT NULL,
    next_update timestamptz NOT NULL,
    signed_snapshot bytea NOT NULL,
    snapshot_sha256 text NOT NULL,
    imported_at timestamptz NOT NULL DEFAULT now(),
    imported_by uuid REFERENCES identity.users(id),
    CONSTRAINT uq_revocation_snapshots_id_org UNIQUE (id, organization_id),
    CONSTRAINT uq_revocation_snapshots_list UNIQUE (organization_id, list_id),
    CONSTRAINT ck_revocation_snapshots_window CHECK (next_update > issued_at AND next_update <= issued_at + interval '31 days'),
    CONSTRAINT ck_revocation_snapshots_key CHECK (signing_key_id ~ '^[A-Za-z0-9._-]{1,64}$'),
    CONSTRAINT ck_revocation_snapshots_hash CHECK (snapshot_sha256 ~ '^[0-9a-f]{64}$')
);
CREATE INDEX ix_revocation_snapshots_freshness ON licensing.revocation_snapshots(organization_id, issued_at DESC, next_update);

CREATE TABLE licensing.revocations (
    organization_id uuid NOT NULL REFERENCES tenancy.organizations(id),
    license_id text NOT NULL,
    snapshot_id uuid NOT NULL,
    revoked_at timestamptz NOT NULL,
    reason text NOT NULL,
    authorized_by uuid REFERENCES identity.users(id),
    recorded_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (organization_id, license_id),
    CONSTRAINT fk_revocations_license FOREIGN KEY (license_id, organization_id)
        REFERENCES licensing.licenses(license_id, organization_id),
    CONSTRAINT fk_revocations_snapshot FOREIGN KEY (snapshot_id, organization_id)
        REFERENCES licensing.revocation_snapshots(id, organization_id),
    CONSTRAINT ck_revocations_reason CHECK (char_length(btrim(reason)) BETWEEN 1 AND 1000)
);
CREATE INDEX ix_revocations_time ON licensing.revocations(organization_id, revoked_at DESC);

CREATE TABLE licensing.entitlements (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id uuid NOT NULL REFERENCES tenancy.organizations(id),
    license_id text NOT NULL,
    revision integer NOT NULL,
    module_key text NOT NULL,
    feature_key text NOT NULL DEFAULT '*',
    enabled boolean NOT NULL,
    valid_from timestamptz NOT NULL,
    valid_until timestamptz,
    max_users integer,
    max_devices integer,
    max_companies integer,
    max_branches integer,
    limits jsonb NOT NULL DEFAULT '{}'::jsonb,
    signed_entitlement_hash text NOT NULL,
    recorded_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT fk_entitlements_license FOREIGN KEY (license_id, organization_id)
        REFERENCES licensing.licenses(license_id, organization_id),
    CONSTRAINT uq_entitlements_revision UNIQUE (organization_id, license_id, revision, module_key, feature_key),
    CONSTRAINT ck_entitlements_revision CHECK (revision > 0),
    CONSTRAINT ck_entitlements_module CHECK (module_key ~ '^[a-z][a-z0-9_.-]{1,63}$'),
    CONSTRAINT ck_entitlements_feature CHECK (feature_key = '*' OR feature_key ~ '^[a-z][a-z0-9_.-]{0,95}$'),
    CONSTRAINT ck_entitlements_dates CHECK (valid_until IS NULL OR valid_until > valid_from),
    CONSTRAINT ck_entitlements_limits CHECK (
        (max_users IS NULL OR max_users >= 0) AND
        (max_devices IS NULL OR max_devices >= 0) AND
        (max_companies IS NULL OR max_companies >= 0) AND
        (max_branches IS NULL OR max_branches >= 0)),
    CONSTRAINT ck_entitlements_hash CHECK (signed_entitlement_hash ~ '^[0-9a-f]{64}$')
);
CREATE INDEX ix_entitlements_lookup ON licensing.entitlements(organization_id, module_key, feature_key, license_id, revision DESC);

-- Every tenant row is scoped to the authenticated user's active organization membership.
ALTER TABLE licensing.licenses ENABLE ROW LEVEL SECURITY;
ALTER TABLE licensing.activation_events ENABLE ROW LEVEL SECURITY;
ALTER TABLE licensing.renewals ENABLE ROW LEVEL SECURITY;
ALTER TABLE licensing.revocation_snapshots ENABLE ROW LEVEL SECURITY;
ALTER TABLE licensing.revocations ENABLE ROW LEVEL SECURITY;
ALTER TABLE licensing.entitlements ENABLE ROW LEVEL SECURITY;

CREATE POLICY licenses_read_member ON licensing.licenses FOR SELECT USING (
    EXISTS (SELECT 1 FROM tenancy.organization_memberships om
        WHERE om.organization_id = licenses.organization_id
          AND om.user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
          AND om.status = 'active'));
CREATE POLICY licenses_insert_admin ON licensing.licenses FOR INSERT WITH CHECK (
    EXISTS (SELECT 1 FROM tenancy.organization_memberships om
        WHERE om.organization_id = licenses.organization_id
          AND om.user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
          AND om.status = 'active' AND om.role IN ('owner', 'admin')));

CREATE POLICY activation_events_read_member ON licensing.activation_events FOR SELECT USING (
    EXISTS (SELECT 1 FROM tenancy.organization_memberships om
        WHERE om.organization_id = activation_events.organization_id
          AND om.user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
          AND om.status = 'active'));
CREATE POLICY activation_events_insert_admin ON licensing.activation_events FOR INSERT WITH CHECK (
    EXISTS (SELECT 1 FROM tenancy.organization_memberships om
        WHERE om.organization_id = activation_events.organization_id
          AND om.user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
          AND om.status = 'active' AND om.role IN ('owner', 'admin')));

CREATE POLICY renewals_read_member ON licensing.renewals FOR SELECT USING (
    EXISTS (SELECT 1 FROM tenancy.organization_memberships om
        WHERE om.organization_id = renewals.organization_id
          AND om.user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
          AND om.status = 'active'));
CREATE POLICY renewals_insert_admin ON licensing.renewals FOR INSERT WITH CHECK (
    EXISTS (SELECT 1 FROM tenancy.organization_memberships om
        WHERE om.organization_id = renewals.organization_id
          AND om.user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
          AND om.status = 'active' AND om.role IN ('owner', 'admin')));

CREATE POLICY revocation_snapshots_read_member ON licensing.revocation_snapshots FOR SELECT USING (
    EXISTS (SELECT 1 FROM tenancy.organization_memberships om
        WHERE om.organization_id = revocation_snapshots.organization_id
          AND om.user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
          AND om.status = 'active'));
CREATE POLICY revocation_snapshots_insert_admin ON licensing.revocation_snapshots FOR INSERT WITH CHECK (
    EXISTS (SELECT 1 FROM tenancy.organization_memberships om
        WHERE om.organization_id = revocation_snapshots.organization_id
          AND om.user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
          AND om.status = 'active' AND om.role IN ('owner', 'admin')));

CREATE POLICY revocations_read_member ON licensing.revocations FOR SELECT USING (
    EXISTS (SELECT 1 FROM tenancy.organization_memberships om
        WHERE om.organization_id = revocations.organization_id
          AND om.user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
          AND om.status = 'active'));
CREATE POLICY revocations_insert_admin ON licensing.revocations FOR INSERT WITH CHECK (
    EXISTS (SELECT 1 FROM tenancy.organization_memberships om
        WHERE om.organization_id = revocations.organization_id
          AND om.user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
          AND om.status = 'active' AND om.role IN ('owner', 'admin')));

CREATE POLICY entitlements_read_member ON licensing.entitlements FOR SELECT USING (
    EXISTS (SELECT 1 FROM tenancy.organization_memberships om
        WHERE om.organization_id = entitlements.organization_id
          AND om.user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
          AND om.status = 'active'));
CREATE POLICY entitlements_insert_admin ON licensing.entitlements FOR INSERT WITH CHECK (
    EXISTS (SELECT 1 FROM tenancy.organization_memberships om
        WHERE om.organization_id = entitlements.organization_id
          AND om.user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
          AND om.status = 'active' AND om.role IN ('owner', 'admin')));

-- Lifecycle and audit rows are append-only to the runtime role. License rows have no UPDATE/DELETE grants.
GRANT SELECT, INSERT ON licensing.licenses TO flexicore_app;
GRANT SELECT, INSERT ON licensing.activation_events TO flexicore_app;
GRANT SELECT, INSERT ON licensing.renewals TO flexicore_app;
GRANT SELECT, INSERT ON licensing.revocation_snapshots TO flexicore_app;
GRANT SELECT, INSERT ON licensing.revocations TO flexicore_app;
GRANT SELECT, INSERT ON licensing.entitlements TO flexicore_app;
REVOKE UPDATE, DELETE, TRUNCATE ON ALL TABLES IN SCHEMA licensing FROM flexicore_app, public;

COMMENT ON SCHEMA licensing IS 'Tenant-scoped immutable ERP licensing lifecycle records; signed source bytes are preserved.';
COMMENT ON TABLE licensing.licenses IS 'Immutable signed license records; lifecycle changes are represented by renewal/revocation/event rows.';
COMMENT ON TABLE licensing.activation_events IS 'Append-only activation, reactivation, transfer, and deactivation history.';
COMMENT ON TABLE licensing.revocation_snapshots IS 'Verified signed revocation snapshots and their freshness windows.';
COMMENT ON TABLE licensing.entitlements IS 'Immutable entitlement revisions derived from a signed license; enforcement must verify the signed source.';
