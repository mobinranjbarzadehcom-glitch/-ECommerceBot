# ECommerceBot — Production Checklist

**Version: 1.0.0** | See also: [SECURITY.md](SECURITY.md) · [BACKUP_RESTORE.md](BACKUP_RESTORE.md) · [FAILURE_TESTING.md](FAILURE_TESTING.md)

Use this checklist before going live and after every major deployment.

---

## Environment Variables

- [ ] `SA_PASSWORD` set — strong password (8+ chars, upper/lower/digit/symbol)
- [ ] `TELEGRAM_BOT_TOKEN` set — obtained from @BotFather
- [ ] `TELEGRAM_WEBHOOK_SECRET` set — random 32+ char string
- [ ] `TELEGRAM_ADMIN_CHAT_ID` set — your Telegram user ID
- [ ] `TELEGRAM_BACKUP_CHANNEL_ID` set (or 0 to disable)

---

## Telegram

- [ ] Bot created via @BotFather
- [ ] Bot token stored securely (not committed to git)
- [ ] Webhook registered: `./scripts/set-webhook.sh`
- [ ] Webhook confirmed:
  ```bash
  curl "https://api.telegram.org/bot<TOKEN>/getWebhookInfo"
  ```
  Expected: `"url"` matches your domain and `"pending_update_count"` is low
- [ ] Webhook secret token matches `TELEGRAM_WEBHOOK_SECRET`
- [ ] Admin chat ID responds to admin commands (`/admin`)

---

## Database

- [ ] SQL Server container healthy: `docker compose ps`
- [ ] Migrations applied (check startup logs):
  ```
  [INF] Database migrations applied
  ```
- [ ] Connection string uses SQL Server auth (not Windows auth) in Docker
- [ ] SA password not the default `YourStrong!Passw0rd`
- [ ] Database health check passes: `curl http://localhost:8080/health`

---

## Redis

- [ ] Redis container healthy: `docker compose exec redis redis-cli ping`
- [ ] `Redis__ConnectionString` set in environment
- [ ] Redis health check returns Healthy (not Degraded):
  ```bash
  curl http://localhost:8080/health | python3 -m json.tool
  ```
- [ ] If Redis is down, app falls back gracefully to memory cache (check logs for `WARNING`)

---

## Backups

- [ ] `BACKUP_ENABLED=true` in production `.env`
- [ ] `BACKUP_RETENTION_DAYS` set appropriately (default: 7)
- [ ] `BACKUP_SCHEDULE_HOURS` set (default: 24)
- [ ] Backup directory accessible: backup health check is Healthy
- [ ] First backup verified after 5-minute startup delay
- [ ] Backup files visible in Docker volume:
  ```bash
  docker compose exec api ls /backups/
  ```
- [ ] Restore procedure tested at least once (see Restore section below)

---

## SSL

- [ ] Domain points to server IP
- [ ] SSL certificate obtained (Let's Encrypt): `sudo certbot --nginx -d your.domain`
- [ ] Certificate auto-renewal configured: `sudo certbot renew --dry-run`
- [ ] Nginx proxies `/api/telegram/webhook` to port 8080
- [ ] HTTPS enforced (HTTP redirects to HTTPS)
- [ ] Telegram can reach webhook (confirmed via `getWebhookInfo`)

---

## Monitoring & Logs

- [ ] Health endpoints reachable from VPS:
  ```bash
  curl http://localhost:8080/health/live
  curl http://localhost:8080/health/ready
  ```
- [ ] Log rotation configured (Serilog 30-day retention set in appsettings)
- [ ] Log files being written:
  ```bash
  docker compose exec api ls /app/Logs/
  ```
- [ ] Error log empty (or reviewed): `Logs/errors-YYYYMMDD.log`
- [ ] Optional: set up uptime monitoring (UptimeRobot, Grafana, etc.) on `/health/live`

---

## Security

- [ ] `.env` not committed to git (check `.gitignore`)
- [ ] SQL Server port 1433 not exposed to internet (remove `ports:` from production compose)
- [ ] Redis port 6379 not exposed to internet
- [ ] API port 8080 not directly exposed — only via Nginx proxy
- [ ] Webhook secret header validated (enabled automatically when `WebhookSecretToken` is set)

---

## Restore Procedure

In case of data loss:

```bash
# 1. Stop the API (prevent new writes)
docker compose stop api

# 2. Restore from backup file
docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$SA_PASSWORD" \
  -Q "RESTORE DATABASE [ECommerceBotDb] FROM DISK = N'/backups/ECommerceBotDb_YYYYMMDD_HHmmss.bak' WITH REPLACE"

# 3. Restart API
docker compose start api

# 4. Verify
curl http://localhost:8080/health
```

---

## Post-Deployment Smoke Test

```bash
# 1. All containers running
docker compose ps

# 2. Health check passes
curl -s http://localhost:8080/health | python3 -m json.tool

# 3. Liveness
curl -s http://localhost:8080/health/live

# 4. Readiness
curl -s http://localhost:8080/health/ready

# 5. Send a message to the bot in Telegram — confirm it responds
# 6. Check logs for errors
docker compose logs api --tail=100 | grep -i error
```

---

## Load Test (Optional but Recommended)

Run the load tester against the live API before announcing to users:

```bash
cd tools/ECommerceBot.LoadTester
dotnet run -- --url https://your.domain --users 100 --scenario all \
  --secret "$TELEGRAM_WEBHOOK_SECRET" --parallel 10
```

Expected: ✅ PASS on all scenarios. See [LOAD_TESTING.md](LOAD_TESTING.md) for full guide.

---

## Failure Testing (Pre-Launch)

Verify critical failure modes before launch. See [FAILURE_TESTING.md](FAILURE_TESTING.md):

- [ ] SQL Server unavailable — bot stays alive, health returns Unhealthy
- [ ] Redis unavailable — app falls back to memory cache, health shows Degraded
- [ ] Duplicate receipt rejected
- [ ] Non-admin cannot use admin callbacks
- [ ] License blocks webhook in production when invalid
