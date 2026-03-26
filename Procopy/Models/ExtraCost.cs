using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Procopy.Models
{
    public class ExtraCost
    {
        [Key]
        public int ExtraCostId { get; set; }

        public int? ProductTypeId { get; set; }

        [Required, MaxLength(50)]
        public string CostType { get; set; } = null!;

        [Required, MaxLength(100)]
        public string CostName { get; set; } = null!;

        [Column(TypeName = "decimal(10,4)")]
        public decimal CostPerUnit { get; set; } = 0;

        [Column(TypeName = "decimal(10,2)")]
        public decimal CostPerThousand { get; set; } = 0;

        [Column(TypeName = "decimal(10,2)")]
        public decimal SetupCostTL { get; set; } = 0;

        public bool IsActive { get; set; } = true;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("ProductTypeId")]
        public virtual PrintProductType? ProductType { get; set; }
    }
}
