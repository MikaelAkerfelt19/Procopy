using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Procopy.Models
{
    public class PrintingCost
    {
        [Key]
        public int PrintingCostId { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = null!;

        [Required, MaxLength(50)]
        public string PrintType { get; set; } = null!;

        public int ColorCount { get; set; }
        public int SideCount { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal SetupCostTL { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal CostPerThousand { get; set; }

        public int MinQuantity { get; set; } = 500;
        public bool IsActive { get; set; } = true;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
