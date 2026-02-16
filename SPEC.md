# Shortcutsy - Game Specification

## 1. Project Overview

**Project Name:** Shortcutsy  
**Type:** Desktop arcade game (Windows)  
**Core Functionality:** A retro 80s Missile Command-style game where asteroids bearing Visual Studio keyboard shortcut prompts approach the player. Pressing the correct shortcut destroys the asteroid with 3D particle explosion effects.  
**Target Users:** Software developers wanting to memorize Visual Studio keyboard shortcuts through gamified practice.

---

## 2. UI/UX Specification

### 2.1 Window Model

- **Single window application** (like Minesweeper)
- Default size: 640x480 pixels
- Resizable: Yes, minimum 640x480
- Window title: "Shortcutsy - Master Your Shortcuts"
- Standard Windows title bar with native controls

### 2.2 Visual Design

#### Color Palette
| Role | Color | Hex |
|------|-------|-----|
| Background (space) | Deep black | `#050510` |
| Stars | White/pale blue | `#FFFFFF`, `#A0C4FF` |
| Ground/launchers | Dark cyan | `#0A3D3D` |
| Missiles (trail) | Cyan glow | `#00FFFF` |
| Explosions (inner) | Yellow | `#FFFF00` |
| Explosions (outer) | Orange/Red | `#FF4400`, `#FF0000` |
| Asteroid | Rocky brown/gray | `#8B7355`, `#6B5344` |
| Prompt text | Neon green | `#00FF88` |
| UI text (score/lives) | White | `#FFFFFF` |
| Game over text | Red | `#FF0044` |
| Title text | Cyan with glow | `#00FFFF` |
| Level numbers | Rainbow animated | RGB cycling |

#### Typography
- **Game prompts:** Consolas (monospace)
- **UI Elements:** Consolas, 14-18px
- **Title:** Impact, 52px bold with black outline and glow
- **Score display:** 24px bold

#### Visual Effects
- Scanline overlay (subtle CRT effect)
- Bloom/glow on explosions and missile trails
- Particle systems for explosions (50-100 particles)
- Starfield parallax background
- Screen shake on explosion
- Rainbow glow animation on level numbers
- Gold medal display for new records
- Title screen pulsing glow effect

### 2.3 Layout Structure

#### Title Screen
```
+----------------------------------+
|                                  |
|        SHORTCUTSY (glow)        |  <- Title with crown
|    Master Your Shortcuts        |  <- Subtitle
|     Press ENTER to start        |  <- Pulsing prompt
|      Press L to select level    |
|                                  |
|            TOP 3                 |  <- High scores (removed in v2)
+----------------------------------+
```

#### Level Select Screen
```
+----------------------------------+
|         SELECT LEVEL             |
|  Use LEFT/RIGHT arrows or type  |
|          LEVEL 5                |  <- Selected level (pulsing)
|     Practice:                    |
|     Save: Ctrl+S                |
|     Find: Ctrl+F                |
|     ...                         |
|   1   2   3   4   5            |  <- Level numbers (rainbow)
|   6   7   8   9   10           |
|   (high scores below each)      |
|     Press ENTER to start        |
+----------------------------------+
```

#### Gameplay Screen
```
+----------------------------------+
|  SCORE: 0000    LEVEL: 1  ♥♥♥   |  <- HUD (top)
+----------------------------------+
|                                  |
|           (3D GAME VIEW)         |
|                                  |
|    [asteroid] --> "Find Refs"   |
|           or                     |
|         "Ctrl+F"                 |
|                                  |
|           [missile]              |
|          /         \             |
|      (launchers at bottom)       |
|                                  |
+----------------------------------+
```

#### New Record Screen
```
+----------------------------------+
|          NEW RECORD!             |  <- Gold pulsing text
|                                  |
|              1                   |  <- Gold medal with #1
|          (medal)                 |
|         Score: 12345             |
|     Press ENTER to continue      |
+----------------------------------+
```

---

## 3. Functional Specification

### 3.1 Shortcut Database

10 levels of increasing difficulty with 5 shortcuts each (50 total):

- **Level 1-2**: Essential shortcuts (Ctrl+C, Ctrl+V, etc.)
- **Level 3**: 3-key shortcuts (Ctrl+Shift+B)
- **Level 4-10**: Function keys, debug commands, multi-key combos

### 3.2 Game States

| State | Description |
|-------|-------------|
| Title | Main menu with start prompt |
| LevelSelect | Choose starting level (1-10) |
| Playing | Active gameplay |
| Paused | Game paused (ESC) |
| GameOver | Shows score and high scores |
| LevelTransition | Animation between levels |
| NewRecord | Gold medal screen when beating a level's high score |

### 3.3 Game Loop

1. Wave starts - asteroids spawn at intervals
2. Asteroid appears with prompt (action name OR shortcut)
3. Player has ~3-5 seconds to press correct shortcut
4. **Correct press:**
   - Missile fires from launcher toward asteroid
   - Asteroid explodes in 3D particle effect
   - Score increases (+100 base, +bonus for speed)
5. **Wrong press:**
   - Asteroid continues approaching (no penalty)
6. **Missed (asteroid passes):**
   - Lives decrease
   - Brief screen flash red
7. Game ends when lives = 0

### 3.4 Difficulty Progression

| Level | Categories | Spawn Rate | Asteroid Speed |
|-------|------------|------------|----------------|
| 1 | Essential | 6 seconds | Slow |
| 2 | + Editing | 5.8 seconds | Slow |
| 3 | + Navigation | 5.6 seconds | Medium |
| 4 | + Debugging | 5.4 seconds | Medium |
| 5 | All | 5.2 seconds | Medium |
| 6+ | All | 5.0-2.0 seconds | Fast |

### 3.5 Scoring

- Base points per asteroid: 100
- Speed bonus: Up to +50 (faster = more points)
- Combo bonus: +25 per consecutive correct answer
- Level multiplier: level × base points

### 3.6 User Interactions

| Input | Action |
|-------|--------|
| Keyboard shortcut | Destroy matching asteroid |
| ESC | Pause/Resume game, or return to title from game over |
| ENTER | Start game / Restart after game over |
| L | Open level select screen |
| LEFT/RIGHT arrows | Navigate levels in level select |
| 1-9, 0 keys | Type level number directly |
| M | Mute/unmute audio |

### 3.7 Data Persistence

- High scores saved to `highscores.json` in app directory
- Best score **per level** (not top 10 overall)
- Format: `{ "1": 5000, "2": 3500, ... }` (level -> score)
- Level select screen displays each level's high score

---

## 4. Audio Specification

### Sound Effects
- **Explosion:** Retro 8-bit explosion sound
- **Missile fire:** Laser/rocket launch sound
- **Game over:** Descending tone
- **Correct answer:** Positive chime
- **Wrong key:** Error buzz (subtle)

### Background Music
- Retro synthwave loop
- Looping track during gameplay
- Mute toggle (M key) persists between sessions

---

## 5. Technical Stack

- **Framework:** MonoGame 3.8+
- **Language:** C# (.NET 9)
- **Build Output:** Windows .exe
- **Fonts:** System.Drawing (Consolas, Impact)

---

## 6. Acceptance Criteria

### Core Gameplay
- [x] Game launches as Windows desktop window (640x480)
- [x] Asteroids spawn and approach in 3D perspective
- [x] Each asteroid displays VS shortcut action or shortcut keys
- [x] Correct shortcut fires missile and destroys asteroid
- [x] 3D particle explosion on destruction
- [x] Score increases when asteroids destroyed
- [x] Lives decrease when asteroids pass
- [x] Game over at 0 lives
- [x] Press ENTER to start/restart

### Level Selection (v2)
- [x] Press L to open level select
- [x] Use LEFT/RIGHT arrows to navigate levels
- [x] Type 1-0 to directly select level
- [x] Each level shows its high score
- [x] Game starts with shortcuts from selected level

### Visual
- [x] Retro 80s Missile Command aesthetic
- [x] Neon cyan/red/yellow color scheme
- [x] Starfield background
- [x] HUD shows score, level, lives
- [x] Title screen with glow and black outline
- [x] Level numbers with rainbow glow animation

### Records & Progression
- [x] High scores tracked per level
- [x] Level select shows high score below each level
- [x] New record screen with gold medal when beating a level's record
- [x] ESC returns to title from game over

### Audio
- [x] Explosion sound effects
- [x] Missile fire sound
- [x] Background synthwave music
- [x] M key mute toggle

### Data
- [x] High scores saved to JSON
- [x] Best score per level stored and displayed
