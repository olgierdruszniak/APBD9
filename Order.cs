namespace Tutorial9.Model;

public class Order
{
    public int Id { get; set; }
    public int IdProduct { get; set; }
    public int Amount { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? Fulfilled { get; set; }
    
}