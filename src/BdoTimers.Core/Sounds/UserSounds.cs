namespace BdoTimers.Core.Sounds;

/// <summary>Sounds the user added, copied into <paramref name="folder"/>; a sound's key is its file name there.</summary>
public sealed class UserSounds(string folder)
{
    public static IReadOnlyList<string> Extensions { get; } = [".wav", ".mp3"];

    public string Folder { get; } = folder;

    public IReadOnlyList<string> Keys() =>
        Directory.Exists(Folder)
            ? Directory.EnumerateFiles(Folder).Select(Path.GetFileName).OfType<string>().Where(HasSoundExtension)
                .Order(StringComparer.OrdinalIgnoreCase).ToList()
            : [];

    public bool Exists(string key) => SoundKeys.IsUserKey(key) && HasSoundExtension(key) && File.Exists(PathFor(key));

    public string PathFor(string key) => Path.Combine(Folder, key);

    /// <summary>Copies the file in under its own name, numbered on a clash, and returns its key.</summary>
    /// <exception cref="ArgumentException">The file is not a .wav or .mp3.</exception>
    public string Import(string sourcePath)
    {
        if (!HasSoundExtension(sourcePath)) throw new ArgumentException("Only WAV and MP3 files can be added.", nameof(sourcePath));
        Directory.CreateDirectory(Folder);
        var name = Path.GetFileNameWithoutExtension(sourcePath);
        var extension = Path.GetExtension(sourcePath);
        var key = name + extension;
        for (var n = 2; File.Exists(PathFor(key)); n++) key = $"{name} ({n}){extension}";
        File.Copy(sourcePath, PathFor(key));
        return key;
    }

    public void Delete(string key)
    {
        if (Exists(key)) File.Delete(PathFor(key));
    }

    static bool HasSoundExtension(string path) => Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
}
