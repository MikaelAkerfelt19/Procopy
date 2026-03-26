using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Procopy.Models
{
    public class PricingRule
    {
        [Key]
        public int RuleId { get; set; }

        [Required, MaxLength(100)]
        public string RuleName { get; set; } = null!;

        [Required, MaxLength(50)]
        public string RuleKey { get; set; } = null!;

        [Column(TypeName = "decimal(10,4)")]
        public decimal RuleValue { get; set; }

        public int? ProductTypeId { get; set; }

        [MaxLength(300)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        [MaxLength(50)]
        public string? GroupName { get; set; }

        // Navigation
        [ForeignKey("ProductTypeId")]
        public virtual PrintProductType? ProductType { get; set; }
    }
}
