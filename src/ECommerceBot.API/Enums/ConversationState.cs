namespace ECommerceBot.API.Enums;

public enum ConversationState
{
    None = 0,
    AwaitingPhone = 1,
    AwaitingPlayerId = 2,
    AwaitingAccountInfo = 3,
    AwaitingReceipt = 4,
    AwaitingTicketMessage = 5,

    // Admin states
    AwaitingRejectReason = 10,
    AwaitingCategoryName = 11,
    AwaitingProductTitle = 12,
    AwaitingProductPrice = 13,
    AwaitingCardNumber = 14,
    AwaitingSettingValue = 15,
    AwaitingAdminMessage = 16,
    AwaitingCardHolder = 17,
    AwaitingCardBank = 18,

    // License states
    AwaitingLicenseKey = 20,

    // Product creation wizard
    AwaitingProductDescription = 30,
    AwaitingProductKeys = 31,

    // Admin management
    AwaitingNewAdminTelegramId = 40,

    // Backup channel
    AwaitingBackupChannelId = 50,

    // Coupon admin wizard
    AwaitingCouponCode = 60,
    AwaitingCouponDiscountValue = 61,
    AwaitingCouponMaxUses = 62,
    AwaitingCouponExpiry = 63,

    // User coupon at checkout
    AwaitingApplyCoupon = 70,
}
