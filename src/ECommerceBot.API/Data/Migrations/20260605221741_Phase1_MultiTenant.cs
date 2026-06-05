using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceBot.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase1_MultiTenant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── 1. Drop old single-tenant unique indexes ───────────────────────
            migrationBuilder.DropIndex(
                name: "IX_TelegramUsers_TelegramId",
                table: "TelegramUsers");

            migrationBuilder.DropIndex(
                name: "IX_BotSettings_Key",
                table: "BotSettings");

            // ── 2. Add TenantId columns (nullable temporarily, defaultValue:0) ─
            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "WalletTransactions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Tickets",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "TelegramUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Products",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "PaymentCards",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Orders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "LicenseInfos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Categories",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "Value",
                table: "BotSettings",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "Key",
                table: "BotSettings",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "BotSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "AuditLogs",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // ── 3. Create SubscriptionPlans table ─────────────────────────────
            migrationBuilder.CreateTable(
                name: "SubscriptionPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Tier = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MonthlyPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    YearlyPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    MaxUsers = table.Column<int>(type: "int", nullable: false),
                    MaxProducts = table.Column<int>(type: "int", nullable: false),
                    MaxAdmins = table.Column<int>(type: "int", nullable: false),
                    MaxOrdersPerMonth = table.Column<int>(type: "int", nullable: false),
                    AllowsAffiliate = table.Column<bool>(type: "bit", nullable: false),
                    AllowsCoupons = table.Column<bool>(type: "bit", nullable: false),
                    AllowsAiSupport = table.Column<bool>(type: "bit", nullable: false),
                    AllowsWhiteLabel = table.Column<bool>(type: "bit", nullable: false),
                    AllowsMultiLanguage = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPlans", x => x.Id);
                });

            // ── 4. Seed default subscription plans ────────────────────────────
            migrationBuilder.Sql(@"
                SET IDENTITY_INSERT SubscriptionPlans ON;
                INSERT INTO SubscriptionPlans (Id, Name, Tier, MonthlyPrice, YearlyPrice,
                    MaxUsers, MaxProducts, MaxAdmins, MaxOrdersPerMonth,
                    AllowsAffiliate, AllowsCoupons, AllowsAiSupport, AllowsWhiteLabel, AllowsMultiLanguage,
                    IsActive, Description, CreatedAt, UpdatedAt)
                VALUES
                    (1, N'Starter',      'Starter',      0,  0,   500,  50,  2,  200, 0, 0, 0, 0, 0, 1, N'پلن پایه', GETUTCDATE(), GETUTCDATE()),
                    (2, N'Professional', 'Professional', 0,  0,  2000, 200,  5, 1000, 1, 1, 0, 0, 1, 1, N'پلن حرفه‌ای', GETUTCDATE(), GETUTCDATE()),
                    (3, N'Enterprise',   'Enterprise',   0,  0, 10000, 999, 20, 9999, 1, 1, 1, 1, 1, 1, N'پلن سازمانی', GETUTCDATE(), GETUTCDATE());
                SET IDENTITY_INSERT SubscriptionPlans OFF;
            ");

            // ── 5. Create Tenants table ───────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TenantName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TenantSlug = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CustomerPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    CustomerEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    BotUsername = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BotTokenEncrypted = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    WebhookSecret = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    PlanId = table.Column<int>(type: "int", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TrialEndsAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsInGracePeriod = table.Column<bool>(type: "bit", nullable: false),
                    MaxUsers = table.Column<int>(type: "int", nullable: false),
                    MaxProducts = table.Column<int>(type: "int", nullable: false),
                    MaxAdmins = table.Column<int>(type: "int", nullable: false),
                    OwnerTelegramId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tenants_SubscriptionPlans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "SubscriptionPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            // ── 6. Seed default tenant (Id=1 — maps to the existing single bot) ─
            migrationBuilder.Sql(@"
                SET IDENTITY_INSERT Tenants ON;
                INSERT INTO Tenants (Id, TenantName, TenantSlug, BotTokenEncrypted,
                    Status, IsActive, PlanId, MaxUsers, MaxProducts, MaxAdmins,
                    IsInGracePeriod, CreatedAt, UpdatedAt)
                VALUES (1, N'فروشگاه پیش‌فرض', N'default', N'',
                    'Active', 1, 1, 500, 50, 5,
                    0, GETUTCDATE(), GETUTCDATE());
                SET IDENTITY_INSERT Tenants OFF;
            ");

            // ── 7. Migrate all existing data to default tenant (Id=1) ─────────
            migrationBuilder.Sql("UPDATE TelegramUsers    SET TenantId = 1 WHERE TenantId = 0");
            migrationBuilder.Sql("UPDATE Categories       SET TenantId = 1 WHERE TenantId = 0");
            migrationBuilder.Sql("UPDATE Products         SET TenantId = 1 WHERE TenantId = 0");
            migrationBuilder.Sql("UPDATE Orders           SET TenantId = 1 WHERE TenantId = 0");
            migrationBuilder.Sql("UPDATE PaymentCards     SET TenantId = 1 WHERE TenantId = 0");
            migrationBuilder.Sql("UPDATE WalletTransactions SET TenantId = 1 WHERE TenantId = 0");
            migrationBuilder.Sql("UPDATE Tickets          SET TenantId = 1 WHERE TenantId = 0");
            migrationBuilder.Sql("UPDATE BotSettings      SET TenantId = 1 WHERE TenantId = 0");
            migrationBuilder.Sql("UPDATE AuditLogs        SET TenantId = 1 WHERE TenantId = 0");
            migrationBuilder.Sql("UPDATE LicenseInfos     SET TenantId = 1 WHERE TenantId = 0");

            // ── 8. Create composite unique indexes ────────────────────────────
            migrationBuilder.CreateIndex(
                name: "IX_TelegramUsers_TenantId_TelegramId",
                table: "TelegramUsers",
                columns: new[] { "TenantId", "TelegramId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BotSettings_TenantId_Key",
                table: "BotSettings",
                columns: new[] { "TenantId", "Key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_PlanId",
                table: "Tenants",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_TenantSlug",
                table: "Tenants",
                column: "TenantSlug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Tenants");
            migrationBuilder.DropTable(name: "SubscriptionPlans");

            migrationBuilder.DropIndex(
                name: "IX_TelegramUsers_TenantId_TelegramId",
                table: "TelegramUsers");

            migrationBuilder.DropIndex(
                name: "IX_BotSettings_TenantId_Key",
                table: "BotSettings");

            migrationBuilder.DropColumn(name: "TenantId", table: "WalletTransactions");
            migrationBuilder.DropColumn(name: "TenantId", table: "Tickets");
            migrationBuilder.DropColumn(name: "TenantId", table: "TelegramUsers");
            migrationBuilder.DropColumn(name: "TenantId", table: "Products");
            migrationBuilder.DropColumn(name: "TenantId", table: "PaymentCards");
            migrationBuilder.DropColumn(name: "TenantId", table: "Orders");
            migrationBuilder.DropColumn(name: "TenantId", table: "LicenseInfos");
            migrationBuilder.DropColumn(name: "TenantId", table: "Categories");
            migrationBuilder.DropColumn(name: "TenantId", table: "BotSettings");
            migrationBuilder.DropColumn(name: "TenantId", table: "AuditLogs");

            migrationBuilder.AlterColumn<string>(
                name: "Value",
                table: "BotSettings",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(4000)",
                oldMaxLength: 4000);

            migrationBuilder.AlterColumn<string>(
                name: "Key",
                table: "BotSettings",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.CreateIndex(
                name: "IX_TelegramUsers_TelegramId",
                table: "TelegramUsers",
                column: "TelegramId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BotSettings_Key",
                table: "BotSettings",
                column: "Key",
                unique: true);
        }
    }
}
