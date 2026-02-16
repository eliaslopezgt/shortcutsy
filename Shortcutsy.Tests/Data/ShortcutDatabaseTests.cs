using Xunit;

namespace Shortcutsy.Tests.Data;

public class ShortcutDatabaseTests
{
    [Fact]
    public void ParseKeys_SingleKey_ReturnsCorrectKeys()
    {
        // Arrange
        var db = new Shortcutsy.Data.ShortcutDatabase("fake.json", null, null);
        
        // Act
        var keys = db.ParseKeys(new List<string> { "Ctrl+S" });
        
        // Assert
        Assert.Equal(2, keys.Count);
    }

    [Fact]
    public void GetShortcutsForLevel_ReturnsCorrectShortcuts()
    {
        // Arrange
        string fakeJson = @"[{""Action"":""Save"",""Keys"":[""Ctrl+S""],""Level"":1}]";
        var db = new Shortcutsy.Data.ShortcutDatabase("fake.json", 
            _ => fakeJson, 
            _ => true);
        db.LoadData();
        
        // Act
        var shortcuts = db.GetShortcutsForLevel(1);
        
        // Assert
        Assert.Single(shortcuts);
        Assert.Equal("Save", shortcuts[0].Action);
    }

    [Fact]
    public void GetShortcutsForLevel_ExcludesHigherLevels()
    {
        // Arrange
        string fakeJson = @"[{""Action"":""Save"",""Keys"":[""Ctrl+S""],""Level"":1},{""Action"":""Build"",""Keys"":[""Ctrl+Shift+B""],""Level"":2}]";
        var db = new Shortcutsy.Data.ShortcutDatabase("fake.json", 
            _ => fakeJson, 
            _ => true);
        db.LoadData();
        
        // Act
        var shortcuts = db.GetShortcutsForLevel(1);
        
        // Assert
        Assert.Single(shortcuts);
        Assert.Equal("Save", shortcuts[0].Action);
    }

    [Fact]
    public void GetMaxLevel_ReturnsCorrectMaxLevel()
    {
        // Arrange
        string fakeJson = @"[{""Action"":""Save"",""Keys"":[""Ctrl+S""],""Level"":1},{""Action"":""Build"",""Keys"":[""Ctrl+Shift+B""],""Level"":3}]";
        var db = new Shortcutsy.Data.ShortcutDatabase("fake.json", 
            _ => fakeJson, 
            _ => true);
        db.LoadData();
        
        // Act
        var maxLevel = db.GetMaxLevel();
        
        // Assert
        Assert.Equal(3, maxLevel);
    }
}
