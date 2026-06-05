namespace ECommerceBot.API.DTOs.Product;

public class CreateProductKeyDto
{
    public string KeyValue { get; set; } = string.Empty;
    public int ProductId { get; set; }
}
