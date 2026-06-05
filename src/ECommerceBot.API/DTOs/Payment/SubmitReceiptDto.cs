namespace ECommerceBot.API.DTOs.Payment;

public class SubmitReceiptDto
{
    public int OrderId { get; set; }
    public string ReceiptPhotoFileId { get; set; } = string.Empty;
    public string ReceiptPhotoUniqueId { get; set; } = string.Empty;
}
