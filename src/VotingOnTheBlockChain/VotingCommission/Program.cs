
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Havit.Blazor.Components.Web;            
using Havit.Blazor.Components.Web.Bootstrap;  
using VotingCommission;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddHxServices();
builder.Services.AddHxMessageBoxHost();
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
SetHxComponents();
await builder.Build().RunAsync();

static void SetHxComponents()
{
    HxPlaceholderContainer.Defaults.Animation = PlaceholderAnimation.Glow;
    HxPlaceholder.Defaults.Color = ThemeColor.Light;

    HxButton.Defaults.Size = ButtonSize.Small;

    HxOffcanvas.Defaults.Backdrop = OffcanvasBackdrop.Static;
    HxOffcanvas.Defaults.HeaderCssClass = "border-bottom";
    HxOffcanvas.Defaults.FooterCssClass = "border-top";

    HxChipList.Defaults.ChipBadgeSettings.Color = ThemeColor.Light;
    HxChipList.Defaults.ChipBadgeSettings.TextColor = ThemeColor.Dark;
    HxChipList.Defaults.ChipBadgeSettings.CssClass = "p-2 rounded-pill";
}