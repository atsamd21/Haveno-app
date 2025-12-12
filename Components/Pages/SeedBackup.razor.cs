using HavenoSharp.Services;
using Manta.Helpers;
using Microsoft.AspNetCore.Components;

namespace Manta.Components.Pages;

public class SeedWord
{
    public string Word { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }

    public SeedWord(string word, bool isCorrect)
    {
        Word = word;
        IsCorrect = isCorrect;
    }
}

public partial class SeedBackup : ComponentBase
{
    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;
    [Inject]
    public IHavenoWalletService HavenoWalletService { get; set; } = default!;

    public List<string> SeedWords { get; set; } = [];
    public List<SeedWord> RemovedSeedWords { get; set; } = [];
    public int[] RemovedIndices { get; set; } = new int[5];

    public string XmrSeed { get; set; } = string.Empty;
    public int Step { get; set; }
    public bool IsValid 
    {
        get
        {
            for (int i = 0; i < RemovedIndices.Length; i++)
            {
                if (!RemovedSeedWords[RemovedIndices[i]].IsCorrect)
                {
                    return false;
                }
            }

            return true;
        }
    }

    public void HandleOnInput(string word, int i)
    {
        RemovedSeedWords[i].Word = word.ToLower();
        RemovedSeedWords[i].IsCorrect = RemovedSeedWords[i].Word == SeedWords[i];
    }

    protected override async Task OnInitializedAsync()
    {
        XmrSeed = await HavenoWalletService.GetXmrSeedAsync();
        SeedWords = XmrSeed.Split(" ").ToList();
        RemovedSeedWords = SeedWords.Select(x => new SeedWord(x, false)).ToList();

        var random = new Random();
        int i = 0;
        for (int j = 0; j < 25; j += 5)
        {
            RemovedIndices[i] = random.Next(j, j + 5);
            RemovedSeedWords[RemovedIndices[i]] = new SeedWord(string.Empty, false);
            i++;
        }

        await base.OnInitializedAsync();
    }

    public void GoBack()
    {
        Step = 0;
        for (int i = 0; i < RemovedIndices.Length; i++)
        {
            RemovedSeedWords[RemovedIndices[i]] = new SeedWord(string.Empty, false);
        }
    }

    public void Submit()
    {
        AppPreferences.Set(AppPreferences.SeedBackupDone, true);
        NavigationManager.NavigateTo("/wallet");
    }
}
