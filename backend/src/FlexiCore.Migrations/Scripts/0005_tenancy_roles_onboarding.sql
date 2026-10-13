-- 0005: role-aware branch writes and owner-controlled onboarding.
-- 0004 is already applied and immutable; this migration replaces its branch policy.

DROP POLICY branches_company_isolation ON tenancy.branches;

-- Any active member of the selected company may read its branches.
CREATE POLICY branches_select_members
    ON tenancy.branches
    FOR SELECT
    USING (
        company_id = NULLIF(current_setting('app.company_id', true), '')::uuid
        AND EXISTS (
            SELECT 1
            FROM tenancy.company_memberships cm
            JOIN tenancy.organization_memberships om
              ON om.organization_id = cm.organization_id
             AND om.user_id = cm.user_id
            WHERE cm.company_id = branches.company_id
              AND cm.user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
              AND cm.status = 'active'
              AND om.status = 'active'
        )
        AND EXISTS (
            SELECT 1
            FROM tenancy.companies c
            JOIN tenancy.organizations o ON o.id = c.organization_id
            WHERE c.id = branches.company_id
              AND c.status = 'active'
              AND o.status = 'active'
        )
    );

-- Only company owners and admins may change branches.
CREATE POLICY branches_insert_admins
    ON tenancy.branches
    FOR INSERT
    WITH CHECK (
        company_id = NULLIF(current_setting('app.company_id', true), '')::uuid
        AND EXISTS (
            SELECT 1
            FROM tenancy.company_memberships cm
            JOIN tenancy.organization_memberships om
              ON om.organization_id = cm.organization_id
             AND om.user_id = cm.user_id
            WHERE cm.company_id = branches.company_id
              AND cm.user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
              AND cm.status = 'active'
              AND cm.role IN ('owner', 'admin')
              AND om.status = 'active'
        )
        AND EXISTS (
            SELECT 1
            FROM tenancy.companies c
            JOIN tenancy.organizations o ON o.id = c.organization_id
            WHERE c.id = branches.company_id
              AND c.status = 'active'
              AND o.status = 'active'
        )
    );

CREATE POLICY branches_update_admins
    ON tenancy.branches
    FOR UPDATE
    USING (
        company_id = NULLIF(current_setting('app.company_id', true), '')::uuid
        AND EXISTS (
            SELECT 1
            FROM tenancy.company_memberships cm
            JOIN tenancy.organization_memberships om
              ON om.organization_id = cm.organization_id
             AND om.user_id = cm.user_id
            WHERE cm.company_id = branches.company_id
              AND cm.user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
              AND cm.status = 'active'
              AND cm.role IN ('owner', 'admin')
              AND om.status = 'active'
        )
        AND EXISTS (
            SELECT 1
            FROM tenancy.companies c
            JOIN tenancy.organizations o ON o.id = c.organization_id
            WHERE c.id = branches.company_id
              AND c.status = 'active'
              AND o.status = 'active'
        )
    )
    WITH CHECK (
        company_id = NULLIF(current_setting('app.company_id', true), '')::uuid
        AND EXISTS (
            SELECT 1
            FROM tenancy.company_memberships cm
            JOIN tenancy.organization_memberships om
              ON om.organization_id = cm.organization_id
             AND om.user_id = cm.user_id
            WHERE cm.company_id = branches.company_id
              AND cm.user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
              AND cm.status = 'active'
              AND cm.role IN ('owner', 'admin')
              AND om.status = 'active'
        )
        AND EXISTS (
            SELECT 1
            FROM tenancy.companies c
            JOIN tenancy.organizations o ON o.id = c.organization_id
            WHERE c.id = branches.company_id
              AND c.status = 'active'
              AND o.status = 'active'
        )
    );

CREATE POLICY branches_delete_admins
    ON tenancy.branches
    FOR DELETE
    USING (
        company_id = NULLIF(current_setting('app.company_id', true), '')::uuid
        AND EXISTS (
            SELECT 1
            FROM tenancy.company_memberships cm
            JOIN tenancy.organization_memberships om
              ON om.organization_id = cm.organization_id
             AND om.user_id = cm.user_id
            WHERE cm.company_id = branches.company_id
              AND cm.user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
              AND cm.status = 'active'
              AND cm.role IN ('owner', 'admin')
              AND om.status = 'active'
        )
        AND EXISTS (
            SELECT 1
            FROM tenancy.companies c
            JOIN tenancy.organizations o ON o.id = c.organization_id
            WHERE c.id = branches.company_id
              AND c.status = 'active'
              AND o.status = 'active'
        )
    );

-- Onboarding. The API role cannot insert into organizations, companies or memberships
-- directly. These functions run as their owner (flexicore_owner), take the caller only
-- from app.user_id, pin search_path, and create the owner membership atomically.
CREATE FUNCTION tenancy.create_organization(p_name text, p_slug text)
RETURNS uuid
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, pg_temp
AS $$
DECLARE
    v_user_id uuid := NULLIF(current_setting('app.user_id', true), '')::uuid;
    v_org_id uuid;
BEGIN
    IF v_user_id IS NULL THEN
        RAISE EXCEPTION 'No authenticated user context.' USING ERRCODE = '28000';
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM identity.users u
        WHERE u.id = v_user_id AND u.status = 'active'
    ) THEN
        RAISE EXCEPTION 'Unknown or inactive user.' USING ERRCODE = '28000';
    END IF;

    INSERT INTO tenancy.organizations (name, slug)
    VALUES (p_name, p_slug)
    RETURNING id INTO v_org_id;

    INSERT INTO tenancy.organization_memberships (organization_id, user_id, role)
    VALUES (v_org_id, v_user_id, 'owner');

    RETURN v_org_id;
END;
$$;

CREATE FUNCTION tenancy.create_company(
    p_organization_id uuid,
    p_name text,
    p_slug text,
    p_industry_key text)
RETURNS uuid
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, pg_temp
AS $$
DECLARE
    v_user_id uuid := NULLIF(current_setting('app.user_id', true), '')::uuid;
    v_company_id uuid;
BEGIN
    IF v_user_id IS NULL THEN
        RAISE EXCEPTION 'No authenticated user context.' USING ERRCODE = '28000';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM tenancy.organization_memberships om
        JOIN tenancy.organizations o ON o.id = om.organization_id
        JOIN identity.users u ON u.id = om.user_id
        WHERE om.organization_id = p_organization_id
          AND om.user_id = v_user_id
          AND om.status = 'active'
          AND om.role IN ('owner', 'admin')
          AND o.status = 'active'
          AND u.status = 'active'
    ) THEN
        RAISE EXCEPTION 'Not permitted to create a company in this organization.'
            USING ERRCODE = '42501';
    END IF;

    INSERT INTO tenancy.companies (organization_id, name, slug, industry_key)
    VALUES (p_organization_id, p_name, p_slug, p_industry_key)
    RETURNING id INTO v_company_id;

    INSERT INTO tenancy.company_memberships (company_id, organization_id, user_id, role)
    VALUES (v_company_id, p_organization_id, v_user_id, 'owner');

    RETURN v_company_id;
END;
$$;

REVOKE ALL ON FUNCTION tenancy.create_organization(text, text) FROM PUBLIC;
REVOKE ALL ON FUNCTION tenancy.create_company(uuid, text, text, text) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION tenancy.create_organization(text, text) TO flexicore_app;
GRANT EXECUTE ON FUNCTION tenancy.create_company(uuid, text, text, text) TO flexicore_app;

COMMENT ON FUNCTION tenancy.create_organization(text, text) IS
    'Creates an organization and makes the current app.user_id its owner.';
COMMENT ON FUNCTION tenancy.create_company(uuid, text, text, text) IS
    'Creates a company for an organization owner or admin and makes them its owner.';