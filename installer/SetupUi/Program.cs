using WixToolset.BootstrapperApplicationApi;

namespace BdoTimers.SetupUi;

internal static class Program
{
    // Kept minimal so the bundle engine connects as soon as possible.
    static int Main()
    {
        ManagedBootstrapperApplication.Run(new SetupApplication());
        return 0;
    }
}
