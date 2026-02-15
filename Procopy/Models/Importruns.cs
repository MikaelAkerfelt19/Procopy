using System.ComponentModel.DataAnnotations;

namespace Procopy.Models
{
    public class ImportRun
    {
        [Key]
        public int RunId { get; set; }

        public int MainCategoryId { get; set; }

        [MaxLength(512)]
        public string StartUrl { get; set; }

        public DateTime StartAt { get; set; }
        public DateTime? EndAt { get; set; }

        [MaxLength(32)]
        public string Status { get; set; }

        public int PagesVisited { get; set; }
        public int ProductsAdded { get; set; }
        public int ProductsSkipped { get; set; }
        public int Errors { get; set; }
    }
}