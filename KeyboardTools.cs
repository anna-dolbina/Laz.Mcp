using System.ComponentModel;
using Laz;
using Laz.Extensions.Keyboard;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Laz.Mcp;

internal sealed class KeyboardTools(LazbotGate gate)
{
    [McpServerTool(Name = "keyboard_key_down")]
    [Description("Presses and holds a key until keyboard_key_up. Used to build modifier-held sequences.")]
    public void KeyDown(
        [Description("Key name, e.g. A, F1, Enter, Control, LeftShift")] string key)
    {
        var parsedKey = EnumParsing.Parse<Key>(key, nameof(key));
        gate.Run(bot => bot.Keyboard.KeyDown(parsedKey));
    }

    [McpServerTool(Name = "keyboard_key_up")]
    [Description("Releases a previously held key.")]
    public void KeyUp(
        [Description("Key name, e.g. A, F1, Enter, Control, LeftShift")] string key)
    {
        var parsedKey = EnumParsing.Parse<Key>(key, nameof(key));
        gate.Run(bot => bot.Keyboard.KeyUp(parsedKey));
    }

    [McpServerTool(Name = "keyboard_stroke")]
    [Description("Presses and releases a single key.")]
    public void Stroke(
        [Description("Key name, e.g. A, F1, Enter, Control, LeftShift")] string key)
    {
        var parsedKey = EnumParsing.Parse<Key>(key, nameof(key));
        gate.Run(bot => bot.Keyboard.Stroke(parsedKey));
    }

    [McpServerTool(Name = "keyboard_combo")]
    [Description("Holds down a list of keys in order, then releases them in reverse order - e.g. [\"Control\", \"Shift\", \"Esc\"] for a shortcut chord.")]
    public void Combo(
        [Description("Key names to hold in order, e.g. [\"Control\", \"Shift\", \"Esc\"]")] string[] keys)
    {
        if (keys.Length == 0)
        {
            throw new McpException("'keys' must contain at least one key name.");
        }

        var parsedKeys = new Key[keys.Length];
        for (var i = 0; i < keys.Length; i++)
        {
            parsedKeys[i] = EnumParsing.Parse<Key>(keys[i], nameof(keys));
        }

        gate.Run(bot =>
        {
            foreach (var parsedKey in parsedKeys)
            {
                bot.Keyboard.KeyDown(parsedKey);
            }

            for (var i = parsedKeys.Length - 1; i >= 0; i--)
            {
                bot.Keyboard.KeyUp(parsedKeys[i]);
            }
        });
    }

    [McpServerTool(Name = "keyboard_type")]
    [Description("Types arbitrary text as a sequence of keystrokes, handling dead keys and shifted characters automatically. Characters that can't be produced by the active keyboard layout fail unless clipboardFallback is set.")]
    public void Type(
        [Description("Text to type")] string text,
        [Description("If true, characters that can't be typed physically are pasted via clipboard (Windows/macOS only); defaults to false")] bool clipboardFallback = false,
        [Description("If true, uses Ctrl/Shift+Insert instead of Ctrl+C/V for the clipboard fallback (Windows only); defaults to false")] bool useCtrlInsert = false)
        => gate.Run(bot => bot.Keyboard.Type(text, clipboardFallback, useCtrlInsert));
}
