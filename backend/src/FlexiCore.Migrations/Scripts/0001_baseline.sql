-- 0001_baseline: the privilege contract between the migration (owner) role and the runtime (app) role.
-- The app role never owns tables, so row-level security added by later migrations always binds it.
grant usage on schema public to flexicore_app;

alter default privileges for role flexicore_owner in schema public
    grant select, insert, update, delete on tables to flexicore_app;

alter default privileges for role flexicore_owner in schema public
    grant usage, select on sequences to flexicore_app;