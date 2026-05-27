-- Initial seed: empty banking database. EF migrations create the schema on app startup.
-- This file exists so the postgres-init hook fires (postgres needs at least one SQL file).
CREATE EXTENSION IF NOT EXISTS pgcrypto;
