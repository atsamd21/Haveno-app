#if ANDROID
using Android.Content;
using System.Text.Json;
#endif

namespace Manta.Helpers;

public static class Preferences
{
    public const string SeedBackupDone = "seed-backup-done";
    //
    public const string SelectedCurrencyCode = "selected-currency-code";
    public const string SelectedPaymentMethod = "selected-payment-method";
    public const string ShowNoDepositOffers = "show-no-deposit-offers";
    public const string IsToggled = "is-toggled";
    public const string OfferPaymentType = "offer-payment-type";
    public const string CustomXmrNode = "custom-xmr-node";
    public const string InitialNoticeAcknowledged = "initial-notice-acknowledged";

#if ANDROID
    public static void Set<T>(string key, T? data)
    {
        var sharedPreferences = Android.App.Application.Context.GetSharedPreferences("data", FileCreationMode.Private);
        var editor = sharedPreferences?.Edit();

        editor?.PutString(key, JsonSerializer.Serialize(data));
        editor?.Apply();
    }

    public static T? Get<T>(string key)
    {
        try
        {
            var str = Android.App.Application.Context.GetSharedPreferences("data", FileCreationMode.Private)?.GetString(key, null);
            if (str is null)
                return default;

            return JsonSerializer.Deserialize<T>(str);
        }
        catch
        {
            return default;
        }
    }
#else
    public static void Set<T>(string key, T data)
    {
        throw new NotImplementedException();
    }

    public static T? Get<T>(string key)
    {
        throw new NotImplementedException();
    }
#endif
}
