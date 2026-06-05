using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerceBot.API.Data.Migrations
{
    /// <inheritdoc />
    public partial class Step6_LicensingLocalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PreferredLanguage",
                table: "TelegramUsers",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "LicenseInfos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    LicenseKey = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    OwnerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OwnerEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CustomerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ProductName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Edition = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BotUsername = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AllowedDomain = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ServerFingerprint = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    MaxUsers = table.Column<int>(type: "int", nullable: false),
                    MaxAdmins = table.Column<int>(type: "int", nullable: false),
                    IsTrial = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActivatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastValidatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    GracePeriodEndsAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Signature = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LicenseInfos", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LicenseInfos_ExpiresAt",
                table: "LicenseInfos",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_LicenseInfos_IsActive",
                table: "LicenseInfos",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_LicenseInfos_LicenseKey",
                table: "LicenseInfos",
                column: "LicenseKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LicenseInfos");

            migrationBuilder.DropColumn(
                name: "PreferredLanguage",
                table: "TelegramUsers");
        }
    }
}
