using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Procopy.Models
{
    public class PrintSize
    {
        [Key]
        public int SizeId { get; set; }

        public int ProductTypeId { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = null!;

        [Column(TypeName = "decimal(8,2)")]
        public decimal? WidthMM { get; set; }

        [Column(TypeName = "decimal(8,2)")]
        public decimal? HeightMM { get; set; }

        [Column(TypeName = "decimal(8,2)")]
        public decimal? DepthMM { get; set; }

        [Column(TypeName = "decimal(10,6)")]
        public decimal? SheetAreaM2 { get; set; }

        [MaxLength(300)]
        public string? ImageUrl { get; set; }

        public int DisplayOrder { get; set; } = 0;
        public bool IsActive { get; set; } = true;

        // Navigation
        [ForeignKey("ProductTypeId")]
        public virtual PrintProductType ProductType { get; set; } = null!;
    }
}
