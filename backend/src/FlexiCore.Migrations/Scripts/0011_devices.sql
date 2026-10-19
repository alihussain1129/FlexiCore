-- 0011_devices: paired devices for activation-only (Local) installations.
-- Only hashes are stored: never a device secret or a pairing code. Devices are never deleted, only revoked.
create schema devices;
grant usage on schema devices to flexicore_app;

create table devices.devices (
    id            uuid primary key default gen_random_uuid(),
    name          text not null,
    secret_hash   bytea not null,
    is_owner      boolean not null default false,
    created_at    timestamptz not null default now(),
    last_seen_at  timestamptz,
    revoked_at    timestamptz,
    constraint ck_devices_name check (char_length(btrim(name)) between 1 and 80),
    constraint ck_devices_secret_hash check (octet_length(secret_hash) = 32)
);

create table devices.pairing_codes (
    id                    uuid primary key default gen_random_uuid(),
    code_hash             bytea not null unique,
    is_owner              boolean not null default false,
    created_by_device_id  uuid references devices.devices (id),
    created_at            timestamptz not null default now(),
    expires_at            timestamptz not null,
    consumed_at           timestamptz,
    consumed_by_device_id uuid references devices.devices (id),
    constraint ck_pairing_codes_hash check (octet_length(code_hash) = 32),
    constraint ck_pairing_codes_expiry check (expires_at > created_at)
);

create index ix_pairing_codes_outstanding on devices.pairing_codes (expires_at) where consumed_at is null;

grant select on devices.devices to flexicore_app;
grant insert (name, secret_hash, is_owner) on devices.devices to flexicore_app;
grant update (last_seen_at, revoked_at) on devices.devices to flexicore_app;

grant select on devices.pairing_codes to flexicore_app;
grant insert (code_hash, is_owner, created_by_device_id, expires_at) on devices.pairing_codes to flexicore_app;
grant update (consumed_at, consumed_by_device_id) on devices.pairing_codes to flexicore_app;

comment on table devices.devices is 'Paired devices. secret_hash is SHA-256 of a 256-bit random secret; the secret itself is never stored.';
comment on table devices.pairing_codes is 'One-time pairing codes (hashed). Single use; expire after minutes.';