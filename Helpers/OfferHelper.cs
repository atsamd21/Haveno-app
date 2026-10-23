using HavenoSharp.Models;

namespace Manta.Helpers;

public static class OfferHelper
{
    public static double GetTakerDepositPercent(this OfferInfo offerInfo)
    {
        double securityDepositPercent;

        if (offerInfo.Direction == "BUY")
            securityDepositPercent = offerInfo.SellerSecurityDepositPct;
        else
            securityDepositPercent = offerInfo.BuyerSecurityDepositPct;

        ulong depositAmount = (ulong)(offerInfo.Amount * securityDepositPercent);
        if (depositAmount < 100_000_000_000)
        {
            securityDepositPercent = 100_000_000_000 / (double)offerInfo.Amount;
        }

        return securityDepositPercent;
    }

    public static ulong GetTakerDepositAmount(this OfferInfo offerInfo)
    {
        return (ulong)(offerInfo.Amount * (offerInfo.GetTakerDepositPercent()));
    }

    public static bool IsFiat(this OfferInfo offerInfo)
    {
        return offerInfo.PaymentMethodId != "BLOCK_CHAINS" && offerInfo.PaymentMethodId != "BLOCK_CHAINS_INSTANT";
    }
}
