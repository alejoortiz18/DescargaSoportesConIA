using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SoportesDescarga;
using SoportesDescarga.Application.Interfaces;
using SoportesDescarga.Infrastructure;
using SoportesDescarga.Shared;
using System.Net.Http.Headers;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .Build();

var services = new ServiceCollection();

services.Configure<AppSettings>(configuration);

services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Warning);
});

services.AddHttpClient<IConsultaSoporteService, ConsultaSoporteService>(client =>
{
    var apiSettings = configuration.GetSection("Api").Get<ApiSettings>()!;
    var baseUrl = apiSettings.BaseUrl.EndsWith('/') ? apiSettings.BaseUrl : apiSettings.BaseUrl + "/";
    client.BaseAddress = new Uri(baseUrl);
    client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", apiSettings.BearerToken);
    client.Timeout = TimeSpan.FromSeconds(30);
});

services.AddSingleton<BrowserManager>();
services.AddSingleton<ILoginService, LoginService>();
services.AddSingleton<IDescargaPdfService, DescargaPdfService>();
services.AddSingleton<IArchivoService, ArchivoService>();
services.AddTransient<AppRunner>();

var serviceProvider = services.BuildServiceProvider();

var browserManager = serviceProvider.GetRequiredService<BrowserManager>();
await browserManager.InitializeAsync();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
    Console.WriteLine("\nCancelando... esperando cierre del ciclo actual...");
};

try
{
    var appRunner = serviceProvider.GetRequiredService<AppRunner>();
    await appRunner.RunAsync(cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Proceso cancelado por el usuario.");
}
finally
{
    await browserManager.DisposeAsync();
}
