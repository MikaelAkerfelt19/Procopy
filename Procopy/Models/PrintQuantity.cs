using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Procopy.Models
{
    public class PrintQuantity
    {
        [Key]
        public int QuantityId { get; set; }

        public int ProductTypeId { get; set; }

        public int Quantity { get; set; }

        public int DisplayOrder { get; set; } = 0;
        public bool IsActive { get; set; } = true;

        // Navigation
        [ForeignKey("ProductTypeId")]
        public virtual PrintProductType ProductType { get; set; } = null!;
    }
}
