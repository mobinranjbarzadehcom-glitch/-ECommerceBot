using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceBot.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase3_PersianSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Seed Persian UI text for default tenant (Id=1).
            // Uses MERGE so re-running the migration is idempotent.
            migrationBuilder.Sql(@"
                MERGE BotSettings AS target
                USING (VALUES
                    -- ── General messages ──────────────────────────────────────────────
                    (1, N'WelcomeMessage',              N'👋 <b>سلام {name}!</b>\n\nبه فروشگاه ما خوش آمدید. از منو زیر انتخاب کنید.'),
                    (1, N'HelpMessage',                 N'❓ <b>راهنما</b>\n\nاز دکمه‌های منو برای دسترسی به بخش‌های مختلف استفاده کنید.'),
                    (1, N'Menu.Title',                  N'📋 <b>منو</b>'),
                    -- ── Payment ───────────────────────────────────────────────────────
                    (1, N'PaymentInstructionMessage',   N'💳 لطفاً مبلغ <b>{amount}</b> را به شماره کارت واریز کرده و رسید را ارسال کنید.'),
                    (1, N'SupportWelcomeMessage',       N'🎫 <b>پشتیبانی</b>\n\nپیام خود را ارسال کنید. به زودی پاسخ خواهید گرفت.'),
                    -- ── Main menu buttons ──────────────────────────────────────────────
                    (1, N'MainMenu.ProductsButton',     N'🛒 محصولات'),
                    (1, N'MainMenu.WalletButton',       N'💰 کیف پول'),
                    (1, N'MainMenu.OrdersButton',       N'📦 سفارش‌ها'),
                    (1, N'MainMenu.SupportButton',      N'🎫 پشتیبانی'),
                    (1, N'MainMenu.HelpButton',         N'❓ راهنما'),
                    -- ── Admin menu buttons ─────────────────────────────────────────────
                    (1, N'AdminMenu.OrdersButton',      N'📋 سفارش‌های در انتظار'),
                    (1, N'AdminMenu.UsersButton',       N'👥 کاربران'),
                    (1, N'AdminMenu.ProductsButton',    N'📦 محصولات'),
                    (1, N'AdminMenu.CategoriesButton',  N'🗂 دسته‌بندی‌ها'),
                    (1, N'AdminMenu.CardsButton',       N'💳 کارت‌های بانکی'),
                    (1, N'AdminMenu.SettingsButton',    N'⚙️ تنظیمات'),
                    (1, N'AdminMenu.StatisticsButton',  N'📊 آمار'),
                    (1, N'AdminMenu.AdminsButton',      N'👑 مدیریت ادمین‌ها'),
                    (1, N'AdminMenu.UserViewButton',    N'👁 مشاهده مثل کاربر'),
                    (1, N'AdminMenu.LicenseButton',     N'🔐 وضعیت لایسنس'),
                    -- ── Shared buttons ────────────────────────────────────────────────
                    (1, N'Buttons.CancelButton',        N'❌ لغو'),
                    (1, N'Buttons.BackButton',          N'⬅️ بازگشت'),
                    (1, N'Buttons.ConfirmButton',       N'✅ تأیید'),
                    -- ── Admin action buttons ───────────────────────────────────────────
                    (1, N'AdminActions.ApproveButton',  N'🟢 تأیید'),
                    (1, N'AdminActions.RejectButton',   N'🔴 رد'),
                    (1, N'AdminActions.RequestNewReceiptButton', N'🔄 رسید جدید'),
                    (1, N'AdminActions.RefundButton',   N'💸 استرداد'),
                    -- ── Error messages ────────────────────────────────────────────────
                    (1, N'Errors.Blocked',              N'❌ حساب شما مسدود شده است.'),
                    (1, N'Errors.RateLimited',          N'⚠️ درخواست‌های زیادی ارسال کردید. لطفاً کمی صبر کنید.'),
                    (1, N'Errors.PlayerIdEmpty',        N'❌ شناسه بازیکن نمی‌تواند خالی باشد. دوباره وارد کنید:'),
                    (1, N'Errors.UseMenuButtons',       N'لطفاً از دکمه‌های منو استفاده کنید.'),
                    (1, N'Errors.PleaseStart',          N'برای شروع /start را ارسال کنید.'),
                    -- ── Order messages ────────────────────────────────────────────────
                    (1, N'OrderPendingMessage',         N'⏳ <b>سفارش #{orderId} ثبت شد!</b>\n\nدر حال بررسی هستیم. به زودی نتیجه اعلام می‌شود.'),
                    (1, N'OrderApprovedMessage',        N'✅ <b>سفارش #{orderId} تأیید شد!</b>\n\nکلیدهای شما:\n{keys}'),
                    (1, N'OrderRejectedMessage',        N'❌ <b>سفارش #{orderId} رد شد.</b>\n\nدلیل: {reason}'),
                    -- ── Products ──────────────────────────────────────────────────────
                    (1, N'Products.SelectCategory',     N'🛒 <b>دسته‌بندی را انتخاب کنید:</b>'),
                    (1, N'Products.NoCategoriesAvailable', N'😔 در حال حاضر محصولی موجود نیست.'),
                    (1, N'Products.EnterPlayerId',      N'🎮 <b>لطفاً شناسه بازیکن / مشخصات حساب را وارد کنید:</b>'),
                    (1, N'Products.OutOfStock',         N'❌ <i>موجودی ندارد</i>'),
                    -- ── Wallet ────────────────────────────────────────────────────────
                    (1, N'Wallet.Title',                N'💰 <b>کیف پول</b>\n\nموجودی: <b>{balance} تومان</b>'),
                    (1, N'Wallet.RecentTransactions',   N'📋 <b>تراکنش‌های اخیر:</b>'),
                    -- ── Orders ────────────────────────────────────────────────────────
                    (1, N'Orders.Empty',                N'📦 هنوز سفارشی ثبت نکرده‌اید.'),
                    (1, N'Orders.RecentTitle',          N'📦 <b>سفارش‌های اخیر شما:</b>'),
                    -- ── Ticket ────────────────────────────────────────────────────────
                    (1, N'Ticket.SubjectFormat',        N'پشتیبانی از {name}'),
                    (1, N'Ticket.CreatedSuccess',       N'✅ <b>تیکت #{ticketId} ثبت شد!</b>\n\nبه زودی پاسخ خواهید گرفت.')
                ) AS source (TenantId, [Key], Value)
                ON target.TenantId = source.TenantId AND target.[Key] = source.[Key]
                WHEN NOT MATCHED THEN
                    INSERT (TenantId, [Key], Value, CreatedAt, UpdatedAt)
                    VALUES (source.TenantId, source.[Key], source.Value, GETUTCDATE(), GETUTCDATE());
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM BotSettings WHERE TenantId = 1 AND [Key] IN (
                    'WelcomeMessage','HelpMessage','Menu.Title','PaymentInstructionMessage',
                    'SupportWelcomeMessage','MainMenu.ProductsButton','MainMenu.WalletButton',
                    'MainMenu.OrdersButton','MainMenu.SupportButton','MainMenu.HelpButton',
                    'AdminMenu.OrdersButton','AdminMenu.UsersButton','AdminMenu.ProductsButton',
                    'AdminMenu.CategoriesButton','AdminMenu.CardsButton','AdminMenu.SettingsButton',
                    'AdminMenu.StatisticsButton','AdminMenu.AdminsButton','AdminMenu.UserViewButton',
                    'AdminMenu.LicenseButton','Buttons.CancelButton','Buttons.BackButton',
                    'Buttons.ConfirmButton','AdminActions.ApproveButton','AdminActions.RejectButton',
                    'AdminActions.RequestNewReceiptButton','AdminActions.RefundButton',
                    'Errors.Blocked','Errors.RateLimited','Errors.PlayerIdEmpty',
                    'Errors.UseMenuButtons','Errors.PleaseStart','OrderPendingMessage',
                    'OrderApprovedMessage','OrderRejectedMessage','Products.SelectCategory',
                    'Products.NoCategoriesAvailable','Products.EnterPlayerId','Products.OutOfStock',
                    'Wallet.Title','Wallet.RecentTransactions','Orders.Empty','Orders.RecentTitle',
                    'Ticket.SubjectFormat','Ticket.CreatedSuccess'
                );
            ");
        }
    }
}
