-- 0012: request-level idempotency for authenticated license imports.
CREATE TABLE licensing.idempotency_requests (
    organization_id uuid NOT NULL REFERENCES tenancy.organizations(id),
    request_id uuid NOT NULL,
    operation text NOT NULL,
    request_sha256 text NOT NULL,
    result_id text NOT NULL,
    actor_user_id uuid NOT NULL REFERENCES identity.users(id),
    recorded_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (organization_id, request_id),
    CHECK (operation ~ '^[a-z][a-z0-9_.-]{1,63}$'),
    CHECK (request_sha256 ~ '^[0-9a-f]{64}$'),
    CHECK (char_length(result_id) BETWEEN 1 AND 128)
);

ALTER TABLE licensing.idempotency_requests ENABLE ROW LEVEL SECURITY;
CREATE POLICY idempotency_requests_read_member ON licensing.idempotency_requests FOR SELECT USING (
    EXISTS (SELECT 1 FROM tenancy.organization_memberships om
        WHERE om.organization_id = idempotency_requests.organization_id
          AND om.user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
          AND om.status = 'active'));
CREATE POLICY idempotency_requests_insert_admin ON licensing.idempotency_requests FOR INSERT WITH CHECK (
    EXISTS (SELECT 1 FROM tenancy.organization_memberships om
        WHERE om.organization_id = idempotency_requests.organization_id
          AND om.user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
          AND om.status = 'active' AND om.role IN ('owner', 'admin')));
GRANT SELECT, INSERT ON licensing.idempotency_requests TO flexicore_app;
REVOKE UPDATE, DELETE, TRUNCATE ON licensing.idempotency_requests FROM flexicore_app, public;
