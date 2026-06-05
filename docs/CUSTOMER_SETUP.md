# ECommerceBot — Customer Setup Guide

**Version: 1.0.0** | راهنمای راه‌اندازی برای مشتریان | Customer Setup Guide

---

## پیش‌نیازها | Prerequisites

- VPS with Docker + Docker Compose (see [DEPLOYMENT.md](DEPLOYMENT.md))
- A Telegram bot created via @BotFather
- Your license package (received from vendor)

---

## 1. License Activation | فعال‌سازی لایسنس

**Step 1:** Deploy the bot (see DEPLOYMENT.md).

**Step 2:** Send `/start` to the bot from your admin Telegram account.

**Step 3:** In the admin menu, press **🔐 وضعیت لایسنس**

**Step 4:** Press **🔑 فعال‌سازی**

**Step 5:** Paste the license package string provided by your vendor and send it.

The bot will validate the signature and activate the license immediately.

**Verify activation:** Press **🔐 وضعیت لایسنس** again — status should show ✅ Valid.

---

## 2. Branding | برندسازی

All brand settings are stored in the database and editable from the Telegram admin panel.

### From Admin Panel → ⚙️ Settings:

| Setting Key | Persian | English |
|---|---|---|
| `Brand.Name` | نام ربات | Bot name |
| `Brand.ShortName` | نام کوتاه | Short name |
| `Brand.SupportUsername` | آیدی پشتیبانی | Support username |
| `Brand.WebsiteUrl` | آدرس وب‌سایت | Website URL |
| `Brand.FooterText.fa` | متن پاورقی فارسی | Persian footer |
| `Brand.FooterText.en` | متن پاورقی انگلیسی | English footer |

### Emoji Customization:

| Key | Usage |
|---|---|
| `Brand.PrimaryEmoji` | Main navigation icon |
| `Brand.SuccessEmoji` | Success messages |
| `Brand.WarningEmoji` | Warning messages |
| `Brand.ErrorEmoji` | Error messages |

Supports Telegram Premium Emoji: `<tg-emoji emoji-id="XXXXX">🎯</tg-emoji>`

---

## 3. Language Settings | تنظیمات زبان

Default language is **Persian (fa-IR)**. English is available as secondary language.

### Editing Localized Messages:

From Admin Panel → ⚙️ Settings, edit keys with `.fa` or `.en` suffix:

```
WelcomeMessage.fa    — Persian welcome message
WelcomeMessage.en    — English welcome message
HelpMessage.fa       — Persian help message
MainMenu.ProductsButton.fa  — Persian "Products" button label
MainMenu.ProductsButton.en  — English "Products" button label
```

### Language Fallback Logic:
1. User's preferred language key (`key.{lang}`)
2. Persian key (`key.fa`)
3. Language-neutral base key (`key`)
4. Built-in Persian default

---

## 4. Telegram Bot Setup | راه‌اندازی ربات

### 4a. Create Bot

1. Message @BotFather: `/newbot`
2. Follow prompts to get your bot token
3. Set token in `.env`: `TELEGRAM_BOT_TOKEN=123456:ABC...`

### 4b. Set Webhook

```bash
# Linux
TELEGRAM_BOT_TOKEN="your-token" \
TELEGRAM_WEBHOOK_URL="https://your.domain/api/telegram/webhook" \
TELEGRAM_WEBHOOK_SECRET="your-secret" \
./scripts/set-webhook.sh
```

### 4c. Verify Webhook

```bash
curl "https://api.telegram.org/bot<TOKEN>/getWebhookInfo"
```

---

## 5. Product Setup | راه‌اندازی محصولات

From Admin Panel:

1. **🗂 Categories** — Create product categories
2. **📦 Products** — Add products under each category
3. For each product, add product keys via the admin panel

---

## 6. Payment Card Setup | تنظیم کارت بانکی

From Admin Panel → **💳 Cards**:

1. Press ➕ Add Card
2. Enter card number, holder name, bank name
3. The first card added becomes the default payment destination

---

## 7. Domain & SSL Setup | تنظیم دامنه و SSL

Telegram requires HTTPS for webhooks. See [DEPLOYMENT.md](DEPLOYMENT.md#3-ssl-setup) for Nginx + Let's Encrypt setup.

---

## 8. Server Fingerprint (for server-bound licenses)

If your license is server-bound:

1. In Telegram admin panel: **🔐 وضعیت لایسنس** → **🖥 اثر انگشت سرور**
2. Provide the fingerprint hash to your vendor before license generation

---

## 9. Backup Setup | تنظیم پشتیبان‌گیری

In `.env`:
```bash
BACKUP_ENABLED=true
BACKUP_RETENTION_DAYS=7
BACKUP_SCHEDULE_HOURS=24
```

See [DEPLOYMENT.md](DEPLOYMENT.md#7-database-backup) for details.

---

## 10. Troubleshooting | عیب‌یابی

### License not activating

- Ensure you copied the **complete** license package (it is long base64 text)
- Check bot logs: `docker compose logs api --tail=50`
- Verify `License:PublicKey` in `appsettings.json` matches the key provided by vendor

### Bot not responding

1. Check webhook: `curl https://api.telegram.org/bot<TOKEN>/getWebhookInfo`
2. Check health: `curl http://localhost:8080/health`
3. Check logs: `docker compose logs api`

### License expired

Contact your vendor to renew. During the grace period (default 72h after expiry), the bot continues operating. After grace period, the webhook endpoint returns 503.

### Server fingerprint changed

If you migrate to a new server, you need a new license bound to the new fingerprint. Contact your vendor with the new fingerprint from the admin panel.

---

## Support | پشتیبانی

For technical support, contact your vendor with:
1. Bot logs: `docker compose logs api --tail=200`
2. Health status: `curl http://localhost:8080/health | python3 -m json.tool`
3. License status from admin panel
