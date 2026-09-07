-- Provable Terms of Use / Privacy Policy consent: when and which version each
-- account accepted. Null for legacy accounts until their next sign-in stamps it.
alter table users add column if not exists terms_accepted_at timestamptz null;
alter table users add column if not exists terms_version text null;
