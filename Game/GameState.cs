namespace Shortcutsy.States;

/// <summary>
/// Defines the various states the game can be in.
/// </summary>
public enum GameState
{
    Title,          // Main menu
    LevelSelect,   // Level selection screen (press L)
    Playing,       // Active gameplay
    Paused,        // Game paused (ESC)
    GameOver,      // Shows score and high scores
    LevelTransition, // Animation between levels
    NewRecord      // Gold medal screen when beating a level's record
}
