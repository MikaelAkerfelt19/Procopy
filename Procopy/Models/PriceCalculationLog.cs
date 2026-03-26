using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Procopy.Models
{
    public class PriceCalculationLog
    {
        [Key]
        public long LogId { get; set; }

        public int ProductTypeId { get; set; }

        [MaxLength(100)]
        public string? SessionId { get; set; }

        [MaxLength(50)]
        public string? IpAddress { get; set; }

        [Required]
        public string InputJson { get; set; } = null!;

        [Required]
        public string ResultJson { get; set; } = null!;

        [Column(TypeName = "decimal(12,2)")]
        public decimal TotalPrice { get; set; }

        [Column(TypeName = "decimal(10,4)")]
        public decimal UnitPrice { get; set; }

        public int Quantity { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        [ForeignKey("ProductTypeId")]
        public virtual PrintProductType ProductType { get; set; } = null!;
    }
}
