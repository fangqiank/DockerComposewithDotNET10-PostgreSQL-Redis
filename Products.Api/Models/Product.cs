namespace Products.Api.Models;

public class Product
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public string Category { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public record CreateProductRequest(
    string Name,
    string Description,
    decimal Price,
    int Stock,
    string Category
);

public record UpdateProductRequest(
    string Name,
    string Description,
    decimal Price,
    int Stock,
    string Category
);
