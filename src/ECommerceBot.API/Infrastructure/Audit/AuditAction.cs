namespace ECommerceBot.API.Infrastructure.Audit;

public static class AuditAction
{
    public const string ApproveOrder = "ApproveOrder";
    public const string RejectOrder = "RejectOrder";
    public const string RefundOrder = "RefundOrder";
    public const string BlockUser = "BlockUser";
    public const string UnblockUser = "UnblockUser";
    public const string AddProduct = "AddProduct";
    public const string EditProductTitle = "EditProductTitle";
    public const string EditProductPrice = "EditProductPrice";
    public const string DisableProduct = "DisableProduct";
    public const string EnableProduct = "EnableProduct";
    public const string AddCategory = "AddCategory";
    public const string EditCategory = "EditCategory";
    public const string DisableCategory = "DisableCategory";
    public const string EnableCategory = "EnableCategory";
    public const string AddCard = "AddCard";
    public const string EditCard = "EditCard";
    public const string DisableCard = "DisableCard";
    public const string SetDefaultCard = "SetDefaultCard";
    public const string EditSetting = "EditSetting";
    public const string MessageUser = "MessageUser";
    public const string RequestNewReceipt = "RequestNewReceipt";
    public const string AddAdminNote = "AddAdminNote";
    public const string ExpireOrder = "ExpireOrder";
    public const string LicenseActivated = "LicenseActivated";
    public const string EditUser = "EditUser";
    public const string AddAdminRole = "AddAdminRole";
    public const string RemoveAdminRole = "RemoveAdminRole";
    public const string CreateCoupon = "CreateCoupon";
    public const string ToggleCoupon = "ToggleCoupon";
    public const string ApplyCoupon = "ApplyCoupon";
    public const string CreateAffiliate = "CreateAffiliate";
    public const string TrackReferral = "TrackReferral";
    public const string BroadcastMessage = "BroadcastMessage";
    public const string ExportOrders = "ExportOrders";
    public const string ExportUsers = "ExportUsers";

    // Phase 6
    public const string CreateRenewalRequest = "CreateRenewalRequest";
    public const string ApproveRenewal = "ApproveRenewal";
    public const string RejectRenewal = "RejectRenewal";
    public const string CreateFaqItem = "CreateFaqItem";
    public const string EditFaqItem = "EditFaqItem";
    public const string DeleteFaqItem = "DeleteFaqItem";
    public const string ScheduleBroadcast = "ScheduleBroadcast";
    public const string CancelBroadcast = "CancelBroadcast";
    public const string UpdateWhiteLabel = "UpdateWhiteLabel";
    public const string ManualBackup = "ManualBackup";
    public const string SuspendTenant = "SuspendTenant";
    public const string ActivateTenant = "ActivateTenant";
    public const string AddTenantNote = "AddTenantNote";
    public const string DeleteTenantNote = "DeleteTenantNote";
    public const string RetryWebhook = "RetryWebhook";
}
