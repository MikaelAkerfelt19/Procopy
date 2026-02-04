using System.ComponentModel.DataAnnotations;

namespace Procopy.Models
{
    public class ImportRule
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [StringLength(100)]
        public string Domain { get; set; } = null!;

        // --- LİSTE SAYFASI AYARLARI ---

        // Ürün linklerini bulmak için XPath. Örn: //div[@class='product-item']//a
        public string? LinkXPath { get; set; }

        // --- DETAY SAYFASI AYARLARI ---

        // Ürün başlığı (H1) için XPath
        public string? TitleXPath { get; set; }

        // Fiyat alanı için XPath
        public string? PriceXPath { get; set; }

        // Ürün kodu (SKU) için XPath
        public string? StockCodeXPath { get; set; }

        // Ana resim için XPath
        public string? ImageXPath { get; set; }

        // Açıklama metni için XPath
        public string? DescriptionXPath { get; set; }
    }
}