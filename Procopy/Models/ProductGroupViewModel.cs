namespace Procopy.Models
{
    public class ProductGroupViewModel
    {
        public Category Category { get; set; }
        public List<Product> Products { get; set; } = new();
        public int TotalCount { get; set; }
    }
}
