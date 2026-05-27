using NomadVaultMySqlDotnet.Configuration;
using NomadVaultMySqlDotnet.Services;

var builder = WebApplication.CreateBuilder(args);

var configPath = Path.Combine(builder.Environment.ContentRootPath, "config", "config.ini");
builder.Configuration.AddIniFile(configPath, optional: true, reloadOnChange: true);

var runtimeConfig = AppRuntimeConfig.FromConfiguration(builder.Configuration);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(runtimeConfig.Default.LogLevel);

builder.Services.AddSingleton(runtimeConfig);
builder.Services.AddHttpClient("vault")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    });
builder.Services.AddSingleton<VaultService>();
builder.Services.AddSingleton<CustomerRepository>();
builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=App}/{action=Index}/{id?}");

var repository = app.Services.GetRequiredService<CustomerRepository>();
await repository.InitializeAsync(CancellationToken.None);

app.Run();
