using System.ComponentModel.DataAnnotations;

namespace Procopy.Models
{
    public class ImportLog
    {
        [Key]
        public long LogId { get; set; }

        public int RunId { get; set; }

        public DateTime At { get; set; }

        [MaxLength(24)]
        public string Level { get; set; }

        [MaxLength(1024)]
        public string Url { get; set; }

        [MaxLength(256)]
        public string Title { get; set; }

        public string Message { get; set; }
    }
}