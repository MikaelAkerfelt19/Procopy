using System;
using System.ComponentModel.DataAnnotations;

namespace Procopy.Models
{
    public class ContactMessage
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Ad Soyad zorunludur.")]
        [StringLength(100)]
        public string Name { get; set; }

        [Required(ErrorMessage = "E-posta zorunludur.")]
        [EmailAddress]
        public string Email { get; set; }

        public string? Phone { get; set; }

        public string? Subject { get; set; }

        [Required]
        public string Message { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string? IpAddress { get; set; }
    }
}