using ECommerceBot.API.DTOs.Payment;
using ECommerceBot.API.Services.Common;

namespace ECommerceBot.API.Services.Interfaces;

public interface IPaymentService
{
    Task<ServiceResult<PaymentCardDto>> GetActivePaymentCardAsync();
    Task<ServiceResult> ValidateReceiptUniqueIdAsync(string fileUniqueId);
    Task<ServiceResult> SubmitReceiptAsync(int orderId, string receiptPhotoFileId, string receiptPhotoUniqueId);
}
