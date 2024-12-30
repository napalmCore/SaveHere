using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

namespace SaveHere.Client
{
  internal class Program
  {
    static async Task Main(string[] args)
    {
      var builder = WebAssemblyHostBuilder.CreateDefault(args);

      builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
      builder.Services.AddMudServices();

      builder.Services.AddAuthorizationCore();
      builder.Services.AddCascadingAuthenticationState();
      builder.Services.AddSingleton<AuthenticationStateProvider, PersistentAuthenticationStateProvider>();

      await builder.Build().RunAsync();
    }
  }
}
