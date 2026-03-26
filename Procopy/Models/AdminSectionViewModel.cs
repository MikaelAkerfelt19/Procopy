namespace Procopy.Models
{
    public class AdminSectionViewModel
    {
        public string SectionName { get; set; } = "";
        public string SectionSlug { get; set; } = "";
        public string SectionIcon { get; set; } = "fas fa-folder";
        public Category? RootCategory { get; set; }
        public List<Product> RecentProducts { get; set; } = new();
        public int TotalCategories { get; set; }
        public int TotalProducts { get; set; }
    }
}
