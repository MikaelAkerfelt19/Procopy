using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Procopy.Models
{
    public class LaminationPrice
    {
        [Key]
        public int LaminationPriceId { get; set; }

        [Required, MaxLength(50)]
        public string LaminationType { get; set; } = null!;

        [Required, MaxLength(100)]
        public string LaminationName { get; set; } = null!;

        [MaxLength(100)]
        public string? SubType { get; set; }

        [Column(TypeName = "decimal(10,4)")]
        public decimal PricePerM2 { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal MinChargeTL { get; set; } = 0;

        public bool IsActive { get; set; } = true;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
