using System.ComponentModel.DataAnnotations;

namespace Procopy.Models
{
    public class PrintProductType
    {
        [Key]
        public int ProductTypeId { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = null!;

        [Required, MaxLength(100)]
        public string Slug { get; set; } = null!;

        [Required, MaxLength(50)]
        public string GroupName { get; set; } = null!;

        [MaxLength(500)]
        public string? Description { get; set; }

        [MaxLength(300)]
        public string? ImageUrl { get; set; }

        public int DisplayOrder { get; set; } = 0;
        public bool IsActive { get; set; } = true;
        public DateTime? CreateDate { get; set; }

        // Navigation
        public virtual ICollection<PrintParameter> Parameters { get; set; } = new List<PrintParameter>();
        public virtual ICollection<PrintSize> Sizes { get; set; } = new List<PrintSize>();
        public virtual ICollection<PrintQuantity> Quantities { get; set; } = new List<PrintQuantity>();
        public virtual ICollection<DieCutCost> DieCutCosts { get; set; } = new List<DieCutCost>();
        public virtual ICollection<ExtraCost> ExtraCosts { get; set; } = new List<ExtraCost>();
        public virtual ICollection<PricingRule> PricingRules { get; set; } = new List<PricingRule>();
        public virtual ICollection<PriceCalculationLog> PriceCalculationLogs { get; set; } = new List<PriceCalculationLog>();
    }
}
