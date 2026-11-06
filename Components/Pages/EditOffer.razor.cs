using Microsoft.AspNetCore.Components;

namespace Manta.Components.Pages;

public partial class EditOffer : ComponentBase
{
    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;
}
