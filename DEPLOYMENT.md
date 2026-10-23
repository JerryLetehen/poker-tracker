# Poker Tracker — Deployment & Operations Runbook

Two Telegram Mini Apps (public + admin) backed by one .NET API and SQLite, deployed as a
**single Fly.io app** that serves both SPAs and the API. Access is enforced server-side by
a manual allow-list keyed on Telegram user id, with one-tap approval of people who "knock".

No secrets live in this repo. All secrets are Fly secrets (set with `fly secrets set`), or
local `.env*` files that are gitignored.

---

## 1. Architecture

```
Telegram public bot ─▶ Public Mini App  (/)       ┐
Telegram admin  bot ─▶ Admin  Mini App  (/admin/) ┤─ HTTPS + initData ─▶ Fly app ─▶ .NET API
                                                   ┘                                 ├─ serves both SPAs
                                                                                     └─ SQLite on a Fly volume (/data)
```

- One Fly app (`poker-tracker-rd`) runs a single machine (SQLite is single-writer) with a
  Fly **volume** mounted at `/data` for the database.
- The API serves the **public** SPA at `/`, the **admin** SPA at `/admin/`, and the API
  itself under `/api/*` (the `/api` prefix is stripped internally). The Telegram bot
  webhook is at `/bot/<public|admin>/webhook/<secret>`.
- `auto_stop_machines` lets the app sleep when idle and wake on the next request (brief
  cold start) to minimise cost.
- Access control is entirely the allow-list: admin endpoints require your Telegram id;
  unknown users are recorded as pending for you to approve in the admin app.

---

## 2. Telegram setup (BotFather)

Create **two prod bots** — a public and an admin bot:

| Bot | Opens | Fly secret |
| --- | --- | --- |
| public bot | the public app at `/` | `TelegramAuth__PublicBotToken` |
| admin bot | the admin app at `/admin/` | `TelegramAuth__AdminBotToken` |

For each bot, set **Bot Settings → Menu Button** to:

- public → `https://poker-tracker-rd.fly.dev/`
- admin → `https://poker-tracker-rd.fly.dev/admin/`

(Optionally set the public bot's **Main Mini App** so it can be opened from a
`https://t.me/<publicbot>?startapp` link you share/pin in a group.)

### Webhooks (so `/start` replies with the user's id)

For each bot, register the webhook with that bot's token and the shared webhook secret:

```
https://api.telegram.org/bot<BOT_TOKEN>/setWebhook?url=https://poker-tracker-rd.fly.dev/bot/<public|admin>/webhook/<WEBHOOK_SECRET>
```

Use `public` for the public bot, `admin` for the admin bot. If you ever **revoke** a bot
token, you must re-run `setWebhook` with the new token (the URL/secret stay the same) and
update the matching Fly secret.

### Finding a Telegram id

Message a bot with `/start` — it replies with the sender's numeric id. The first admin is
seeded from `TelegramAuth__BootstrapAdminIds`; everyone else is approved from the admin app.

---

## 3. Local development

No external setup needed — dev-mock auth lets both apps run in a plain browser.

```bash
./scripts/dev.sh
# Public: http://localhost:5173   Admin: http://localhost:5174   API: http://localhost:5264
```

`poker_frontend/.env.local` and `poker_admin/.env.local` set `VITE_DEV_MOCK_AUTH=true`.

### Testing the real bot locally (tunnel)

1. `./scripts/dev.sh`
2. Expose a frontend over HTTPS, e.g. `cloudflared tunnel --url http://localhost:5173`.
3. Point a test bot's Mini App URL at the tunnel URL, open it on your phone (real initData).
4. For the API to validate real initData, run it with mock auth off and the bot token set
   (see `.env.example` for the variable names; keep real values in a gitignored `.env*`).

The Vite configs allow `*.trycloudflare.com` / `*.ngrok*` hosts for this.

---

## 4. Fly.io setup (one-time)

```bash
fly auth login

# Create the app (name must be globally unique) and a volume for SQLite:
fly apps create poker-tracker-rd
fly volumes create poker_data --app poker-tracker-rd --region fra --size 1

# Set secrets (values go to Fly, never into the repo):
fly secrets set \
  TelegramAuth__PublicBotToken='<prod-public-token>' \
  TelegramAuth__AdminBotToken='<prod-admin-token>' \
  TelegramAuth__BootstrapAdminIds='<your-telegram-id>' \
  TelegramAuth__WebhookSecret='<random hex, e.g. openssl rand -hex 32>' \
  Cors__AllowedOrigins__0='https://poker-tracker-rd.fly.dev' \
  --app poker-tracker-rd

fly deploy --app poker-tracker-rd --ha=false
```

`fly.toml` holds the non-secret config (single machine, volume mount at `/data`,
`FrontendHosting__Enabled=true`, health check on `/health`, auto-stop when idle).

### Seeding real data into the Fly volume

The volume starts empty. To load an existing SQLite database:

```bash
# Consolidate WAL into the file locally first:
cp data/poker-dev.db /tmp/seed.db && sqlite3 /tmp/seed.db "PRAGMA wal_checkpoint(TRUNCATE); VACUUM;"

# Upload and swap it in:
fly ssh sftp shell --app poker-tracker-rd  # then: put /tmp/seed.db /data/poker-seed.db
fly ssh console --app poker-tracker-rd -C "sh -c 'rm -f /data/poker.db /data/poker.db-wal /data/poker.db-shm && mv /data/poker-seed.db /data/poker.db && chown app:app /data/poker.db'"
fly apps restart poker-tracker-rd
```

---

## 5. Deploying updates

```bash
fly deploy --app poker-tracker-rd --ha=false
```

Migrations run automatically on startup. The volume (and its data) persists across deploys.
Ask Kiro to "deploy to Fly" and it runs this for you.

Rollback: `fly releases --app poker-tracker-rd` to list, then
`fly deploy --image <previous-image-ref>` or `fly releases rollback`.

---

## 6. Managing access

- A new person opens the bot → sees an **Access pending** screen with their Telegram id,
  and is recorded as pending.
- In the **admin** app → **Access** tab → approve them to Public or Admin (or add/remove by
  id). Changes take effect immediately, no redeploy.

---

## 7. Backups

- The Fly volume has **automatic daily snapshots** (retained several days) — see
  `fly volumes snapshots list <volume-id>`.
- The admin app's **Database** tab offers an on-demand `.zip` download (SQLite online
  backup, WAL-safe). Keep a copy off-platform.
- To pull the live DB: `fly ssh sftp get /data/poker.db ./poker-backup.db --app poker-tracker-rd`.

---

## 8. Secrets & going public

- Secrets are Fly secrets (`fly secrets list` shows names only) and local gitignored
  `.env*` files. `.env.example` / `poker_*/.env.example` are the committed templates.
- `fly.toml` is safe to commit (no secrets).
- Run `./scripts/scan-secrets.sh` to scan git history before publishing.
- Install the pre-commit secret guard once: `./scripts/install-git-hooks.sh`.
- If any real token is ever exposed, revoke it in BotFather, `fly secrets set` the new one,
  re-run `setWebhook`, and redeploy.
