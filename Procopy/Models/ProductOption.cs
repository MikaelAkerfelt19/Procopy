using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Procopy.Models
{
    public class ProductOption
    {
        [Key]
        public int OptionId { get; set; }

        public int ProductId { get; set; }
        [ForeignKey("ProductId")]
        public virtual Product Product { get; set; }

        public string Name { get; set; } = null!; // Örn: "Ebat", "Kağıt Cinsi"

        public bool IsDropdown { get; set; } = false; // True ise SelectBox, False ise RadioButton

        public int DisplayOrder { get; set; } = 0; // Sıralama

        public virtual ICollection<ProductOptionValue> Values { get; set; } = new List<ProductOptionValue>();
    }
}