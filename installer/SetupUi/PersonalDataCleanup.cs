using System.IO;
using System.Text.RegularExpressions;

namespace BdoTimers.SetupUi;

/// <summary>Removes only the app's data and its restore folders, without following directory links.</summary>
internal static class PersonalDataCleanup
{
    static readonly Regex RestoreName = new(@"^(?:Data\.before-restore-\d{8}-\d{6}-|\.restore-)[a-f0-9]{32}$", RegexOptions.IgnoreCase);

    public static void Delete(string appFolder)
    {
        var root = Path.GetFullPath(appFolder);
        if (!Directory.Exists(root)) return;
        foreach (var directory in Directory.GetDirectories(root))
        {
            var name = Path.GetFileName(directory);
            if (string.Equals(name, "Data", StringComparison.OrdinalIgnoreCase) || RestoreName.IsMatch(name))
                DeleteTree(directory);
        }
    }

    static void DeleteTree(string folder)
    {
        if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0)
        {
            Directory.Delete(folder);
            return;
        }
        foreach (var entry in Directory.GetFileSystemEntries(folder))
        {
            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.Directory) != 0) DeleteTree(entry);
            else
            {
                if ((attributes & FileAttributes.ReparsePoint) == 0 && (attributes & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(entry, attributes & ~FileAttributes.ReadOnly);
                File.Delete(entry);
            }
        }
        Directory.Delete(folder);
    }
}
