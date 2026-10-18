-- 0010: organization members can be identified by username (local accounts have no email).
-- add_organization_member takes one normalized identifier: an '@' means email, anything else means username.
-- list_organization_members also returns the username. Both are dropped and recreated because Postgres cannot
-- change a function's parameter names or result columns in place; grants are re-applied below.

DROP FUNCTION tenancy.add_organization_member(uuid, text, text);
DROP FUNCTION tenancy.list_organization_members(uuid);

CREATE FUNCTION tenancy.list_organization_members(p_organization_id uuid)
RETURNS TABLE (member_user_id uuid, member_email text, member_role text, member_status text, joined_at timestamptz, member_username text)
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, pg_temp
AS $$
DECLARE
    v_user_id uuid := NULLIF(current_setting('app.user_id', true), '')::uuid;
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
        RAISE EXCEPTION 'Not permitted to list members of this organization.' USING ERRCODE = '42501';
    END IF;

    RETURN QUERY
    SELECT om.user_id, u.email, om.role, om.status, om.created_at, u.username
    FROM tenancy.organization_memberships om
    JOIN identity.users u ON u.id = om.user_id
    WHERE om.organization_id = p_organization_id
    ORDER BY om.created_at, om.user_id
    LIMIT 500;
END;
$$;

CREATE FUNCTION tenancy.add_organization_member(
    p_organization_id uuid,
    p_identifier_normalized text,
    p_role text)
RETURNS uuid
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, pg_temp
AS $$
DECLARE
    v_user_id uuid := NULLIF(current_setting('app.user_id', true), '')::uuid;
    v_correlation_id text := NULLIF(current_setting('app.correlation_id', true), '');
    v_caller_role text;
    v_target_id uuid;
BEGIN
    IF v_user_id IS NULL THEN
        RAISE EXCEPTION 'No authenticated user context.' USING ERRCODE = '28000';
    END IF;

    SELECT om.role INTO v_caller_role
    FROM tenancy.organization_memberships om
    JOIN tenancy.organizations o ON o.id = om.organization_id
    JOIN identity.users u ON u.id = om.user_id
    WHERE om.organization_id = p_organization_id
      AND om.user_id = v_user_id
      AND om.status = 'active'
      AND om.role IN ('owner', 'admin')
      AND o.status = 'active'
      AND u.status = 'active';

    -- Admins may add plain members only; owners may add any role.
    IF v_caller_role IS NULL OR (v_caller_role = 'admin' AND p_role IS DISTINCT FROM 'member') THEN
        RAISE EXCEPTION 'Not permitted to add this member.' USING ERRCODE = '42501';
    END IF;

    IF p_identifier_normalized LIKE '%@%' THEN
        SELECT u.id INTO v_target_id
        FROM identity.users u
        WHERE u.email_normalized = p_identifier_normalized AND u.status = 'active';
    ELSE
        SELECT u.id INTO v_target_id
        FROM identity.users u
        WHERE u.username_normalized = p_identifier_normalized AND u.status = 'active';
    END IF;

    IF v_target_id IS NULL THEN
        -- Same refusal as a permission failure, so the response does not say whether the account exists.
        RAISE EXCEPTION 'Not permitted to add this member.' USING ERRCODE = '42501';
    END IF;

    -- A duplicate raises a unique violation (23505); an invalid role raises a check violation (23514).
    INSERT INTO tenancy.organization_memberships (organization_id, user_id, role)
    VALUES (p_organization_id, v_target_id, p_role);

    INSERT INTO audit.events (actor_user_id, action, target_type, target_id, outcome, correlation_id, metadata)
    VALUES (v_user_id, 'tenancy.member.add', 'organization', p_organization_id::text, 'success',
            v_correlation_id, jsonb_build_object('member_user_id', v_target_id, 'role', p_role));

    RETURN v_target_id;
END;
$$;

REVOKE ALL ON FUNCTION tenancy.list_organization_members(uuid) FROM PUBLIC;
REVOKE ALL ON FUNCTION tenancy.add_organization_member(uuid, text, text) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION tenancy.list_organization_members(uuid) TO flexicore_app;
GRANT EXECUTE ON FUNCTION tenancy.add_organization_member(uuid, text, text) TO flexicore_app;

COMMENT ON FUNCTION tenancy.list_organization_members(uuid) IS
    'Lists members of an organization (email and username) for an active owner or admin of it.';
COMMENT ON FUNCTION tenancy.add_organization_member(uuid, text, text) IS
    'Adds an existing active user (by normalized email if it contains @, else normalized username) to an organization; owners add any role, admins add members. Audits the success.';