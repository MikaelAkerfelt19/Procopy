using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Procopy.Models
{
    public class DieCutCost
    {
        [Key]
        public int DieCutCostId { get; set; }

        public int ProductTypeId { get; set; }

        public int? SizeId { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal DieCostTL { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal CuttingPerThousand { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal GluingPerThousand { get; set; } = 0;

        public bool IsActive { get; set; } = true;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("ProductTypeId")]
        public virtual PrintProductType ProductType { get; set; } = null!;

        [ForeignKey("SizeId")]
        public virtual PrintSize? Size { get; set; }
    }
}
