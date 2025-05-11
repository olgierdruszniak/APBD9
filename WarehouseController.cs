using Microsoft.AspNetCore.Mvc;
using Tutorial9.Services;

namespace Tutorial9.Controllers
{
    [Route("api/warehouse")]
    [ApiController]
    public class WarehouseController : ControllerBase
    {
        private readonly IDbService _dbService;

        public WarehouseController(IDbService dbService)
        {
            _dbService = dbService;
        }

        [HttpPost("add-product")]
        public async Task<IActionResult> AddProductToWarehouse(
            [FromQuery] int productId,
            [FromQuery] int warehouseId,
            [FromQuery] int amount,
            [FromQuery] DateTime createdAt)
        {
            // Manual validation
            if (productId <= 0) return BadRequest("Product ID must be positive");
            if (warehouseId <= 0) return BadRequest("Warehouse ID must be positive");
            if (amount <= 0) return BadRequest("Amount must be greater than 0");
            if (createdAt > DateTime.Now) return BadRequest("CreatedAt cannot be in the future");

            try
            {
                var result = await _dbService.AddProductToWarehouseAsync(
                    productId, 
                    warehouseId, 
                    amount, 
                    createdAt);
                
                return Ok(new { IdProductWarehouse = result });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}