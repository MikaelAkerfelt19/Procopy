using System;
using System.Collections.Generic;

namespace Procopy.Models;

public partial class ProductStat
{
    public long StatId { get; set; }

    public int ProductId { get; set; }

    public byte InteractionType { get; set; }

    public string? Ipaddress { get; set; }

    public DateTime? InteractionDate { get; set; }

    public virtual Product Product { get; set; } = null!;
}
