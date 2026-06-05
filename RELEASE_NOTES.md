# ECommerceBot — Release Notes

## Version 1.0.0 — 2026-06-05 — Initial Commercial Release

ECommerceBot 1.0.0 is the first production-ready commercial release. It is a complete Telegram-based digital product storefront with admin management panel, wallet system, support tickets, license protection, and white-label branding.

---

### What's Included

**Complete Telegram Bot**
- Product catalog with categories, products, and digital key fulfillment
- Order lifecycle: browse → select → pay → admin review → key delivery
- Card payment with receipt photo upload and admin approval
- Wallet payment (instant fulfillment, no admin approval required)
- Support ticket system with admin reply
- Wallet with balance, charge history, and transaction ledger

**Admin Panel (100% Telegram-based)**
- Review and approve/reject pending orders with one tap
- Manage products, categories, payment cards, and all bot settings
- View user list, block/unblock users, send messages
- Statistics dashboard (order counts, user counts)
- License status, activation, and server fingerprint display

**CMS via BotSetting Table**
- All messages, button labels, and brand text editable from Telegram
- Persian and English localization with automatic fallback
- Premium Emoji (`<tg-emoji>`) support throughout
- No redeployment required for content changes

**License System**
- RSA-signed license packages
- Hardware binding via server fingerprint
- Bot username binding
- User and admin limit enforcement
- Offline grace period (configurable, default 72h)
- Trial license support

**Production Infrastructure**
- Docker + SQL Server + Redis fully containerized
- Automatic EF Core migrations on startup
- Scheduled database backups with retention (default 7 days)
- Pending order expiration background service (every 15 minutes)
- Health checks: /health, /health/live, /health/ready
- Rate limiting: 300 req/min on webhook, 5 msg/10s per user
- Serilog structured logging with file rotation

---

### System Requirements

| Component | Minimum | Recommended |
|-----------|---------|-------------|
| OS | Ubuntu 22.04 LTS | Ubuntu 22.04 LTS |
| CPU | 1 vCPU | 2 vCPU |
| RAM | 1 GB | 2 GB |
| Disk | 20 GB SSD | 40 GB SSD |
| Docker | 24+ | 24+ |
| Docker Compose | 2.20+ | 2.20+ |

---

### Known Limitations

1. **Single instance** — no built-in horizontal scaling. Redis is used for caching, not for distributed state. Multiple instances behind a load balancer require sticky sessions or external conversation state storage.

2. **SQL Server only** — database provider is SQL Server. PostgreSQL or SQLite are not supported without code changes.

3. **No web dashboard** — all management is via Telegram. A web-based CMS panel is not included.

4. **Product creation via Telegram is limited** — product details (title, price) can be edited via Telegram, but creating new products with full metadata is intended for direct database seeding or a future web CMS.

5. **Crypto payment** — PaymentMethod.CryptoPayment is defined as an enum value but the fulfillment flow uses card payment logic (receipt photo). Dedicated crypto payment gateway integration is not included.

6. **Load test tool** — `tools/ECommerceBot.LoadTester` is a functional skeleton. It sends real HTTP requests to the webhook. A full load test setup requires the API to be running with a test database.

---

### Breaking Changes

None — this is the initial release.

---

### Upgrade Path

No upgrade path required — this is version 1.0.0. Future releases will provide migration guides in this file.

---

### Support

For commercial support, customization, or white-label setup:
- Contact the vendor who provided your license
- See `docs/CUSTOMER_SETUP.md` for initial configuration guidance
- See `docs/DEPLOYMENT.md` for full deployment instructions
