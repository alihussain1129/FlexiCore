-- Local username sign-in. Forward-only: 0002_identity.sql is not modified.
-- Hosted email accounts keep working; local accounts may have a username and no email.

alter table identity.users alter column email drop not null;
alter table identity.users alter column email_normalized drop not null;

alter table identity.users add column username text;
alter table identity.users add column username_normalized text;

alter table identity.users add constraint ck_users_username_length
    check (username is null or char_length(username) between 3 and 32);
alter table identity.users add constraint ck_users_username_normalized_length
    check (username_normalized is null or char_length(username_normalized) between 3 and 32);

-- Each identifier is stored together with its normalized form, or not at all.
alter table identity.users add constraint ck_users_email_pair
    check ((email is null) = (email_normalized is null));
alter table identity.users add constraint ck_users_username_pair
    check ((username is null) = (username_normalized is null));

-- Every account must be able to sign in with something.
alter table identity.users add constraint ck_users_login_identifier
    check (email_normalized is not null or username_normalized is not null);

create unique index ux_users_username_normalized
    on identity.users (username_normalized)
    where username_normalized is not null;

comment on column identity.users.username_normalized is
    'Lower-case invariant form of username; unique. Used for local sign-in.';