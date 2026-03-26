using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Procopy.Models
{
    public class PrintParameterValue
    {
        [Key]
        public int ValueId { get; set; }

        public int ParameterId { get; set; }

        public int? ParentValueId { get; set; }

        [Required, MaxLength(100)]
        public string Label { get; set; } = null!;

        [Required, MaxLength(50)]
        public string Value { get; set; } = null!;

        public int DisplayOrder { get; set; } = 0;
        public bool IsDefault { get; set; } = false;
        public bool IsActive { get; set; } = true;

        // Navigation
        [ForeignKey("ParameterId")]
        public virtual PrintParameter Parameter { get; set; } = null!;

        [ForeignKey("ParentValueId")]
        public virtual PrintParameterValue? Parent { get; set; }

        public virtual ICollection<PrintParameterValue> Children { get; set; } = new List<PrintParameterValue>();
    }
}
