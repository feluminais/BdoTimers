using System.IO;
using BdoTimers.App.Alerts;
using BdoTimers.App.Art;
using BdoTimers.Core.Diagnostics;
using BdoTimers.Core.Model;
using BdoTimers.Core.Sounds;
using BdoTimers.Core.Storage;
using Microsoft.Win32;

namespace BdoTimers.App;

/// <summary>The pictures and sounds the player adds from disk.</summary>
public sealed class UserFiles(ArtLibrary art, UserSounds sounds, TimerStore timers, PersistentState<AppSettings> settings)
{
    /// <summary>
    /// Asks for a picture and copies it into the pictures folder. File is null when cancelled or refused; a copy that
    /// fails or a file that isn't a readable picture gives the error to show.
    /// </summary>
    public (string? File, string? Error) ChoosePicture()
    {
        var dialog = new OpenFileDialog { Filter = "Pictures|*.png;*.jpg;*.jpeg;*.bmp;*.gif|All files|*.*" };
        if (dialog.ShowDialog() != true) return (null, null);
        var error = $"Couldn't add {Path.GetFileName(dialog.FileName)}.";
        string file;
        try { file = art.Import(dialog.FileName); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error($"Couldn't import picture {dialog.FileName}", ex);
            return (null, error);
        }
        if (art.UserPicture(file) is not null) return (file, null);
        art.Delete(file);
        return (null, error);
    }

    /// <summary>Asks for a WAV or MP3 and copies it into the user's sounds. Key is null when cancelled or refused.</summary>
    public (string? Key, string? Error) AddSound()
    {
        var dialog = new OpenFileDialog { Filter = "Sounds (WAV, MP3)|*.wav;*.mp3", Title = "Add a sound" };
        if (dialog.ShowDialog() != true) return (null, null);
        if (!SoundChannel.CanDecode(dialog.FileName)) return (null, $"Couldn't play {Path.GetFileName(dialog.FileName)}.");
        try { return (sounds.Import(dialog.FileName), null); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Log.Error($"Couldn't add sound {dialog.FileName}", ex);
            return (null, $"Couldn't add {Path.GetFileName(dialog.FileName)}.");
        }
    }

    /// <summary>
    /// Deletes a user sound; timers and the app-wide sound that used it go back to their defaults. Returns the error
    /// to show, or null once it's gone.
    /// </summary>
    public string? RemoveSound(string key)
    {
        try { sounds.Delete(key); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Most likely still playing; the file is released when playback ends.
            Log.Error($"Couldn't remove sound {key}", ex);
            return $"Couldn't remove {Path.GetFileNameWithoutExtension(key)}. Try again in a moment.";
        }
        timers.ForgetSound(key);
        settings.Update(s => s.AlertSound == key ? s with { AlertSound = BuiltInSounds.Default } : s);
        return null;
    }
}
