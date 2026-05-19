using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace CourtFlow.Web.Client;

/// <summary>
/// Bridges <see cref="IWebAssemblyHostEnvironment"/> to <see cref="IHostEnvironment"/> for shared Razor
/// (e.g. dev-only UI) without an invalid cast in the browser runtime.
/// </summary>
internal sealed class WebAssemblyHostEnvironmentAdapter(IWebAssemblyHostEnvironment wasm) : IHostEnvironment
{
    public string EnvironmentName
    {
        get => wasm.Environment;
        set { }
    }

    public string ApplicationName
    {
        get => typeof(Program).Assembly.GetName().Name ?? "CourtFlow.Web.Client";
        set { }
    }

    public string ContentRootPath
    {
        get => string.Empty;
        set { }
    }

    public IFileProvider ContentRootFileProvider
    {
        get => new NullFileProvider();
        set { }
    }
}
