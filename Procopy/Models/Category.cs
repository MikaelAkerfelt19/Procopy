using System;
using System.Collections.Generic;

namespace Procopy.Models;

public partial class Category
{
    public int CategoryId { get; set; }

    public int? ParentId { get; set; }

    public string CategoryName { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string? ImageUrl { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreateDate { get; set; }
    public int DisplayOrder { get; set; } = 0;

    public virtual ICollection<Category> InverseParent { get; set; } = new List<Category>();

    public virtual ICollection<ProductCategory> ProductCategories { get; set; }

    public virtual Category? Parent { get; set; }
    public string? ImportUrl { get; set; } 

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
