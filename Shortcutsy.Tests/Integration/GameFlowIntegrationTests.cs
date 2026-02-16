using System;
using System.Collections.Generic;
using Xunit;

namespace Shortcutsy.Tests.Integration;

public class GameFlowIntegrationTests
{
    [Fact]
    public void FullGameFlow_InitializeAndPlay_ScoreTracking()
    {
        // Arrange - Setup with test data
        string shortcutsJson = @"
        [
            {""Action"":""Save"",""Keys"":[""Ctrl+S""],""Level"":1},
            {""Action"":""Copy"",""Keys"":[""Ctrl+C""],""Level"":1},
            {""Action"":""Find"",""Keys"":[""Ctrl+F""],""Level"":2}
        ]";
        
        var db = new Shortcutsy.Data.ShortcutDatabase(
            "test.json",
            _ => shortcutsJson,
            _ => true);
        
        // Act - Initialize database
        db.LoadData();
        
        // Get level 1 shortcuts
        var level1Shortcuts = db.GetShortcutsForLevel(1);
        
        // Assert - Verify shortcuts loaded correctly
        Assert.Equal(2, level1Shortcuts.Count);
        Assert.Contains(level1Shortcuts, s => s.Action == "Save");
        Assert.Contains(level1Shortcuts, s => s.Action == "Copy");
        
        // Test level 2
        var allShortcuts = db.GetShortcutsForLevel(2);
        Assert.Equal(3, allShortcuts.Count);
        
        // Test high score manager
        var hs = new Shortcutsy.Data.HighScoreManager(
            _ => "{}",
            _ => { },
            _ => false);
        hs.Load();
        
        // Simulate game: add scores
        bool isNewRecord1 = hs.AddScore(1000, 1);
        Assert.True(isNewRecord1); // First score should be a record
        Assert.Equal(1000, hs.GetScoreForLevel(1));
        
        bool isNewRecord2 = hs.AddScore(1500, 1);
        Assert.True(isNewRecord2); // Higher score should be a new record
        Assert.Equal(1500, hs.GetScoreForLevel(1));
        
        bool isNotNewRecord = hs.AddScore(1000, 1);
        Assert.False(isNotNewRecord); // Lower score should not be a record
        Assert.Equal(1500, hs.GetScoreForLevel(1)); // Should keep higher score
        
        // Different level should track independently
        bool isNewRecord3 = hs.AddScore(2000, 2);
        Assert.True(isNewRecord3);
        Assert.Equal(2000, hs.GetScoreForLevel(2));
    }

    [Fact]
    public void LevelProgression_ShortcutsUnlockedSequentially()
    {
        // Arrange
        string shortcutsJson = @"
        [
            {""Action"":""Save"",""Keys"":[""Ctrl+S""],""Level"":1},
            {""Action"":""Copy"",""Keys"":[""Ctrl+C""],""Level"":2},
            {""Action"":""Find"",""Keys"":[""Ctrl+F""],""Level"":3}
        ]";
        
        var db = new Shortcutsy.Data.ShortcutDatabase(
            "test.json",
            _ => shortcutsJson,
            _ => true);
        
        // Act
        db.LoadData();
        
        // Assert - Level 1 only shows level 1 shortcuts
        var level1 = db.GetShortcutsForLevel(1);
        Assert.Single(level1);
        Assert.Equal("Save", level1[0].Action);
        
        // Level 2 shows level 1 and 2
        var level2 = db.GetShortcutsForLevel(2);
        Assert.Equal(2, level2.Count);
        
        // Level 3 shows all
        var level3 = db.GetShortcutsForLevel(3);
        Assert.Equal(3, level3.Count);
        
        // Max level is 3
        Assert.Equal(3, db.GetMaxLevel());
    }

    [Fact]
    public void KeyParsing_ComplexCombinations()
    {
        // Arrange
        var db = new Shortcutsy.Data.ShortcutDatabase();
        
        // Act - Parse various key combinations
        var ctrlS = db.ParseKeys(new List<string> { "Ctrl+S" });
        var ctrlShiftB = db.ParseKeys(new List<string> { "Ctrl+Shift+B" });
        var multiKey = db.ParseKeys(new List<string> { "Ctrl", "K", "Ctrl", "C" });
        
        // Assert
        Assert.Equal(2, ctrlS.Count);
        Assert.Equal(3, ctrlShiftB.Count);
        Assert.Equal(4, multiKey.Count);
    }
}
