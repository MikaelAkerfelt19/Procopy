namespace Procopy.Helpers;

/// <summary>
/// Sitenin her yerinde geçen iletişim bilgilerini tek noktadan okur.
/// Değerler appsettings.json > SiteSettings altında tutulur; okunamazsa
/// aşağıdaki varsayılanlara düşer, böylece linkler hiçbir zaman boş kalmaz.
/// </summary>
public static class SiteSettingsExtensions
{
    public const string DefaultWhatsAppPhone = "905537350038";
    public const string DefaultPhoneDisplay = "+90 553 735 00 38";
    public const string DefaultContactName = "Serdar Demir";

    /// <summary>wa.me linklerinde kullanılan, ülke kodlu ve sadece rakamlardan oluşan numara.</summary>
    public static string WhatsAppPhone(this IConfiguration config)
        => Fallback(config["SiteSettings:WhatsAppPhone"], DefaultWhatsAppPhone);

    /// <summary>Ekranda gösterilen, boşluklu okunabilir numara.</summary>
    public static string PhoneDisplay(this IConfiguration config)
        => Fallback(config["SiteSettings:PhoneDisplay"], DefaultPhoneDisplay);

    /// <summary>İletişimde görünen yetkili adı.</summary>
    public static string ContactName(this IConfiguration config)
        => Fallback(config["SiteSettings:ContactName"], DefaultContactName);

    private static string Fallback(string? value, string standIn)
        => string.IsNullOrWhiteSpace(value) ? standIn : value;
}
