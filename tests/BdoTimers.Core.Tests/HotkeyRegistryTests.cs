using BdoTimers.Core.Model;
using BdoTimers.Core.Scheduling;

namespace BdoTimers.Core.Tests;

public class HotkeyRegistryTests
{
    static readonly HotkeyTarget Target = new(HotkeyAction.ControlCountdown, Guid.NewGuid());
    static readonly Hotkey Key = new(HotkeyModifiers.Ctrl, 0x47);

    sealed class Registration : IHotkeyRegistration
    {
        public Dictionary<int, Hotkey> Held { get; } = [];
        public bool Refuse { get; set; }
        public bool Register(int id, Hotkey key)
        {
            if (Refuse) return false;
            Held.Add(id, key);
            return true;
        }
        public void Unregister(int id) => Held.Remove(id);
    }

    [Fact]
    public void Bindings_release_on_change_clear_delete_and_dispose_and_old_messages_are_ignored()
    {
        var native = new Registration();
        var registry = new HotkeyRegistry(native);
        registry.Set(new Dictionary<HotkeyTarget, Hotkey> { [Target] = Key });
        var oldId = native.Held.Keys.Single();
        Assert.Equal(Target, registry.TargetFor(oldId));
        // Status changes publish timer data but must leave an unchanged binding and its pending messages intact.
        registry.Set(new Dictionary<HotkeyTarget, Hotkey> { [Target] = Key });
        Assert.Equal(oldId, native.Held.Keys.Single());
        registry.Set(new Dictionary<HotkeyTarget, Hotkey> { [Target] = Key with { VirtualKey = 0x48 } });
        Assert.Null(registry.TargetFor(oldId));
        Assert.Equal(0x48, native.Held.Values.Single().VirtualKey);
        registry.Set(new Dictionary<HotkeyTarget, Hotkey>());
        Assert.Empty(native.Held);
        registry.Set(new Dictionary<HotkeyTarget, Hotkey> { [Target] = Key });
        registry.Dispose();
        Assert.Empty(native.Held);
    }

    [Fact]
    public void Nested_capture_suspends_all_bindings_and_applies_edits_only_after_the_last_resume()
    {
        var native = new Registration();
        using var registry = new HotkeyRegistry(native);
        registry.Set(new Dictionary<HotkeyTarget, Hotkey> { [Target] = Key, [new(HotkeyAction.Show)] = DefaultHotkeys.Show });
        registry.Suspend();
        registry.Suspend();
        Assert.Empty(native.Held);
        registry.Set(new Dictionary<HotkeyTarget, Hotkey> { [Target] = Key with { VirtualKey = 0x48 } });
        registry.Resume();
        Assert.Empty(native.Held);
        registry.Resume();
        Assert.Equal(0x48, native.Held.Values.Single().VirtualKey);
    }

    [Fact]
    public void Windows_refusal_is_reported_and_cleared_when_registration_recovers_or_binding_is_removed()
    {
        var native = new Registration { Refuse = true };
        using var registry = new HotkeyRegistry(native);
        var changed = 0;
        registry.RefusedChanged += () => changed++;
        registry.Set(new Dictionary<HotkeyTarget, Hotkey> { [Target] = Key });
        Assert.Contains(Target, registry.Refused);
        Assert.Empty(native.Held);
        Assert.Equal(1, changed);
        native.Refuse = false;
        registry.Suspend();
        registry.Resume();
        Assert.Empty(registry.Refused);
        Assert.Single(native.Held);
        Assert.Equal(2, changed);
        native.Refuse = true;
        registry.Set(new Dictionary<HotkeyTarget, Hotkey> { [Target] = Key with { VirtualKey = 0x48 } });
        registry.Set(new Dictionary<HotkeyTarget, Hotkey>());
        Assert.Empty(registry.Refused);
    }
}
