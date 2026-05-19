# Railway PostgreSQL Setup for Yurt Backend

This document explains how to configure the .NET 10 backend to use PostgreSQL on Railway.

## What changed

- The backend now uses `Npgsql.EntityFrameworkCore.PostgreSQL` instead of SQL Server.
- `Yurt.Infrastructure/DependencyInjection.cs` now reads the DB connection string from:
  - `DATABASE_URL`
  - `RAILWAY_DATABASE_URL`
  - `ConnectionStrings:DefaultConnection` (fallback for local development)
- `Yurt.WebApi/appsettings.json` now contains a local PostgreSQL connection string example.

## Railway environment variables

Railway typically provides PostgreSQL credentials through a URL like:

```text
postgres://username:password@host:port/database
```

The backend will automatically detect this URL from an environment variable named either:

- `DATABASE_URL`
- `RAILWAY_DATABASE_URL`

If you are deploying on Railway, set the Postgres add-on and verify the provided `DATABASE_URL` variable.

## What to set in Railway

1. Go to your Railway project.
2. Add the PostgreSQL plugin / database.
3. In the Railway Environment variables panel, confirm that `DATABASE_URL` is present.
4. If Railway provides a different variable name, add `RAILWAY_DATABASE_URL` with the same value.
5. Add any other required secrets for your app, for example:
   - `Jwt__Secret`
   - `Jwt__Issuer`
   - `Jwt__Audience`
   - `Payment__WebhookCallbackUrl`
   - `Payment__WebhookSecret`
   - `Payment__SandboxSecret`

> Note: In .NET configuration, nested settings use `__` to separate levels when stored as environment variables.

## Local development fallback

If `DATABASE_URL` / `RAILWAY_DATABASE_URL` is not set, the app now falls back to the `ConnectionStrings:DefaultConnection` value in `appsettings.json`.

Example local PostgreSQL connection string:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=YurtDb;Username=postgres;Password=postgres;Pooling=true;"
}
```

Update this to match your local Postgres credentials if needed.

## Running migrations

Because the app now uses `UseNpgsql`, you should use EF Core migrations against PostgreSQL.

From the `backend/src/Yurt.Infrastructure` project folder:

```bash
dotnet ef migrations add <Name> --project Yurt.Infrastructure.csproj --startup-project ../Yurt.WebApi/Yurt.WebApi.csproj
```

Then apply migrations:

```bash
dotnet ef database update --project Yurt.Infrastructure.csproj --startup-project ../Yurt.WebApi/Yurt.WebApi.csproj
```

## Important notes

- Railway `DATABASE_URL` is already in URL format, and the backend converts it into a valid Npgsql connection string.
- Ensure `Jwt__Secret` is strong and at least 32 characters.
- `AllowedOrigins` may need to include your Railway app origin if the frontend is hosted separately.
