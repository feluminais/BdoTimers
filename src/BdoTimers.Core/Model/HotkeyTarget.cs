namespace BdoTimers.Core.Model;

public enum HotkeyAction { AlwaysShow, Show, StartHorseRegistration, ControlCountdown, MoveOverlay }

/// <summary>A shortcut's action and, for custom countdowns, the timer it controls.</summary>
public readonly record struct HotkeyTarget(HotkeyAction Action, Guid? TimerId = null);
