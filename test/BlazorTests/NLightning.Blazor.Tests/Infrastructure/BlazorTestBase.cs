using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace NLightning.Blazor.Tests.Infrastructure;

using Helpers;

public class BlazorTestBase : IAsyncLifetime
{
    private readonly WebApplication _server;

    protected static readonly string RootUri = "http://127.0.0.1:8085";

    protected readonly StringWriter ConsoleOutput;

    protected IPage? Page;

    public BlazorTestBase()
    {
        ConsoleOutput = new StringWriter();
        Console.SetOut(ConsoleOutput);

        var builder = WebApplication.CreateBuilder(["--urls", RootUri]);
        builder.Services.AddDirectoryBrowser();

        _server = builder.Build();

        if (!_server.Environment.IsDevelopment())
        {
            _server.UseExceptionHandler("/Error");
        }
        else
        {
            _server.UseDeveloperExceptionPage();
        }

        const string assetsFilePath = "NLightning.BlazorTestApp.staticwebassets.runtime.json";

        if (File.Exists(assetsFilePath))
        {
            var directoryPaths = StaticAssetsHelper.GetRootLevelEntries(assetsFilePath);

            foreach (var path in directoryPaths)
            {
                _server.UseFileServer(new FileServerOptions
                {
                    FileProvider = new PhysicalFileProvider(path.ContentRoot),
                    RequestPath = path.RelativePath,
                    StaticFileOptions =
                    {
                        ServeUnknownFileTypes = true,
                        OnPrepareResponse = ctx =>
                        {
                            if (!IsBlazorFile(ctx.File.Name))
                            {
                                return;
                            }

                            // Set no-cache headers for Blazor-specific files
                            ctx.Context.Response.GetTypedHeaders().CacheControl =
                                new Microsoft.Net.Http.Headers.CacheControlHeaderValue
                                {
                                    Public = true,
                                    MaxAge = TimeSpan.FromDays(0)
                                };

                            ctx.Context.Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.Pragma] = "no-cache";
                            ctx.Context.Response.Headers[Microsoft.Net.Http.Headers.HeaderNames.Expires] = "0";
                        }
                    }
                });
            }
        }
        else
        {
            throw new FileNotFoundException("Could not find test assets file.");
        }

        _server.RunAsync();
    }

    public async ValueTask InitializeAsync()
    {
        // Playwright initialization logic to intercept HTTP requests
        var playwright = await Playwright.CreateAsync();
        var browser = await playwright.Chromium.LaunchAsync();
        var context = await browser.NewContextAsync();
        Page = await context.NewPageAsync();
    }

    protected void ClearOutput()
    {
        ConsoleOutput.GetStringBuilder().Clear();
    }

    private static bool IsBlazorFile(string fileName)
    {
        return fileName.EndsWith("dll") || fileName.EndsWith("wasm") || fileName.EndsWith("blazor") ||
               fileName.EndsWith("dat");
    }

    public async ValueTask DisposeAsync()
    {
        await _server.StopAsync();
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true });
        await ConsoleOutput.DisposeAsync();
    }
}