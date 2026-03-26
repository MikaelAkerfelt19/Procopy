using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Procopy.Models
{
    public class PrintParameter
    {
        [Key]
        public int ParameterId { get; set; }

        public int ProductTypeId { get; set; }

        [Required, MaxLength(100)]
        public string Name { get; set; } = null!;

        [Required, MaxLength(50)]
        public string FieldKey { get; set; } = null!;

        /// <summary>select, radio, number, checkbox</summary>
        [Required, MaxLength(20)]
        public string FieldType { get; set; } = "select";

        public bool IsRequired { get; set; } = true;
        public int DisplayOrder { get; set; } = 0;

        public int? DependsOnParameterId { get; set; }

        [MaxLength(300)]
        public string? HelpText { get; set; }

        public bool IsActive { get; set; } = true;

        // Navigation
        [ForeignKey("ProductTypeId")]
        public virtual PrintProductType ProductType { get; set; } = null!;

        [ForeignKey("DependsOnParameterId")]
        public virtual PrintParameter? DependsOn { get; set; }

        public virtual ICollection<PrintParameterValue> Values { get; set; } = new List<PrintParameterValue>();
    }
}
