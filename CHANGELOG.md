# Changelog

All notable changes to ECommerceBot are documented in this file.

Format: [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)
Versioning: [Semantic Versioning](https://semver.org/)

---

## [1.0.0] — 2026-06-05

### Added — Foundation (Step 1)
- ASP.NET Core 9 Web API project structure
- Entity Framework Core 9 with SQL Server provider
- 17 domain entities: TelegramUser, Category, Product, ProductKey, Order, OrderItem, Transaction, WalletTransaction, PaymentCard, Cart, CartItem, BotSetting, Ticket, TicketMessage, AuditLog, LicenseInfo, BaseEntity
- 9 enums: UserRole, OrderStatus, PaymentStatus, PaymentMethod, ProductStatus, TicketStatus, WalletTransactionType, ConversationState (18 states), LicenseStatus
- Repository pattern with 15 repositories and IGenericRepository<T>
- Unit of Work pattern aggregating all repositories
- EF Core Fluent API configurations with cascade/restrict rules
- Unique indexes on Order.ReceiptPhotoUniqueId and BotSetting.Key
- Initial EF Core migration (InitialCreate)

### Added — Business Logic (Step 2)
- UserService: GetOrCreateUser, ChargeWallet, AdjustWallet, RefundToWallet, BlockUser/UnblockUser
- OrderService: CreateOrder (anti-spam, anti-duplicate receipt), ApproveOrder, RejectOrder, ExpireOrder, ExpireStaleOrdersAsync
- PaymentService: GetActivePaymentCard (card rotation), ValidateReceiptUniqueId, SubmitReceipt
- TicketService: CreateTicket, ReplyTicket, ResolveTicket with multi-message threads
- AdminService: facade layer over all admin operations
- SettingService: GetSetting, SetSetting, IsCardRotationEnabled, GetActivePaymentCard
- ServiceResult<T> and ServiceResult pattern for error propagation
- ProductionHardening migration

### Added — Telegram Bot + CMS (Step 3)
- Webhook endpoint: POST /api/telegram/webhook
- UpdateDispatcher routing messages and callbacks to handlers
- MessageHandler: /start, main menu buttons, 18-state conversation state machine
- CallbackQueryHandler: inline button routing for all user and admin flows
- Conversation states: AwaitingPlayerId, AwaitingReceipt, AwaitingTicketMessage, AwaitingRejectReason, AwaitingCategoryName, AwaitingProductTitle, AwaitingProductPrice, AwaitingCardNumber, AwaitingCardHolder, AwaitingCardBank, AwaitingSettingValue, AwaitingAdminMessage, AwaitingLicenseKey
- ConversationManager with OrderContext and AdminContext serialized to TempData JSON
- KeyboardBuilder generating inline and reply keyboards
- BotTextService reading all messages from BotSetting table with fallback defaults
- TelegramMessageService: send, edit, photo, forward, admin notify, backup channel
- Full CMS: all messages and button labels editable via Telegram admin panel
- Card rotation algorithm (round-robin)
- OrderContext and AdminContext state persistence

### Added — Security + Infrastructure (Step 4)
- Webhook secret token validation (X-Telegram-Bot-Api-Secret-Token)
- HtmlSanitizer.Encode for user-supplied content (XSS prevention)
- Admin role authorization guard in CallbackQueryHandler
- Per-user rate limiting (5 messages/10 seconds)
- Admin action rate limiting
- Fixed window rate limiter on webhook endpoint (300 req/min)
- GlobalExceptionMiddleware for unhandled exception handling
- Serilog structured logging with file rotation (30-day retention)
- Separate telegram.log and errors.log sinks
- AuditLogService tracking all admin actions
- StartupValidator blocking misconfigured production starts

### Added — Docker + Deployment (Step 5)
- Dockerfile (multi-stage, non-root user)
- docker-compose.yml with SQL Server and Redis services
- docker-compose.production.yml with resource limits and restart policies
- .dockerignore
- .env.example with all required variables
- Auto-migration on container startup
- Health checks with dependency wait (healthcheck depends_on)
- Backup volume shared between API and SQL Server containers
- scripts/set-webhook.sh and set-webhook.ps1
- scripts/delete-webhook.sh and delete-webhook.ps1
- DatabaseBackupService with configurable schedule, retention, and compression
- OrderExpirationService sweeping stale pending orders every 15 minutes
- Redis integration with in-memory cache fallback
- Health check endpoints: /health, /health/live, /health/ready

### Added — Licensing + Localization (Step 6)
- LicenseInfo entity and LicenseOptions configuration
- RSA signature validation via ILicenseSignatureValidator
- IServerFingerprintService for hardware binding
- LicenseMiddleware blocking invalid licenses in production
- LicenseValidationBackgroundService for periodic re-validation
- License scenarios: Valid, Trial, Expired, GracePeriod, Disabled, SignatureInvalid, BotMismatch, ServerMismatch, UserLimitExceeded, AdminLimitExceeded, NotActivated
- Admin Telegram panel: license status, activate license, show fingerprint
- LocalizationService with Persian-first fallback chain (key.fa → base key → default)
- Multi-language support: fa-IR (default), en-US
- CacheService with Redis + in-memory distributed cache fallback
- Step6_LicensingLocalization migration

### Added — Release Candidate (Step 7)
- Version set to 1.0.0 (AssemblyVersion, FileVersion, InformationalVersion)
- BotSettingKeys constants expanded to cover all messages, buttons, and brand keys
- Brand key constants: Brand.Name, Brand.ShortName, Brand.SupportUsername, Brand.WebsiteUrl, Brand.FooterText, Brand.PoweredByText, Brand.ShowPoweredBy, Brand.PrimaryEmoji, Brand.SuccessEmoji, Brand.WarningEmoji, Brand.ErrorEmoji
- Load test tool: tools/ECommerceBot.LoadTester with 4 scenarios (start, browse, order, ticket, all)
- New unit tests: HtmlSanitizerTests (10), StartupValidatorTests (5), AdminAuthorizationTests (10), WhiteLabelSettingsTests (6), OrderExpirationServiceTests (4), PremiumEmojiTests (5) — total 52 new tests
- Total test suite: 123 tests, all passing
- Documentation: CHANGELOG.md, RELEASE_NOTES.md, docs/RELEASE.md, docs/SECURITY.md, docs/LOAD_TESTING.md, docs/BACKUP_RESTORE.md, docs/FAILURE_TESTING.md, docs/COMMERCIAL_READINESS.md

### Fixed — Step 7
- No compile errors or warnings detected in baseline or post-changes build
- All 123 tests pass (0 failures, 0 skipped)
