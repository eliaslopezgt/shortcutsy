using System;
using System.Collections.Generic;
using Xunit;

namespace Shortcutsy.Tests.Data;

public class HighScoreManagerTests
{
    [Fact]
    public void AddScore_NewScore_ReturnsTrue()
    {
        // Arrange
        var manager = new Shortcutsy.Data.HighScoreManager(
            _ => "{}",
            _ => { },
            _ => false);
        manager.Load();
        
        // Act
        bool isNewRecord = manager.AddScore(1000, 1);
        
        // Assert
        Assert.True(isNewRecord);
        Assert.Equal(1000, manager.GetScoreForLevel(1));
    }

    [Fact]
    public void AddScore_HigherScore_ReturnsTrue()
    {
        // Arrange
        var manager = new Shortcutsy.Data.HighScoreManager(
            _ => "{}",
            _ => { },
            _ => false);
        manager.Load();
        manager.AddScore(1000, 1);
        
        // Act
        bool isNewRecord = manager.AddScore(2000, 1);
        
        // Assert
        Assert.True(isNewRecord);
        Assert.Equal(2000, manager.GetScoreForLevel(1));
    }

    [Fact]
    public void AddScore_LowerScore_ReturnsFalse()
    {
        // Arrange
        var manager = new Shortcutsy.Data.HighScoreManager(
            _ => "{}",
            _ => { },
            _ => false);
        manager.Load();
        manager.AddScore(1000, 1);
        
        // Act
        bool isNewRecord = manager.AddScore(500, 1);
        
        // Assert
        Assert.False(isNewRecord);
        Assert.Equal(1000, manager.GetScoreForLevel(1));
    }

    [Fact]
    public void AddScore_DifferentLevels_TracksIndependently()
    {
        // Arrange
        var manager = new Shortcutsy.Data.HighScoreManager(
            _ => "{}",
            _ => { },
            _ => false);
        manager.Load();
        
        // Act
        manager.AddScore(1000, 1);
        manager.AddScore(2000, 2);
        
        // Assert
        Assert.Equal(1000, manager.GetScoreForLevel(1));
        Assert.Equal(2000, manager.GetScoreForLevel(2));
    }

    [Fact]
    public void GetScoreForLevel_MissingLevel_ReturnsZero()
    {
        // Arrange
        var manager = new Shortcutsy.Data.HighScoreManager(
            _ => "{}",
            _ => { },
            _ => false);
        manager.Load();
        
        // Act
        int score = manager.GetScoreForLevel(99);
        
        // Assert
        Assert.Equal(0, score);
    }
}
