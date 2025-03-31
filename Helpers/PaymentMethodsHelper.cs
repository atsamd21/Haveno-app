﻿namespace Manta.Helpers;

public static class PaymentMethodsHelper
{
    public static Dictionary<string, string> PaymentMethodsDictionary  = new()
    {
        { "NATIONAL_BANK", "National bank transfer" },
        { "SAME_BANK", "Transfer with same bank" },
        { "SPECIFIC_BANKS", "Transfers with specific banks" },
        { "US_POSTAL_MONEY_ORDER", "US Postal Money Order" },
        { "CASH_DEPOSIT", "Cash Deposit" },
        { "PAY_BY_MAIL", "Pay By Mail" },
        { "CASH_AT_ATM", "Cardless Cash" },
        { "MONEY_GRAM", "MoneyGram" },
        { "WESTERN_UNION", "Western Union" },
        { "F2F", "Face to face (in person)" },
        { "JAPAN_BANK", "Japan Bank Furikomi" },
        { "AUSTRALIA_PAYID", "Australian PayID" },

        { "NATIONAL_BANK_SHORT", "National banks" },
        { "SAME_BANK_SHORT", "Same bank" },
        { "SPECIFIC_BANKS_SHORT", "Specific banks" },
        { "US_POSTAL_MONEY_ORDER_SHORT", "US Money Order" },
        { "CASH_DEPOSIT_SHORT", "Cash Deposit" },
        { "PAY_BY_MAIL_SHORT", "Pay By Mail" },
        { "CASH_AT_ATM_SHORT", "Cardless Cash" },
        { "MONEY_GRAM_SHORT", "MoneyGram" },
        { "WESTERN_UNION_SHORT", "Western Union" },
        { "F2F_SHORT", "F2F" },
        { "JAPAN_BANK_SHORT", "Japan Furikomi" },
        { "AUSTRALIA_PAYID_SHORT", "PayID" },

        { "UPHOLD", "Uphold" },
        { "MONEY_BEAM", "MoneyBeam (N26)" },
        { "POPMONEY", "Popmoney" },
        { "REVOLUT", "Revolut" },
        { "PERFECT_MONEY", "Perfect Money" },
        { "ALI_PAY", "AliPay" },
        { "WECHAT_PAY", "WeChat Pay" },
        { "SEPA", "SEPA" },
        { "SEPA_INSTANT", "SEPA Instant Payments" },
        { "FASTER_PAYMENTS", "Faster Payments" },
        { "SWISH", "Swish" },
        { "ZELLE", "Zelle" },
        { "CHASE_QUICK_PAY", "Chase QuickPay" },
        { "INTERAC_E_TRANSFER", "Interac e-Transfer" },
        { "HAL_CASH", "HalCash" },
        { "BLOCK_CHAINS", "Cryptocurrencies" },
        { "PROMPT_PAY", "PromptPay" },
        { "ADVANCED_CASH", "Advanced Cash" },
        { "TRANSFERWISE", "Wise" },
        { "TRANSFERWISE_USD", "Wise-USD" },
        { "PAYSERA", "Paysera" },
        { "PAXUM", "Paxum" },
        { "NEFT", "India/NEFT" },
        { "RTGS", "India/RTGS" },
        { "IMPS", "India/IMPS" },
        { "UPI", "India/UPI" },
        { "PAYTM", "India/PayTM" },
        { "NEQUI", "Nequi" },
        { "BIZUM", "Bizum" },
        { "PIX", "Pix" },
        { "AMAZON_GIFT_CARD", "Amazon eGift Card" },
        { "BLOCK_CHAINS_INSTANT", "Cryptocurrencies Instant" },
        { "CAPITUAL", "Capitual" },
        { "CELPAY", "CelPay" },
        { "MONESE", "Monese" },
        { "SATISPAY", "Satispay" },
        { "TIKKIE", "Tikkie" },
        { "VERSE", "Verse" },
        { "STRIKE", "Strike" },
        { "SWIFT", "SWIFT International Wire Transfer" },
        { "ACH_TRANSFER", "ACH Transfer" },
        { "DOMESTIC_WIRE_TRANSFER", "Domestic Wire Transfer" },
        { "BSQ_SWAP", "BSQ Swap" },

        { "OK_PAY", "OKPay" },
        { "CASH_APP", "Cash App" },
        { "VENMO", "Venmo" },
        { "PAYPAL", "PayPal" },
        { "PAYSAFE", "Paysafe" },

        { "UPHOLD_SHORT", "Uphold" },
        { "MONEY_BEAM_SHORT", "MoneyBeam (N26)" },
        { "POPMONEY_SHORT", "Popmoney" },
        { "REVOLUT_SHORT", "Revolut" },
        { "PERFECT_MONEY_SHORT", "Perfect Money" },
        { "ALI_PAY_SHORT", "AliPay" },
        { "WECHAT_PAY_SHORT", "WeChat Pay" },
        { "SEPA_SHORT", "SEPA" },
        { "SEPA_INSTANT_SHORT", "SEPA Instant" },
        { "FASTER_PAYMENTS_SHORT", "Faster Payments" },
        { "SWISH_SHORT", "Swish" },
        { "ZELLE_SHORT", "Zelle" },
        { "CHASE_QUICK_PAY_SHORT", "Chase QuickPay" },
        { "INTERAC_E_TRANSFER_SHORT", "Interac e-Transfer" },
        { "HAL_CASH_SHORT", "HalCash" },
        { "BLOCK_CHAINS_SHORT", "Cryptocurrencies" },
        { "PROMPT_PAY_SHORT", "PromptPay" },
        { "ADVANCED_CASH_SHORT", "Advanced Cash" },
        { "TRANSFERWISE_SHORT", "Wise" },
        { "TRANSFERWISE_USD_SHORT", "Wise-USD" },
        { "PAYSERA_SHORT", "Paysera" },
        { "PAXUM_SHORT", "Paxum" },
        { "NEFT_SHORT", "NEFT" },
        { "RTGS_SHORT", "RTGS" },
        { "IMPS_SHORT", "IMPS" },
        { "UPI_SHORT", "UPI" },
        { "PAYTM_SHORT", "PayTM" },
        { "NEQUI_SHORT", "Nequi" },
        { "BIZUM_SHORT", "Bizum" },
        { "PIX_SHORT", "Pix" },
        { "AMAZON_GIFT_CARD_SHORT", "Amazon eGift Card" },
        { "BLOCK_CHAINS_INSTANT_SHORT", "Cryptocurrencies Instant" },
        { "CAPITUAL_SHORT", "Capitual" },
        { "CELPAY_SHORT", "CelPay" },
        { "MONESE_SHORT", "Monese" },
        { "SATISPAY_SHORT", "Satispay" },
        { "TIKKIE_SHORT", "Tikkie" },
        { "VERSE_SHORT", "Verse" },
        { "STRIKE_SHORT", "Strike" },
        { "SWIFT_SHORT", "SWIFT" },
        { "ACH_TRANSFER_SHORT", "ACH" },
        { "DOMESTIC_WIRE_TRANSFER_SHORT", "Domestic Wire" },
        { "BSQ_SWAP_SHORT", "BSQ Swap" },

        { "OK_PAY_SHORT", "OKPay" },
        { "CASH_APP_SHORT", "Cash App" },
        { "VENMO_SHORT", "Venmo" },
        { "PAYPAL_SHORT", "PayPal" },
        { "PAYSAFE_SHORT", "Paysafe" }
    };
}
