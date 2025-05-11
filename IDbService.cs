namespace Tutorial9.Services;

public interface IDbService
{
    Task<int> AddProductToWarehouseAsync(
        int productId, 
        int warehouseId, 
        int amount, 
        DateTime createdAt);
    // ... other methods
}