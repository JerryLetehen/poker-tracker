# Poker Tracker

A home-game poker tracker delivered as two Telegram Mini Apps — a **public** app (leaderboard,
games, player stats) and an **admin** app (record games, manage players, control access) —
backed by a .NET minimal API and SQLite. Access is gated entirely server-side by a manual
allow-list of Telegram user ids, with a one-tap approval flow for new people.

## Stack

- **API:** .NET 10 minimal API, Entity Framework Core, SQLite.
- **Frontends:** React + Vite + Tailwind (two SPAs: `poker_frontend` public, `poker_admin` admin).
- **Auth:** Telegram Mini App `initData` validation (HMAC-SHA256) + allow-list authorization.
- **Deploy:** single Fly.io app serving both SPAs and the API; SQLite on a Fly volume.

## How access works

Each request from a Mini App carries Telegram's signed `initData`. The API validates it
against the relevant bot token, then checks the user's Telegram id against the allow-list:

- **Public endpoints** require the user to be on the public list (admins included).
- **Admin endpoints** require the user to be on the admin list.
- Unknown-but-valid users are recorded as **pending** and shown their own Telegram id, so an
  admin can approve them from the admin app — no redeploy.

The first admin is seeded from configuration; everyone else is managed in-app.

## Local development

```bash
./scripts/dev.sh
# Public: http://localhost:5173   Admin: http://localhost:5174   API: http://localhost:5264
```

Local dev uses mock auth (no Telegram bot needed). To exercise the real Telegram flow, see
[DEPLOYMENT.md](./DEPLOYMENT.md) (tunnel section).

## Configuration & secrets

No secrets are committed. Configuration is read from environment variables; see
[`.env.example`](./.env.example) and the per-frontend `.env.example` files for the full list.
In production, secrets are Fly secrets (`fly secrets set`).

## Deployment

See [DEPLOYMENT.md](./DEPLOYMENT.md) for the full Fly.io setup, bot configuration, data
seeding, updates, backups, and access management.

## Tests

```bash
dotnet test                       # API (xUnit)
cd poker_frontend && npx vitest run
cd poker_admin && npx vitest run
```
