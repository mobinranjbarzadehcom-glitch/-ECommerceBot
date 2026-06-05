# ECommerceBot — Complete Project Documentation

**Stack:** ASP.NET Core 9 · Entity Framework Core 9 · SQL Server · Telegram.Bot 22  
**Architecture:** Repository + Unit of Work · Business Service Layer · Telegram Webhook Bot  
**Build status:** 0 errors · 0 warnings · 32/32 tests passing

---

## 1. Folder Structure

```
ECommerceBot/
├── ECommerceBot.sln
├── DOCUMENTATION.md
│
├── src/
│   └── ECommerceBot.API/
│       ├── ECommerceBot.API.csproj
│       ├── Program.cs
│       ├── appsettings.json
│       ├── appsettings.Development.json
│       │
│       ├── Controllers/
│       │   └── TelegramWebhookController.cs       # POST /api/telegram/webhook
│       │
│       ├── Enums/
│       │   ├── ConversationState.cs
│       │   ├── OrderStatus.cs
│       │   ├── PaymentMethod.cs
│       │   ├── PaymentStatus.cs
│       │   ├── ProductStatus.cs
│       │   ├── TicketStatus.cs
│       │   ├── UserRole.cs
│       │   └── WalletTransactionType.cs
│       │
│       ├── Entities/
│       │   ├── BaseEntity.cs
│       │   ├── BotSetting.cs
│       │   ├── Cart.cs
│       │   ├── CartItem.cs
│       │   ├── Category.cs
│       │   ├── Order.cs
│       │   ├── OrderItem.cs
│       │   ├── PaymentCard.cs
│       │   ├── Product.cs
│       │   ├── ProductKey.cs
│       │   ├── TelegramUser.cs
│       │   ├── Ticket.cs
│       │   ├── TicketMessage.cs
│       │   ├── Transaction.cs
│       │   └── WalletTransaction.cs
│       │
│       ├── Data/
│       │   ├── AppDbContext.cs
│       │   ├── Configurations/              # Fluent API — one file per entity (18 files)
│       │   └── Migrations/                  # InitialCreate (2026-06-04)
│       │
│       ├── DTOs/
│       │   ├── Admin/       AdminNoteDto, RejectOrderDto
│       │   ├── Cart/        CartDto, CartItemDto, AddToCartDto
│       │   ├── Category/    CategoryDto, CreateCategoryDto, UpdateCategoryDto
│       │   ├── Common/      ApiResponseDto<T>, PagedResultDto<T>
│       │   ├── Order/       OrderDto, OrderItemDto, CreateOrderDto, CreateOrderRequest
│       │   ├── Payment/     PaymentCardDto, CreatePaymentCardDto, SubmitReceiptDto
│       │   ├── Product/     ProductDto, CreateProductDto, UpdateProductDto, ProductKeyDto, CreateProductKeyDto
│       │   ├── Setting/     BotSettingDto, UpdateSettingDto
│       │   ├── Ticket/      TicketDto, TicketMessageDto, CreateTicketDto, ReplyTicketDto
│       │   ├── Transaction/ TransactionDto
│       │   ├── User/        UserDto, CreateUserDto, UpdateUserDto
│       │   └── Wallet/      WalletTransactionDto, ChargeWalletDto, AdjustWalletDto
│       │
│       ├── Repositories/
│       │   ├── Interfaces/   IGenericRepository<T> + 12 entity-specific interfaces
│       │   └── Implementations/  GenericRepository<T> + 12 concrete repositories
│       │
│       ├── UnitOfWork/
│       │   ├── IUnitOfWork.cs
│       │   └── UnitOfWork.cs
│       │
│       ├── Services/
│       │   ├── Common/       ServiceResult.cs, ServiceResult<T>.cs, BotSettingKeys.cs
│       │   ├── Interfaces/   IUserService, IOrderService, IPaymentService,
│       │   │                 ITicketService, IAdminService, ISettingService
│       │   └── Implementations/  UserService, OrderService, PaymentService,
│       │                         TicketService, AdminService, SettingService
│       │
│       └── Telegram/
│           ├── IUpdateDispatcher.cs
│           ├── UpdateDispatcher.cs
│           ├── Options/       TelegramOptions.cs
│           ├── Handlers/      IMessageHandler, MessageHandler
│           │                  ICallbackQueryHandler, CallbackQueryHandler
│           ├── Keyboards/     IKeyboardBuilder, KeyboardBuilder
│           ├── Messages/      ITelegramMessageService, TelegramMessageService
│           ├── Services/      IBotTextService, BotTextService
│           └── States/        IConversationManager, ConversationManager
│                              OrderContext.cs, AdminContext.cs
│
└── tests/
    └── ECommerceBot.Tests/
        ├── ECommerceBot.Tests.csproj
        └── Services/
            ├── UserServiceTests.cs      (7 tests)
            ├── OrderServiceTests.cs     (8 tests)
            ├── PaymentServiceTests.cs   (7 tests)
            └── TicketServiceTests.cs   (10 tests)
```

---

## 2. Database Entities

All entities inherit `BaseEntity` which provides `Id` (int), `CreatedAt` (DateTime), `UpdatedAt` (DateTime, auto-set by `AppDbContext.SaveChanges`).

### TelegramUser
Primary user record, created on first `/start`.

| Column | Type | Notes |
|--------|------|-------|
| Id | int PK | |
| TelegramId | long | Unique index |
| ChatId | long | Telegram chat ID used for sending messages |
| FirstName | nvarchar(100) | |
| LastName | nvarchar(100) | nullable |
| Username | nvarchar(50) | nullable, no @ prefix |
| PhoneNumber | nvarchar(20) | nullable |
| Role | nvarchar(20) | `Customer` or `Admin` |
| IsBlocked | bit | default false |
| WalletBalance | decimal(18,2) | default 0 |
| CurrentState | nvarchar(50) | ConversationState enum as string |
| TempData | nvarchar(2000) | JSON payload for active conversation context |
| LastActivity | datetime2 | nullable, updated on every interaction |

**Relations:** one Cart, many Orders, many Transactions, many WalletTransactions, many CreatedTickets, many AssignedTickets, many TicketMessages.

---

### Category

| Column | Type | Notes |
|--------|------|-------|
| Name | nvarchar(100) | |
| Description | nvarchar(500) | nullable |
| ImageUrl | nvarchar(500) | nullable |
| IsActive | bit | default true |
| DisplayOrder | int | default 0, controls sort order in bot menus |

**Relations:** many Products (Restrict delete).

---

### Product

| Column | Type | Notes |
|--------|------|-------|
| Name | nvarchar(200) | |
| Description | nvarchar(2000) | nullable |
| Price | decimal(18,2) | |
| ImageUrl | nvarchar(500) | nullable |
| Status | nvarchar(20) | `Active`, `Inactive`, `OutOfStock` |
| CategoryId | int FK | |
| DisplayOrder | int | default 0, controls sort order in bot menus |

**Relations:** many ProductKeys (Cascade), many CartItems (Restrict), many OrderItems (Restrict).

---

### ProductKey
Individual fulfillable digital key for a Product.

| Column | Type | Notes |
|--------|------|-------|
| KeyValue | nvarchar | The actual product key string |
| IsUsed | bit | set to true when assigned to an OrderItem |
| ProductId | int FK | |
| OrderItemId | int FK | nullable, set on approval (SetNull delete) |

---

### Order

| Column | Type | Notes |
|--------|------|-------|
| UserId | int FK | |
| TotalAmount | decimal(18,2) | Price snapshot at time of order |
| Status | nvarchar(20) | See OrderStatus enum |
| Notes | nvarchar(1000) | nullable, internal notes |
| ReceiptPhotoFileId | nvarchar(200) | Telegram file_id for receipt photo |
| ReceiptPhotoUniqueId | nvarchar(200) | **Unique index (filtered, NOT NULL only)** — anti-duplicate |
| AccountDetails | nvarchar(500) | Player ID / account info provided by user |
| AdminNotes | nvarchar(1000) | Rejection reason or approval note from admin |
| ExpiresAt | datetime2 | nullable, set to +24h for card/crypto orders |

**Relations:** many OrderItems (Cascade), one Transaction (Cascade), many WalletTransactions (SetNull), many Tickets (SetNull).

---

### OrderItem
Line item within an Order. Stores the price snapshot.

| Column | Type | Notes |
|--------|------|-------|
| OrderId | int FK | |
| ProductId | int FK | |
| Quantity | int | |
| UnitPrice | decimal(18,2) | Snapshot of product price at order time |

**Relations:** many ProductKeys (product keys assigned here on approval).

---

### Transaction
Payment record linked to an Order.

| Column | Type | Notes |
|--------|------|-------|
| OrderId | int FK | one-to-one with Order |
| UserId | int FK | |
| Amount | decimal(18,2) | |
| Status | nvarchar(20) | See PaymentStatus enum |
| Method | nvarchar(20) | See PaymentMethod enum |
| PaymentReference | nvarchar | nullable, ReceiptPhotoUniqueId for card/crypto |
| FailureReason | nvarchar | nullable, rejection reason |
| PaidAt | datetime2 | nullable, set on approval |

---

### WalletTransaction
Immutable ledger entry for every wallet movement.

| Column | Type | Notes |
|--------|------|-------|
| UserId | int FK | |
| Amount | decimal(18,2) | Positive (credit) or negative (debit) |
| BalanceBefore | decimal(18,2) | Snapshot before this transaction |
| BalanceAfter | decimal(18,2) | Snapshot after this transaction |
| Type | nvarchar(20) | See WalletTransactionType enum |
| Description | nvarchar(500) | nullable |
| RelatedOrderId | int FK | nullable (SetNull delete) |

---

### PaymentCard
Bank card shown to users for manual payment.

| Column | Type | Notes |
|--------|------|-------|
| CardNumber | nvarchar(30) | |
| CardHolderName | nvarchar(100) | |
| BankName | nvarchar(100) | |
| IsActive | bit | only active cards are shown to users |
| IsDefault | bit | used when card rotation is disabled |
| DisplayOrder | int | sort order for rotation |

---

### BotSetting
Key-value store for all dynamic bot configuration and CMS text.

| Column | Type | Notes |
|--------|------|-------|
| Key | nvarchar(100) | **Unique index** |
| Value | nvarchar(1000) | Supports HTML and `<tg-emoji>` tags |
| Description | nvarchar(500) | nullable, human-readable note |

---

### Ticket
User support ticket.

| Column | Type | Notes |
|--------|------|-------|
| UserId | int FK | ticket creator |
| Subject | nvarchar(200) | |
| Status | nvarchar(20) | See TicketStatus enum |
| AssignedAdminId | int FK | nullable (SetNull delete), admin who took it |
| RelatedOrderId | int FK | nullable (SetNull delete), linked order |

**Relations:** many TicketMessages (Cascade).

---

### TicketMessage
Single message in a Ticket conversation.

| Column | Type | Notes |
|--------|------|-------|
| TicketId | int FK | |
| SenderId | int FK | TelegramUser who wrote this message |
| Content | nvarchar(2000) | |
| IsAdminMessage | bit | true when sent by admin |

---

### Cart / CartItem
Standard shopping cart (created automatically, not currently used by Telegram bot flows).

| Cart | UserId (FK one-to-one with TelegramUser) |
|------|---|
| CartItem | CartId, ProductId, Quantity |

---

## 3. Enums

| Enum | Values |
|------|--------|
| `UserRole` | `Customer=0`, `Admin=1` |
| `OrderStatus` | `Pending=0`, `Processing=1`, `Completed=2`, `Cancelled=3`, `Refunded=4` |
| `PaymentStatus` | `Pending=0`, `Completed=1`, `Failed=2`, `Refunded=3` |
| `PaymentMethod` | `CryptoPayment=0`, `CardPayment=1`, `WalletBalance=2` |
| `ProductStatus` | `Active=0`, `Inactive=1`, `OutOfStock=2` |
| `WalletTransactionType` | `Charge=0`, `Purchase=1`, `Refund=2`, `Bonus=3`, `Adjustment=4` |
| `TicketStatus` | `Open=0`, `InProgress=1`, `Resolved=2`, `Closed=3` |
| `ConversationState` | `None=0`, `AwaitingPhone=1`, `AwaitingPlayerId=2`, `AwaitingAccountInfo=3`, `AwaitingReceipt=4`, `AwaitingTicketMessage=5`, `AwaitingRejectReason=10`, `AwaitingCategoryName=11`, `AwaitingProductTitle=12`, `AwaitingProductPrice=13`, `AwaitingCardNumber=14`, `AwaitingSettingValue=15`, `AwaitingAdminMessage=16`, `AwaitingCardHolder=17`, `AwaitingCardBank=18` |

---

## 4. Services

All services return `ServiceResult` (non-generic, for void operations) or `ServiceResult<T>` (for operations that return data). Both expose `IsSuccess` and `ErrorMessage`.

### IUserService
| Method | Description |
|--------|-------------|
| `GetOrCreateUserAsync(telegramId, firstName, lastName, username)` | Upserts user on `/start` |
| `GetUserByTelegramIdAsync(telegramId)` | Lookup by Telegram user ID |
| `GetWalletBalanceAsync(userId)` | Returns current wallet balance |
| `ChargeWalletAsync(userId, amount, description)` | Credit wallet (Type=Charge), must be positive |
| `AddBonusAsync(userId, amount, description)` | Credit wallet (Type=Bonus), must be positive |
| `AdjustWalletAsync(userId, amount, description)` | Positive or negative; blocks if result < 0 |
| `RefundToWalletAsync(userId, amount, orderId, description)` | Credit wallet (Type=Refund), called on order rejection |
| `GetWalletTransactionsAsync(userId)` | Full ledger history |
| `BlockUserAsync(telegramId)` | Sets IsBlocked=true |
| `UnblockUserAsync(telegramId)` | Sets IsBlocked=false |
| `GetAllUsersAsync(page, pageSize)` | Paginated user list for admin |

All wallet operations are wrapped in a DB transaction. Negative balance is prevented.

---

### IOrderService
| Method | Description |
|--------|-------------|
| `CreateOrderAsync(userId, CreateOrderRequest)` | Full order creation with anti-spam, anti-duplicate, wallet deduction or pending flow |
| `GetOrderByIdAsync(orderId)` | Fetch single order with items and keys |
| `GetUserOrdersAsync(userId)` | All orders for a user |
| `GetPendingOrdersAsync(page, pageSize)` | Paginated pending orders for admin |
| `ApproveOrderAsync(orderId, adminId)` | Assigns product keys, marks Completed, updates Transaction |
| `RejectOrderAsync(orderId, adminId, reason)` | Marks Cancelled, refunds wallet if WalletBalance payment |
| `ExpireOrderAsync(orderId)` | Single order expiry (delegates to RejectOrder) |
| `ExpireStaleOrdersAsync()` | Batch-expires all orders past their ExpiresAt |

**Anti-spam rule:** max 2 pending orders per user at any time.  
**Anti-duplicate rule:** `ReceiptPhotoUniqueId` checked against unique DB index before order is created.

**Wallet payment flow:** balance checked → keys availability checked → order created as Completed → keys assigned → wallet debited → WalletTransaction recorded — all in one DB transaction.

**Card/crypto payment flow:** order created as Pending → Transaction created as Pending → admin reviews receipt → ApproveOrder or RejectOrder.

---

### IPaymentService
| Method | Description |
|--------|-------------|
| `GetActivePaymentCardAsync()` | Returns active card (with rotation if enabled) |
| `ValidateReceiptUniqueIdAsync(fileUniqueId)` | Returns failure if already used |
| `SubmitReceiptAsync(orderId, fileId, uniqueId)` | Attaches receipt photo to existing pending order |

---

### ITicketService
| Method | Description |
|--------|-------------|
| `CreateTicketAsync(userId, CreateTicketDto)` | Creates ticket + first message in one transaction |
| `ReplyTicketAsync(ticketId, senderId, ReplyTicketDto, isAdmin)` | Adds message; admin reply sets status to InProgress |
| `ResolveTicketAsync(ticketId, adminId)` | Sets status to Resolved |
| `GetTicketByIdAsync(ticketId)` | Fetch with all messages |
| `GetUserTicketsAsync(userId)` | All tickets for a user |
| `GetOpenTicketsAsync(page, pageSize)` | Paginated Open+InProgress tickets for admin |

---

### IAdminService
Facade layer that delegates to IOrderService and IUserService.

| Method | Description |
|--------|-------------|
| `ApproveOrderAsync(orderId, adminId)` | → OrderService.ApproveOrderAsync |
| `RejectOrderAsync(orderId, adminId, reason)` | → OrderService.RejectOrderAsync |
| `BlockUserAsync(telegramId, adminId)` | → UserService.BlockUserAsync |
| `UnblockUserAsync(telegramId, adminId)` | → UserService.UnblockUserAsync |
| `AddAdminNoteAsync(orderId, note)` | Appends note to Order.AdminNotes |
| `GetPendingOrdersAsync(page, pageSize)` | → OrderService.GetPendingOrdersAsync |
| `GetAllUsersAsync(page, pageSize)` | → UserService.GetAllUsersAsync |

---

### ISettingService
| Method | Description |
|--------|-------------|
| `GetSettingAsync(key)` | Returns raw value or failure if not found |
| `SetSettingAsync(key, value, description)` | Upsert into BotSetting table |
| `GetBoolSettingAsync(key, defaultValue)` | Parses value as bool |
| `GetStringSettingAsync(key)` | Returns nullable string |
| `IsCardRotationEnabledAsync()` | Reads `IsCardRotationEnabled` setting |
| `GetActivePaymentCardAsync()` | Card rotation: picks next card after `LastUsedCardId`, updates the setting |
| `GetAllSettingsAsync()` | Returns all BotSetting rows |

**Card rotation algorithm:** get all active cards ordered by `DisplayOrder, Id`; find current index by `LastUsedCardId`; return `(currentIndex + 1) % count`; save new `LastUsedCardId`.

---

## 5. Telegram Flow (User)

### Entry: /start
```
User sends /start
  → UpdateDispatcher looks up TelegramUser by TelegramId
  → MessageHandler.HandleStartAsync()
      → UserService.GetOrCreateUserAsync() if user not found
      → Update ChatId, LastActivity, clear CurrentState and TempData
      → Send WelcomeMessage from BotSetting
      → Show main menu (reply keyboard)
```

### Main Menu
Reply keyboard buttons (text from BotSetting):

| Button | Action |
|--------|--------|
| 🛒 Products | Shows active categories as inline buttons |
| 💰 Wallet | Shows balance + last 5 wallet transactions |
| 📦 Orders | Shows last 5 orders with status icons |
| 🎫 Support | Sets state to AwaitingTicketMessage, shows cancel button |
| ❓ Help | Sends HelpMessage from BotSetting |

### Products Flow
```
User taps "🛒 Products"
  → Show active categories (ordered by DisplayOrder) as InlineKeyboard
  → callback: cat:{id}

User taps a category
  → Show active products in that category (ordered by DisplayOrder) as InlineKeyboard
  → callback: prod:{id}

User taps a product
  → Show product details (name, price, description, available key count)
  → If out of stock → show disabled message
  → If available:
      → Save OrderContext(productId, productName, productPrice) to TempData
      → Set CurrentState = AwaitingPlayerId
      → Ask for Player ID

User sends Player ID (state: AwaitingPlayerId)
  → Validate not empty
  → Save PlayerId to OrderContext in TempData
  → Set CurrentState = AwaitingReceipt
  → Show active PaymentCard (with rotation if enabled)
  → Show PaymentInstructionMessage with amount from BotSetting
  → Ask for receipt photo

User sends receipt photo (state: AwaitingReceipt)
  → Get largest photo: message.Photo[^1]
  → Validate FileUniqueId via PaymentService.ValidateReceiptUniqueIdAsync
  → Read OrderContext from TempData
  → Call OrderService.CreateOrderAsync (PaymentMethod.CardPayment)
      → Anti-spam check (max 2 pending)
      → Anti-duplicate check (ReceiptPhotoUniqueId unique index)
      → Create Order (Pending) + OrderItem + Transaction (Pending)
  → Clear CurrentState and TempData
  → Send OrderPendingMessage to user
  → Forward receipt photo to all AdminChatIds (from TelegramOptions)
  → Forward receipt photo to all DB admin users (Role=Admin, ChatId > 0)
  → Forward original message to BackupChannelId (if set)
```

### Wallet Flow
```
User taps "💰 Wallet"
  → Show WalletBalance
  → Show last 5 WalletTransactions (type + amount + date)
```
Wallet charges/bonuses/adjustments are performed by admin only (via admin panel or direct API).

### Orders Flow
```
User taps "📦 Orders"
  → Show last 5 orders with:
      ✅ Completed  ⏳ Pending  ❌ Cancelled  ❓ Other
      Order ID, amount, date
```

### Support Flow
```
User taps "🎫 Support"
  → Send SupportWelcomeMessage
  → Set CurrentState = AwaitingTicketMessage
  → Show cancel button

User sends text (state: AwaitingTicketMessage)
  → Call TicketService.CreateTicketAsync
      → Subject = "Support from {FirstName}"
      → Message = user's text
  → Clear state
  → Confirm: "✅ Ticket #{id} created!"
```

### State: Cancellation
Any message matching "❌ Cancel" while in a state clears the state and returns the user to the main menu.

---

## 6. Admin Flow

### Admin Detection
A user with `Role = UserRole.Admin` sees the **admin menu** instead of the user menu after `/start`.

### Admin Menu
Reply keyboard buttons (text from BotSetting):

| Button | Action |
|--------|--------|
| 📋 Pending Orders | Lists pending orders with receipt photo |
| 👥 Users | Lists last 10 users with balance and role |
| 📦 Products | Lists all active products with inline management buttons |
| 🗂 Categories | Lists all categories with inline management buttons |
| 💳 Cards | Lists all active payment cards with inline management buttons |
| ⚙️ Settings | Lists editable BotSetting keys as inline buttons |
| 📊 Statistics | Shows order counts (total/pending/completed) and user counts |

### Pending Orders Flow
```
Admin taps "📋 Pending Orders"
  → Fetch up to 10 pending orders
  → For each order, send receipt photo (if available) or text with:
      Order ID, user name, amount, account details, created time
  → Inline buttons: 🟢 Approve  🔴 Reject  🔄 New Receipt  💸 Refund
```

### Approve Order
```
Admin taps 🟢 Approve (callback: order:approve:{id})
  → AdminService.ApproveOrderAsync
      → Check order is Pending
      → Pre-validate key availability for all order items
      → Assign ProductKeys to each OrderItem
      → Mark keys as IsUsed = true
      → Mark Order as Completed
      → Update Transaction to Completed + PaidAt = UtcNow
  → Edit admin message: "✅ Order #{id} approved."
  → Look up order user's ChatId
  → Send OrderApprovedMessage to user with product keys listed as <code>key</code>
```

### Reject Order
```
Admin taps 🔴 Reject (callback: order:reject:{id})
  → Set AdminContext(TargetOrderId) in TempData
  → Set CurrentState = AwaitingRejectReason
  → Ask admin for rejection reason text

Admin sends reason (state: AwaitingRejectReason)
  → AdminService.RejectOrderAsync
      → Mark Order as Cancelled
      → Store reason in AdminNotes
      → Mark Transaction as Failed with FailureReason
      → If PaymentMethod = WalletBalance: call UserService.RefundToWalletAsync
  → Clear state
  → Send OrderRejectedMessage to order's user with reason
```

### Request New Receipt
```
Admin taps 🔄 New Receipt (callback: order:newreceipt:{id})
  → Look up order user's ChatId
  → Send message to user: "Admin is requesting a new receipt for Order #{id}."
  → Confirm to admin: "✅ New receipt requested."
```

### Refund
```
Admin taps 💸 Refund (callback: order:refund:{id})
  → Call UserService.RefundToWalletAsync(userId, order.TotalAmount, orderId)
  → Notify admin and user of refund
```

### Message User
```
Admin taps ✉️ Message (callback from order context)
  → Set AdminContext(TargetUserId) in TempData
  → Set CurrentState = AwaitingAdminMessage

Admin sends message text
  → Look up target user ChatId
  → Forward message to user: "📨 Message from Admin: {text}"
  → Clear state
```

---

## 7. CMS Capabilities

All CMS data is stored in the `BotSettings` table and read by `BotTextService` on every request, with hardcoded fallback defaults. Changes take effect immediately without redeployment.

### Editing via Telegram Admin Panel
```
Admin taps "⚙️ Settings"
  → Shows list of editable keys as InlineKeyboard (callback: adm:set:{key})

Admin taps a setting key
  → Shows current value
  → Sets CurrentState = AwaitingSettingValue
  → Sets AdminContext(TargetSettingKey) in TempData

Admin sends new value
  → BotTextService.SetAsync(key, value) → upserts BotSetting row
  → Confirm: "✅ Setting {key} updated."
```

### Category Management
```
"🗂 Categories" → InlineKeyboard list of all categories
  → Tap category → show: current name, active status, DisplayOrder
  → ✏️ Rename  → AwaitingCategoryName state → renames category
  → 🔴/🟢 Toggle → flips IsActive
"➕ Add Category" → AwaitingCategoryName state → creates new category
```

### Product Management
```
"📦 Products" → InlineKeyboard list of active products
  → Tap product → show: name, price, available key count, status
  → ✏️ Rename → AwaitingProductTitle state → renames product
  → 💰 Price  → AwaitingProductPrice state → updates price
  → 🔴/🟢 Toggle → flips ProductStatus (Active ↔ Inactive)
```
> New product creation with full details (category, description, image) requires the REST API or direct DB access.

### Payment Card Management
```
"💳 Cards" → InlineKeyboard list of active payment cards
  → Tap card → show: number, holder, bank, active status, default flag
  → 🔴/🟢 Toggle → flips IsActive
  → ⭐ Set Default → sets this card as default; unsets all others
"➕ Add Card" → 3-step state machine:
  AwaitingCardNumber → AwaitingCardHolder → AwaitingCardBank → card created
```

---

## 8. BotSetting Keys

All keys below are read by `BotTextService.GetAsync(key, fallback)`. If a row exists in the BotSettings table, the DB value is used. Otherwise the fallback default is used. Values support HTML (`<b>`, `<i>`, `<code>`, `<a>`) and Telegram Premium Emoji (`<tg-emoji emoji-id="..."></tg-emoji>`).

### Message Templates

| Key | Default Value | Template Variables |
|-----|--------------|-------------------|
| `WelcomeMessage` | `👋 <b>Welcome to ECommerceBot!</b>\n\nUse the menu below to get started.` | `{name}` |
| `HelpMessage` | Multi-line help text with menu overview | — |
| `SupportWelcomeMessage` | `🎫 <b>Support</b>\n\nSend your message and we'll get back to you shortly.` | — |
| `PaymentInstructionMessage` | `💳 <b>Payment Instructions</b>\n\nPlease transfer <b>{amount}</b> to the card shown...` | `{amount}` |
| `OrderPendingMessage` | `⏳ <b>Order #{orderId} Submitted</b>\n\nYour order is under review.` | `{orderId}` |
| `OrderApprovedMessage` | `✅ <b>Order #{orderId} Approved!</b>\n\nYour product keys:\n\n{keys}` | `{orderId}`, `{keys}` |
| `OrderRejectedMessage` | `❌ <b>Order #{orderId} Rejected</b>\n\nReason: {reason}` | `{orderId}`, `{reason}` |
| `OrderExpiredMessage` | `⏰ <b>Order #{orderId} Expired</b>\n\nYour order was not confirmed in time.` | `{orderId}` |

### User Main Menu Button Labels

| Key | Default |
|-----|---------|
| `MainMenu.ProductsButton` | `🛒 Products` |
| `MainMenu.WalletButton` | `💰 Wallet` |
| `MainMenu.OrdersButton` | `📦 Orders` |
| `MainMenu.SupportButton` | `🎫 Support` |
| `MainMenu.HelpButton` | `❓ Help` |

### Admin Menu Button Labels

| Key | Default |
|-----|---------|
| `AdminMenu.OrdersButton` | `📋 Pending Orders` |
| `AdminMenu.UsersButton` | `👥 Users` |
| `AdminMenu.ProductsButton` | `📦 Products` |
| `AdminMenu.CategoriesButton` | `🗂 Categories` |
| `AdminMenu.CardsButton` | `💳 Cards` |
| `AdminMenu.SettingsButton` | `⚙️ Settings` |
| `AdminMenu.StatisticsButton` | `📊 Statistics` |

### Admin Action Button Labels

| Key | Default |
|-----|---------|
| `AdminActions.ApproveButton` | `🟢 Approve` |
| `AdminActions.RejectButton` | `🔴 Reject` |
| `AdminActions.RequestNewReceiptButton` | `🔄 Request New Receipt` |
| `AdminActions.MessageUserButton` | `✉️ Message User` |
| `AdminActions.RefundButton` | `💸 Refund` |

### System Settings (read by SettingService, not BotTextService)

| Key | Type | Description |
|-----|------|-------------|
| `IsCardRotationEnabled` | `bool` (`"true"` / `"false"`) | Enables round-robin card rotation |
| `LastUsedCardId` | `int` (string) | ID of the last card shown; updated automatically by SettingService |

---

## 9. Required `appsettings.json` Values

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=ECommerceBotDb;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "Telegram": {
    "BotToken": "<YOUR_BOT_TOKEN>",
    "WebhookSecretToken": "<RANDOM_SECRET_STRING>",
    "AdminChatIds": [ 123456789 ],
    "BackupChannelId": 0
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `ConnectionStrings.DefaultConnection` | ✅ | SQL Server connection string |
| `Telegram.BotToken` | ✅ | Token from @BotFather |
| `Telegram.WebhookSecretToken` | ⚠️ Recommended | Random string sent as `X-Telegram-Bot-Api-Secret-Token` header by Telegram. If empty, the header is not validated. |
| `Telegram.AdminChatIds` | ⚠️ Recommended | Array of long Telegram chat IDs. New order receipts are forwarded here. If empty, only DB admin users (Role=Admin) receive notifications. |
| `Telegram.BackupChannelId` | Optional | Telegram channel or group ID. Receipt photos are forwarded here for archiving. `0` = disabled. |

---

## 10. Deployment Requirements

### Prerequisites
| Requirement | Minimum Version |
|-------------|----------------|
| .NET Runtime | 9.0 |
| SQL Server | 2019+ (or Azure SQL) |
| OS | Windows Server 2019+, Ubuntu 20.04+, or any Docker host |
| HTTPS endpoint | Required by Telegram Webhook |

### Step-by-step Deployment

**1. Clone and build**
```bash
cd src/ECommerceBot.API
dotnet publish -c Release -o ./publish
```

**2. Configure `appsettings.json`**
Fill in `BotToken`, `WebhookSecretToken`, `AdminChatIds`, and `DefaultConnection`.

**3. Apply database migration**
```powershell
$env:DOTNET_ROOT = "C:\dotnet9"
$env:PATH = "C:\dotnet9;$env:USERPROFILE\.dotnet\tools;" + $env:PATH
dotnet ef database update --project src/ECommerceBot.API
```
> **Note:** The existing migration (`InitialCreate`, 2026-06-04) covers the original schema. The entities updated in Steps 2 and 3 (new fields: `ChatId`, `CurrentState`, `TempData`, `LastActivity` on `TelegramUser`; `DisplayOrder` on `Category` and `Product`; 5 new tables: `WalletTransactions`, `PaymentCards`, `BotSettings`, `Tickets`, `TicketMessages`) require a **new migration** to be generated:
> ```bash
> dotnet ef migrations add BusinessLayer
> dotnet ef database update
> ```

**4. Register the webhook with Telegram**
```
https://api.telegram.org/bot<TOKEN>/setWebhook
  ?url=https://yourdomain.com/api/telegram/webhook
  &secret_token=<WebhookSecretToken>
```

**5. Seed an admin user**
Set `Role = 'Admin'` in the `TelegramUsers` table for any user after they have started the bot (so their `ChatId` is populated).

**6. Seed initial payment card**
Insert a row in `PaymentCards` with `IsActive=1, IsDefault=1`.

**7. Optionally seed BotSettings**
Insert rows for any message templates you want to customize before launch. If left empty, all fallback defaults are used.

---

## 11. Current Project Limitations

### Functional Gaps

| Area | Limitation |
|------|-----------|
| **Migration** | The `InitialCreate` migration only covers the original 9 entities. A second migration for the 5 new tables and 7 new columns must be generated before first run. |
| **Product Creation via Bot** | Adding a new product via the Telegram admin panel shows "coming soon". Full product creation requires direct DB access or a future REST API endpoint. |
| **Cart Integration** | `Cart` and `CartItem` entities and repositories exist but the Telegram bot does not currently use the cart — orders are created directly from the product detail screen. |
| **Quantity > 1** | `CreateOrderRequest.Quantity` is wired through the full order flow, but the bot always sets Quantity=1. Multi-quantity ordering is not exposed in the UI. |
| **Order Expiry Automation** | `OrderService.ExpireStaleOrdersAsync()` is implemented but not scheduled. It must be called by a cron job, background service, or external scheduler. There is no `IHostedService` registered. |
| **Crypto Payment** | `PaymentMethod.CryptoPayment` is a valid enum value and creates a pending order with receipt, but there is no crypto-specific validation, address display, or automated on-chain confirmation. It follows the same manual receipt flow as card payment. |
| **Pagination in Bot** | Pending orders page is limited to 10. User order history shows last 5. There is no "next page" navigation in the Telegram UI. |
| **Media Handling** | `SendPhotoAsync` uses `InputFile.FromFileId()` which requires Telegram to already have the file. Uploading new image files (e.g. product images) is not supported via the bot. |

### Security Considerations

| Area | Note |
|------|------|
| **Admin Elevation** | Admin role is assigned directly in the database. There is no bot-based role promotion command. This is intentional — accidental promotion is prevented. |
| **Webhook Validation** | If `WebhookSecretToken` is left empty in config, the endpoint accepts any POST request. Always set a secret in production. |
| **Bot Token Exposure** | The `TelegramBotClient` is registered as a singleton using the token from `appsettings.json`. Ensure `appsettings.json` is excluded from version control and use environment variables or Azure Key Vault in production. |
| **TempData Size** | `TempData` is capped at `nvarchar(2000)`. Large serialized contexts could be silently truncated by EF Core if not validated. |
| **No Rate Limiting** | There is no per-user rate limiting on bot interactions. A single user can trigger many DB reads per second. |

### Architecture Notes

| Area | Note |
|------|------|
| **Background Dispatch** | The webhook controller uses fire-and-forget (`Task.Run`) for update processing. Unhandled exceptions inside the dispatcher are logged but do not surface to Telegram (the bot will appear unresponsive without error feedback to the user). |
| **No BotSetting Cache** | `BotTextService` reads from DB on every call. For high-traffic bots consider adding an in-memory cache (e.g., `IMemoryCache`) with an invalidation mechanism. |
| **IUnitOfWork Lifetime** | The `UnitOfWork` (and therefore `AppDbContext`) is `Scoped`. Because the webhook processes updates inside `Task.Run` with a discarded `CancellationToken`, the scoped DI container is resolved from the original HTTP request scope. This is safe for single-update processing but would not work for parallel dispatch without explicit scope creation. |
| **Callback Data Length** | Telegram enforces a 64-byte limit on callback data. The longest pattern in use is `adm:prod:{id}:rename` (~20 chars). This is safe for item IDs up to ~40 digits. |
| **No REST API Controllers** | Aside from the webhook endpoint, there are no REST API controllers. All DTOs, repositories, and services are wired but not exposed via HTTP. A REST API layer (e.g. for a web dashboard) would require adding controllers. |
| **Single Migration** | The project has one `InitialCreate` migration that does not reflect the schema additions from Steps 2 and 3. Running `dotnet ef database update` will not create the newer tables/columns until a new migration is added. |
