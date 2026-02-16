#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.IO;

namespace Shortcutsy.Data;

/// <summary>
/// Represents a single high score entry.
/// </summary>
public class HighScoreEntry
{
    public int Score { get; set; }
    public int Level { get; set; }
    public string Date { get; set; } = "";
}

/// <summary>
/// Manages high score persistence, storing best score per level.
/// </summary>
public class HighScoreManager
{
    private const string FileName = "highscores.json";
    private Dictionary<int, int> _levelScores = new();
    private readonly string _filePath;
    private readonly Func<string, bool>? _fileExists;
    private readonly Func<string, string>? _fileReader;
    private readonly Action<string>? _fileWriter;

    /// <summary>
    /// Creates a HighScoreManager with default file-based persistence.
    /// </summary>
    public HighScoreManager() : this(null, null, null)
    {
    }

    /// <summary>
    /// Creates a HighScoreManager with custom file handling (for testing).
    /// </summary>
    public HighScoreManager(Func<string, string>? fileReader, Action<string>? fileWriter, Func<string, bool>? fileExists)
    {
        _filePath = Path.Combine(AppContext.BaseDirectory ?? "", FileName);
        _fileReader = fileReader;
        _fileWriter = fileWriter;
        _fileExists = fileExists ?? File.Exists;
    }

    /// <summary>
    /// Loads high scores from disk.
    /// </summary>
    public void Load()
    {
        try
        {
            if (_fileExists?.Invoke(_filePath) ?? File.Exists(_filePath))
            {
                string json = _fileReader?.Invoke(_filePath) ?? File.ReadAllText(_filePath);
                _levelScores = JsonSerializer.Deserialize<Dictionary<int, int>>(json) ?? new();
            }
        }
        catch
        {
            _levelScores = new();
        }
    }

    /// <summary>
    /// Saves high scores to disk.
    /// </summary>
    public void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(_levelScores, new JsonSerializerOptions { WriteIndented = true });
            if (_fileWriter != null)
            {
                _fileWriter(json);
            }
            else
            {
                File.WriteAllText(_filePath, json);
            }
        }
        catch { }
    }

    /// <summary>
    /// Adds a new score for a level. Returns true if it's a new record for that level.
    /// </summary>
    public bool AddScore(int score, int level)
    {
        bool isNewRecord = false;
        
        if (!_levelScores.ContainsKey(level) || score > _levelScores[level])
        {
            _levelScores[level] = score;
            isNewRecord = true;
        }
        
        Save();
        return isNewRecord;
    }

    /// <summary>
    /// Gets the high score for a specific level.
    /// </summary>
    public int GetScoreForLevel(int level)
    {
        return _levelScores.ContainsKey(level) ? _levelScores[level] : 0;
    }

    /// <summary>
    /// Gets all stored high scores as a list.
    /// </summary>
    public List<HighScoreEntry> GetScores()
    {
        return _levelScores.Select(kv => new HighScoreEntry { Level = kv.Key, Score = kv.Value, Date = "" })
            .OrderByDescending(s => s.Score)
            .ToList();
    }

    /// <summary>
    /// Gets all level scores as dictionary.
    /// </summary>
    public Dictionary<int, int> GetAllScores()
    {
        return new Dictionary<int, int>(_levelScores);
    }

    /// <summary>
    /// Clears all high scores (for testing).
    /// </summary>
    public void Clear()
    {
        _levelScores.Clear();
    }
}
