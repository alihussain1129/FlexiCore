-- 0002_identity: global (not tenant-scoped) identity tables.
-- identity.refresh_sessions is created now so the schema is settled once; no code uses it until Phase 1b-B.
-- A new schema has no default privileges, so every grant below is explicit and minimal: no delete.
create schema identity;
grant usage on schema identity to flexicore_app;

create table identity.users (
    id                 uuid primary key default gen_random_uuid(),
    email              text not null,
    email_normalized   text not null,
    password_hash      text not null,
    status             text not null default 'active',
    failed_login_count integer not null default 0,
    locked_until       timestamptz,
    created_at         timestamptz not null default now(),
    last_login_at      timestamptz,
    constraint ck_users_status check (status in ('active', 'suspended')),
    constraint ck_users_failed_login_count check (failed_login_count >= 0),
    constraint ck_users_email_length check (char_length(email) between 3 and 254),
    constraint ck_users_email_normalized_length check (char_length(email_normalized) between 3 and 254)
);

create unique index ux_users_email_normalized on identity.users (email_normalized);

create table identity.refresh_sessions (
    id                uuid primary key default gen_random_uuid(),
    user_id           uuid not null references identity.users (id),
    family_id         uuid not null,
    token_hash        bytea not null,
    created_at        timestamptz not null default now(),
    expires_at        timestamptz not null,
    family_expires_at timestamptz not null,
    rotated_at        timestamptz,
    revoked_at        timestamptz,
    revoked_reason    text,
    device_label      text,
    constraint ck_refresh_sessions_token_hash_length check (octet_length(token_hash) = 32),
    constraint ck_refresh_sessions_expiry check (expires_at > created_at and expires_at <= family_expires_at)
);

create unique index ux_refresh_sessions_token_hash on identity.refresh_sessions (token_hash);
create index ix_refresh_sessions_user on identity.refresh_sessions (user_id);
create index ix_refresh_sessions_family on identity.refresh_sessions (family_id);

grant select, insert, update on identity.users to flexicore_app;
grant select, insert, update on identity.refresh_sessions to flexicore_app;

comment on table identity.users is 'Global table: not tenant-scoped.';
comment on table identity.refresh_sessions is 'Global table: not tenant-scoped.';