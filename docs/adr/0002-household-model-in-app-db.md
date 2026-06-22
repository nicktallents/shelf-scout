# Household model lives in the app database, not the auth provider

Households, Memberships, and Invite Tokens are owned by this application's database. The auth provider (oauth2-proxy / Google) is responsible only for identity — it does not model group membership or household relationships.

## Considered options

A full IdP such as Authentik can manage groups, which could represent Households. Rejected: it couples domain logic (who is in which household, what role do they have, invite flows) to infrastructure that exists primarily for authentication. Changes to household membership would require touching the auth system. Keeping this in the app DB means the app has full control over its own domain model with no external dependency for CRUD operations.
