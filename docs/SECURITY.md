# ECommerceBot — Security Reference

Version: 1.0.0

---

## Security Architecture

### 1. Webhook Authentication

Every incoming Telegram update is authenticated before processing:

```
Telegram → POST /api/telegram/webhook
           Header: X-Telegram-Bot-Api-Secret-Token: <secret>
```

- Secret is set in `Telegram:WebhookSecretToken` configuration
- Mismatch returns HTTP 401 and logs a warning with the requester IP
- If `WebhookSecretToken` is empty, the check is skipped (StartupValidator warns about this)

**Required action**: Always set `TELEGRAM_WEBHOOK_SECRET` to a random 32+ character string in production.

---

### 2. Admin Authorization

Admin-only actions are enforced at two layers:

**Layer 1 — CallbackQueryHandler** (inline buttons):
```csharp
case "adm":
    if (user.Role != UserRole.Admin) { ... return; }
case "lic":
    if (user.Role != UserRole.Admin) { ... return; }
```

**Layer 2 — MessageHandler** (reply keyboard):
```csharp
if (user.Role == UserRole.Admin)
    await HandleAdminMenuButtonAsync(...);
```

Non-admin users attempting admin callbacks receive `❌ Admin only.` and the attempt is logged at Warning level.

**Admin Chat IDs**: `Telegram:AdminChatIds` in configuration are used by `TelegramMessageService` to send new order notifications. Admin role in the database (UserRole.Admin) controls panel access.

---

### 3. HTML Injection Prevention

User-supplied text is always encoded before embedding in Telegram HTML messages:

```csharp
// Correct — user input goes through Encode()
$"User: {HtmlSanitizer.Encode(user.FirstName)}"

// Correct — bot-authored templates use Passthrough()
HtmlSanitizer.Passthrough(template)
```

`HtmlSanitizer.Encode` replaces `&`, `<`, `>`, `"` with HTML entities. This prevents:
- HTML tag injection into messages
- `<script>` or `<a href=...>` injection
- `<tg-emoji>` spoofing by users

Bot-authored templates (from BotSetting) may contain valid `<b>`, `<code>`, `<tg-emoji>` — these are passed through Passthrough and never re-encoded.

---

### 4. Rate Limiting

**Webhook endpoint** (fixed window):
- 300 requests per minute
- Exceeding returns HTTP 429

**Per-user message rate** (in-process):
- 5 messages per 10 seconds
- Exceeding triggers a warning message to the user

**Admin action rate** (in-process):
- Separate admin limiter for high-frequency admin callbacks

---

### 5. Callback Data Validation

All callback query data is validated before processing:

```csharp
if (string.IsNullOrWhiteSpace(data) || data.Length > 64)
{
    // Reject
}
```

- Empty callbacks are rejected
- Callbacks exceeding 64 characters are rejected
- Unknown action prefixes are logged and silently ignored

---

### 6. License Middleware

In production with `LicenseOptions.RequireValidLicenseInProduction = true`:
- Invalid license returns HTTP 503 with `{"error":"service_unavailable"}`
- Health check endpoints (`/health`, `/health/live`, `/health/ready`) are always allowed
- License status is cached to avoid DB hit on every request

---

### 7. Blocked Users

Blocked users are rejected early in both handlers:
```csharp
if (user.IsBlocked)
{
    await _msg.SendHtmlAsync(chatId, "❌ You are blocked.");
    return;
}
```

Blocking is persisted in the database (`TelegramUser.IsBlocked`).

---

### 8. Sensitive Data in Logs

Serilog is configured to avoid logging:
- Telegram bot tokens (never in logged strings)
- SQL Server passwords (from connection string — not logged)
- License keys (activation input is not logged at Information level)
- Receipt photo file IDs (logged at Information level by design — for audit)

**Production recommendation**: Set minimum log level to `Warning` for Microsoft and System namespaces.

---

### 9. Docker Security

- **Non-root container user**: Dockerfile creates and uses `app` user
- **SQL Server port**: Port 1433 should NOT be exposed in production compose (`docker-compose.production.yml` removes the port mapping)
- **Redis port**: Port 6379 should NOT be exposed externally
- **API port 8080**: Only exposed via Nginx proxy, not directly to internet
- **Secrets**: All secrets via environment variables, never hardcoded in source

---

### 10. Database Security

- **Anti-duplicate receipt**: Unique index on `Orders.ReceiptPhotoUniqueId` prevents the same receipt being submitted twice
- **SQL injection**: All queries use EF Core parameterized queries — no raw SQL with user input
- **Backup files**: Backup directory should have restricted filesystem permissions
- **Connection string**: Uses SQL Server authentication in Docker (not Windows auth)

---

## Security Checklist

- [ ] `WebhookSecretToken` set and not empty
- [ ] `AdminChatIds` contains only your own Telegram user ID
- [ ] SQL Server port 1433 NOT exposed externally
- [ ] Redis port 6379 NOT exposed externally
- [ ] `.env` file NOT committed to git
- [ ] API accessible ONLY via Nginx proxy (not directly on port 8080)
- [ ] HTTPS enforced (Telegram requires it for webhooks)
- [ ] SA_PASSWORD is strong (not the default)
- [ ] Log files in `/app/Logs/` are not publicly accessible
- [ ] Backup files are not publicly accessible
- [ ] License middleware enabled in production (`License:Enabled=true`)

---

## Reporting Security Issues

Contact your vendor/developer directly. Do not open public issues for security vulnerabilities.
