using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Procopy.Models
{
    // Bu sınıf, Ürünler ile Kategoriler arasındaki köprüyü kurar.
    public class ProductCategory
    {
        public int ProductId { get; set; }
        public Product Product { get; set; }

        public int CategoryId { get; set; }
        public Category Category { get; set; }
   

        // Bu kategori, ürünün "Ana Kategorisi" mi? (Linklerde ve kırıntı yapısında hangisi görünsün?)
        public bool IsMainCategory { get; set; }
    }
}