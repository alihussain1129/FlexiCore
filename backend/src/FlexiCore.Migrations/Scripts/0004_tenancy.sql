CREATE SCHEMA tenancy;
GRANT USAGE ON SCHEMA tenancy TO flexicore_app;

CREATE TABLE tenancy.organizations (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    name text NOT NULL,
    slug text NOT NULL UNIQUE,
    status text NOT NULL DEFAULT 'active',
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_organizations_name
        CHECK (char_length(btrim(name)) BETWEEN 1 AND 160),
    CONSTRAINT ck_organizations_slug
        CHECK (slug ~ '^[a-z0-9][a-z0-9-]{1,78}[a-z0-9]$'),
    CONSTRAINT ck_organizations_status
        CHECK (status IN ('active', 'suspended'))
);

CREATE TABLE tenancy.organization_memberships (
    organization_id uuid NOT NULL
        REFERENCES tenancy.organizations(id),
    user_id uuid NOT NULL
        REFERENCES identity.users(id),
    role text NOT NULL DEFAULT 'member',
    status text NOT NULL DEFAULT 'active',
    created_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (organization_id, user_id),
    CONSTRAINT ck_organization_memberships_role
        CHECK (role IN ('owner', 'admin', 'member')),
    CONSTRAINT ck_organization_memberships_status
        CHECK (status IN ('active', 'suspended'))
);

CREATE TABLE tenancy.companies (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id uuid NOT NULL
        REFERENCES tenancy.organizations(id),
    name text NOT NULL,
    slug text NOT NULL,
    industry_key text NOT NULL,
    status text NOT NULL DEFAULT 'active',
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_companies_organization_slug
        UNIQUE (organization_id, slug),
    CONSTRAINT uq_companies_id_organization
        UNIQUE (id, organization_id),
    CONSTRAINT ck_companies_name
        CHECK (char_length(btrim(name)) BETWEEN 1 AND 160),
    CONSTRAINT ck_companies_slug
        CHECK (slug ~ '^[a-z0-9][a-z0-9-]{1,78}[a-z0-9]$'),
    CONSTRAINT ck_companies_industry_key
        CHECK (industry_key ~ '^[a-z][a-z0-9_]{1,63}$'),
    CONSTRAINT ck_companies_status
        CHECK (status IN ('active', 'suspended'))
);

CREATE TABLE tenancy.company_memberships (
    company_id uuid NOT NULL,
    organization_id uuid NOT NULL,
    user_id uuid NOT NULL,
    role text NOT NULL DEFAULT 'member',
    status text NOT NULL DEFAULT 'active',
    created_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (company_id, user_id),
    CONSTRAINT fk_company_memberships_company
        FOREIGN KEY (company_id, organization_id)
        REFERENCES tenancy.companies(id, organization_id),
    CONSTRAINT fk_company_memberships_organization_member
        FOREIGN KEY (organization_id, user_id)
        REFERENCES tenancy.organization_memberships(organization_id, user_id),
    CONSTRAINT ck_company_memberships_role
        CHECK (role IN ('owner', 'admin', 'member')),
    CONSTRAINT ck_company_memberships_status
        CHECK (status IN ('active', 'suspended'))
);

CREATE TABLE tenancy.branches (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id uuid NOT NULL
        REFERENCES tenancy.companies(id),
    name text NOT NULL,
    code text NOT NULL,
    status text NOT NULL DEFAULT 'active',
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_branches_company_code
        UNIQUE (company_id, code),
    CONSTRAINT ck_branches_name
        CHECK (char_length(btrim(name)) BETWEEN 1 AND 160),
    CONSTRAINT ck_branches_code
        CHECK (char_length(btrim(code)) BETWEEN 1 AND 40),
    CONSTRAINT ck_branches_status
        CHECK (status IN ('active', 'suspended'))
);

CREATE INDEX ix_organization_memberships_user
    ON tenancy.organization_memberships(user_id, status);

CREATE INDEX ix_companies_organization
    ON tenancy.companies(organization_id, status);

CREATE INDEX ix_company_memberships_user
    ON tenancy.company_memberships(user_id, status);

CREATE INDEX ix_branches_company
    ON tenancy.branches(company_id, status);

-- The API runtime role is not an owner of these tables.
-- The migration/owner role remains trusted for administrative operations.
ALTER TABLE tenancy.organizations ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenancy.organization_memberships ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenancy.companies ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenancy.company_memberships ENABLE ROW LEVEL SECURITY;
ALTER TABLE tenancy.branches ENABLE ROW LEVEL SECURITY;

-- Users can see only organizations for which they have an active membership.
CREATE POLICY organizations_read_members
    ON tenancy.organizations
    FOR SELECT
    USING (
        EXISTS (
            SELECT 1
            FROM tenancy.organization_memberships om
            WHERE om.organization_id = organizations.id
              AND om.user_id =
                  NULLIF(current_setting('app.user_id', true), '')::uuid
              AND om.status = 'active'
        )
    );

-- Membership listings are limited to the authenticated user.
CREATE POLICY organization_memberships_read_self
    ON tenancy.organization_memberships
    FOR SELECT
    USING (
        user_id =
            NULLIF(current_setting('app.user_id', true), '')::uuid
    );

CREATE POLICY companies_read_members
    ON tenancy.companies
    FOR SELECT
    USING (
        companies.status = 'active'
        AND EXISTS (
            SELECT 1
            FROM tenancy.company_memberships cm
            JOIN tenancy.organization_memberships om
              ON om.organization_id = cm.organization_id
             AND om.user_id = cm.user_id
            JOIN tenancy.organizations o
              ON o.id = cm.organization_id
            WHERE cm.company_id = companies.id
              AND cm.organization_id = companies.organization_id
              AND cm.user_id =
                  NULLIF(current_setting('app.user_id', true), '')::uuid
              AND cm.status = 'active'
              AND om.status = 'active'
              AND o.status = 'active'
        )
    );

CREATE POLICY company_memberships_read_self
    ON tenancy.company_memberships
    FOR SELECT
    USING (
        user_id =
            NULLIF(current_setting('app.user_id', true), '')::uuid
    );

-- Every branch operation must match both the selected company and
-- an active membership for the current user.
CREATE POLICY branches_company_isolation
    ON tenancy.branches
    FOR ALL
    USING (
        company_id =
            NULLIF(current_setting('app.company_id', true), '')::uuid
        AND EXISTS (
            SELECT 1
            FROM tenancy.company_memberships cm
            JOIN tenancy.organization_memberships om
              ON om.organization_id = cm.organization_id
             AND om.user_id = cm.user_id
            WHERE cm.company_id = branches.company_id
              AND cm.user_id =
                  NULLIF(current_setting('app.user_id', true), '')::uuid
              AND cm.status = 'active'
              AND om.status = 'active'
        )
        AND EXISTS (
            SELECT 1
            FROM tenancy.companies c
            JOIN tenancy.organizations o
              ON o.id = c.organization_id
            WHERE c.id = branches.company_id
              AND c.status = 'active'
              AND o.status = 'active'
        )
    )
    WITH CHECK (
        company_id =
            NULLIF(current_setting('app.company_id', true), '')::uuid
        AND EXISTS (
            SELECT 1
            FROM tenancy.company_memberships cm
            JOIN tenancy.organization_memberships om
              ON om.organization_id = cm.organization_id
             AND om.user_id = cm.user_id
            WHERE cm.company_id = branches.company_id
              AND cm.user_id =
                  NULLIF(current_setting('app.user_id', true), '')::uuid
              AND cm.status = 'active'
              AND om.status = 'active'
        )
        AND EXISTS (
            SELECT 1
            FROM tenancy.companies c
            JOIN tenancy.organizations o
              ON o.id = c.organization_id
            WHERE c.id = branches.company_id
              AND c.status = 'active'
              AND o.status = 'active'
        )
    );

-- Grant only what this phase needs. Company and membership creation/
-- administration will be introduced with the authenticated API workflows.
GRANT SELECT ON
    tenancy.organizations,
    tenancy.organization_memberships,
    tenancy.companies,
    tenancy.company_memberships,
    tenancy.branches
TO flexicore_app;

GRANT INSERT, UPDATE, DELETE
    ON tenancy.branches
    TO flexicore_app;

COMMENT ON SCHEMA tenancy IS
    'Organization, company, branch and membership foundation.';

COMMENT ON TABLE tenancy.branches IS
    'Company-scoped data protected by row-level security.';

