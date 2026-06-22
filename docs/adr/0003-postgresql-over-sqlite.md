# PostgreSQL over SQLite

The database is PostgreSQL, not SQLite. For a single-household self-hosted app, SQLite would be sufficient — the concurrency ceiling will not be reached, and a single volume-mounted file is simpler to operate than a separate container.

PostgreSQL was chosen because the developer is using this project as a learning vehicle for PostgreSQL. EF Core's migration model is identical between the two, so switching later would be straightforward if needed.
