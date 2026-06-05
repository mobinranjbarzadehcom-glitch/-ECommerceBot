# ECommerceBot — Backup and Restore

Version: 1.0.0

---

## How Backup Works

ECommerceBot runs `DatabaseBackupService` as a .NET background service. It performs a SQL Server `BACKUP DATABASE` command on a configurable schedule.

### Configuration

In `.env`:
```bash
BACKUP_ENABLED=true
BACKUP_SCHEDULE_HOURS=24      # How often to back up (hours)
BACKUP_RETENTION_DAYS=7       # How many days to keep old backups
BACKUP_DIRECTORY=/backups     # Directory inside the container
```

In `appsettings.json`:
```json
"Backup": {
  "Enabled": true,
  "ScheduleHours": 24,
  "RetentionDays": 7,
  "Directory": "Backups"
}
```

### Backup Schedule

1. API container starts
2. 5-minute delay (allows SQL Server to fully initialize)
3. First backup runs
4. Subsequent backups run every `ScheduleHours` hours
5. After each backup, files older than `RetentionDays` are deleted

### Where Backups Are Stored

Backups are stored in the `backups` Docker volume, which is mounted at `/backups` inside both the `api` and `sqlserver` containers.

Naming format:
```
ECommerceBotDb_YYYYMMDD_HHmmss.bak
```

Example: `ECommerceBotDb_20260605_143022.bak`

---

## Manual Backup

Trigger a manual backup at any time:

```bash
docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$SA_PASSWORD" \
  -Q "BACKUP DATABASE [ECommerceBotDb] \
      TO DISK = N'/backups/manual_$(date +%Y%m%d_%H%M%S).bak' \
      WITH COMPRESSION, STATS = 10"
```

---

## Viewing Backup Files

```bash
# List all backup files
docker compose exec api ls -lh /backups/

# Check backup health
curl http://localhost:8080/health | python3 -m json.tool | grep backup
```

---

## Restore Procedure

### Standard Restore

Use when restoring the database to a known-good backup.

```bash
# Step 1: Stop the API to prevent writes during restore
docker compose stop api

# Step 2: Restore from a backup file
# Replace FILENAME with the backup you want to restore from
docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$SA_PASSWORD" \
  -Q "RESTORE DATABASE [ECommerceBotDb] \
      FROM DISK = N'/backups/ECommerceBotDb_YYYYMMDD_HHmmss.bak' \
      WITH REPLACE, RECOVERY, STATS = 10"

# Step 3: Restart the API
docker compose start api

# Step 4: Verify
curl http://localhost:8080/health
```

### Restore to a New Server

1. Copy the `.bak` file to the new server's backup volume
2. Start only the `sqlserver` container on the new server
3. Run the restore command above
4. Start the `api` container — migrations will be verified on startup

```bash
# Copy backup to new server
scp /local/path/ECommerceBotDb_20260605.bak user@newserver:/opt/ecommercebot/backups/

# On the new server
docker compose up -d sqlserver
docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$SA_PASSWORD" \
  -Q "RESTORE DATABASE [ECommerceBotDb] \
      FROM DISK = N'/backups/ECommerceBotDb_20260605.bak' \
      WITH REPLACE"
docker compose up -d api
```

---

## Backup Health Check

The backup directory health check verifies:
- The backup directory exists and is writable
- The last backup file is not older than `ScheduleHours * 2`

```bash
curl http://localhost:8080/health/ready
```

A `Degraded` status for `backup-directory` means:
- Directory does not exist or is not writable
- No recent backup found

---

## Common Issues

### Backup service not running
Check that `BACKUP_ENABLED=true` in `.env` and the container is healthy:
```bash
docker compose logs api | grep -i backup
```

### Backup fails: permission denied
The backup directory must be writable by both the `api` container (app user) and readable by `sqlserver`. Ensure the Docker volume is shared correctly between both services in `docker-compose.yml`.

### Restore fails: database in use
Stop the API before restoring:
```bash
docker compose stop api
# ... restore ...
docker compose start api
```

### Restore fails: file not found
Verify the file path inside the container:
```bash
docker compose exec sqlserver ls /backups/
```

### Old backups not being deleted
Check `BACKUP_RETENTION_DAYS` is set. Cleanup only runs after a successful backup.

---

## Backup Verification

After restoring, verify data integrity:

```bash
# Check row counts
docker compose exec sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$SA_PASSWORD" \
  -d ECommerceBotDb \
  -Q "SELECT 'Users' AS tbl, COUNT(*) AS cnt FROM TelegramUsers
      UNION SELECT 'Orders', COUNT(*) FROM Orders
      UNION SELECT 'Products', COUNT(*) FROM Products"

# Verify health
curl http://localhost:8080/health
```
