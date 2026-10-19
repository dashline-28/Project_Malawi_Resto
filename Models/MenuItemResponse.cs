namespace Project_Malawi_Resto.Models;

public sealed record MenuItemResponse(
    uint Id,
    string Name,
    string? Description,
    decimal Price,
    string Category,
    bool IsAvailable,
    string ProductType,
    int Quantity,
    bool IsEnabled);
