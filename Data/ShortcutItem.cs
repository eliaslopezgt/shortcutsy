#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.IO;

namespace Shortcutsy.Data;

/// <summary>
/// Represents a single keyboard shortcut with its action name, key combination, and difficulty level.
/// </summary>
public class ShortcutItem
{
    public string Action { get; set; } = "";
    public List<Microsoft.Xna.Framework.Input.Keys> KeyCombo { get; set; } = new();
    public int Level { get; set; } = 1;

    /// <summary>
    /// Converts the key combination to a human-readable string (e.g., "Ctrl+S").
    /// </summary>
    public string GetShortcutString()
    {
        var parts = new List<string>();
        foreach (var key in KeyCombo)
        {
            string keyStr = key.ToString();
            if (keyStr.StartsWith("Left")) keyStr = keyStr.Substring(4);
            else if (keyStr.StartsWith("Right")) keyStr = "Right" + keyStr.Substring(5);
            else if (keyStr.StartsWith("NumPad")) keyStr = "Num" + keyStr.Substring(6);
            parts.Add(keyStr);
        }
        return string.Join("+", parts);
    }
}

/// <summary>
/// JSON configuration model for deserializing shortcuts from shortcuts.json.
/// </summary>
public class ShortcutConfig
{
    public string? Action { get; set; }
    public List<string>? Keys { get; set; }
    public int Level { get; set; } = 1;
}
