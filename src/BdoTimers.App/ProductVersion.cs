using System.Reflection;

namespace BdoTimers.App;

/// <summary>The product version stamped by publish.ps1, shared with the MSI and setup bundle.</summary>
internal static class ProductVersion
{
    public static string Number { get; } = typeof(App).Assembly
        .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
        ?? typeof(App).Assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version ?? "";
}
