# SQL Scripts

This folder is reserved for checked-in SQL scripts used by the repository.

For Phase 2 of MCP production hardening, database creation and seed data will be delivered here as SQL scripts instead of being created or seeded by application startup code.

Planned contents:

- database creation script
- database seed script for approved local or non-production clients, if required

Current contents:

- `001_create_client_registry.sql`
  - creates the `ClinicalTrialsMcp` database and required registry tables
- `002_seed_local_dev_client.sql`
  - seeds the local development client and the hashed API key for `clinical-trials-local-dev-key`
- `db_description.md`
  - describes the schema, tables, fields, constraints, and local seed data
