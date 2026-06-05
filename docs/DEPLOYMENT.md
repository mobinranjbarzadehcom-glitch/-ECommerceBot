# ECommerceBot — Deployment Guide

**Version: 1.0.0** | See also: [SECURITY.md](SECURITY.md) · [BACKUP_RESTORE.md](BACKUP_RESTORE.md) · [PRODUCTION_CHECKLIST.md](PRODUCTION_CHECKLIST.md)

## VPS Requirements

| Resource | Minimum | Recommended |
|---|---|---|
| CPU | 1 vCPU | 2 vCPU |
| RAM | 1 GB | 2 GB |
| Disk | 20 GB SSD | 40 GB SSD |
| OS | Ubuntu 22.04 LTS | Ubuntu 22.04 LTS |
| Docker | 24+ | 24+ |
| Docker Compose | 2.20+ | 2.20+ |

---

## 1. Initial Server Setup

```bash
# Install Docker
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER
newgrp docker

# Verify
docker --version
docker compose version
```

---

## 2. Clone & Configure

```bash
git clone <your-repo-url> /opt/ecommercebot
cd /opt/ecommercebot

# Create environment file from template
cp .env.example .env
nano .env      # Fill in all required values
```

**Required values in `.env`:**

| Variable | Description |
|---|---|
| `SA_PASSWORD` | SQL Server SA password (min 8 chars, mixed case + symbol) |
| `TELEGRAM_BOT_TOKEN` | Token from @BotFather |
| `TELEGRAM_WEBHOOK_SECRET` | Random secret string (used to verify Telegram requests) |
| `TELEGRAM_ADMIN_CHAT_ID` | Your Telegram user ID (get from @userinfobot) |

---

## 3. SSL Setup (required for Telegram webhooks)

Telegram requires HTTPS for webhooks. Use Nginx + Let's Encrypt in front of the container.

### Install Nginx + Certbot

```bash
sudo apt install nginx certbot python3-certbot-nginx -y

# Obtain certificate (replace with your domain)
sudo certbot --nginx -d bot.yourdomain.com
```

### Nginx Configuration

Create `/etc/nginx/sites-available/ecommercebot`:

```nginx
server {
    listen 443 ssl;
    server_name bot.yourdomain.com;

    ssl_certificate     /etc/letsencrypt/live/bot.yourdomain.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/bot.yourdomain.com/privkey.pem;

    location /api/telegram/webhook {
        proxy_pass         http://127.0.0.1:8080;
        proxy_set_header   Host $host;
        proxy_set_header   X-Real-IP $remote_addr;
        proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
    }

    location /health {
        proxy_pass http://127.0.0.1:8080;
        allow 127.0.0.1;
        deny  all;
    }
}
```

```bash
sudo ln -s /etc/nginx/sites-available/ecommercebot /etc/nginx/sites-enabled/
sudo nginx -t && sudo systemctl reload nginx

# Auto-renew certificate
sudo certbot renew --dry-run
```

---

## 4. Docker Deployment

```bash
cd /opt/ecommercebot

# Production deployment (uses both compose files)
docker compose -f docker-compose.yml -f docker-compose.production.yml up -d

# Verify all containers are running
docker compose ps

# Tail logs
docker compose logs -f api
```

The API container automatically:
1. Waits for SQL Server and Redis to be healthy
2. Applies pending EF Core migrations
3. Starts the bot

---

## 5. Register Telegram Webhook

After deployment, register the webhook with Telegram:

```bash
# Linux
TELEGRAM_BOT_TOKEN="your-token" \
TELEGRAM_WEBHOOK_URL="https://bot.yourdomain.com/api/telegram/webhook" \
TELEGRAM_WEBHOOK_SECRET="your-secret" \
./scripts/set-webhook.sh
```

```powershell
# Windows
$env:TELEGRAM_BOT_TOKEN = "your-token"
$env:TELEGRAM_WEBHOOK_URL = "https://bot.yourdomain.com/api/telegram/webhook"
$env:TELEGRAM_WEBHOOK_SECRET = "your-secret"
.\scripts\set-webhook.ps1
```

---

## 6. Redis

Redis is included in `docker-compose.yml` and starts automatically. Configuration:

- `Redis__ConnectionString=redis:6379` (set in compose)
- `Redis__InstanceName=ECommerceBot:` (key namespace)
- Persistence: AOF enabled (`appendonly yes`)
- Memory limit: 128 MB with `allkeys-lru` eviction
- If Redis is unavailable at startup, the app falls back to in-memory cache automatically

---

## 7. Database Backup

Enable automated backups in `.env`:

```bash
BACKUP_ENABLED=true
BACKUP_RETENTION_DAYS=7
BACKUP_SCHEDULE_HOURS=24
```

Backups are written to the `backups` Docker volume (shared between `api` and `sqlserver` containers) at `/backups/ECommerceBotDb_YYYYMMDD_HHmmss.bak`.

**Manual backup:**
```bash
docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$SA_PASSWORD" \
  -Q "BACKUP DATABASE [ECommerceBotDb] TO DISK = N'/backups/manual_$(date +%Y%m%d).bak'"
```

---

## 8. Upgrade Process

```bash
cd /opt/ecommercebot
git pull

# Rebuild and restart (zero-downtime not guaranteed — single instance)
docker compose -f docker-compose.yml -f docker-compose.production.yml up -d --build api

# Verify
docker compose ps
curl http://localhost:8080/health
```

EF Core migrations are applied automatically on container startup.

---

## 9. Health Endpoints

| Endpoint | Purpose |
|---|---|
| `GET /health` | All checks (DB + Redis + Backup) |
| `GET /health/live` | Liveness — is the process alive? (DB only) |
| `GET /health/ready` | Readiness — is it ready to serve traffic? (all checks) |

All return JSON with per-check details and HTTP 200 (healthy) or 503 (unhealthy/degraded).

---

## 10. Troubleshooting

**Container fails to start:**
```bash
docker compose logs api --tail=50
```

**Database connection errors:**
```bash
docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$SA_PASSWORD" -Q "SELECT 1"
```

**Redis not connecting:**
```bash
docker compose exec redis redis-cli ping
```

**Reset bot state (delete webhook and pending updates):**
```bash
TELEGRAM_BOT_TOKEN="..." DROP_PENDING=true ./scripts/delete-webhook.sh
```

**View logs:**
```bash
# Container stdout
docker compose logs -f api

# Application log files
docker compose exec api tail -f /app/Logs/api-$(date +%Y%m%d).log
docker compose exec api tail -f /app/Logs/errors-$(date +%Y%m%d).log
```
