-- 0007: accounting foundation. Double-entry ledger, decimal-safe, tenant-isolated.
-- Writes go through SECURITY DEFINER functions; the app role has SELECT only.
-- Not yet included: fiscal periods and period locking, multi-currency, drafts and approvals,
-- audit event rows (to follow once the audit writer has been reviewed).

CREATE SCHEMA accounting;
GRANT USAGE ON SCHEMA accounting TO flexicore_app;

CREATE TABLE accounting.accounts (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id uuid NOT NULL REFERENCES tenancy.companies(id),
    code text NOT NULL,
    name text NOT NULL,
    account_type text NOT NULL,
    parent_id uuid,
    is_postable boolean NOT NULL DEFAULT true,
    status text NOT NULL DEFAULT 'active',
    created_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_accounts_company_code UNIQUE (company_id, code),
    CONSTRAINT uq_accounts_id_company UNIQUE (id, company_id),
    CONSTRAINT fk_accounts_parent FOREIGN KEY (parent_id, company_id)
        REFERENCES accounting.accounts(id, company_id),
    CONSTRAINT ck_accounts_code CHECK (code ~ '^[A-Za-z0-9][A-Za-z0-9.-]{0,31}$'),
    CONSTRAINT ck_accounts_name CHECK (char_length(btrim(name)) BETWEEN 1 AND 160),
    CONSTRAINT ck_accounts_type
        CHECK (account_type IN ('asset', 'liability', 'equity', 'revenue', 'expense')),
    CONSTRAINT ck_accounts_status CHECK (status IN ('active', 'inactive'))
);

CREATE TABLE accounting.journal_entries (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id uuid NOT NULL REFERENCES tenancy.companies(id),
    entry_number bigint NOT NULL,
    entry_date date NOT NULL,
    memo text NOT NULL,
    reverses_entry_id uuid,
    idempotency_key text,
    posted_by uuid NOT NULL REFERENCES identity.users(id),
    posted_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_journal_entries_company_number UNIQUE (company_id, entry_number),
    CONSTRAINT uq_journal_entries_id_company UNIQUE (id, company_id),
    CONSTRAINT fk_journal_entries_reverses FOREIGN KEY (reverses_entry_id, company_id)
        REFERENCES accounting.journal_entries(id, company_id),
    CONSTRAINT ck_journal_entries_memo CHECK (char_length(btrim(memo)) BETWEEN 1 AND 500),
    CONSTRAINT ck_journal_entries_key
        CHECK (idempotency_key IS NULL OR char_length(idempotency_key) BETWEEN 1 AND 100)
);

-- An entry can be reversed only once, and a retried request cannot post twice.
CREATE UNIQUE INDEX uq_journal_entries_reverses
    ON accounting.journal_entries(reverses_entry_id) WHERE reverses_entry_id IS NOT NULL;
CREATE UNIQUE INDEX uq_journal_entries_idempotency
    ON accounting.journal_entries(company_id, idempotency_key) WHERE idempotency_key IS NOT NULL;

CREATE TABLE accounting.journal_lines (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    entry_id uuid NOT NULL,
    company_id uuid NOT NULL,
    line_no integer NOT NULL,
    account_id uuid NOT NULL,
    debit numeric(19,4) NOT NULL DEFAULT 0,
    credit numeric(19,4) NOT NULL DEFAULT 0,
    description text,
    CONSTRAINT uq_journal_lines_entry_line UNIQUE (entry_id, line_no),
    CONSTRAINT fk_journal_lines_entry FOREIGN KEY (entry_id, company_id)
        REFERENCES accounting.journal_entries(id, company_id),
    CONSTRAINT fk_journal_lines_account FOREIGN KEY (account_id, company_id)
        REFERENCES accounting.accounts(id, company_id),
    CONSTRAINT ck_journal_lines_nonnegative CHECK (debit >= 0 AND credit >= 0),
    -- exactly one side carries the amount
    CONSTRAINT ck_journal_lines_one_side CHECK ((debit = 0) <> (credit = 0)),
    CONSTRAINT ck_journal_lines_description
        CHECK (description IS NULL OR char_length(description) <= 300)
);

CREATE INDEX ix_accounts_company ON accounting.accounts(company_id, status);
CREATE INDEX ix_journal_entries_company_date ON accounting.journal_entries(company_id, entry_date);
CREATE INDEX ix_journal_lines_account ON accounting.journal_lines(company_id, account_id);

-- Defense in depth: even the owner role cannot commit an unbalanced entry.
CREATE FUNCTION accounting.assert_entry_balanced()
RETURNS trigger
LANGUAGE plpgsql
SET search_path = pg_catalog, pg_temp
AS $$
DECLARE
    v_debit numeric;
    v_credit numeric;
    v_count integer;
BEGIN
    SELECT coalesce(sum(debit), 0), coalesce(sum(credit), 0), count(*)
      INTO v_debit, v_credit, v_count
      FROM accounting.journal_lines
     WHERE entry_id = NEW.entry_id;
    IF v_count < 2 OR v_debit <> v_credit THEN
        RAISE EXCEPTION 'Journal entry is not balanced.' USING ERRCODE = '23514';
    END IF;
    RETURN NULL;
END;
$$;

CREATE CONSTRAINT TRIGGER trg_journal_lines_balanced
    AFTER INSERT ON accounting.journal_lines
    DEFERRABLE INITIALLY DEFERRED
    FOR EACH ROW EXECUTE FUNCTION accounting.assert_entry_balanced();

-- Posted records are never edited. Mistakes are corrected by a reversing entry.
CREATE FUNCTION accounting.forbid_change()
RETURNS trigger
LANGUAGE plpgsql
SET search_path = pg_catalog, pg_temp
AS $$
BEGIN
    RAISE EXCEPTION 'Posted accounting records are immutable. Post a reversing entry instead.'
        USING ERRCODE = '55000';
END;
$$;

CREATE TRIGGER trg_journal_entries_immutable
    BEFORE UPDATE OR DELETE ON accounting.journal_entries
    FOR EACH ROW EXECUTE FUNCTION accounting.forbid_change();
CREATE TRIGGER trg_journal_lines_immutable
    BEFORE UPDATE OR DELETE ON accounting.journal_lines
    FOR EACH ROW EXECUTE FUNCTION accounting.forbid_change();
CREATE TRIGGER trg_journal_entries_no_truncate
    BEFORE TRUNCATE ON accounting.journal_entries
    FOR EACH STATEMENT EXECUTE FUNCTION accounting.forbid_change();
CREATE TRIGGER trg_journal_lines_no_truncate
    BEFORE TRUNCATE ON accounting.journal_lines
    FOR EACH STATEMENT EXECUTE FUNCTION accounting.forbid_change();

-- Access check shared by the policies and the write functions.
CREATE FUNCTION accounting.has_company_access(p_company_id uuid, p_require_admin boolean)
RETURNS boolean
LANGUAGE sql
STABLE
SECURITY DEFINER
SET search_path = pg_catalog, pg_temp
AS $$
    SELECT p_company_id IS NOT NULL
       AND p_company_id = NULLIF(current_setting('app.company_id', true), '')::uuid
       AND EXISTS (
            SELECT 1
              FROM tenancy.company_memberships cm
              JOIN tenancy.organization_memberships om
                ON om.organization_id = cm.organization_id AND om.user_id = cm.user_id
              JOIN tenancy.companies c ON c.id = cm.company_id
              JOIN tenancy.organizations o ON o.id = c.organization_id
              JOIN identity.users u ON u.id = cm.user_id
             WHERE cm.company_id = p_company_id
               AND cm.user_id = NULLIF(current_setting('app.user_id', true), '')::uuid
               AND cm.status = 'active'
               AND om.status = 'active'
               AND c.status = 'active'
               AND o.status = 'active'
               AND u.status = 'active'
               AND (NOT p_require_admin OR cm.role IN ('owner', 'admin'))
       );
$$;

ALTER TABLE accounting.accounts ENABLE ROW LEVEL SECURITY;
ALTER TABLE accounting.journal_entries ENABLE ROW LEVEL SECURITY;
ALTER TABLE accounting.journal_lines ENABLE ROW LEVEL SECURITY;

CREATE POLICY accounts_read_members ON accounting.accounts
    FOR SELECT USING (accounting.has_company_access(company_id, false));
CREATE POLICY journal_entries_read_members ON accounting.journal_entries
    FOR SELECT USING (accounting.has_company_access(company_id, false));
CREATE POLICY journal_lines_read_members ON accounting.journal_lines
    FOR SELECT USING (accounting.has_company_access(company_id, false));

CREATE FUNCTION accounting.create_account(
    p_code text,
    p_name text,
    p_account_type text,
    p_parent_id uuid DEFAULT NULL,
    p_is_postable boolean DEFAULT true)
RETURNS uuid
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, pg_temp
AS $$
DECLARE
    v_company uuid := NULLIF(current_setting('app.company_id', true), '')::uuid;
    v_id uuid;
BEGIN
    IF NULLIF(current_setting('app.user_id', true), '') IS NULL OR v_company IS NULL THEN
        RAISE EXCEPTION 'No authenticated company context.' USING ERRCODE = '28000';
    END IF;
    IF NOT accounting.has_company_access(v_company, true) THEN
        RAISE EXCEPTION 'Not permitted to manage accounts.' USING ERRCODE = '42501';
    END IF;
    INSERT INTO accounting.accounts (company_id, code, name, account_type, parent_id, is_postable)
    VALUES (v_company, p_code, p_name, p_account_type, p_parent_id, p_is_postable)
    RETURNING id INTO v_id;
    RETURN v_id;
END;
$$;

-- p_lines: [{"accountId": "...", "debit": "100.00", "credit": "0", "description": "..."}]
CREATE FUNCTION accounting.post_journal_entry(
    p_entry_date date,
    p_memo text,
    p_lines jsonb,
    p_idempotency_key text DEFAULT NULL)
RETURNS uuid
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, pg_temp
AS $$
DECLARE
    v_user uuid := NULLIF(current_setting('app.user_id', true), '')::uuid;
    v_company uuid := NULLIF(current_setting('app.company_id', true), '')::uuid;
    v_entry uuid;
    v_number bigint;
    v_line jsonb;
    v_n integer := 0;
    v_debit numeric;
    v_credit numeric;
    v_total_debit numeric := 0;
    v_total_credit numeric := 0;
    v_account uuid;
BEGIN
    IF v_user IS NULL OR v_company IS NULL THEN
        RAISE EXCEPTION 'No authenticated company context.' USING ERRCODE = '28000';
    END IF;
    IF NOT accounting.has_company_access(v_company, true) THEN
        RAISE EXCEPTION 'Not permitted to post journal entries.' USING ERRCODE = '42501';
    END IF;
    IF p_entry_date IS NULL THEN
        RAISE EXCEPTION 'Entry date is required.' USING ERRCODE = '23514';
    END IF;
    IF p_lines IS NULL OR jsonb_typeof(p_lines) <> 'array'
       OR jsonb_array_length(p_lines) < 2 OR jsonb_array_length(p_lines) > 500 THEN
        RAISE EXCEPTION 'An entry needs between 2 and 500 lines.' USING ERRCODE = '23514';
    END IF;

    -- One poster per company at a time: gap-free numbers and race-free idempotency.
    PERFORM pg_advisory_xact_lock(hashtextextended('accounting:' || v_company::text, 0));

    IF p_idempotency_key IS NOT NULL THEN
        SELECT id INTO v_entry FROM accounting.journal_entries
         WHERE company_id = v_company AND idempotency_key = p_idempotency_key;
        IF FOUND THEN
            RETURN v_entry;
        END IF;
    END IF;

    SELECT coalesce(max(entry_number), 0) + 1 INTO v_number
      FROM accounting.journal_entries WHERE company_id = v_company;

    INSERT INTO accounting.journal_entries
        (company_id, entry_number, entry_date, memo, idempotency_key, posted_by)
    VALUES (v_company, v_number, p_entry_date, p_memo, p_idempotency_key, v_user)
    RETURNING id INTO v_entry;

    FOR v_line IN SELECT value FROM jsonb_array_elements(p_lines) LOOP
        v_n := v_n + 1;
        v_debit := coalesce((v_line->>'debit')::numeric, 0);
        v_credit := coalesce((v_line->>'credit')::numeric, 0);
        IF v_debit <> round(v_debit, 4) OR v_credit <> round(v_credit, 4) THEN
            RAISE EXCEPTION 'Amounts may have at most 4 decimal places.' USING ERRCODE = '23514';
        END IF;

        SELECT a.id INTO v_account FROM accounting.accounts a
         WHERE a.id = (v_line->>'accountId')::uuid
           AND a.company_id = v_company AND a.is_postable AND a.status = 'active';
        IF NOT FOUND THEN
            RAISE EXCEPTION 'Unknown, inactive or non-postable account.' USING ERRCODE = '23514';
        END IF;

        INSERT INTO accounting.journal_lines
            (entry_id, company_id, line_no, account_id, debit, credit, description)
        VALUES (v_entry, v_company, v_n, v_account, v_debit, v_credit, v_line->>'description');

        v_total_debit := v_total_debit + v_debit;
        v_total_credit := v_total_credit + v_credit;
    END LOOP;

    IF v_total_debit <> v_total_credit THEN
        RAISE EXCEPTION 'Journal entry is not balanced.' USING ERRCODE = '23514';
    END IF;
    RETURN v_entry;
END;
$$;

CREATE FUNCTION accounting.reverse_journal_entry(
    p_entry_id uuid,
    p_entry_date date,
    p_idempotency_key text DEFAULT NULL)
RETURNS uuid
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, pg_temp
AS $$
DECLARE
    v_user uuid := NULLIF(current_setting('app.user_id', true), '')::uuid;
    v_company uuid := NULLIF(current_setting('app.company_id', true), '')::uuid;
    v_original accounting.journal_entries%ROWTYPE;
    v_entry uuid;
    v_number bigint;
BEGIN
    IF v_user IS NULL OR v_company IS NULL THEN
        RAISE EXCEPTION 'No authenticated company context.' USING ERRCODE = '28000';
    END IF;
    IF NOT accounting.has_company_access(v_company, true) THEN
        RAISE EXCEPTION 'Not permitted to reverse journal entries.' USING ERRCODE = '42501';
    END IF;
    IF p_entry_date IS NULL THEN
        RAISE EXCEPTION 'Entry date is required.' USING ERRCODE = '23514';
    END IF;

    PERFORM pg_advisory_xact_lock(hashtextextended('accounting:' || v_company::text, 0));

    IF p_idempotency_key IS NOT NULL THEN
        SELECT id INTO v_entry FROM accounting.journal_entries
         WHERE company_id = v_company AND idempotency_key = p_idempotency_key;
        IF FOUND THEN
            RETURN v_entry;
        END IF;
    END IF;

    SELECT * INTO v_original FROM accounting.journal_entries
     WHERE id = p_entry_id AND company_id = v_company;
    IF NOT FOUND THEN
        RAISE EXCEPTION 'Journal entry not found.' USING ERRCODE = '23503';
    END IF;

    SELECT coalesce(max(entry_number), 0) + 1 INTO v_number
      FROM accounting.journal_entries WHERE company_id = v_company;

    -- A second reversal of the same entry violates uq_journal_entries_reverses (23505).
    INSERT INTO accounting.journal_entries
        (company_id, entry_number, entry_date, memo, reverses_entry_id, idempotency_key, posted_by)
    VALUES (v_company, v_number, p_entry_date,
            left('Reversal of entry ' || v_original.entry_number::text, 500),
            v_original.id, p_idempotency_key, v_user)
    RETURNING id INTO v_entry;

    INSERT INTO accounting.journal_lines
        (entry_id, company_id, line_no, account_id, debit, credit, description)
    SELECT v_entry, v_company, l.line_no, l.account_id, l.credit, l.debit, l.description
      FROM accounting.journal_lines l
     WHERE l.entry_id = v_original.id;

    RETURN v_entry;
END;
$$;

-- Runs as the caller, so row-level security limits it to the selected company.
CREATE FUNCTION accounting.trial_balance(p_as_of date DEFAULT NULL)
RETURNS TABLE (
    account_id uuid,
    account_code text,
    account_name text,
    account_type text,
    total_debit numeric,
    total_credit numeric)
LANGUAGE sql
STABLE
SET search_path = pg_catalog, pg_temp
AS $$
    SELECT a.id, a.code, a.name, a.account_type,
           coalesce(sum(x.debit), 0)::numeric(19,4),
           coalesce(sum(x.credit), 0)::numeric(19,4)
      FROM accounting.accounts a
      LEFT JOIN (
            SELECT l.account_id, l.debit, l.credit
              FROM accounting.journal_lines l
              JOIN accounting.journal_entries e ON e.id = l.entry_id
             WHERE p_as_of IS NULL OR e.entry_date <= p_as_of
      ) x ON x.account_id = a.id
     GROUP BY a.id, a.code, a.name, a.account_type
     ORDER BY a.code;
$$;

REVOKE ALL ON ALL TABLES IN SCHEMA accounting FROM flexicore_app;
GRANT SELECT ON accounting.accounts, accounting.journal_entries, accounting.journal_lines
    TO flexicore_app;

REVOKE ALL ON FUNCTION accounting.assert_entry_balanced() FROM PUBLIC;
REVOKE ALL ON FUNCTION accounting.forbid_change() FROM PUBLIC;
REVOKE ALL ON FUNCTION accounting.has_company_access(uuid, boolean) FROM PUBLIC;
REVOKE ALL ON FUNCTION accounting.create_account(text, text, text, uuid, boolean) FROM PUBLIC;
REVOKE ALL ON FUNCTION accounting.post_journal_entry(date, text, jsonb, text) FROM PUBLIC;
REVOKE ALL ON FUNCTION accounting.reverse_journal_entry(uuid, date, text) FROM PUBLIC;
REVOKE ALL ON FUNCTION accounting.trial_balance(date) FROM PUBLIC;
GRANT EXECUTE ON FUNCTION accounting.has_company_access(uuid, boolean) TO flexicore_app;
GRANT EXECUTE ON FUNCTION accounting.create_account(text, text, text, uuid, boolean) TO flexicore_app;
GRANT EXECUTE ON FUNCTION accounting.post_journal_entry(date, text, jsonb, text) TO flexicore_app;
GRANT EXECUTE ON FUNCTION accounting.reverse_journal_entry(uuid, date, text) TO flexicore_app;
GRANT EXECUTE ON FUNCTION accounting.trial_balance(date) TO flexicore_app;

COMMENT ON SCHEMA accounting IS 'Double-entry general ledger. Writes only through functions.';
