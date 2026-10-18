using BepInEx.Configuration;
using UnityEngine.InputSystem;

namespace MapAssist;

internal static class Hotkeys
{
    internal static ConfigEntry<Key> Menu = null!, Overlay = null!, Health = null!, Mana = null!, Secrets = null!;
    internal static void Bind(ConfigFile config)
    {
        Menu = BindKey("Menu", Key.F4, "Expand or collapse the Map Secrets menu");
        Overlay = BindKey("Overlay", Key.F6, "Toggle all markers");
        Health = BindKey("Health", Key.F7, "Toggle health hearts");
        Mana = BindKey("Mana", Key.F8, "Toggle mana hearts");
        Secrets = BindKey("Secrets", Key.F10, "Toggle hidden paths");
        ConfigEntry<Key> BindKey(string name, Key fallback, string description) => config.Bind("Hotkeys", name, fallback,
            description + " while the area map is open. None disables the shortcut. Change in this file.");
    }

    internal static bool Pressed(Keyboard keyboard, ConfigEntry<Key> setting)
        => setting.Value != Key.None && Enum.IsDefined(typeof(Key), setting.Value) && keyboard[setting.Value].wasPressedThisFrame;

    internal static string Label(ConfigEntry<Key>? setting) => setting == null || setting.Value == Key.None ? "" : setting.Value.ToString();
}
