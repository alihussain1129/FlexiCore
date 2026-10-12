-- 0003_audit: append-only audit trail. Global table; tenant_id stays nullable until tenants exist (Phase 1c).
-- No foreign keys on purpose: the audit trail must outlive the rows it describes.
create schema audit;
grant usage on schema audit to flexicore_app;

create table audit.events (
    id             uuid primary key default gen_random_uuid(),
    occurred_at    timestamptz not null default now(),
    actor_user_id  uuid,
    tenant_id      uuid,
    action         text not null,
    target_type    text,
    target_id      text,
    outcome        text not null,
    correlation_id text,
    metadata       jsonb not null default '{}'::jsonb,
    constraint ck_events_outcome check (outcome in ('success', 'failure', 'denied')),
    constraint ck_events_action_not_blank check (char_length(btrim(action)) > 0)
);

create index ix_events_occurred_at on audit.events (occurred_at);
create index ix_events_actor on audit.events (actor_user_id, occurred_at);
create index ix_events_action on audit.events (action, occurred_at);

-- The app role can append and read, never change or remove.
grant select, insert on audit.events to flexicore_app;
revoke update, delete, truncate on audit.events from flexicore_app, public;

comment on table audit.events is 'Global table: not tenant-scoped. Append-only for the app role.';