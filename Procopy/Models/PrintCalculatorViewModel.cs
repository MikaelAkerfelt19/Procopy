namespace Procopy.Models
{
    public class PrintCalculatorViewModel
    {
        public PrintProductType ProductType { get; set; } = null!;
        public List<PrintParameter> Parameters { get; set; } = new();
        public List<PrintSize> Sizes { get; set; } = new();
        public List<PrintQuantity> Quantities { get; set; } = new();
    }
}
