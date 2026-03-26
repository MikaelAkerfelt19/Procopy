using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Procopy.Models
{
    public class PaperPrice
    {
        [Key]
        public int PaperPriceId { get; set; }

        [Required, MaxLength(50)]
        public string PaperType { get; set; } = null!;

        [Required, MaxLength(100)]
        public string PaperName { get; set; } = null!;

        public int GramPerM2 { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal PricePerKg { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
