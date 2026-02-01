using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Procopy.Models
{
    public class ProductOptionValue
    {
        [Key]
        public int ValueId { get; set; }

        public int OptionId { get; set; }
        [ForeignKey("OptionId")]
        public virtual ProductOption Option { get; set; }

        public string Name { get; set; } = null!; // Örn: "A4", "Parlak Selefon"

        [Column(TypeName = "decimal(18,2)")]
        public decimal PriceAdjustment { get; set; } = 0; // Fiyata eklenecek tutar (+50 TL)

        public bool IsDefault { get; set; } = false; // Varsayılan seçili mi gelsin?
    }
}