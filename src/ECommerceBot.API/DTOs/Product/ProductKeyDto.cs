namespace ECommerceBot.API.DTOs.Product;

public class ProductKeyDto
{
    public int Id { get; set; }
    public string KeyValue { get; set; } = string.Empty;
    public bool IsUsed { get; set; }
    public int ProductId { get; set; }
}
