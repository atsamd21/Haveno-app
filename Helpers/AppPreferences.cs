using System.Text.Json;

namespace Manta.Helpers;

public static class AppPreferences
{
    public const string PreferencesInitialized = "preferences-initialized";
    //
    public const string PreferredCurrency = "preferred-currency";
    public const string SeedBackupDone = "seed-backup-done";
    //
    public const string SelectedCurrencyCode = "selected-currency-code";
    public const string SelectedPaymentMethod = "selected-payment-method";
    public const string ShowNoDepositOffers = "show-no-deposit-offers";
    public const string IsToggled = "is-toggled";
    public const string OfferPaymentType = "offer-payment-type";
    //
    // TODO is this still needed?
    public const string CustomXmrNode = "custom-xmr-node";
    public const string InitialNoticeAcknowledged = "initial-notice-acknowledged";

    public const string RootfsVersion = "rootfs-version";

    /// <summary>
    /// Note: value types will not return a null value.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="key"></param>
    /// <returns></returns>
    public static T? Get<T>(string key)
    { 
        var value = Preferences.Get(key, null);
        if (value is null)
            return default;

        return JsonSerializer.Deserialize<T>(value);
    }

    public static void Set<T>(string key, T? value)
    {
        Preferences.Set(key, JsonSerializer.Serialize(value));
    }
}
