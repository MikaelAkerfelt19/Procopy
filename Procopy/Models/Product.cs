using System;
using System.Collections.Generic;

namespace Procopy.Models;

public partial class Product
{
    public int ProductId { get; set; }

    public int CategoryId { get; set; }

    public string ProductName { get; set; } = null!;

    public string? ProductCode { get; set; }

    public string? Description { get; set; }

    public string? MainImageUrl { get; set; }

    public bool? IsFeatured { get; set; }

    public bool? IsActive { get; set; }

    public string Slug { get; set; } = null!;

    public DateTime? CreateDate { get; set; }
    public string? SourceUrl { get; set; }
    public decimal? Price { get; set; }
    public decimal? OldPrice { get; set; }
    public ICollection<ProductCategory>? ProductCategories { get; set; }
    public int? TotalWhatsappClicks { get; set; }

    public virtual Category Category { get; set; }

    public virtual ICollection<ProductImage> ProductImages { get; set; } = new List<ProductImage>();
   
    public virtual ICollection<ProductOption> ProductOptions { get; set; } = new List<ProductOption>();
    public virtual ICollection<ProductStat> ProductStats { get; set; } = new List<ProductStat>();
}
