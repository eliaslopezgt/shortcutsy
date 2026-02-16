#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.IO;
using Microsoft.Xna.Framework.Input;
using Keys = Microsoft.Xna.Framework.Input.Keys;

namespace Shortcutsy.Data;

/// <summary>
/// Manages the keyboard shortcut database, loading from JSON and providing shortcuts by level.
/// </summary>
public class ShortcutDatabase
{
    public List<ShortcutItem> AllShortcuts { get; private set; } = new();
    public Dictionary<int, List<ShortcutItem>> ByLevel { get; private set; } = new();

    private readonly string _configPath;
    private readonly Func<string, string>? _fileReader;
    private readonly Func<string, bool>? _fileExists;

    /// <summary>
    /// Maps JSON key strings to MonoGame Keys enum values.
    /// </summary>
    private static readonly Dictionary<string, Keys> KeyMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Ctrl", Keys.LeftControl },
        { "Control", Keys.LeftControl },
        { "Alt", Keys.LeftAlt },
        { "Shift", Keys.LeftShift },
        { "F1", Keys.F1 }, { "F2", Keys.F2 }, { "F3", Keys.F3 }, { "F4", Keys.F4 },
        { "F5", Keys.F5 }, { "F6", Keys.F6 }, { "F7", Keys.F7 }, { "F8", Keys.F8 },
        { "F9", Keys.F9 }, { "F10", Keys.F10 }, { "F11", Keys.F11 }, { "F12", Keys.F12 },
        { "A", Keys.A }, { "B", Keys.B }, { "C", Keys.C }, { "D", Keys.D },
        { "E", Keys.E }, { "F", Keys.F }, { "G", Keys.G }, { "H", Keys.H },
        { "I", Keys.I }, { "J", Keys.J }, { "K", Keys.K }, { "L", Keys.L },
        { "M", Keys.M }, { "N", Keys.N }, { "O", Keys.O }, { "P", Keys.P },
        { "Q", Keys.Q }, { "R", Keys.R }, { "S", Keys.S }, { "T", Keys.T },
        { "U", Keys.U }, { "V", Keys.V }, { "W", Keys.W }, { "X", Keys.X },
        { "Y", Keys.Y }, { "Z", Keys.Z },
        { "Up", Keys.Up }, { "Down", Keys.Down }, { "Left", Keys.Left }, { "Right", Keys.Right },
        { "Space", Keys.Space }, { "Enter", Keys.Enter }, { "Escape", Keys.Escape },
        { "Tab", Keys.Tab }, { "Backspace", Keys.Back }, { "Delete", Keys.Delete },
        { "Home", Keys.Home }, { "End", Keys.End }, { "PageUp", Keys.PageUp }, { "PageDown", Keys.PageDown },
        { "Num0", Keys.NumPad0 }, { "Num1", Keys.NumPad1 }, { "Num2", Keys.NumPad2 },
        { "Num3", Keys.NumPad3 }, { "Num4", Keys.NumPad4 }, { "Num5", Keys.NumPad5 },
        { "Num6", Keys.NumPad6 }, { "Num7", Keys.NumPad7 }, { "Num8", Keys.NumPad8 }, { "Num9", Keys.NumPad9 },
        { "-", Keys.OemMinus }, { "Minus", Keys.OemMinus },
        { "\\", Keys.OemBackslash }, { "Backslash", Keys.OemBackslash },
        { "/", Keys.OemQuestion }, { "?", Keys.OemQuestion },
        { ".", Keys.OemPeriod }, { "Period", Keys.OemPeriod },
        { ",", Keys.OemComma }, { "Comma", Keys.OemComma },
        { ";", Keys.OemSemicolon }, { "Semicolon", Keys.OemSemicolon },
        { "'", Keys.OemQuotes }, { "Quote", Keys.OemQuotes },
        { "[", Microsoft.Xna.Framework.Input.Keys.OemOpenBrackets }, { "OpenBracket", Keys.OemOpenBrackets },
        { "]", Keys.OemCloseBrackets }, { "CloseBracket", Keys.OemCloseBrackets },
    };

    /// <summary>
    /// Creates a ShortcutDatabase with default file-based loading.
    /// </summary>
    public ShortcutDatabase() : this("shortcuts.json", null, null)
    {
    }

    /// <summary>
    /// Creates a ShortcutDatabase with custom file handling (for testing).
    /// </summary>
    public ShortcutDatabase(string configPath, Func<string, string>? fileReader, Func<string, bool>? fileExists)
    {
        _configPath = configPath;
        _fileReader = fileReader;
        _fileExists = fileExists ?? File.Exists;
    }

    /// <summary>
    /// Initializes the database by loading from JSON file, falling back to defaults if not found.
    /// </summary>
    public void LoadData()
    {
        string configPath = _configPath;
        
        if (!(_fileExists?.Invoke(configPath) ?? File.Exists(configPath)) && AppContext.BaseDirectory != null)
        {
            configPath = Path.Combine(AppContext.BaseDirectory, "shortcuts.json");
        }
        
        if (_fileExists?.Invoke(configPath) ?? File.Exists(configPath))
        {
            try
            {
                string json = _fileReader?.Invoke(configPath) ?? File.ReadAllText(configPath);
                var config = JsonSerializer.Deserialize<List<ShortcutConfig>>(json);
                if (config != null)
                {
                    AllShortcuts = config
                        .Where(c => !string.IsNullOrEmpty(c.Action) && c.Keys != null && c.Keys.Count > 0)
                        .Select(c => new ShortcutItem
                        {
                            Action = c.Action!,
                            Level = c.Level,
                            KeyCombo = ParseKeys(c.Keys!)
                        })
                        .ToList();
                    
                    if (AllShortcuts.Count > 0)
                    {
                        ByLevel = AllShortcuts.GroupBy(s => s.Level).ToDictionary(g => g.Key, g => g.ToList());
                        return;
                    }
                }
            }
            catch
            {
                // Fall back to defaults
            }
        }
        
        LoadDefaultShortcuts();
    }

    /// <summary>
    /// Parses a list of key strings (e.g., ["Ctrl", "S"] or ["Ctrl+S"]) into Keys enum values.
    /// </summary>
    public List<Keys> ParseKeys(List<string> keyStrings)
    {
        var keys = new List<Keys>();
        foreach (var keyCombo in keyStrings)
        {
            var parts = keyCombo.Split('+');
            foreach (var part in parts)
            {
                string k = part.Trim();
                if (KeyMappings.TryGetValue(k, out var monoKey))
                {
                    keys.Add(monoKey);
                }
            }
        }
        return keys;
    }

    /// <summary>
    /// Loads default hardcoded shortcuts as fallback.
    /// </summary>
    private void LoadDefaultShortcuts()
    {
        AllShortcuts = new List<ShortcutItem>
        {
            new() { Action = "Save", KeyCombo = new List<Keys> { Keys.LeftControl, Keys.S }, Level = 1 },
            new() { Action = "Find", KeyCombo = new List<Keys> { Keys.LeftControl, Keys.F }, Level = 1 },
            new() { Action = "Copy", KeyCombo = new List<Keys> { Keys.LeftControl, Keys.C }, Level = 1 },
            new() { Action = "Paste", KeyCombo = new List<Keys> { Keys.LeftControl, Keys.V }, Level = 1 },
            new() { Action = "Cut", KeyCombo = new List<Keys> { Keys.LeftControl, Keys.X }, Level = 1 },
        };
        
        ByLevel = AllShortcuts.GroupBy(s => s.Level).ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>
    /// Gets all shortcuts up to the specified maximum level.
    /// </summary>
    public List<ShortcutItem> GetShortcutsForLevel(int maxLevel)
    {
        return AllShortcuts.Where(s => s.Level <= maxLevel).ToList();
    }

    /// <summary>
    /// Gets shortcuts for a specific level.
    /// </summary>
    public List<ShortcutItem> GetShortcutsForLevelExact(int level)
    {
        return ByLevel.ContainsKey(level) ? ByLevel[level] : new List<ShortcutItem>();
    }

    /// <summary>
    /// Gets the maximum level in the database.
    /// </summary>
    public int GetMaxLevel()
    {
        return AllShortcuts.Count > 0 ? AllShortcuts.Max(s => s.Level) : 0;
    }

    /// <summary>
    /// Static initialization for backwards compatibility.
    /// </summary>
    private static ShortcutDatabase? _instance;
    public static ShortcutDatabase Instance => _instance ??= new ShortcutDatabase();

    public static void Initialize()
    {
        _instance = new ShortcutDatabase();
        _instance.LoadData();
    }
}
