-- 0006: audit rows and an organization cap for onboarding.
-- 0005 is already applied and immutable; this migration replaces its two functions in place.
-- CREATE OR REPLACE keeps the function owner (flexicore_owner) and the existing grants.
--
-- Successful creations are audited here, inside the same transaction, so an audit row cannot be
-- skipped and cannot exist without the organization or company it describes. Refusals roll the
-- transaction back, so the application audits those separately.

CREATE OR REPLACE FUNCTION tenancy.create_organization(p_name text, p_slug text)
RETURNS uuid
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, pg_temp
AS $$
DECLARE
    -- Changing this limit means a new migration, on purpose: the rule lives with the data.
    c_max_owned_organizations CONSTANT integer := 10;
    v_user_id uuid := NULLIF(current_setting('app.user_id', true), '')::uuid;
    v_correlation_id text := NULLIF(current_setting('app.correlation_id', true), '');
    v_org_id uuid;
    v_owned integer;
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

    -- Serialize creations per user so two simultaneous requests cannot both pass the cap check.
    PERFORM pg_advisory_xact_lock(hashtextextended('tenancy.create_organization:' || v_user_id::text, 0));

    SELECT count(*) INTO v_owned
    FROM tenancy.organization_memberships om
    WHERE om.user_id = v_user_id
      AND om.role = 'owner'
      AND om.status = 'active';

    IF v_owned >= c_max_owned_organizations THEN
        RAISE EXCEPTION 'Organization limit reached.' USING ERRCODE = '53400';
    END IF;

    INSERT INTO tenancy.organizations (name, slug)
    VALUES (p_name, p_slug)
    RETURNING id INTO v_org_id;

    INSERT INTO tenancy.organization_memberships (organization_id, user_id, role)
    VALUES (v_org_id, v_user_id, 'owner');

    INSERT INTO audit.events (actor_user_id, action, target_type, target_id, outcome, correlation_id, metadata)
    VALUES (v_user_id, 'tenancy.organization.create', 'organization', v_org_id::text, 'success',
            v_correlation_id, jsonb_build_object('slug', p_slug));

    RETURN v_org_id;
END;
$$;

CREATE OR REPLACE FUNCTION tenancy.create_company(
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
    v_correlation_id text := NULLIF(current_setting('app.correlation_id', true), '');
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

    -- Company creation always targets the parent organization, for both successes and refusals.
    INSERT INTO audit.events (actor_user_id, action, target_type, target_id, outcome, correlation_id, metadata)
    VALUES (v_user_id, 'tenancy.company.create', 'organization', p_organization_id::text, 'success',
            v_correlation_id,
            jsonb_build_object('company_id', v_company_id, 'slug', p_slug, 'industry_key', p_industry_key));

    RETURN v_company_id;
END;
$$;

REVOKE ALL ON FUNCTION tenancy.create_organization(text, text) FROM PUBLIC;
REVOKE ALL ON FUNCTION tenancy.create_company(uuid, text, text, text) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION tenancy.create_organization(text, text) TO flexicore_app;
GRANT EXECUTE ON FUNCTION tenancy.create_company(uuid, text, text, text) TO flexicore_app;

COMMENT ON FUNCTION tenancy.create_organization(text, text) IS
    'Creates an organization for app.user_id (owner), enforces the owned-organization cap, and audits the success.';
COMMENT ON FUNCTION tenancy.create_company(uuid, text, text, text) IS
    'Creates a company for an organization owner or admin and audits the success.';