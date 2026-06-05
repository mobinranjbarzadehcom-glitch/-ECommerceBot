using ECommerceBot.API.DTOs.Payment;
using ECommerceBot.API.Services.Common;
using ECommerceBot.API.Services.Interfaces;
using ECommerceBot.API.UnitOfWork;

namespace ECommerceBot.API.Services.Implementations;

public class PaymentService : IPaymentService
{
    private readonly IUnitOfWork _uow;
    private readonly ISettingService _settingService;

    public PaymentService(IUnitOfWork uow, ISettingService settingService)
    {
        _uow = uow;
        _settingService = settingService;
    }

    public async Task<ServiceResult<PaymentCardDto>> GetActivePaymentCardAsync() =>
        await _settingService.GetActivePaymentCardAsync();

    public async Task<ServiceResult> ValidateReceiptUniqueIdAsync(string fileUniqueId)
    {
        if (string.IsNullOrWhiteSpace(fileUniqueId))
            return ServiceResult.Failure("Receipt photo unique ID is required");

        var existing = await _uow.Orders.GetByReceiptUniqueIdAsync(fileUniqueId);
        return existing is not null
            ? ServiceResult.Failure("This receipt has already been used")
            : ServiceResult.Success();
    }

    public async Task<ServiceResult> SubmitReceiptAsync(int orderId, string receiptPhotoFileId, string receiptPhotoUniqueId)
    {
        if (string.IsNullOrWhiteSpace(receiptPhotoFileId))
            return ServiceResult.Failure("Receipt photo file ID is required");

        if (string.IsNullOrWhiteSpace(receiptPhotoUniqueId))
            return ServiceResult.Failure("Receipt photo unique ID is required");

        var duplicateCheck = await ValidateReceiptUniqueIdAsync(receiptPhotoUniqueId);
        if (!duplicateCheck.IsSuccess)
            return duplicateCheck;

        var order = await _uow.Orders.GetByIdAsync(orderId);
        if (order is null)
            return ServiceResult.Failure("Order not found");

        if (order.Status != Enums.OrderStatus.Pending)
            return ServiceResult.Failure("Order is not in pending status");

        order.ReceiptPhotoFileId = receiptPhotoFileId;
        order.ReceiptPhotoUniqueId = receiptPhotoUniqueId;
        _uow.Orders.Update(order);
        await _uow.SaveChangesAsync();

        return ServiceResult.Success();
    }
}
