using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace Tutorial9.Services;

public class DbService : IDbService
{
    private readonly IConfiguration _configuration;
    public DbService(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    
    public async Task DoSomethingAsync()
    {
        await using SqlConnection connection = new SqlConnection(_configuration.GetConnectionString("Default"));
        await using SqlCommand command = new SqlCommand();
        
        command.Connection = connection;
        await connection.OpenAsync();

        DbTransaction transaction = await connection.BeginTransactionAsync();
        command.Transaction = transaction as SqlTransaction;

        // BEGIN TRANSACTION
        try
        {
            command.CommandText = "INSERT INTO Animal VALUES (@IdAnimal, @Name);";
            command.Parameters.AddWithValue("@IdAnimal", 1);
            command.Parameters.AddWithValue("@Name", "Animal1");
        
            await command.ExecuteNonQueryAsync();
        
            command.Parameters.Clear();
            command.CommandText = "INSERT INTO Animal VALUES (@IdAnimal, @Name);";
            command.Parameters.AddWithValue("@IdAnimal", 2);
            command.Parameters.AddWithValue("@Name", "Animal2");
        
            await command.ExecuteNonQueryAsync();
            
            await transaction.CommitAsync();
        }
        catch (Exception e)
        {
            await transaction.RollbackAsync();
            throw;
        }
        // END TRANSACTION
    }

    public async Task ProcedureAsync()
    {
        await using SqlConnection connection = new SqlConnection(_configuration.GetConnectionString("Default"));
        await using SqlCommand command = new SqlCommand();
        
        command.Connection = connection;
        await connection.OpenAsync();
        
        command.CommandText = "NazwaProcedury";
        command.CommandType = CommandType.StoredProcedure;
        
        command.Parameters.AddWithValue("@Id", 2);
        
        await command.ExecuteNonQueryAsync();
        
    }
    
    public async Task<int> AddProductToWarehouseAsync(
        int productId, 
        int warehouseId, 
        int amount, 
        DateTime createdAt)
    {
        await using var connection = new SqlConnection(_configuration.GetConnectionString("Default"));
        await connection.OpenAsync();
    
        // Explicitly declare as SqlTransaction
        SqlTransaction transaction = null;

        try
        {
            transaction = (SqlTransaction)await connection.BeginTransactionAsync();
            
            if (!await RecordExists(connection, "Product", "IdProduct", productId, transaction))
                throw new Exception("Product not found");
            
            if (!await RecordExists(connection, "Warehouse", "IdWarehouse", warehouseId, transaction))
                throw new Exception("Warehouse not found");
            
            var orderId = await FindValidOrder(connection, productId, amount, createdAt, transaction);
            if (orderId == null)
                throw new Exception("No valid order found");
            
            if (await OrderFulfilled(connection, orderId.Value, transaction))
                throw new Exception("Order already fulfilled");
            
            await UpdateOrder(connection, orderId.Value, transaction);
            
            var price = await GetProductPrice(connection, productId, transaction) * amount;
            
            var newId = await InsertProductWarehouse(
                connection,
                productId,
                warehouseId,
                orderId.Value,
                amount,
                price,
                transaction);

            await transaction.CommitAsync();
            return newId;
        }
        catch
        {
            transaction?.Rollback();
            throw;
        }
        finally
        {
            transaction?.Dispose();
        }
    }

private async Task<bool> RecordExists(SqlConnection conn, string table, string idColumn, int id, SqlTransaction transaction)
{
    var cmd = new SqlCommand($"SELECT 1 FROM {table} WHERE {idColumn} = @id", conn, transaction);
    cmd.Parameters.AddWithValue("@id", id);
    return (await cmd.ExecuteScalarAsync()) != null;
}

private async Task<int?> FindValidOrder(SqlConnection conn, int productId, int amount, DateTime createdAt, SqlTransaction transaction)
{
    var cmd = new SqlCommand(
        @"SELECT TOP 1 IdOrder FROM [Order] 
          WHERE IdProduct = @productId 
          AND Amount = @amount
          AND CreatedAt < @createdAt
          AND FulfilledAt IS NULL", 
        conn, 
        transaction);
    
    cmd.Parameters.AddWithValue("@productId", productId);
    cmd.Parameters.AddWithValue("@amount", amount);
    cmd.Parameters.AddWithValue("@createdAt", createdAt);
    
    return (int?)await cmd.ExecuteScalarAsync();
}

private async Task<bool> OrderFulfilled(SqlConnection conn, int orderId, SqlTransaction transaction)
{
    var cmd = new SqlCommand(
        "SELECT 1 FROM Product_Warehouse WHERE IdOrder = @orderId", 
        conn, 
        transaction);
    
    cmd.Parameters.AddWithValue("@orderId", orderId);
    return (await cmd.ExecuteScalarAsync()) != null;
}

private async Task UpdateOrder(SqlConnection conn, int orderId, SqlTransaction transaction)
{
    var cmd = new SqlCommand(
        "UPDATE [Order] SET FulfilledAt = GETDATE() WHERE IdOrder = @orderId",
        conn,
        transaction);
    
    cmd.Parameters.AddWithValue("@orderId", orderId);
    await cmd.ExecuteNonQueryAsync();
}

private async Task<decimal> GetProductPrice(SqlConnection conn, int productId, SqlTransaction transaction)
{
    var cmd = new SqlCommand(
        "SELECT Price FROM Product WHERE IdProduct = @productId",
        conn,
        transaction);
    
    cmd.Parameters.AddWithValue("@productId", productId);
    return (decimal)await cmd.ExecuteScalarAsync();
}

private async Task<int> InsertProductWarehouse(
    SqlConnection conn,
    int productId,
    int warehouseId,
    int orderId,
    int amount,
    decimal price,
    SqlTransaction transaction)
{
    var cmd = new SqlCommand(
        @"INSERT INTO Product_Warehouse (
            IdWarehouse, 
            IdProduct, 
            IdOrder, 
            Amount, 
            Price, 
            CreatedAt
          ) 
          VALUES (
            @warehouseId, 
            @productId, 
            @orderId, 
            @amount, 
            @price, 
            GETDATE()
          );
          SELECT SCOPE_IDENTITY();",
        conn,
        transaction);
    
    cmd.Parameters.AddWithValue("@warehouseId", warehouseId);
    cmd.Parameters.AddWithValue("@productId", productId);
    cmd.Parameters.AddWithValue("@orderId", orderId);
    cmd.Parameters.AddWithValue("@amount", amount);
    cmd.Parameters.AddWithValue("@price", price);
    
    return Convert.ToInt32(await cmd.ExecuteScalarAsync());
}
}