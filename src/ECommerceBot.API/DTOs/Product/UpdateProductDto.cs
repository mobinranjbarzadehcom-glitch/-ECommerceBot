using ECommerceBot.API.Enums;

namespace ECommerceBot.API.DTOs.Product;

public class UpdateProductDto
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public decimal? Price { get; set; }
    public string? ImageUrl { get; set; }
    public ProductStatus? Status { get; set; }
    public int? CategoryId { get; set; }
}
