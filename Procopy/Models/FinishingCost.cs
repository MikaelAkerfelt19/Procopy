using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Procopy.Models
{
    public class FinishingCost
    {
        [Key]
        public int FinishingCostId { get; set; }

        [Required, MaxLength(50)]
        public string FinishingType { get; set; } = null!;

        [Required, MaxLength(100)]
        public string FinishingName { get; set; } = null!;

        [Column(TypeName = "decimal(10,2)")]
        public decimal SetupCostTL { get; set; }

        [Column(TypeName = "decimal(10,4)")]
        public decimal CostPerM2 { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
