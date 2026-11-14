-- 0011: count tenant-wide quotas without weakening row-level security for ordinary application queries.
CREATE FUNCTION tenancy.count_active_organization_users(p_organization_id uuid)
RETURNS integer
LANGUAGE plpgsql
STABLE
SECURITY DEFINER
SET search_path = pg_catalog, tenancy
AS $$
DECLARE
    actor_id uuid := NULLIF(current_setting('app.user_id', true), '')::uuid;
    total integer;
BEGIN
    IF actor_id IS NULL OR NOT EXISTS (
        SELECT 1 FROM tenancy.organization_memberships caller
        WHERE caller.organization_id = p_organization_id
          AND caller.user_id = actor_id AND caller.status = 'active'
          AND caller.role IN ('owner', 'admin')) THEN
        RAISE EXCEPTION 'organization administrator required' USING ERRCODE = '42501';
    END IF;
    SELECT count(*)::integer INTO total FROM tenancy.organization_memberships member
    WHERE member.organization_id = p_organization_id AND member.status = 'active';
    RETURN total;
END;
$$;

CREATE FUNCTION tenancy.count_organization_companies(p_organization_id uuid)
RETURNS integer
LANGUAGE plpgsql
STABLE
SECURITY DEFINER
SET search_path = pg_catalog, tenancy
AS $$
DECLARE
    actor_id uuid := NULLIF(current_setting('app.user_id', true), '')::uuid;
    total integer;
BEGIN
    IF actor_id IS NULL OR NOT EXISTS (
        SELECT 1 FROM tenancy.organization_memberships caller
        WHERE caller.organization_id = p_organization_id
          AND caller.user_id = actor_id AND caller.status = 'active'
          AND caller.role IN ('owner', 'admin')) THEN
        RAISE EXCEPTION 'organization administrator required' USING ERRCODE = '42501';
    END IF;
    SELECT count(*)::integer INTO total FROM tenancy.companies company
    WHERE company.organization_id = p_organization_id AND company.status = 'active';
    RETURN total;
END;
$$;

REVOKE ALL ON FUNCTION tenancy.count_active_organization_users(uuid) FROM PUBLIC;
REVOKE ALL ON FUNCTION tenancy.count_organization_companies(uuid) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION tenancy.count_active_organization_users(uuid) TO flexicore_app;
GRANT EXECUTE ON FUNCTION tenancy.count_organization_companies(uuid) TO flexicore_app;
