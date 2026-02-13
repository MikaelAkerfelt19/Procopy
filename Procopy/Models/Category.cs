// ✅ Models/Category.cs - GÜNCELLENMİŞ

using Procopy.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class Category
{
    [Key]
    public int CategoryId { get; set; }

        public int? ParentId { get; set; }

    [Required(ErrorMessage = "Kategori adı zorunludur.")]
    [StringLength(100)]
    public string CategoryName { get; set; } = string.Empty;

    [Required]
    [StringLength(150)]
    public string Slug { get; set; } = string.Empty;

    [StringLength(250)]
    public string? ImageUrl { get; set; }

    // Hiyerarşi seviyesi (1: Ana, 2: Alt, 3: Ürün Grubu)
    [NotMapped]
    public int Level => GetLevel();

    public bool IsActive { get; set; } = true;
    public DateTime CreateDate { get; set; } = DateTime.Now;
    public int DisplayOrder { get; set; } = 0;

    [StringLength(500)]
    public string? ImportUrl { get; set; }

    // Navigation Properties
    [ForeignKey("ParentId")]
    public virtual Category? Parent { get; set; }
    public virtual ICollection<Category> Children { get; set; } = new List<Category>();
    public virtual ICollection<ProductCategory> ProductCategories { get; set; } = new List<ProductCategory>();

    // Hiyerarşi seviyesini hesapla
    private int GetLevel()
    {
        int level = 1;
        var current = Parent;
        while (current != null)
        {
            level++;
            current = current.Parent;
        }
        return level;
    }

    // Tüm alt kategorileri recursive olarak getir
    public IEnumerable<Category> GetAllDescendants()
    {
        var descendants = new List<Category>();
        foreach (var child in Children.Where(c => c.IsActive))
        {
            descendants.Add(child);
            descendants.AddRange(child.GetAllDescendants());
        }
        return descendants;
    }
}