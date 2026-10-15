-- 0008: change a member's role and remove a member.
-- Both take a per-organization advisory lock, so the last-owner check cannot race with another change.
-- SQLSTATE 55000 means "would leave the organization without an active owner".

CREATE FUNCTION tenancy.change_organization_member_role(
    p_organization_id uuid,
    p_target_user_id uuid,
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
    v_target_role text;
    v_owners integer;
BEGIN
    IF v_user_id IS NULL THEN
        RAISE EXCEPTION 'No authenticated user context.' USING ERRCODE = '28000';
    END IF;

    PERFORM pg_advisory_xact_lock(hashtextextended('tenancy.members:' || p_organization_id::text, 0));

    SELECT om.role INTO v_caller_role
    FROM tenancy.organization_memberships om
    JOIN tenancy.organizations o ON o.id = om.organization_id
    JOIN identity.users u ON u.id = om.user_id
    WHERE om.organization_id = p_organization_id
      AND om.user_id = v_user_id
      AND om.status = 'active'
      AND o.status = 'active'
      AND u.status = 'active';

    IF v_caller_role IS DISTINCT FROM 'owner' THEN
        RAISE EXCEPTION 'Not permitted to change roles.' USING ERRCODE = '42501';
    END IF;

    SELECT om.role INTO v_target_role
    FROM tenancy.organization_memberships om
    WHERE om.organization_id = p_organization_id AND om.user_id = p_target_user_id
    FOR UPDATE;

    IF v_target_role IS NULL THEN
        RAISE EXCEPTION 'Not permitted to change roles.' USING ERRCODE = '42501';
    END IF;

    IF v_target_role = 'owner' AND p_role IS DISTINCT FROM 'owner' THEN
        SELECT count(*) INTO v_owners
        FROM tenancy.organization_memberships om
        WHERE om.organization_id = p_organization_id AND om.role = 'owner' AND om.status = 'active';

        IF v_owners <= 1 THEN
            RAISE EXCEPTION 'An organization must keep at least one active owner.' USING ERRCODE = '55000';
        END IF;
    END IF;

    -- An invalid role raises a check violation (23514).
    UPDATE tenancy.organization_memberships
    SET role = p_role
    WHERE organization_id = p_organization_id AND user_id = p_target_user_id;

    INSERT INTO audit.events (actor_user_id, action, target_type, target_id, outcome, correlation_id, metadata)
    VALUES (v_user_id, 'tenancy.member.role_change', 'organization', p_organization_id::text, 'success',
            v_correlation_id,
            jsonb_build_object('member_user_id', p_target_user_id, 'from_role', v_target_role, 'role', p_role));

    RETURN p_target_user_id;
END;
$$;

CREATE FUNCTION tenancy.remove_organization_member(
    p_organization_id uuid,
    p_target_user_id uuid)
RETURNS uuid
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, pg_temp
AS $$
DECLARE
    v_user_id uuid := NULLIF(current_setting('app.user_id', true), '')::uuid;
    v_correlation_id text := NULLIF(current_setting('app.correlation_id', true), '');
    v_caller_role text;
    v_target_role text;
    v_owners integer;
    v_company_rows integer;
BEGIN
    IF v_user_id IS NULL THEN
        RAISE EXCEPTION 'No authenticated user context.' USING ERRCODE = '28000';
    END IF;

    PERFORM pg_advisory_xact_lock(hashtextextended('tenancy.members:' || p_organization_id::text, 0));

    SELECT om.role INTO v_caller_role
    FROM tenancy.organization_memberships om
    JOIN tenancy.organizations o ON o.id = om.organization_id
    JOIN identity.users u ON u.id = om.user_id
    WHERE om.organization_id = p_organization_id
      AND om.user_id = v_user_id
      AND om.status = 'active'
      AND o.status = 'active'
      AND u.status = 'active';

    IF v_caller_role IS NULL THEN
        RAISE EXCEPTION 'Not permitted to remove this member.' USING ERRCODE = '42501';
    END IF;

    SELECT om.role INTO v_target_role
    FROM tenancy.organization_memberships om
    WHERE om.organization_id = p_organization_id AND om.user_id = p_target_user_id
    FOR UPDATE;

    IF v_target_role IS NULL THEN
        RAISE EXCEPTION 'Not permitted to remove this member.' USING ERRCODE = '42501';
    END IF;

    -- Leaving is always allowed. Otherwise owners remove anyone and admins remove plain members.
    IF p_target_user_id <> v_user_id
       AND NOT (v_caller_role = 'owner' OR (v_caller_role = 'admin' AND v_target_role = 'member')) THEN
        RAISE EXCEPTION 'Not permitted to remove this member.' USING ERRCODE = '42501';
    END IF;

    IF v_target_role = 'owner' THEN
        SELECT count(*) INTO v_owners
        FROM tenancy.organization_memberships om
        WHERE om.organization_id = p_organization_id AND om.role = 'owner' AND om.status = 'active';

        IF v_owners <= 1 THEN
            RAISE EXCEPTION 'An organization must keep at least one active owner.' USING ERRCODE = '55000';
        END IF;
    END IF;

    -- company_memberships references the organization membership, so it must go first.
    DELETE FROM tenancy.company_memberships
    WHERE organization_id = p_organization_id AND user_id = p_target_user_id;
    GET DIAGNOSTICS v_company_rows = ROW_COUNT;

    DELETE FROM tenancy.organization_memberships
    WHERE organization_id = p_organization_id AND user_id = p_target_user_id;

    INSERT INTO audit.events (actor_user_id, action, target_type, target_id, outcome, correlation_id, metadata)
    VALUES (v_user_id, 'tenancy.member.remove', 'organization', p_organization_id::text, 'success',
            v_correlation_id,
            jsonb_build_object('member_user_id', p_target_user_id, 'role', v_target_role,
                               'company_memberships_removed', v_company_rows));

    RETURN p_target_user_id;
END;
$$;

REVOKE ALL ON FUNCTION tenancy.change_organization_member_role(uuid, uuid, text) FROM PUBLIC;
REVOKE ALL ON FUNCTION tenancy.remove_organization_member(uuid, uuid) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION tenancy.change_organization_member_role(uuid, uuid, text) TO flexicore_app;
GRANT EXECUTE ON FUNCTION tenancy.remove_organization_member(uuid, uuid) TO flexicore_app;

COMMENT ON FUNCTION tenancy.change_organization_member_role(uuid, uuid, text) IS
    'Owner-only role change. Refuses to demote the last active owner (55000). Audits the success.';
COMMENT ON FUNCTION tenancy.remove_organization_member(uuid, uuid) IS
    'Removes a member (owner: anyone, admin: members, anyone: self), deleting their company memberships first. Refuses to remove the last active owner (55000). Audits the success.';