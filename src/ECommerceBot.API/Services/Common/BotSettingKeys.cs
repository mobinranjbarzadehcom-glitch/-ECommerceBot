namespace ECommerceBot.API.Services.Common;

public static class BotSettingKeys
{
    // Payment / rotation
    public const string IsCardRotationEnabled = "IsCardRotationEnabled";
    public const string LastUsedCardId = "LastUsedCardId";

    // Messages
    public const string WelcomeMessage = "WelcomeMessage";
    public const string HelpMessage = "HelpMessage";
    public const string PaymentInstructionMessage = "PaymentInstructionMessage";
    public const string OrderApprovedMessage = "OrderApprovedMessage";
    public const string OrderRejectedMessage = "OrderRejectedMessage";
    public const string OrderPendingMessage = "OrderPendingMessage";
    public const string OrderExpiredMessage = "OrderExpiredMessage";
    public const string SupportWelcomeMessage = "SupportWelcomeMessage";

    // Main menu buttons
    public const string MainMenuProductsButton = "MainMenu.ProductsButton";
    public const string MainMenuWalletButton = "MainMenu.WalletButton";
    public const string MainMenuOrdersButton = "MainMenu.OrdersButton";
    public const string MainMenuSupportButton = "MainMenu.SupportButton";
    public const string MainMenuHelpButton = "MainMenu.HelpButton";

    // Admin menu buttons
    public const string AdminMenuOrdersButton = "AdminMenu.OrdersButton";
    public const string AdminMenuUsersButton = "AdminMenu.UsersButton";
    public const string AdminMenuProductsButton = "AdminMenu.ProductsButton";
    public const string AdminMenuCategoriesButton = "AdminMenu.CategoriesButton";
    public const string AdminMenuCardsButton = "AdminMenu.CardsButton";
    public const string AdminMenuSettingsButton = "AdminMenu.SettingsButton";
    public const string AdminMenuStatisticsButton = "AdminMenu.StatisticsButton";
    public const string AdminMenuLicenseButton = "AdminMenu.LicenseButton";
    public const string AdminMenuFaqButton = "AdminMenu.FaqButton";
    public const string AdminMenuBrandingButton = "AdminMenu.BrandingButton";
    public const string AdminMenuResourceUsageButton = "AdminMenu.ResourceUsageButton";
    public const string MainMenuFaqButton = "MainMenu.FaqButton";

    // Admin action buttons
    public const string AdminActionsApproveButton = "AdminActions.ApproveButton";
    public const string AdminActionsRejectButton = "AdminActions.RejectButton";
    public const string AdminActionsRefundButton = "AdminActions.RefundButton";
    public const string AdminActionsMessageButton = "AdminActions.MessageButton";
    public const string AdminActionsNewReceiptButton = "AdminActions.NewReceiptButton";

    // White-label branding (editable via admin panel)
    public const string BrandName = "Brand.Name";
    public const string BrandShortName = "Brand.ShortName";
    public const string BrandSupportUsername = "Brand.SupportUsername";
    public const string BrandWebsiteUrl = "Brand.WebsiteUrl";
    public const string BrandFooterText = "Brand.FooterText";
    public const string BrandPoweredByText = "Brand.PoweredByText";
    public const string BrandShowPoweredBy = "Brand.ShowPoweredBy";
    public const string BrandPrimaryEmoji = "Brand.PrimaryEmoji";
    public const string BrandSuccessEmoji = "Brand.SuccessEmoji";
    public const string BrandWarningEmoji = "Brand.WarningEmoji";
    public const string BrandErrorEmoji = "Brand.ErrorEmoji";
    public const string BrandLogoFileId = "Brand.LogoFileId";

    // Backup
    public const string BackupChannelId = "Backup.ChannelId";

    // Renewal
    public const string RenewalPaymentInstruction = "Renewal.PaymentInstruction";

    // License messages
    public const string LicenseActivationSuccessMessage = "License.ActivationSuccessMessage";
    public const string LicenseActivationFailedMessage = "License.ActivationFailedMessage";
}
