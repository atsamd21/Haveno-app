using System.Globalization;

namespace Manta.Helpers;

public enum Currency
{
    AED,
    AFN,
    ALL,
    AMD,
    ANG,
    AOA,
    ARS,
    AUD,
    AWG,
    AZN,
    BAM,
    BBD,
    BDT,
    BGN,
    BHD,
    BIF,
    BMD,
    BND,
    BOB,
    BRL,
    BSD,
    BTN,
    BWP,
    BYN,
    BZD,
    CAD,
    CDF,
    CHF,
    CLP,
    CNY,
    COP,
    CRC,
    CUC,
    CUP,
    CVE,
    CZK,
    DJF,
    DKK,
    DOP,
    DZD,
    EGP,
    ERN,
    ETB,
    EUR,
    FJD,
    FKP,
    GBP,
    GEL,
    GGP,
    GHS,
    GIP,
    GMD,
    GNF,
    GTQ,
    GYD,
    HKD,
    HNL,
    HRK,
    HTG,
    HUF,
    IDR,
    ILS,
    IMP,
    INR,
    IQD,
    IRR,
    ISK,
    JEP,
    JMD,
    JOD,
    JPY,
    KES,
    KGS,
    KHR,
    KMF,
    KPW,
    KRW,
    KWD,
    KYD,
    KZT,
    LAK,
    LBP,
    LKR,
    LRD,
    LSL,
    LYD,
    MAD,
    MDL,
    MGA,
    MKD,
    MMK,
    MNT,
    MOP,
    MRU,
    MUR,
    MVR,
    MWK,
    MXN,
    MYR,
    MZN,
    NAD,
    NGN,
    NIO,
    NOK,
    NPR,
    NZD,
    OMR,
    PAB,
    PEN,
    PGK,
    PHP,
    PKR,
    PLN,
    PYG,
    QAR,
    RON,
    RSD,
    RUB,
    RWF,
    SAR,
    SBD,
    SCR,
    SDG,
    SEK,
    SGD,
    SHP,
    SLL,
    SOS,
    SRD,
    SSP,
    STN,
    SVC,
    SYP,
    SZL,
    THB,
    TJS,
    TMT,
    TND,
    TOP,
    TRY,
    TTD,
    TWD,
    TZS,
    UAH,
    UGX,
    USD,
    UYU,
    UZS,
    VES,
    VND,
    VUV,
    WST,
    XCD,
    XOF,
    XPF,
    YER,
    ZAR,
    ZMW,
    ZWL
}

public enum CryptoCurrency
{
    BTC,
    BCH,
    ETH,
    LTC,
    DAI_ERC20,
    USDT_ERC20,
    USDT_TRC20,
    USDC_ERC20
}

public static class CryptoCurrencyHelper
{
    public static Dictionary<string, string> CryptoCurrenciesDictionary = new()
    {
        { "BTC", "Bitcoin" },
        { "BCH", "Bitcoin Cash" },
        { "ETH", "Ether" },
        { "LTC", "Litecoin" },
        { "DAI-ERC20", "Dai Stablecoin (ERC20)" },
        { "USDT-ERC20", "Tether USD (ERC20)" },
        { "USDT-TRC20", "Tether USD (TRC20)" },
        { "USDC-ERC20", "USD Coin (ERC20)" },
        { "ADA", "Cardano" },
        { "DOGE", "Dogecoin" },
        { "XRP", "Ripple" },
        { "SOL", "Solana" },
        { "TRX", "Tron" }
    };
}

public static class CurrencyCultureInfo
{
    public static string FallbackCurrency = "USD";

    public static CultureInfo? GetCultureInfoForCurrency(Currency currency)
    {
        return currency switch
        {
            Currency.AED => new CultureInfo("ar-AE"),
            Currency.AFN => new CultureInfo("ps-AF"),
            Currency.ALL => new CultureInfo("sq-AL"),
            Currency.AMD => new CultureInfo("hy-AM"),
            Currency.ANG => new CultureInfo("nl-SX"),
            Currency.AOA => new CultureInfo("pt-AO"),
            Currency.ARS => new CultureInfo("es-AR"),
            Currency.AUD => new CultureInfo("en-AU"),
            Currency.AWG => new CultureInfo("nl-AW"),
            Currency.AZN => new CultureInfo("az-Latn-AZ"),
            Currency.BAM => new CultureInfo("bs-Latn-BA"),
            Currency.BBD => new CultureInfo("en-BB"),
            Currency.BDT => new CultureInfo("bn-BD"),
            Currency.BGN => new CultureInfo("bg-BG"),
            Currency.BHD => new CultureInfo("ar-BH"),
            Currency.BIF => new CultureInfo("rn-BI"),
            Currency.BMD => new CultureInfo("en-BM"),
            Currency.BND => new CultureInfo("ms-BN"),
            Currency.BOB => new CultureInfo("es-BO"),
            Currency.BRL => new CultureInfo("pt-BR"),
            Currency.BSD => new CultureInfo("en-BS"),
            Currency.BTN => new CultureInfo("dz-BT"),
            Currency.BWP => new CultureInfo("en-BW"),
            Currency.BYN => new CultureInfo("be-BY"),
            Currency.BZD => new CultureInfo("en-BZ"),
            Currency.CAD => new CultureInfo("en-CA"),
            Currency.CDF => new CultureInfo("fr-CD"),
            Currency.CHF => new CultureInfo("fr-CH"),
            Currency.CLP => new CultureInfo("es-CL"),
            Currency.CNY => new CultureInfo("zh-CN"),
            Currency.COP => new CultureInfo("es-CO"),
            Currency.CRC => new CultureInfo("es-CR"),
            Currency.CUC => new CultureInfo("es-CU"),
            Currency.CUP => new CultureInfo("es-CU"),
            Currency.CVE => new CultureInfo("pt-CV"),
            Currency.CZK => new CultureInfo("cs-CZ"),
            Currency.DJF => new CultureInfo("fr-DJ"),
            Currency.DKK => new CultureInfo("da-DK"),
            Currency.DOP => new CultureInfo("es-DO"),
            Currency.DZD => new CultureInfo("ar-DZ"),
            Currency.EGP => new CultureInfo("ar-EG"),
            Currency.ERN => new CultureInfo("ti-ER"),
            Currency.ETB => new CultureInfo("am-ET"),
            Currency.EUR => new CultureInfo("fr-FR"), 
            Currency.FJD => new CultureInfo("en-FJ"),
            Currency.FKP => new CultureInfo("en-FK"),
            Currency.GBP => new CultureInfo("en-GB"),
            Currency.GEL => new CultureInfo("ka-GE"),
            Currency.GGP => new CultureInfo("en-GG"),
            Currency.GHS => new CultureInfo("ak-GH"),
            Currency.GIP => new CultureInfo("en-GI"),
            Currency.GMD => new CultureInfo("en-GM"),
            Currency.GNF => new CultureInfo("fr-GN"),
            Currency.GTQ => new CultureInfo("es-GT"),
            Currency.GYD => new CultureInfo("en-GY"),
            Currency.HKD => new CultureInfo("zh-HK"),
            Currency.HNL => new CultureInfo("es-HN"),
            Currency.HRK => new CultureInfo("hr-HR"),
            Currency.HTG => new CultureInfo("fr-HT"),
            Currency.HUF => new CultureInfo("hu-HU"),
            Currency.IDR => new CultureInfo("id-ID"),
            Currency.ILS => new CultureInfo("he-IL"),
            Currency.IMP => new CultureInfo("en-IM"),
            Currency.INR => new CultureInfo("hi-IN"),
            Currency.IQD => new CultureInfo("ar-IQ"),
            Currency.IRR => new CultureInfo("fa-IR"),
            Currency.ISK => new CultureInfo("is-IS"),
            Currency.JEP => new CultureInfo("en-JE"),
            Currency.JMD => new CultureInfo("en-JM"),
            Currency.JOD => new CultureInfo("ar-JO"),
            Currency.JPY => new CultureInfo("ja-JP"),
            Currency.KES => new CultureInfo("sw-KE"),
            Currency.KGS => new CultureInfo("ky-KG"),
            Currency.KHR => new CultureInfo("km-KH"),
            Currency.KMF => new CultureInfo("ar-KM"),
            Currency.KPW => new CultureInfo("ko-KP"),
            Currency.KRW => new CultureInfo("ko-KR"),
            Currency.KWD => new CultureInfo("ar-KW"),
            Currency.KYD => new CultureInfo("en-KY"),
            Currency.KZT => new CultureInfo("kk-KZ"),
            Currency.LAK => new CultureInfo("lo-LA"),
            Currency.LBP => new CultureInfo("ar-LB"),
            Currency.LKR => new CultureInfo("si-LK"),
            Currency.LRD => new CultureInfo("en-LR"),
            Currency.LSL => new CultureInfo("en-LS"),
            Currency.LYD => new CultureInfo("ar-LY"),
            Currency.MAD => new CultureInfo("ar-MA"),
            Currency.MDL => new CultureInfo("ro-MD"),
            Currency.MGA => new CultureInfo("mg-MG"),
            Currency.MKD => new CultureInfo("mk-MK"),
            Currency.MMK => new CultureInfo("my-MM"),
            Currency.MNT => new CultureInfo("mn-MN"),
            Currency.MOP => new CultureInfo("zh-MO"),
            Currency.MRU => new CultureInfo("ar-MR"),
            Currency.MUR => new CultureInfo("en-MU"),
            Currency.MVR => new CultureInfo("dv-MV"),
            Currency.MWK => new CultureInfo("ny-MW"),
            Currency.MXN => new CultureInfo("es-MX"),
            Currency.MYR => new CultureInfo("ms-MY"),
            Currency.MZN => new CultureInfo("pt-MZ"),
            Currency.NAD => new CultureInfo("en-NA"),
            Currency.NGN => new CultureInfo("en-NG"),
            Currency.NIO => new CultureInfo("es-NI"),
            Currency.NOK => new CultureInfo("nb-NO"),
            Currency.NPR => new CultureInfo("ne-NP"),
            Currency.NZD => new CultureInfo("en-NZ"),
            Currency.OMR => new CultureInfo("ar-OM"),
            Currency.PAB => new CultureInfo("es-PA"),
            Currency.PEN => new CultureInfo("es-PE"),
            Currency.PGK => new CultureInfo("en-PG"),
            Currency.PHP => new CultureInfo("en-PH"),
            Currency.PKR => new CultureInfo("ur-PK"),
            Currency.PLN => new CultureInfo("pl-PL"),
            Currency.PYG => new CultureInfo("es-PY"),
            Currency.QAR => new CultureInfo("ar-QA"),
            Currency.RON => new CultureInfo("ro-RO"),
            Currency.RSD => new CultureInfo("sr-Latn-RS"),
            Currency.RUB => new CultureInfo("ru-RU"),
            Currency.RWF => new CultureInfo("rw-RW"),
            Currency.SAR => new CultureInfo("ar-SA"),
            Currency.SBD => new CultureInfo("en-SB"),
            Currency.SCR => new CultureInfo("fr-SC"),
            Currency.SDG => new CultureInfo("ar-SD"),
            Currency.SEK => new CultureInfo("sv-SE"),
            Currency.SGD => new CultureInfo("zh-SG"),
            Currency.SHP => new CultureInfo("en-SH"),
            Currency.SLL => new CultureInfo("en-SL"),
            Currency.SOS => new CultureInfo("so-SO"),
            Currency.SRD => new CultureInfo("nl-SR"),
            Currency.SSP => new CultureInfo("en-SS"),
            Currency.STN => new CultureInfo("pt-ST"),
            Currency.SVC => new CultureInfo("es-SV"),
            Currency.SYP => new CultureInfo("ar-SY"),
            Currency.SZL => new CultureInfo("en-SZ"),
            Currency.THB => new CultureInfo("th-TH"),
            Currency.TJS => new CultureInfo("tg-Cyrl-TJ"),
            Currency.TMT => new CultureInfo("tk-TM"),
            Currency.TND => new CultureInfo("ar-TN"),
            Currency.TOP => new CultureInfo("to-TO"),
            Currency.TRY => new CultureInfo("tr-TR"),
            Currency.TTD => new CultureInfo("en-TT"),
            Currency.TWD => new CultureInfo("zh-TW"),
            Currency.TZS => new CultureInfo("sw-TZ"),
            Currency.UAH => new CultureInfo("uk-UA"),
            Currency.UGX => new CultureInfo("sw-UG"),
            Currency.USD => new CultureInfo("en-US"),
            Currency.UYU => new CultureInfo("es-UY"),
            Currency.UZS => new CultureInfo("uz-Latn-UZ"),
            Currency.VES => new CultureInfo("es-VE"),
            Currency.VND => new CultureInfo("vi-VN"),
            Currency.VUV => new CultureInfo("bi-VU"),
            Currency.WST => new CultureInfo("sm-WS"),
            Currency.XCD => new CultureInfo("en-AG"), 
            Currency.XOF => new CultureInfo("fr-SN"), 
            Currency.XPF => new CultureInfo("fr-PF"), 
            Currency.YER => new CultureInfo("ar-YE"),
            Currency.ZAR => new CultureInfo("en-ZA"),
            Currency.ZMW => new CultureInfo("en-ZM"),
            Currency.ZWL => new CultureInfo("en-ZW"),
            //_ => throw new ArgumentException("Currency is not valid", nameof(currency))
            _ => null
        };
    }

    public static NumberFormatInfo? GetFormatForCurrency(Currency currency)
    {
        return GetCultureInfoForCurrency(currency)?.NumberFormat ?? null;
    }

    public static string? GetCurrencyFullName(Currency currency)
    {
        var cultureInfo = GetCultureInfoForCurrency(currency);
        if (cultureInfo is null)
            return null;

        RegionInfo region = new(cultureInfo.Name);
        return region.CurrencyEnglishName + $" ({currency})";
    }

    public static IEnumerable<string> GetCurrencyFullNames()
    {
        foreach (var currency in Enum.GetValues(typeof(Currency)))
        {
            var cultureInfo = GetCultureInfoForCurrency((Currency)currency);
            if (cultureInfo is null)
            {
                yield return $"{currency}";
            }
            else
            {
                RegionInfo region = new(cultureInfo.Name);
                yield return region.CurrencyEnglishName + $" ({currency})";
            }
        }
    }

    public static IEnumerable<KeyValuePair<string, string>> GetCurrencyFullNamesAndCurrencyCodeDictionary()
    {
        foreach (var currency in Enum.GetValues(typeof(Currency)))
        {
            var cultureInfo = GetCultureInfoForCurrency((Currency)currency);
            if (cultureInfo is null)
            {
                yield return new KeyValuePair<string, string>(currency.ToString()!, $"{currency}");
            }
            else
            {
                RegionInfo region = new(cultureInfo.Name);
                yield return new KeyValuePair<string, string>(currency.ToString()!, region.CurrencyEnglishName + $" ({currency})");
            }
        }
    }
}
