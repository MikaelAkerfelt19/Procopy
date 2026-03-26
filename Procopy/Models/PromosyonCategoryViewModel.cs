namespace Procopy.Models
{
    public class PromosyonCategoryViewModel
    {
        public List<CategoryMenuItem> Categories { get; set; } = new();
        public CategoryMenuItem? ActiveCategory { get; set; }
        public List<Product> Products { get; set; } = new();
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
        public string? CurrentCategorySlug { get; set; }
    }

    public class CategoryMenuItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Slug { get; set; } = "";
        public string IconClass { get; set; } = "fas fa-tag";
        public int ProductCount { get; set; }
        public List<CategoryMenuItem> Children { get; set; } = new();
    }
}
