using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using SaveHere.Components;
using SaveHere.Components.Account;
using SaveHere.Data;
using SaveHere.Models.db;

namespace SaveHere
{
  public class Program
  {
    public static void Main(string[] args)
    {
      var builder = WebApplication.CreateBuilder(args);

      // Initializing the database
      var dbPath = Path.Combine(Directory.GetCurrentDirectory(), "db");
      if (!Directory.Exists(dbPath))
      {
        try
        {
          Directory.CreateDirectory(dbPath);
        }
        catch
        {
          throw new InvalidOperationException("Could not create the database directory.");
        }
      }
      builder.Services.AddDbContext<AppDbContext>(options =>
          options.UseSqlite($"Data Source={Path.Combine(dbPath, "database.sqlite3.db")}")
      );

      // Add MudBlazor services
      builder.Services.AddMudServices();

      // Add services to the container.
      builder.Services.AddRazorComponents()
          .AddInteractiveServerComponents()
          .AddInteractiveWebAssemblyComponents();

      builder.Services.AddCascadingAuthenticationState();
      builder.Services.AddScoped<IdentityUserAccessor>();
      builder.Services.AddScoped<IdentityRedirectManager>();
      builder.Services.AddScoped<AuthenticationStateProvider, PersistingRevalidatingAuthenticationStateProvider>();

      builder.Services.AddAuthentication(options =>
          {
            options.DefaultScheme = IdentityConstants.ApplicationScheme;
            options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
          })
          .AddIdentityCookies();

      var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
      builder.Services.AddDbContext<ApplicationDbContext>(options =>
          options.UseSqlServer(connectionString));
      builder.Services.AddDatabaseDeveloperPageExceptionFilter();

      builder.Services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = true)
          .AddEntityFrameworkStores<ApplicationDbContext>()
          .AddSignInManager()
          .AddDefaultTokenProviders();

      builder.Services.AddSingleton<IEmailSender<ApplicationUser>, IdentityNoOpEmailSender>();

      var app = builder.Build();

      // Configure the HTTP request pipeline.
      if (app.Environment.IsDevelopment())
      {
        app.UseWebAssemblyDebugging();
        app.UseMigrationsEndPoint();
      }
      else
      {
        app.UseExceptionHandler("/Error");
        // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
        app.UseHsts();
      }

      app.UseHttpsRedirection();

      app.UseStaticFiles();
      app.UseAntiforgery();

      app.MapRazorComponents<App>()
          .AddInteractiveServerRenderMode()
          .AddInteractiveWebAssemblyRenderMode()
          .AddAdditionalAssemblies(typeof(Client._Imports).Assembly);

      // Add additional endpoints required by the Identity /Account Razor components.
      app.MapAdditionalIdentityEndpoints();

      // Ensuring the database is created
      using (var scope = app.Services.CreateScope())
      {
        var dbc = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbc.Database.EnsureCreated();
        dbc.Database.Migrate();
      }

      app.Run();
    }
  }
}
