-- 0012_catalog: units and products (catalog slice 1).
-- The app role gets narrow column grants: it can never write version, updated_at, change_seq or
-- origin_device_id, and it can never delete. A trigger stamps those columns, so a client cannot fake them.
-- Writes are limited to company owners and admins by row-level security. Records are soft-deleted.
create schema catalog;
grant usage on schema catalog to flexicore_app;

create sequence catalog.change_seq as bigint;

-- Role of the current caller (app.user_id) in the current company (app.company_id), or null when the caller
-- has no active access. SECURITY DEFINER so it works whatever the caller may read in tenancy.
create function catalog.caller_role()
returns text
language sql
stable
security definer
set search_path = pg_catalog, pg_temp
as $$
    select cm.role
    from tenancy.company_memberships cm
    join tenancy.organization_memberships om
      on om.organization_id = cm.organization_id and om.user_id = cm.user_id
    join tenancy.companies c on c.id = cm.company_id
    join tenancy.organizations o on o.id = c.organization_id
    join identity.users u on u.id = cm.user_id
    where cm.company_id = nullif(current_setting('app.company_id', true), '')::uuid
      and cm.user_id = nullif(current_setting('app.user_id', true), '')::uuid
      and cm.status = 'active' and om.status = 'active'
      and c.status = 'active' and o.status = 'active' and u.status = 'active'
$$;

revoke all on function catalog.caller_role() from public;
grant execute on function catalog.caller_role() to flexicore_app;

-- Stamps version, updated_at and change_seq. Runs as the owner so the app role needs no sequence rights.
create function catalog.stamp_row()
returns trigger
language plpgsql
security definer
set search_path = pg_catalog, pg_temp
as $$
begin
    if tg_op = 'INSERT' then
        new.version := 1;
    else
        if new.id <> old.id or new.company_id <> old.company_id then
            raise exception 'id and company_id are immutable.' using errcode = '23514';
        end if;
        new.version := old.version + 1;
    end if;
    new.updated_at := now();
    new.change_seq := nextval('catalog.change_seq');
    return new;
end;
$$;

revoke all on function catalog.stamp_row() from public;

create table catalog.units (
    id               uuid primary key,
    company_id       uuid not null references tenancy.companies (id),
    code             text not null,
    name             text not null,
    version          integer not null default 1,
    updated_at       timestamptz not null default now(),
    origin_device_id uuid,
    deleted_at       timestamptz,
    change_seq       bigint not null,
    constraint uq_units_id_company unique (id, company_id),
    constraint ck_units_code check (code ~ '^[A-Za-z0-9][A-Za-z0-9._-]{0,19}$'),
    constraint ck_units_name check (char_length(btrim(name)) between 1 and 160)
);

create unique index uq_units_company_code on catalog.units (company_id, code) where deleted_at is null;
create index ix_units_company_change on catalog.units (company_id, change_seq);

create table catalog.products (
    id               uuid primary key,
    company_id       uuid not null references tenancy.companies (id),
    sku              text not null,
    barcode          text,
    name             text not null,
    unit_id          uuid,
    sale_price       numeric(18,4) not null default 0,
    cost_price       numeric(18,4) not null default 0,
    version          integer not null default 1,
    updated_at       timestamptz not null default now(),
    origin_device_id uuid,
    deleted_at       timestamptz,
    change_seq       bigint not null,
    constraint uq_products_id_company unique (id, company_id),
    constraint fk_products_unit foreign key (unit_id, company_id) references catalog.units (id, company_id),
    constraint ck_products_sku check (sku ~ '^[A-Za-z0-9][A-Za-z0-9._-]{0,39}$'),
    constraint ck_products_barcode check (barcode is null or barcode ~ '^[A-Za-z0-9-]{4,64}$'),
    constraint ck_products_name check (char_length(btrim(name)) between 1 and 160),
    constraint ck_products_sale_price check (sale_price >= 0),
    constraint ck_products_cost_price check (cost_price >= 0)
);

create unique index uq_products_company_sku on catalog.products (company_id, sku) where deleted_at is null;
create index ix_products_company_change on catalog.products (company_id, change_seq);
create index ix_products_company_barcode on catalog.products (company_id, barcode) where barcode is not null;
create index ix_products_company_name on catalog.products (company_id, name);

create trigger trg_units_stamp before insert or update on catalog.units
    for each row execute function catalog.stamp_row();
create trigger trg_products_stamp before insert or update on catalog.products
    for each row execute function catalog.stamp_row();

-- Row-level security: members read, owners and admins write, always inside the selected company.
alter table catalog.units enable row level security;
alter table catalog.products enable row level security;

create policy units_select on catalog.units for select
    using (company_id = nullif(current_setting('app.company_id', true), '')::uuid
           and catalog.caller_role() is not null);
create policy units_insert on catalog.units for insert
    with check (company_id = nullif(current_setting('app.company_id', true), '')::uuid
                and catalog.caller_role() in ('owner', 'admin'));
create policy units_update on catalog.units for update
    using (company_id = nullif(current_setting('app.company_id', true), '')::uuid
           and catalog.caller_role() in ('owner', 'admin'))
    with check (company_id = nullif(current_setting('app.company_id', true), '')::uuid
                and catalog.caller_role() in ('owner', 'admin'));

create policy products_select on catalog.products for select
    using (company_id = nullif(current_setting('app.company_id', true), '')::uuid
           and catalog.caller_role() is not null);
create policy products_insert on catalog.products for insert
    with check (company_id = nullif(current_setting('app.company_id', true), '')::uuid
                and catalog.caller_role() in ('owner', 'admin'));
create policy products_update on catalog.products for update
    using (company_id = nullif(current_setting('app.company_id', true), '')::uuid
           and catalog.caller_role() in ('owner', 'admin'))
    with check (company_id = nullif(current_setting('app.company_id', true), '')::uuid
                and catalog.caller_role() in ('owner', 'admin'));

-- No delete grant: records are soft-deleted by setting deleted_at.
grant select on catalog.units to flexicore_app;
grant insert (id, company_id, code, name) on catalog.units to flexicore_app;
grant update (code, name, deleted_at) on catalog.units to flexicore_app;

grant select on catalog.products to flexicore_app;
grant insert (id, company_id, sku, barcode, name, unit_id, sale_price, cost_price) on catalog.products to flexicore_app;
grant update (sku, barcode, name, unit_id, sale_price, cost_price, deleted_at) on catalog.products to flexicore_app;

comment on table catalog.units is 'Units of measure. Tenant-scoped (company_id, RLS). Soft delete via deleted_at.';
comment on table catalog.products is 'Products. Tenant-scoped (company_id, RLS). Soft delete via deleted_at.';
comment on column catalog.units.origin_device_id is 'Reserved for the sync slice. Not writable by the app role.';