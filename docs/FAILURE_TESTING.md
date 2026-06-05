# ECommerceBot — Failure Scenario Test Plan

Version: 1.0.0

This document describes how to manually test that ECommerceBot handles failure conditions gracefully. All tests should be performed in a staging environment before each production release.

---

## 1. SQL Server Unavailable

**Objective**: Verify the API does not crash and serves degraded health while the database is down.

**Steps**:
```bash
# Stop only the database
docker compose stop sqlserver

# Check health
curl http://localhost:8080/health
# Expected: HTTP 503, "status": "Unhealthy", database check = "Unhealthy"

# Check liveness
curl http://localhost:8080/health/live
# Expected: HTTP 503 (liveness includes DB check)

# Send a Telegram message to bot
# Expected: Bot does not respond (dispatch fails), but container stays alive

# Check logs
docker compose logs api --tail=20
# Expected: "Database health check failed" warnings — no crash/restart
```

**Recovery**:
```bash
docker compose start sqlserver
# Wait ~30 seconds for SQL Server to be ready
curl http://localhost:8080/health
# Expected: Returns to Healthy
```

---

## 2. Redis Unavailable

**Objective**: Verify the app falls back to in-memory cache when Redis is down.

**Steps**:
```bash
# Stop Redis
docker compose stop redis

# Check health
curl http://localhost:8080/health
# Expected: HTTP 503, redis check = "Degraded" (not Unhealthy — Redis is optional)

# Send a Telegram message to bot
# Expected: Bot responds normally using in-memory cache fallback

# Check logs
docker compose logs api | grep -i redis
# Expected: "Redis connection failed — falling back to in-memory" on next restart
# Or "Degraded" in health check (no crash)
```

**Recovery**:
```bash
docker compose start redis
# Redis reconnects automatically
curl http://localhost:8080/health
# Expected: redis check returns to Healthy
```

---

## 3. Telegram API Unavailable

**Objective**: Verify the bot handles Telegram API timeouts without crashing.

**Steps**:
- Block outbound Telegram API access (firewall or hosts file)
- Send a message to the bot webhook manually:
  ```bash
  curl -X POST http://localhost:8080/api/telegram/webhook \
    -H "Content-Type: application/json" \
    -H "X-Telegram-Bot-Api-Secret-Token: your-secret" \
    -d '{"update_id":1,"message":{"message_id":1,"date":1733406600,"from":{"id":123,"is_bot":false,"first_name":"Test"},"chat":{"id":123,"type":"private"},"text":"/start"}}'
  ```

**Expected**:
- HTTP 200 returned immediately (fire-and-forget dispatch)
- Background dispatch logs errors like "Error processing webhook update"
- No container crash
- Health checks continue to pass

---

## 4. Backup Directory Not Writable

**Objective**: Verify backup failure does not crash the application.

**Steps**:
```bash
# Make backup directory read-only
docker compose exec api chmod 444 /backups

# Wait for next backup cycle (or restart service)
docker compose logs api | grep -i backup
# Expected: "Backup failed after ... Will retry at next scheduled interval."

# Health check
curl http://localhost:8080/health/ready
# Expected: backup-directory check = "Degraded"

# Restore permissions
docker compose exec api chmod 755 /backups
```

---

## 5. Invalid License

**Objective**: Verify the bot blocks requests in production when license is invalid.

**Precondition**: `License:Enabled=true`, `License:RequireValidLicenseInProduction=true`, `ASPNETCORE_ENVIRONMENT=Production`

**Steps**:
```bash
# Deactivate the license from database
docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$SA_PASSWORD" \
  -d ECommerceBotDb \
  -Q "UPDATE LicenseInfos SET IsActive = 0"

# Force license re-check (wait up to 6h or restart)
docker compose restart api

# Send webhook request
curl -X POST http://localhost:8080/api/telegram/webhook \
  -H "Content-Type: application/json" \
  -H "X-Telegram-Bot-Api-Secret-Token: your-secret" \
  -d '{"update_id":1}'
# Expected: HTTP 503, {"error":"service_unavailable","status":"Disabled"}

# Health check
curl http://localhost:8080/health
# Expected: Still passes (health is always allowed)
```

---

## 6. Expired License

**Objective**: Verify expired license blocks requests (beyond grace period).

**Steps**:
```bash
# Set license to expire in the past, past grace period
docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$SA_PASSWORD" \
  -d ECommerceBotDb \
  -Q "UPDATE LicenseInfos SET ExpiresAt = '2020-01-01', GracePeriodEndsAt = '2020-01-04'"

docker compose restart api

# Send webhook request
# Expected: HTTP 503, status = "Expired"
```

---

## 7. Missing Environment Variables

**Objective**: Verify StartupValidator prevents a misconfigured production start.

**Steps**:
```bash
# Remove bot token from environment
# Edit .env: TELEGRAM_BOT_TOKEN=

docker compose up api
# Expected: Container starts, logs "Missing configuration: Telegram:BotToken — Bot token is not configured."
# In Production: Container exits with "Application cannot start: 1 required configuration value(s) are missing."
# In Development: Warning logged, app continues
```

---

## 8. Duplicate Receipt Attack

**Objective**: Verify a user cannot submit the same receipt twice.

**Steps**:
1. Place an order normally and submit a receipt photo
2. Go through the order flow again with the same product
3. Forward the same receipt photo from your Telegram history (same FileUniqueId)

**Expected**: Bot responds with `❌ This receipt has already been submitted.` on the second attempt.

---

## 9. Non-Admin Attempting Admin Actions

**Objective**: Verify admin callback data cannot be used by regular users.

**Steps**:
- As a non-admin user, manually craft a callback with `adm:` prefix:
  ```bash
  curl -X POST http://localhost:8080/api/telegram/webhook \
    -H "Content-Type: application/json" \
    -H "X-Telegram-Bot-Api-Secret-Token: your-secret" \
    -d '{"update_id":9,"callback_query":{"id":"x","from":{"id":CUSTOMER_ID,...},"data":"adm:cat:1:toggle","message":{...}}}'
  ```

**Expected**: Bot sends `❌ Admin only.` to the user and logs a Warning with the user's TelegramId.

---

## 10. Rate Limit Exceeded

**Objective**: Verify the rate limiter returns 429 when exceeded.

**Steps**:
```bash
# Send 301 rapid requests
for i in $(seq 1 301); do
  curl -s -o /dev/null -w "%{http_code}\n" \
    -X POST http://localhost:8080/api/telegram/webhook \
    -H "Content-Type: application/json" \
    -H "X-Telegram-Bot-Api-Secret-Token: your-secret" \
    -d '{"update_id":'$i'}' &
done | sort | uniq -c
# Expected: Most return 200, some return 429 after limit is exceeded
```

---

## Test Results Tracker

| Scenario | Date Tested | Result | Notes |
|----------|------------|--------|-------|
| SQL Server unavailable | | | |
| Redis unavailable | | | |
| Telegram API unavailable | | | |
| Backup directory not writable | | | |
| Invalid license | | | |
| Expired license | | | |
| Missing env vars | | | |
| Duplicate receipt | | | |
| Non-admin admin action | | | |
| Rate limit exceeded | | | |
