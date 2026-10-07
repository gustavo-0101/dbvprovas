-- Papéis do dbvprovas (D-121): o dono das tabelas roda as migrations; o app nunca é dono
-- nem ignora o RLS. As senhas chegam como variáveis do psql (-v owner_password=... -v app_password=...).
CREATE ROLE dbv_owner LOGIN PASSWORD :'owner_password';
CREATE ROLE dbv_app LOGIN PASSWORD :'app_password' NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS;
CREATE DATABASE dbvprovas OWNER dbv_owner;
\connect dbvprovas
REVOKE CREATE ON SCHEMA public FROM PUBLIC;
GRANT USAGE ON SCHEMA public TO dbv_app;
