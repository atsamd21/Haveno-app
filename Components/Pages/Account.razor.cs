using Grpc.Core;
using HavenoSharp.Extensions;
using HavenoSharp.Models;
using HavenoSharp.Models.Requests;
using HavenoSharp.Services;
using Manta.Components.Reusable;
using Manta.Helpers;
using Manta.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace Manta.Components.Pages;

public partial class Account : ComponentBase
{
    [Inject]
    public IHavenoPaymentAccountService PaymentAccountService { get; set; } = default!;

    private ValidationMessageStore? _messageStore;
    private EditContext? _editContext;

    public bool SubmitButtonDisabled { get; set; } = true;

    public PaymentAccountForm? PaymentAccountForm { get; set; }
    public CreateCryptoCurrencyPaymentAccountRequest? CreateCryptoCurrencyPaymentAccountRequest { get; set; }
    public PaymentAccount? SelectedPaymentAccount { get; set; }

    public Dictionary<string, string> TraditionalPaymentMethodStrings { get; set; } = [];
    public List<SelectedCurrency> SupportedCurrencyCodes { get; set; } = [];
    public List<SelectedAcceptedCountry> AcceptedNonEUSEPACountries { get; set; } = [];
    public List<SelectedAcceptedCountry> AcceptedEUSEPACountries { get; set; } = [];
    public List<PaymentAccount> PaymentAccounts { get; set; } = [];
    public List<PaymentAccount> CryptoPaymentAccounts { get; set; } = [];
    public List<PaymentMethod> TraditionalPaymentMethods { get; set; } = [];
    public List<PaymentMethod> CryptoPaymentMethods { get; set; } = [];

    public long MaxTradePeriod { get; set; }
    public long MaxTradeLimit { get; set; }

    public SearchableDropdown PaymentMethodSearchableDropdown { get; set; } = default!;

    public bool CustomAccountNameEnabled { get; set; }
    public bool UseIntermediaryBankEnabled { get; set; }
    public bool ReceivingBankCollapsed { get; set; }
    public bool IntermediaryBankCollapsed { get; set; } = true;

    [Parameter]
    [SupplyParameterFromQuery]
    public string? AccountToCreate { get; set; }

    public string? AccountToDelete { get; set; }
    public bool ShowDeleteAccountModal { get; set; }

    public PaymentAccountFormField? AccountNameField { get; set; }
    public PaymentAccountFormField? CopyFromField { get; set; }

    public string SelectedPaymentMethodId 
    { 
        get; 
        set 
        { 
            field = value;
            SelectedPaymentAccount = null;
            CustomAccountNameEnabled = false;

            if (string.IsNullOrEmpty(value))
            {
                SupportedCurrencyCodes = [];
                AcceptedNonEUSEPACountries = [];
                AcceptedEUSEPACountries = [];
                PaymentAccountForm = default!;
                return;
            }

            if (field == "BLOCK_CHAINS")
            {
                CreateCryptoCurrencyPaymentAccountRequest = new();
            }
            else
            {
                CreateCryptoCurrencyPaymentAccountRequest = null;
            }

            var paymentMethod = TraditionalPaymentMethods
                .FirstOrDefault(x => x.Id == SelectedPaymentMethodId);

            if (paymentMethod is null)
                throw new Exception("paymentMethod was null");

            MaxTradePeriod = paymentMethod.MaxTradePeriod;
            MaxTradeLimit = paymentMethod.MaxTradeLimit;

            SupportedCurrencyCodes = paymentMethod
                .SupportedAssetCodes.Select(x => new SelectedCurrency { Code = x, IsSelected = false })
                .ToList();

            // TODO cache this and clear when daemon version changes
            PaymentAccountForm = Task.Run(() => PaymentAccountService.GetPaymentAccountFormAsync(SelectedPaymentMethodId)).GetAwaiter().GetResult();

            AccountNameField = PaymentAccountForm.Fields.FirstOrDefault(x => x.Id == FieldId.ACCOUNT_NAME);

            var fieldId = GetPaymentAccountName(SelectedPaymentMethodId);
            if (fieldId is not null)
            {
                CopyFromField = PaymentAccountForm.Fields.FirstOrDefault(x => x.Id == fieldId);
            }

            var acceptedCountriesField = PaymentAccountForm.Fields.FirstOrDefault(x => x.Id == FieldId.ACCEPTED_COUNTRY_CODES);
            if (acceptedCountriesField is not null)
            {
                AcceptedEUSEPACountries = acceptedCountriesField.SupportedSepaEuroCountries
                    .Select(x => new SelectedAcceptedCountry { Code = x.Code, CodeWithCountry = $"{x.Name} ({x.Code})", IsSelected = true })
                    .ToList();

                AcceptedNonEUSEPACountries = acceptedCountriesField.SupportedSepaNonEuroCountries
                    .Select(x => new SelectedAcceptedCountry { Code = x.Code, CodeWithCountry = $"{x.Name} ({x.Code})", IsSelected = true })
                    .ToList();
            }

            _editContext = new EditContext(PaymentAccountForm);
            _messageStore = new ValidationMessageStore(_editContext);
            _editContext.OnFieldChanged += ValidateModel;
        }
    } = string.Empty;

    public bool IsFetching { get; set; }

    public FieldId? GetPaymentAccountName(string id)
    {
        switch (id)
        {
            case "PAY_BY_MAIL":
                return FieldId.HOLDER_ADDRESS;
            case "MONEY_GRAM":
            case "FASTER_PAYMENTS":
            case "STRIKE":
                return FieldId.HOLDER_NAME;
            case "F2F":
                return FieldId.CITY;
            case "AUSTRALIA_PAYID":
                return FieldId.BANK_ACCOUNT_NAME;
            case "UPHOLD":
                return FieldId.ACCOUNT_ID;
            case "REVOLUT":
                return FieldId.USERNAME;
            case "SEPA":
            case "SEPA_INSTANT":
            case "ZELLE":
                return FieldId.EMAIL_OR_MOBILE_NR;
            case "SWIFT":
                return FieldId.BENEFICIARY_NAME;
            case "CASH_APP":
                return FieldId.EMAIL_OR_MOBILE_NR_OR_CASHTAG;
            case "VENMO":
            case "PAYPAL":
                return FieldId.EMAIL_OR_MOBILE_NR_OR_USERNAME;
            case "PAYSAFE":
            case "WISE":
            case "PAXUM":
                return FieldId.EMAIL;
            case "BLOCK_CHAINS":
            case "CASH_AT_ATM":
            default: return null;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {

        }

        await base.OnAfterRenderAsync(firstRender);
    }

    protected override async Task OnInitializedAsync()
    {
        // Might be worth switching these to for with a max of 3 attempts...
        while (true)
        {
            try
            {
                PaymentAccounts = await PaymentAccountService.GetPaymentAccountsAsync();
                TraditionalPaymentMethods = await PaymentAccountService.GetPaymentMethodsAsync();
                TraditionalPaymentMethods = TraditionalPaymentMethods.Where(x => x.Id != "AMAZON_GIFT_CARD").ToList();

                var filteredPaymentMethodIds = TraditionalPaymentMethods
                    .Select(x => x.Id);

                TraditionalPaymentMethodStrings = PaymentMethodsHelper.PaymentMethodsDictionary
                    .Where(x => filteredPaymentMethodIds.Contains(x.Key)).OrderBy(x => x.Value).ToDictionary();

                if (!string.IsNullOrEmpty(AccountToCreate))
                {
                    SelectedPaymentMethodId = AccountToCreate;
                }

                break;
            }
            catch (Exception)
            {

            }

            await Task.Delay(2_000);
        }

        await base.OnInitializedAsync();
    }

    public void HandleCountryChanged(string country, FieldId fieldId)
    {
        if (PaymentAccountForm is null)
            return;

        var countryField = PaymentAccountForm.Fields.FirstOrDefault(x => x.Id == fieldId);
        if (countryField is not null)
        {
            countryField.Value = country;
        }
    }

    public void HandleAccountClick(PaymentAccount paymentAccount)
    {
        PaymentAccountForm = null;
        SelectedPaymentAccount = paymentAccount;
        PaymentMethodSearchableDropdown.Clear();
    }

    public async void ValidateModel(object? sender, FieldChangedEventArgs e)
    {
        if (sender is null || _messageStore is null || _editContext is null)
            return;

        if (e.FieldIdentifier.Model is not PaymentAccountFormField field || PaymentAccountForm is null)
            return;

        var errorMessage = await PaymentAccountService.ValidateFormFieldAsync(new ValidateFormFieldRequest
        {
            FieldId = field.Id,
            Form = PaymentAccountForm,
            Value = field.Value
        });

        _messageStore.Clear(() => field.Value);

        if (!string.IsNullOrEmpty(errorMessage))
        {
            _messageStore.Add(() => field.Value, errorMessage);
        }

        SubmitButtonDisabled = !_editContext.Validate();
        _editContext.NotifyValidationStateChanged();

        if (!CustomAccountNameEnabled && AccountNameField is not null && CopyFromField is not null && field.Id == CopyFromField.Id)
        {
            AccountNameField.Value = $"{TraditionalPaymentMethodStrings[SelectedPaymentMethodId]}: {CopyFromField.Value}";
        }

        StateHasChanged();
    }

    public void HandleCryptoAddressChange()
    {
        if (!CustomAccountNameEnabled && AccountNameField is not null && CreateCryptoCurrencyPaymentAccountRequest is not null)
        {
            CreateCryptoCurrencyPaymentAccountRequest.AccountName = $"{CreateCryptoCurrencyPaymentAccountRequest.CurrencyCode}: {CreateCryptoCurrencyPaymentAccountRequest?.Address}";
        }
    }

    public void HandleCurrencyChange()
    {
        if (CustomAccountNameEnabled)
            return;

        if (AccountNameField is not null && CopyFromField is null)
            AccountNameField.Value = $"{TraditionalPaymentMethodStrings[SelectedPaymentMethodId]}: {string.Join(", ", SupportedCurrencyCodes.Where(x => x.IsSelected).Select(x => x.Code))}";
    }

    public async Task DeleteAccountAsync(string paymentAccountId)
    {
        await PaymentAccountService.DeletePaymentAccountAsync(paymentAccountId);

        PaymentAccounts = [.. PaymentAccounts.Where(x => x.Id != paymentAccountId)];
        SelectedPaymentAccount = null;
        ShowDeleteAccountModal = false;
    }

    public async Task CreateCryptoPaymentAccountAsync()
    {
        if (CreateCryptoCurrencyPaymentAccountRequest is null)
            return;

        var createdCryptoAccount = await PaymentAccountService.CreateCryptoCurrencyPaymentAccountAsync(CreateCryptoCurrencyPaymentAccountRequest);
        PaymentAccounts = [.. PaymentAccounts, createdCryptoAccount];

        CreateCryptoCurrencyPaymentAccountRequest = null;
        PaymentAccountForm = null;
        PaymentMethodSearchableDropdown.Clear();
    }

    public async Task CreatePaymentAccountAsync()
    {
        if (PaymentAccountForm is null)
            return;

        IsFetching = true;

        try
        {
            // Do this properly, use UseIntermediaryBankEnabled etc
            if (PaymentAccountForm.Id == FormId.SWIFT)
                PaymentAccountForm.Fields = PaymentAccountForm.Fields.Where(x => !string.IsNullOrEmpty(x.Value)).ToList();

            var request = new CreatePaymentAccountRequest
            {
                PaymentAccountForm = PaymentAccountForm
            };

            var currenciesField = request.PaymentAccountForm.Fields.FirstOrDefault(x => x.Id == FieldId.TRADE_CURRENCIES);
            if (currenciesField is not null)
            {
                currenciesField.Value = string.Join(",", SupportedCurrencyCodes.Where(x => x.IsSelected).Select(x => x.Code));
            }

            var acceptedCountriesField = request.PaymentAccountForm.Fields.FirstOrDefault(x => x.Id == FieldId.ACCEPTED_COUNTRY_CODES);
            if (acceptedCountriesField is not null)
            {
                acceptedCountriesField.Value = string.Join(",", [..AcceptedEUSEPACountries.Where(x => x.IsSelected).Select(x => x.Code), ..AcceptedNonEUSEPACountries.Where(x => x.IsSelected).Select(x => x.Code)]);
            }

            var createdPaymentAccount = await PaymentAccountService.CreatePaymentAccountAsync(request);
            PaymentAccounts = [.. PaymentAccounts, createdPaymentAccount];

            var filteredPaymentMethodIds = TraditionalPaymentMethods
                .Select(x => x.Id);

            TraditionalPaymentMethodStrings = PaymentMethodsHelper.PaymentMethodsDictionary
                .Where(x => filteredPaymentMethodIds.Contains(x.Key)).OrderBy(x => x.Value).ToDictionary();

            SelectedPaymentAccount = null;
            PaymentAccountForm = null;
            CreateCryptoCurrencyPaymentAccountRequest = null;
            SelectedPaymentMethodId = string.Empty;
            PaymentMethodSearchableDropdown.Clear();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
        finally
        {
            IsFetching = false;
        }
    }
}
