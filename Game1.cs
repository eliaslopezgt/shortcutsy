#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Audio;
using Shortcutsy.Data;
using Shortcutsy.Entities;
using Shortcutsy.States;
using Shortcutsy.Rendering;
using Color = Microsoft.Xna.Framework.Color;
using TextRenderer = Shortcutsy.Rendering.TextRenderer;

namespace Shortcutsy;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private SpriteBatch _spriteBatch = null!;
    private TextRenderer _textRenderer = null!;
    private Texture2D _pixelTexture = null!;
    private Texture2D _asteroidTexture = null!;
    private Texture2D _launcherTexture = null!;
    private Texture2D _spaceshipTexture = null!;
    private SoundEffect? _explosionSound;
    private SoundEffect? _shootSound;
    private SoundEffect? _bgMusic;
    private SoundEffectInstance? _bgMusicInstance;
    
    private GameState _gameState = GameState.Title;
    private int _score;
    private int _level = 1;
    private int _lives = 3;
    private int _combo;
    private float _spawnTimer;
    private float _spawnInterval = 6f;
    private string _lastShortcutKey = "";
    private bool _muted;
    private float _screenShake;
    private float _redFlash;
    private Asteroid? _currentTarget;
    private float _muzzleFlashTimer;
    private bool _muzzleFlashLeft;
    
    private List<Asteroid> _asteroids = new();
    private List<Missile> _missiles = new();
    private List<Explosion> _explosions = new();
    private List<Star> _stars = new();
    private HighScoreManager _highScoreManager;
    
    private KeyboardState _lastKeyboardState;
    private float _keyPressTimer;
    private const float KeyPressDelay = 0.1f;
    
    private float _gameTime;
    private Random _random = new();
    private string _debugKeys = "";
    private List<float> _reactionTimes = new();
    private Dictionary<string, int> _wrongAttemptsPerShortcut = new();
    private Dictionary<string, int> _appearanceCountPerShortcut = new(); // How many times shortcut has appeared
    private Dictionary<string, int> _consecutiveHitsWithoutClue = new(); // CONSECUTIVE hits when clue NOT shown
    private int _pendingAsteroidSpawns;
    private const int ClueAppearances = 3; // Show clue first 3 times
    private float _baseSpeed = 30f;
    private List<ShortcutItem> _availableShortcuts = new();
    private HashSet<string> _masteredShortcuts = new(); // Shortcuts with 3 consecutive correct hits
    private float _levelTransitionTimer;
    private float _starSpeedMultiplier = 1f;
    private bool _levelTransitionTriggered;
    private int _maxLevel;
    private int _selectedLevel = 1;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        
        Window.Title = "Shortcutsy - Master Your Shortcuts";
        _graphics.PreferredBackBufferWidth = 640;
        _graphics.PreferredBackBufferHeight = 480;
        
        _highScoreManager = new HighScoreManager();
        _highScoreManager.Load();
        
        ShortcutDatabase.Initialize();
        
        var allShortcuts = ShortcutDatabase.Instance.GetShortcutsForLevel(10);
        _maxLevel = allShortcuts.Max(s => s.Level);
    }

    protected override void Initialize()
    {
        base.Initialize();
        CreateStarfield();
    }

    protected override void LoadContent()
    {
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        
        _pixelTexture = new Texture2D(GraphicsDevice, 1, 1);
        _pixelTexture.SetData(new[] { Color.White });
        
        _asteroidTexture = CreateAsteroidTexture();
        _launcherTexture = CreateLauncherTexture();
        _spaceshipTexture = CreateSpaceshipTexture();
        
        _textRenderer = new TextRenderer(GraphicsDevice);
        
        try
        {
            _explosionSound = CreateExplosionSound();
            _shootSound = CreateShootSound();
            
            try {
                using var stream = TitleContainer.OpenStream("Content/music.wav");
                _bgMusic = SoundEffect.FromStream(stream);
                _bgMusicInstance = _bgMusic.CreateInstance();
                _bgMusicInstance.IsLooped = true;
            } catch { _bgMusic = null; }
        }
        catch
        {
            _explosionSound = null;
            _shootSound = null;
        }
    }

    private SoundEffect CreateExplosionSound()
    {
        int sampleRate = 44100;
        int duration = 200;
        int samples = sampleRate * duration / 1000;
        var data = new byte[samples * 2];
        
        for (int i = 0; i < samples; i++)
        {
            double t = (double)i / sampleRate;
            double freq = 100 * Math.Exp(-t * 10);
            short sample = (short)(Math.Sin(2 * Math.PI * freq * t) * 3000 * Math.Exp(-t * 8));
            data[i * 2] = (byte)(sample & 0xFF);
            data[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }
        
        return CreateSoundEffectFromWav(data, sampleRate);
    }

    private SoundEffect CreateShootSound()
    {
        int sampleRate = 44100;
        int duration = 100;
        int samples = sampleRate * duration / 1000;
        var data = new byte[samples * 2];
        
        for (int i = 0; i < samples; i++)
        {
            double t = (double)i / sampleRate;
            double freq = 800 - t * 3000;
            short sample = (short)(Math.Sin(2 * Math.PI * freq * t) * 2000 * Math.Exp(-t * 20));
            data[i * 2] = (byte)(sample & 0xFF);
            data[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }
        
        return CreateSoundEffectFromWav(data, sampleRate);
    }

    private SoundEffect CreateSoundEffectFromWav(byte[] pcmData, int sampleRate)
    {
        using var stream = new System.IO.MemoryStream();
        using var writer = new System.IO.BinaryWriter(stream);
        
        writer.Write(new[] { 'R', 'I', 'F', 'F' });
        writer.Write(36 + pcmData.Length);
        writer.Write(new[] { 'W', 'A', 'V', 'E' });
        writer.Write(new[] { 'f', 'm', 't', ' ' });
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(sampleRate);
        writer.Write(sampleRate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write(new[] { 'd', 'a', 't', 'a' });
        writer.Write(pcmData.Length);
        writer.Write(pcmData);
        
        stream.Position = 0;
        return SoundEffect.FromStream(stream);
    }

    private Texture2D CreateAsteroidTexture()
    {
        Texture2D texture = new Texture2D(GraphicsDevice, 64, 64);
        Color[] data = new Color[64 * 64];
        
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                float dx = x - 32;
                float dy = y - 32;
                float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                
                if (dist < 28 && dist > 20)
                {
                    float noise = (float)_random.NextDouble() * 0.3f;
                    int gray = (int)(100 + noise * 50);
                    data[y * 64 + x] = new Color(gray, gray * 0.8f, gray * 0.7f);
                }
                else if (dist <= 20)
                {
                    data[y * 64 + x] = new Color(80, 70, 60);
                }
                else
                {
                    data[y * 64 + x] = Color.Transparent;
                }
            }
        }
        
        texture.SetData(data);
        return texture;
    }

    private Texture2D CreateLauncherTexture()
    {
        int width = 48;
        int height = 64;
        Texture2D texture = new Texture2D(GraphicsDevice, width, height);
        Color[] data = new Color[width * height];
        
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color c = Color.Transparent;
                
                // Base platform - concrete gray
                if (y >= 48)
                {
                    int shade = 40 + (x % 4) * 3;
                    c = new Color(shade, shade + 5, shade + 10);
                }
                // Main body - metallic bunker
                else if (y >= 20 && x >= 8 && x < 40)
                {
                    // Vertical panel lines
                    if ((x == 12 || x == 20 || x == 28 || x == 36) && y >= 25)
                        c = new Color(30, 35, 40);
                    else if (y >= 44)
                        c = new Color(25, 30, 35);  // Darker bottom
                    else if (y >= 30 && x >= 10 && x < 38)
                        c = new Color(45, 50, 55);  // Highlight
                    else
                        c = new Color(35, 40, 45);  // Base metal
                }
                // Barrel - darker metal with glow tip
                else if (y < 20 && y >= 8 && x >= 18 && x < 30)
                {
                    if (y < 10)
                        c = new Color(0, 255, 255);  // Cyan glow tip
                    else if (x == 18 || x == 29)
                        c = new Color(20, 25, 30);  // Barrel edge
                    else
                        c = new Color(30, 35, 40);  // Barrel body
                }
                // Top details
                else if (y >= 16 && y < 22 && x >= 12 && x < 36)
                {
                    if (x == 14 || x == 15 || x == 32 || x == 33)
                        c = new Color(60, 60, 60);  // Antenna mounts
                }
                // Rivets
                else if ((x == 10 || x == 37) && (y == 25 || y == 35 || y == 45))
                {
                    c = new Color(50, 50, 50);
                }
                
                data[y * width + x] = c;
            }
        }
        
        texture.SetData(data);
        return texture;
    }
    
    private Texture2D CreateSpaceshipTexture()
    {
        int size = 128;
        Texture2D texture = new Texture2D(GraphicsDevice, size, size);
        Color[] data = new Color[size * size];
        
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float cx = x - size / 2;
                float cy = y - size / 2;
                float dist = (float)Math.Sqrt(cx * cx + cy * cy);
                
                Color c = Color.Transparent;
                
                // Main body - elongated teardrop shape
                float bodyDist = Math.Abs(cx) / 60f + Math.Abs(cy - 5) / 50f;
                
                if (bodyDist < 1.0f)
                {
                    // Green metallic hull with shading
                    float shade = 1f - (cy + 20) / 80f;
                    int green = (int)(60 + shade * 100);
                    int gray = (int)(40 + shade * 30);
                    c = new Color(gray, green, gray);
                }
                
                // Cockpit dome - grey glass with highlight
                float domeY = (cy - 10) / 35f;
                float domeX = cx / 25f;
                float domeDist = domeY * domeY + domeX * domeX;
                if (domeDist < 0.8f && cy < 20)
                {
                    // Gradient from light to dark
                    float highlight = 1f - (cy + 15) / 55f;
                    int gray = (int)(160 + highlight * 60);
                    c = new Color(gray - 20, gray, gray + 30);
                }
                
                // Side wings
                if (cy > -10 && cy < 40 && Math.Abs(cx) > 35 && Math.Abs(cx) < 55)
                {
                    float wingShade = 1f - (cy + 10) / 50f;
                    int green = (int)(50 + wingShade * 80);
                    c = new Color(30, green, 30);
                }
                
                // Wing tips
                if (cy > 0 && cy < 30 && Math.Abs(cx) > 50 && Math.Abs(cx) < 60)
                {
                    c = new Color(80, 40, 30);
                }
                
                // Engine exhaust ports
                if (cy > 45 && cy < 55 && Math.Abs(cx) < 12)
                {
                    float exhaust = (cy - 45) / 10f;
                    int glow = (int)(100 + exhaust * 155);
                    c = new Color(glow, glow * 2, glow);
                }
                
                // Antenna
                if (cy < -25 && cy > -40 && Math.Abs(cx) < 2)
                {
                    c = new Color(100, 100, 100);
                }
                
                // Antenna tip light
                if (cy <= -38 && Math.Abs(cx) < 3)
                {
                    c = new Color(255, 0, 0);
                }
                
                data[y * size + x] = c;
            }
        }
        
        texture.SetData(data);
        return texture;
    }

    private void CreateStarfield()
    {
        _stars.Clear();
        
        // Regular white stars
        for (int i = 0; i < 25; i++)
        {
            _stars.Add(new Star
            {
                Position = new Vector3(
                    _random.Next(640),
                    _random.Next(480),
                    _random.Next(100)
                ),
                Size = _random.Next(1, 2),
                Brightness = (float)_random.NextDouble() * 0.5f + 0.5f,
                TwinkleSpeed = _random.Next(1, 5),
                TwinkleOffset = (float)_random.NextDouble() * MathF.PI * 2
            });
        }
        
        // Blue-ish stars (distant)
        for (int i = 0; i < 10; i++)
        {
            _stars.Add(new Star
            {
                Position = new Vector3(
                    _random.Next(640),
                    _random.Next(480),
                    _random.Next(80)
                ),
                Size = _random.Next(1, 2),
                Brightness = (float)_random.NextDouble() * 0.3f + 0.3f,
                TwinkleSpeed = _random.Next(2, 4),
                TwinkleOffset = (float)_random.NextDouble() * MathF.PI * 2
            });
        }
        
        // Some brighter "near" stars
        for (int i = 0; i < 5; i++)
        {
            _stars.Add(new Star
            {
                Position = new Vector3(
                    _random.Next(640),
                    _random.Next(480),
                    _random.Next(50) + 50
                ),
                Size = _random.Next(2, 3),
                Brightness = (float)_random.NextDouble() * 0.3f + 0.7f,
                TwinkleSpeed = _random.Next(1, 3),
                TwinkleOffset = (float)_random.NextDouble() * MathF.PI * 2
            });
        }
    }

    protected override void Update(GameTime gameTime)
    {
        float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
        _gameTime += deltaTime;
        
        KeyboardState keyboardState = Keyboard.GetState();
        
        if (keyboardState.IsKeyDown(Keys.Escape) && !_lastKeyboardState.IsKeyDown(Keys.Escape))
        {
            if (_gameState == GameState.Playing)
                _gameState = GameState.Paused;
            else if (_gameState == GameState.Paused)
                _gameState = GameState.Playing;
        }
        
        if (keyboardState.IsKeyDown(Keys.M) && !_lastKeyboardState.IsKeyDown(Keys.M))
        {
            _muted = !_muted;
            if (_bgMusicInstance != null)
            {
                if (_muted)
                    _bgMusicInstance.Pause();
                else if (_gameState == GameState.Playing)
                    _bgMusicInstance.Play();
            }
        }
        
        if (_gameState == GameState.Title || _gameState == GameState.GameOver)
        {
            if (keyboardState.IsKeyDown(Keys.Enter) && !_lastKeyboardState.IsKeyDown(Keys.Enter))
            {
                _selectedLevel = 1;
                StartGame(_selectedLevel);
            }
            if (keyboardState.IsKeyDown(Keys.L) && !_lastKeyboardState.IsKeyDown(Keys.L))
            {
                _gameState = GameState.LevelSelect;
            }
            if (_gameState == GameState.GameOver && keyboardState.IsKeyDown(Keys.Escape) && !_lastKeyboardState.IsKeyDown(Keys.Escape))
            {
                _gameState = GameState.Title;
            }
        }
        else if (_gameState == GameState.LevelSelect)
        {
            if (keyboardState.IsKeyDown(Keys.Enter) && !_lastKeyboardState.IsKeyDown(Keys.Enter))
            {
                StartGame(_selectedLevel);
            }
            if (keyboardState.IsKeyDown(Keys.Escape) && !_lastKeyboardState.IsKeyDown(Keys.Escape))
            {
                _gameState = GameState.Title;
            }
            if (keyboardState.IsKeyDown(Keys.Left) && !_lastKeyboardState.IsKeyDown(Keys.Left))
            {
                if (_selectedLevel > 1) _selectedLevel--;
            }
            if (keyboardState.IsKeyDown(Keys.Right) && !_lastKeyboardState.IsKeyDown(Keys.Right))
            {
                if (_selectedLevel < _maxLevel) _selectedLevel++;
            }
            
            // Allow typing level number directly
            int typedLevel = 0;
            if (keyboardState.IsKeyDown(Keys.D1) && !_lastKeyboardState.IsKeyDown(Keys.D1)) typedLevel = 1;
            else if (keyboardState.IsKeyDown(Keys.D2) && !_lastKeyboardState.IsKeyDown(Keys.D2)) typedLevel = 2;
            else if (keyboardState.IsKeyDown(Keys.D3) && !_lastKeyboardState.IsKeyDown(Keys.D3)) typedLevel = 3;
            else if (keyboardState.IsKeyDown(Keys.D4) && !_lastKeyboardState.IsKeyDown(Keys.D4)) typedLevel = 4;
            else if (keyboardState.IsKeyDown(Keys.D5) && !_lastKeyboardState.IsKeyDown(Keys.D5)) typedLevel = 5;
            else if (keyboardState.IsKeyDown(Keys.D6) && !_lastKeyboardState.IsKeyDown(Keys.D6)) typedLevel = 6;
            else if (keyboardState.IsKeyDown(Keys.D7) && !_lastKeyboardState.IsKeyDown(Keys.D7)) typedLevel = 7;
            else if (keyboardState.IsKeyDown(Keys.D8) && !_lastKeyboardState.IsKeyDown(Keys.D8)) typedLevel = 8;
            else if (keyboardState.IsKeyDown(Keys.D9) && !_lastKeyboardState.IsKeyDown(Keys.D9)) typedLevel = 9;
            else if (keyboardState.IsKeyDown(Keys.D0) && !_lastKeyboardState.IsKeyDown(Keys.D0)) typedLevel = 10;
            
            if (typedLevel > 0 && typedLevel <= _maxLevel)
            {
                _selectedLevel = typedLevel;
            }
        }
        else if (_gameState == GameState.Playing)
        {
            _keyPressTimer -= deltaTime;
            
            HandleKeyboardInput(keyboardState);
            
            UpdateGame(deltaTime);
        }
        else if (_gameState == GameState.LevelTransition)
        {
            UpdateLevelTransition(deltaTime);
        }
        else if (_gameState == GameState.NewRecord)
        {
            if (keyboardState.IsKeyDown(Keys.Enter) && !_lastKeyboardState.IsKeyDown(Keys.Enter))
            {
                _gameState = GameState.GameOver;
            }
        }
        
        _lastKeyboardState = keyboardState;
        
        if (_screenShake > 0) _screenShake -= deltaTime * 10;
        if (_redFlash > 0) _redFlash -= deltaTime * 2;
        if (_muzzleFlashTimer > 0) _muzzleFlashTimer -= deltaTime;
        
        base.Update(gameTime);
    }

    private void HandleKeyboardInput(KeyboardState keyboardState)
    {
        var pressedKeys = keyboardState.GetPressedKeys().ToList();
        _debugKeys = string.Join(",", pressedKeys.Select(k => k.ToString()));
        
        if (_keyPressTimer > 0 && pressedKeys.Count > 0) return;
        
        var newKeys = pressedKeys
            .Where(k => !_lastKeyboardState.GetPressedKeys().Contains(k))
            .ToList();
        
        if (newKeys.Count == 0) return;
        
        bool currentTargetLocked = false;
        if (_currentTarget != null && _currentTarget.Shortcut != null)
        {
            string shortcutKey = _currentTarget.Shortcut.GetShortcutString();
            int wrongForThis = _wrongAttemptsPerShortcut.ContainsKey(shortcutKey) ? _wrongAttemptsPerShortcut[shortcutKey] : 0;
            int consecutiveHits = _consecutiveHitsWithoutClue.ContainsKey(shortcutKey) ? _consecutiveHitsWithoutClue[shortcutKey] : 0;
            // Lock target if: showing clue (wrong >= 2 or appearance < 3) OR already mastered
            bool isMastered = _masteredShortcuts.Contains(shortcutKey);
            if (_currentTarget.ShowShortcut || isMastered || consecutiveHits >= 3)
            {
                currentTargetLocked = true;
            }
        }
        
        if (!currentTargetLocked)
        {
            _currentTarget = _asteroids.Where(a => a.IsActive).OrderBy(a => a.Position.Z).FirstOrDefault();
        }
        
        _keyPressTimer = KeyPressDelay;
        
        foreach (var asteroid in _asteroids.Where(a => a.IsActive).OrderBy(a => a.Position.Z))
        {
            if (asteroid.Shortcut == null) continue;
            
            var requiredKeys = asteroid.Shortcut.KeyCombo;
            
            bool match = requiredKeys.All(rk => pressedKeys.Contains(rk)) &&
                        requiredKeys.Count == pressedKeys.Count;
            
            if (match)
            {
                float reactionTime = _gameTime - asteroid.SpawnTime;
                _reactionTimes.Add(reactionTime);
                if (_reactionTimes.Count > 10) _reactionTimes.RemoveAt(0);
                
                string shortcutKey = asteroid.Shortcut.GetShortcutString();
                
                // Reset wrong attempts on success
                _wrongAttemptsPerShortcut[shortcutKey] = 0;
                
                // Track consecutive hits when clue NOT shown
                if (!asteroid.ShowShortcut)
                {
                    if (!_consecutiveHitsWithoutClue.ContainsKey(shortcutKey))
                        _consecutiveHitsWithoutClue[shortcutKey] = 0;
                    _consecutiveHitsWithoutClue[shortcutKey]++;
                    
                    // Check if mastered (3 consecutive hits without clue)
                    if (_consecutiveHitsWithoutClue[shortcutKey] >= 3)
                    {
                        _masteredShortcuts.Add(shortcutKey);
                    }
                }
                else
                {
                    // If clue was shown, reset consecutive counter
                    _consecutiveHitsWithoutClue[shortcutKey] = 0;
                }
                
                FireMissile(asteroid);
                _combo++;
                asteroid.WrongAttempts = 0;
                CheckSpeedIncrease();
                return;
            }
        }
        
        if (_currentTarget != null && _currentTarget.Shortcut != null)
        {
            _currentTarget.WrongAttempts++;
            string shortcutKey = _currentTarget.Shortcut.GetShortcutString();
            if (!_wrongAttemptsPerShortcut.ContainsKey(shortcutKey))
                _wrongAttemptsPerShortcut[shortcutKey] = 0;
            _wrongAttemptsPerShortcut[shortcutKey]++;
            
            // Reset consecutive hits counter - user saw the clue
            _consecutiveHitsWithoutClue[shortcutKey] = 0;
        }
        
        _combo = 0;
    }
    
    private void CheckSpeedIncrease()
    {
        if (_reactionTimes.Count < 6) return;
        
        float avg = _reactionTimes.Average();
        float variance = _reactionTimes.Select(t => (t - avg) * (t - avg)).Average();
        float stdDev = (float)Math.Sqrt(variance);
        
        // If player is doing well (avg < 2s, consistent), speed up faster
        if (stdDev < 0.5f && avg < 2.0f)
        {
            _baseSpeed *= 1.1f;
            _reactionTimes.Clear();
        }
        // If player is doing very well (avg < 1.5s), speed up even faster
        else if (stdDev < 0.4f && avg < 1.5f)
        {
            _baseSpeed *= 1.15f;
            _reactionTimes.Clear();
        }
    }

    private void FireMissile(Asteroid target)
    {
        bool fromLeft = _random.Next(2) == 0;
        
        Missile missile = new Missile
        {
            Position = new Vector3(fromLeft ? 50 : 590, 440, 0),
            TargetPosition = target.Position,
            TargetAsteroid = target
        };
        
        _missiles.Add(missile);
        
        _muzzleFlashTimer = 0.1f;
        _muzzleFlashLeft = fromLeft;
        
        if (!_muted && _shootSound != null)
            _shootSound.Play();
    }

    private void UpdateGame(float deltaTime)
    {
        // Skip spawning during level transition
        if (_gameState != GameState.LevelTransition)
        {
            _spawnTimer += deltaTime;
            
            float currentInterval = _spawnInterval - (_level - 1) * 0.2f;
            currentInterval = Math.Max(currentInterval, 2.0f);
            
            int activeAsteroids = _asteroids.Count(a => a.IsActive && a.Shortcut != null);
            
            if (_spawnTimer >= currentInterval && activeAsteroids < 3)
            {
                SpawnAsteroid();
                _spawnTimer = 0;
            }
        }
        
        foreach (var asteroid in _asteroids)
        {
            asteroid.Update(deltaTime);
            
            if (asteroid.IsActive && asteroid.HasReachedPlayer())
            {
                asteroid.IsActive = false;
                _pendingAsteroidSpawns++;
                _lives--;
                _redFlash = 1f;
                _combo = 0;
                
                if (_lives <= 0)
                {
                    GameOver();
                }
            }
        }
        
        foreach (var missile in _missiles)
        {
            missile.Update(deltaTime);
            
            if (!missile.IsActive && missile.TargetAsteroid != null && !missile.TargetAsteroid.IsActive)
            {
                CreateExplosion(missile.TargetAsteroid.Position);
                _pendingAsteroidSpawns++;
                
                float timeBonus = Math.Max(0, 3f - (_gameTime - missile.TargetAsteroid.SpawnTime)) / 3f * 50;
                int baseScore = 100;
                int comboBonus = _combo * 25;
                int levelMultiplier = _level;
                
                _score += (baseScore + (int)timeBonus + comboBonus) * levelMultiplier;
                
                _screenShake = 1f;
            }
        }
        
        foreach (var explosion in _explosions)
        {
            explosion.Update(deltaTime);
        }
        
        _missiles.RemoveAll(m => !m.IsActive);
        _asteroids.RemoveAll(a => !a.IsActive);
        _explosions.RemoveAll(e => !e.IsActive);
        
        while (_pendingAsteroidSpawns > 0)
        {
            SpawnAsteroid();
            _pendingAsteroidSpawns--;
        }
        
        if (_currentTarget != null && !_currentTarget.IsActive)
        {
            _currentTarget = _asteroids.Where(a => a.IsActive).OrderBy(a => a.Position.Z).FirstOrDefault();
        }
    }

    private void UpdateAvailableShortcuts()
    {
        // Check if any shortcut from CURRENT level has been mastered (3 CONSECUTIVE hits WITHOUT clue)
        var currentLevelShortcuts = _availableShortcuts.Where(s => s.Level == _level).ToList();
        foreach (var shortcut in currentLevelShortcuts)
        {
            string key = shortcut.GetShortcutString();
            int consecutiveHits = _consecutiveHitsWithoutClue.ContainsKey(key) ? _consecutiveHitsWithoutClue[key] : 0;
            
            if (consecutiveHits >= 3 && !_masteredShortcuts.Contains(key))
            {
                _masteredShortcuts.Add(key);
            }
        }
        
        // Check if all shortcuts in current level are mastered
        if (!_levelTransitionTriggered && _gameState == GameState.Playing)
        {
            var allMastered = currentLevelShortcuts.All(s => _masteredShortcuts.Contains(s.GetShortcutString()));
            
            if (allMastered && currentLevelShortcuts.Count > 0)
            {
                TriggerLevelTransition();
            }
        }
    }
    
    private void TriggerLevelTransition()
    {
        _levelTransitionTriggered = true;
        _gameState = GameState.LevelTransition;
        _levelTransitionTimer = 0f;
        _starSpeedMultiplier = 5f;
        
        // Create big explosion in center of screen
        var mainExplosion = new Explosion
        {
            Position = new Vector3(320, 240, 50),
            Size = 200f,
            MaxTime = 1.5f
        };
        
        // Add particles for big explosion
        for (int i = 0; i < 50; i++)
        {
            float angle = (float)_random.NextDouble() * MathF.PI * 2;
            float speed = 50f + (float)_random.NextDouble() * 150f;
            mainExplosion.Particles.Add(new ExplosionParticle
            {
                Position = new Vector3(320, 240, 50),
                Velocity = new Vector3(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed, 0),
                Color = new Color(1f, 0.5f + (float)_random.NextDouble() * 0.5f, 0),
                Size = 3f + (float)_random.NextDouble() * 5f,
                Life = 0.5f + (float)_random.NextDouble() * 1f
            });
        }
        _explosions.Add(mainExplosion);
        
        // Add more explosions for effect
        for (int i = 0; i < 5; i++)
        {
            var explosion = new Explosion
            {
                Position = new Vector3(
                    320 + ((float)_random.NextDouble() - 0.5f) * 200,
                    240 + ((float)_random.NextDouble() - 0.5f) * 150,
                    50
                ),
                Size = 100f,
                MaxTime = 1.5f
            };
            
            for (int j = 0; j < 20; j++)
            {
                float angle = (float)_random.NextDouble() * MathF.PI * 2;
                float speed = 30f + (float)_random.NextDouble() * 100f;
                explosion.Particles.Add(new ExplosionParticle
                {
                    Position = explosion.Position,
                    Velocity = new Vector3(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed, 0),
                    Color = new Color(1f, 0.3f + (float)_random.NextDouble() * 0.7f, 0),
                    Size = 2f + (float)_random.NextDouble() * 4f,
                    Life = 0.3f + (float)_random.NextDouble() * 0.8f
                });
            }
            _explosions.Add(explosion);
        }
        
        // Screen shake
        _screenShake = 20f;
        
        // Play explosion sound
        if (_explosionSound != null)
        {
            _explosionSound.Play();
        }
    }
    
    private void UpdateLevelTransition(float deltaTime)
    {
        _levelTransitionTimer += deltaTime;
        
        // Slow down stars over time
        _starSpeedMultiplier = MathF.Max(1f, 5f - _levelTransitionTimer * 4f);
        
        // After 1 second, show level announcement (continue stars fast)
        // After 2.5 seconds, move to next level
        if (_levelTransitionTimer >= 2.5f)
        {
            if (_level < _maxLevel)
            {
                _level++;
                
                // Clear all existing spaceships
                _asteroids.Clear();
                _missiles.Clear();
                
                // Reset spawn timer so new level starts fresh
                _spawnTimer = 0;
                
                // Replace shortcuts with ONLY the new level's shortcuts
                var allShortcuts = ShortcutDatabase.Instance.GetShortcutsForLevel(10);
                _availableShortcuts = allShortcuts.Where(s => s.Level == _level).ToList();
            }
            
            _levelTransitionTriggered = false;
            _starSpeedMultiplier = 1f;
            _gameState = GameState.Playing;
        }
    }
    
    private void SpawnAsteroid()
    {
        // Update available shortcuts
        UpdateAvailableShortcuts();
        
        // Only use shortcuts from CURRENT level that are NOT mastered
        var choices = _availableShortcuts
            .Where(s => s.Level == _level && !_masteredShortcuts.Contains(s.GetShortcutString()))
            .Where(s => s.GetShortcutString() != _lastShortcutKey)
            .ToList();
        
        if (choices.Count == 0) 
            choices = _availableShortcuts
                .Where(s => s.Level == _level && !_masteredShortcuts.Contains(s.GetShortcutString()))
                .ToList();
        
        // If less than 4 shortcuts on screen, allow mastered shortcuts as review
        int activeCount = _asteroids.Count(a => a.IsActive && a.Shortcut != null);
        if (choices.Count == 0 && activeCount < 4 && _masteredShortcuts.Count > 0)
        {
            // Allow some mastered shortcuts as review
            choices = _availableShortcuts
                .Where(s => _masteredShortcuts.Contains(s.GetShortcutString()))
                .Where(s => s.Level == _level)
                .Where(s => s.GetShortcutString() != _lastShortcutKey)
                .ToList();
        }
        
        if (choices.Count == 0) 
            choices = _availableShortcuts;
        
        var shortcut = choices[_random.Next(choices.Count)];
        _lastShortcutKey = shortcut.GetShortcutString();
        
        float x;
        float y;
        int attempts = 0;
        
        do
        {
            x = _random.Next(100, 540);
            y = _random.Next(80, 350);
            attempts++;
        } while (attempts < 10 && _asteroids.Any(a => a.IsActive && 
            Math.Abs(a.Position.X - x) < 80 && Math.Abs(a.Position.Y - y) < 60));
        
        float startZ = 500f;
        
        float speed = _baseSpeed + _level * 5f;
        speed = Math.Min(speed, 120f);
        
        // Determine if we show the clue
        string shortcutKey = shortcut.GetShortcutString();
        
        // Increment appearance count
        if (!_appearanceCountPerShortcut.ContainsKey(shortcutKey))
            _appearanceCountPerShortcut[shortcutKey] = 0;
        _appearanceCountPerShortcut[shortcutKey]++;
        
        int appearanceCount = _appearanceCountPerShortcut[shortcutKey];
        int wrongCount = _wrongAttemptsPerShortcut.ContainsKey(shortcutKey) ? _wrongAttemptsPerShortcut[shortcutKey] : 0;
        bool isMastered = _masteredShortcuts.Contains(shortcutKey);
        
        // Show clue: first 3 appearances OR if 2+ wrong attempts, but NOT if mastered
        bool showShortcut = !isMastered && (appearanceCount <= ClueAppearances || wrongCount >= 2);
        
        Asteroid asteroid = new Asteroid
        {
            Position = new Vector3(x, y, startZ),
            Velocity = new Vector3(_random.Next(-10, 10), _random.Next(0, 5), -speed),
            Shortcut = shortcut,
            ShowShortcut = showShortcut,
            Size = _random.Next(25, 40),
            SpawnTime = _gameTime,
            WrongAttempts = wrongCount
        };
        
        _asteroids.Add(asteroid);
        
        if (_currentTarget == null || !_currentTarget.IsActive)
        {
            _currentTarget = asteroid;
        }
    }

    private void CreateExplosion(Vector3 position)
    {
        Explosion explosion = new Explosion
        {
            Position = position,
            MaxTime = 0.8f,
            Size = 50f
        };
        
        for (int i = 0; i < 50; i++)
        {
            Vector3 velocity = new Vector3(
                (float)(_random.NextDouble() - 0.5f) * 300,
                (float)(_random.NextDouble() - 0.5f) * 300,
                (float)(_random.NextDouble() - 0.5f) * 100
            );
            
            Color color;
            float t = (float)_random.NextDouble();
            if (t < 0.3f)
                color = Color.Yellow;
            else if (t < 0.7f)
                color = new Color(255, 100, 0);
            else
                color = Color.Red;
            
            explosion.Particles.Add(new ExplosionParticle
            {
                Position = position,
                Velocity = velocity,
                Life = (float)_random.NextDouble() * 0.5f + 0.3f,
                MaxLife = 0.8f,
                Color = color,
                Size = _random.Next(3, 8)
            });
        }
        
        _explosions.Add(explosion);
        
        if (!_muted && _explosionSound != null)
            _explosionSound.Play();
    }

    private void StartGame(int startingLevel = 1)
    {
        _gameState = GameState.Playing;
        _score = 0;
        _level = startingLevel;
        _lives = 3;
        _combo = 0;
        _spawnTimer = 0;
        _gameTime = 0;
        _asteroids.Clear();
        _missiles.Clear();
        _explosions.Clear();
        _wrongAttemptsPerShortcut.Clear();
        _appearanceCountPerShortcut.Clear();
        _consecutiveHitsWithoutClue.Clear();
        _reactionTimes.Clear();
        _currentTarget = null;
        _lastShortcutKey = "";
        _masteredShortcuts.Clear();
        _levelTransitionTimer = 0;
        _starSpeedMultiplier = 1f;
        _levelTransitionTriggered = false;
        
        var allShortcuts = ShortcutDatabase.Instance.GetShortcutsForLevel(10);
        
        // Initialize with shortcuts for the starting level
        _availableShortcuts = allShortcuts.Where(s => s.Level == startingLevel).ToList();
        
        if (_bgMusicInstance != null && !_muted)
        {
            _bgMusicInstance.Play();
        }
    }

    private void GameOver()
    {
        bool isNewRecord = _highScoreManager.AddScore(_score, _level);
        
        if (isNewRecord)
        {
            _gameState = GameState.NewRecord;
        }
        else
        {
            _gameState = GameState.GameOver;
        }
        
        if (_bgMusicInstance != null)
            _bgMusicInstance.Stop();
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(5, 5, 16));
        
        Matrix transform = Matrix.Identity;
        if (_screenShake > 0)
        {
            float shakeX = (float)(_random.NextDouble() - 0.5f) * 10 * _screenShake;
            float shakeY = (float)(_random.NextDouble() - 0.5f) * 10 * _screenShake;
            transform = Matrix.CreateTranslation(shakeX, shakeY, 0);
        }
        
        _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, null, null, null, null, transform);
        
        DrawStarfield();
        
        if (_gameState == GameState.Playing || _gameState == GameState.Paused || _gameState == GameState.LevelTransition)
        {
            DrawLaunchers();
            DrawAsteroids();
            DrawMissiles();
            DrawExplosions();
            DrawHUD();
        }
        
        if (_redFlash > 0)
        {
            _spriteBatch.Draw(_pixelTexture, new Rectangle(0, 0, 640, 480), 
                new Color((byte)255, (byte)0, (byte)0, (byte)(_redFlash * 100)));
        }
        
        _spriteBatch.End();
        
        _spriteBatch.Begin();
        
        if (_gameState == GameState.Title)
        {
            DrawTitleScreen();
        }
        else if (_gameState == GameState.LevelSelect)
        {
            DrawLevelSelectScreen();
        }
        else if (_gameState == GameState.GameOver)
        {
            DrawGameOverScreen();
        }
        else if (_gameState == GameState.Paused)
        {
            DrawPausedScreen();
        }
        else if (_gameState == GameState.LevelTransition)
        {
            DrawLevelTransitionScreen();
        }
        else if (_gameState == GameState.NewRecord)
        {
            DrawNewRecordScreen();
        }
        
        if (_gameState != GameState.Playing)
        {
            DrawScanlines();
        }
        
        _spriteBatch.End();
        
        base.Draw(gameTime);
    }

    private void DrawStarfield()
    {
        foreach (var star in _stars)
        {
            star.Position.Z -= 50f * 0.016f * _starSpeedMultiplier;
            if (star.Position.Z <= 0)
            {
                star.Position.Z = 100f;
                star.Position.X = 320 + (float)(_random.NextDouble() - 0.5) * 200;
                star.Position.Y = 240 + (float)(_random.NextDouble() - 0.5) * 150;
            }
            
            float scale = 100f / star.Position.Z;
            float x = 320 + (star.Position.X - 320) * scale;
            float y = 240 + (star.Position.Y - 240) * scale;
            
            if (x < -10 || x > 650 || y < -10 || y > 490) continue;
            
            float twinkle = MathF.Sin(_gameTime * star.TwinkleSpeed + star.TwinkleOffset) * 0.3f + 0.7f;
            float brightness = star.Brightness * twinkle * scale;
            brightness = Math.Min(1f, brightness);
            
            int size = Math.Max(1, (int)(star.Size * scale));
            
            // Different star colors based on position/Index
            int starIndex = _stars.IndexOf(star);
            Color color;
            if (starIndex < 25)
            {
                // White stars
                color = new Color(brightness, brightness, brightness);
            }
            else if (starIndex < 35)
            {
                // Blue-ish distant stars
                color = new Color(brightness * 0.7f, brightness * 0.8f, brightness);
            }
            else
            {
                // Bright close stars - slight cyan
                color = new Color(brightness * 0.8f, brightness, brightness);
            }
            
            _spriteBatch.Draw(_pixelTexture, new Rectangle((int)x, (int)y, size, size), color);
        }
    }

    private void DrawLaunchers()
    {
        // Left launcher
        _spriteBatch.Draw(_launcherTexture, new Vector2(20, 400), Color.White);
        
        // Right launcher (flipped)
        _spriteBatch.Draw(_launcherTexture, new Vector2(572, 400), null, Color.White, 0, Vector2.Zero, 1, SpriteEffects.FlipHorizontally, 0);
        
        // Base platforms
        _spriteBatch.Draw(_pixelTexture, new Rectangle(10, 455, 60, 25), new Color(40, 45, 50));
        _spriteBatch.Draw(_pixelTexture, new Rectangle(570, 455, 60, 25), new Color(40, 45, 50));
        
        // Muzzle flash
        if (_muzzleFlashTimer > 0)
        {
            float flashSize = _muzzleFlashTimer * 400;
            Color flashColor = new Color((byte)0, (byte)255, (byte)255, (byte)(_muzzleFlashTimer * 10 * 255));
            if (_muzzleFlashLeft)
            {
                _spriteBatch.Draw(_pixelTexture, new Rectangle(35, 385, (int)flashSize, (int)(flashSize/2)), flashColor);
            }
            else
            {
                _spriteBatch.Draw(_pixelTexture, new Rectangle(600 - (int)flashSize, 385, (int)flashSize, (int)(flashSize/2)), flashColor);
            }
        }
    }

    private void DrawAsteroids()
    {
        var activeAsteroids = _asteroids.Where(a => a.IsActive && a.Shortcut != null).OrderBy(a => a.Position.Z).ToList();
        
        for (int i = 0; i < activeAsteroids.Count; i++)
        {
            var asteroid = activeAsteroids[i];
            
            float progress = 1f - (asteroid.Position.Z + 100f) / 600f;
            progress = Math.Max(0, Math.Min(1, progress));
            
            // Make spaceship smaller but text bigger
            float scale = 0.2f + progress * 0.4f;
            scale = Math.Max(0.2f, scale);
            
            // Calculate time until impact (Z goes from ~500 to -50, velocity is negative)
            float speed = Math.Abs(asteroid.Velocity.Z);
            float secondsLeft = (asteroid.Position.Z + 50) / speed;
            
            // Flash red when 5 seconds or less left
            bool isDanger = secondsLeft <= 5f;
            bool blinkOn = !isDanger || ((int)(_gameTime * 8) % 2 == 0);
            
            int shipSize = (int)(96 * scale);  // Smaller base size
            Vector2 shipPos = new Vector2(asteroid.Position.X - shipSize/2, asteroid.Position.Y - shipSize/2);
            
            if (isDanger && blinkOn)
            {
                _spriteBatch.Draw(_spaceshipTexture, shipPos, null, new Color(255, 100, 100), 0, Vector2.Zero, scale, SpriteEffects.None, 0);
            }
            else
            {
                _spriteBatch.Draw(_spaceshipTexture, shipPos, null, Color.White, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
            }
            
            string actionText = asteroid.Shortcut!.Action;
            string shortcutKey = asteroid.Shortcut!.GetShortcutString();
            
            // Use the ShowShortcut that was set when asteroid was created
            // (based on appearance count and wrong attempts)
            
            // Draw text BELOW the spaceship, large and readable with black background
            float textScale = 1.0f + progress * 0.5f;
            textScale = Math.Max(1.0f, textScale);
            
            var actionSize = _textRenderer.MeasureString(actionText);
            float actionWidth = actionSize.X * textScale;
            float actionHeight = actionSize.Y * textScale;
            
            // Position text below the spaceship
            float textPosY = asteroid.Position.Y + shipSize * 0.8f;
            
            // Draw action text
            Vector2 actionPos = new Vector2(asteroid.Position.X - actionWidth/2, textPosY - actionHeight/2);
            int bgPad = 6;
            _spriteBatch.Draw(_pixelTexture, new Rectangle((int)actionPos.X - bgPad, (int)actionPos.Y - bgPad, (int)actionWidth + bgPad*2, (int)actionHeight + bgPad*2), new Color(0, 0, 0));
            _textRenderer.DrawString(_spriteBatch, actionText, new Vector2(actionPos.X - 1, actionPos.Y - 1), Color.Black, false, false, false, textScale);
            _textRenderer.DrawString(_spriteBatch, actionText, new Vector2(actionPos.X + 1, actionPos.Y - 1), Color.Black, false, false, false, textScale);
            _textRenderer.DrawString(_spriteBatch, actionText, new Vector2(actionPos.X - 1, actionPos.Y + 1), Color.Black, false, false, false, textScale);
            _textRenderer.DrawString(_spriteBatch, actionText, new Vector2(actionPos.X + 1, actionPos.Y + 1), Color.Black, false, false, false, textScale);
            _textRenderer.DrawString(_spriteBatch, actionText, actionPos, Color.White, false, false, false, textScale);
            
            // Draw shortcut on line below if needed
            if (asteroid.ShowShortcut)
            {
                var shortcutSize = _textRenderer.MeasureString(shortcutKey);
                float shortcutWidth = shortcutSize.X * textScale * 0.8f;
                float shortcutHeight = shortcutSize.Y * textScale * 0.8f;
                
                float shortcutTextPosY = textPosY + actionHeight * 0.8f;
                Vector2 shortcutPos = new Vector2(asteroid.Position.X - shortcutWidth/2, shortcutTextPosY - shortcutHeight/2);
                
                _spriteBatch.Draw(_pixelTexture, new Rectangle((int)shortcutPos.X - bgPad, (int)shortcutPos.Y - bgPad, (int)shortcutWidth + bgPad*2, (int)shortcutHeight + bgPad*2), new Color(0, 0, 0));
                _textRenderer.DrawString(_spriteBatch, shortcutKey, new Vector2(shortcutPos.X - 1, shortcutPos.Y - 1), Color.Black, false, false, false, textScale * 0.8f);
                _textRenderer.DrawString(_spriteBatch, shortcutKey, new Vector2(shortcutPos.X + 1, shortcutPos.Y - 1), Color.Black, false, false, false, textScale * 0.8f);
                _textRenderer.DrawString(_spriteBatch, shortcutKey, new Vector2(shortcutPos.X - 1, shortcutPos.Y + 1), Color.Black, false, false, false, textScale * 0.8f);
                _textRenderer.DrawString(_spriteBatch, shortcutKey, new Vector2(shortcutPos.X + 1, shortcutPos.Y + 1), Color.Black, false, false, false, textScale * 0.8f);
                _textRenderer.DrawString(_spriteBatch, shortcutKey, shortcutPos, new Color(180, 180, 180), false, false, false, textScale * 0.8f);
            }
        }
    }

    private void DrawMissiles()
    {
        foreach (var missile in _missiles.Where(m => m.IsActive))
        {
            for (int i = 0; i < missile.Trail.Count; i++)
            {
                float alpha = (float)i / missile.Trail.Count * 0.5f;
                Vector2 pos = new Vector2(missile.Trail[i].X, missile.Trail[i].Y);
                _spriteBatch.Draw(_pixelTexture, pos, new Color((byte)0, (byte)255, (byte)255, (byte)(alpha * 255)));
            }
            
            _spriteBatch.Draw(_pixelTexture, new Vector2(missile.Position.X, missile.Position.Y), new Color((byte)0, (byte)255, (byte)255));
        }
    }

    private void DrawExplosions()
    {
        foreach (var explosion in _explosions)
        {
            float progress = explosion.Time / explosion.MaxTime;
            
            foreach (var particle in explosion.Particles)
            {
                float alpha = particle.Life / particle.MaxLife;
                Color color = new Color(
                    particle.Color.R,
                    particle.Color.G,
                    particle.Color.B,
                    (byte)(alpha * 255)
                );
                
                int size = (int)(particle.Size * (1 + progress));
                _spriteBatch.Draw(_pixelTexture, 
                    new Rectangle((int)particle.Position.X, (int)particle.Position.Y, size, size), 
                    color);
            }
        }
    }

    private void DrawHUD()
    {
        string scoreText = $"SCORE: {_score:D6}";
        string levelText = $"LEVEL: {_level}";
        string livesText = $"LIVES: {new string('♥', _lives)}";
        
        _textRenderer.DrawString(_spriteBatch, scoreText, new Vector2(10, 10), Color.White);
        _textRenderer.DrawString(_spriteBatch, levelText, new Vector2(280, 10), Color.White);
        _textRenderer.DrawString(_spriteBatch, livesText, new Vector2(520, 10), Color.Red);
        
        if (!string.IsNullOrEmpty(_debugKeys))
        {
            _textRenderer.DrawString(_spriteBatch, "DEBUG: " + _debugKeys, new Vector2(10, 55), Color.Cyan);
        }
        
        if (_combo > 1)
        {
            string comboText = $"COMBO x{_combo}";
            _textRenderer.DrawString(_spriteBatch, comboText, new Vector2(10, 35), Color.Yellow);
        }
        
        if (_reactionTimes.Count > 0)
        {
            float avgReaction = _reactionTimes.Average();
            string reactionText = $"AVG: {avgReaction:F2}s";
            _textRenderer.DrawString(_spriteBatch, reactionText, new Vector2(10, 75), Color.Gray);
        }
        
        string hintText = _muted ? "[M] Unmute" : "[M] Mute";
        _textRenderer.DrawString(_spriteBatch, hintText, new Vector2(320 - _textRenderer.MeasureString(hintText).X / 2, 460), new Color(100, 100, 100));
    }

    private void DrawTitleScreen()
    {
        string title = "SHORTCUTSY";
        string subtitle = "Master Your Shortcuts";
        string prompt = "Press ENTER to start";
        
        float titleX = 320 - _textRenderer.MeasureString(title, huge: true).X / 2;
        
        float pulse = (float)Math.Sin(_gameTime * 3);
        float glowIntensity = pulse * 0.3f + 0.7f;
        
        // Draw glow behind title
        for (int i = 3; i >= 1; i--)
        {
            float glowSize = i * 8;
            var glowColor = new Microsoft.Xna.Framework.Color((byte)0, (byte)(100 * glowIntensity), (byte)(100 * glowIntensity), (byte)(50 * glowIntensity));
            _textRenderer.DrawString(_spriteBatch, title, new Vector2(titleX - glowSize/2, 140 - glowSize/2), glowColor, huge: true);
        }
        
        // Draw black outline
        for (int ox = -2; ox <= 2; ox++)
        {
            for (int oy = -2; oy <= 2; oy++)
            {
                if (ox == 0 && oy == 0) continue;
                _textRenderer.DrawString(_spriteBatch, title, new Vector2(titleX + ox, 140 + oy), Color.Black, huge: true);
            }
        }
        
        // Draw crown on top
        float crownX = titleX + 10;
        float crownY = 120;
        DrawCrown(crownX, crownY, true);
        
        _textRenderer.DrawString(_spriteBatch, title, new Vector2(titleX, 140), new Color(0, 255, 255), huge: true);
        
        // Subtitle with glow and outline
        Color subtitleColor = new Color(0, (byte)(200 * glowIntensity), (byte)(136 * glowIntensity));
        _textRenderer.DrawString(_spriteBatch, subtitle, new Vector2(320 - _textRenderer.MeasureString(subtitle).X / 2 + 1, 211), Color.Black);
        _textRenderer.DrawString(_spriteBatch, subtitle, new Vector2(320 - _textRenderer.MeasureString(subtitle).X / 2, 210), subtitleColor);
        
        float promptPulse = (float)Math.Sin(_gameTime * 3) * 0.3f + 0.7f;
        _textRenderer.DrawString(_spriteBatch, prompt, new Vector2(320 - _textRenderer.MeasureString(prompt).X / 2, 290), 
            new Color((byte)(255 * promptPulse), (byte)(255 * promptPulse), (byte)(255 * promptPulse)));
        
        string levelSelectHint = "Press L to select level";
        _textRenderer.DrawString(_spriteBatch, levelSelectHint, new Vector2(320 - _textRenderer.MeasureString(levelSelectHint).X / 2, 340), new Color(80, 80, 80));
    }
    
    private void DrawCrown(float x, float y, bool tilted = false)
    {
        if (tilted)
        {
            // Tilted crown like wearing it - draw at an angle using offset rectangles
            // Main crown points - tilted
            _spriteBatch.Draw(_pixelTexture, new Rectangle((int)x, (int)y, 4, 8), new Color(255, 215, 0));
            _spriteBatch.Draw(_pixelTexture, new Rectangle((int)x + 6, (int)y - 2, 4, 7), new Color(255, 220, 0));
            _spriteBatch.Draw(_pixelTexture, new Rectangle((int)x + 12, (int)y - 1, 4, 6), new Color(255, 215, 0));
            _spriteBatch.Draw(_pixelTexture, new Rectangle((int)x + 18, (int)y - 2, 4, 7), new Color(255, 220, 0));
            _spriteBatch.Draw(_pixelTexture, new Rectangle((int)x + 24, (int)y, 4, 6), new Color(255, 215, 0));
            // Crown base - tilted
            _spriteBatch.Draw(_pixelTexture, new Rectangle((int)x - 2, (int)y + 6, 32, 5), new Color(255, 180, 0));
            _spriteBatch.Draw(_pixelTexture, new Rectangle((int)x, (int)y + 11, 28, 3), new Color(200, 150, 0));
        }
        else
        {
            // Draw crown as if the letter is wearing it
            // Gold crown with points
            for (int i = 0; i < 5; i++)
            {
                int cx = (int)x + i * 5;
                _spriteBatch.Draw(_pixelTexture, new Rectangle(cx, (int)y, 3, 6), new Color(255, 215, 0));
            }
            // Crown base
            _spriteBatch.Draw(_pixelTexture, new Rectangle((int)x - 3, (int)y + 5, 30, 5), new Color(255, 180, 0));
            // Crown band
            _spriteBatch.Draw(_pixelTexture, new Rectangle((int)x - 2, (int)y + 10, 28, 3), new Color(200, 150, 0));
        }
    }

    private void DrawGameOverScreen()
    {
        string gameOver = "GAME OVER";
        string finalScore = $"Final Score: {_score}";
        string prompt = "Press ENTER to restart";
        string backHint = "Press ESC for title";
        
        _textRenderer.DrawString(_spriteBatch, gameOver, new Vector2(320 - _textRenderer.MeasureString(gameOver, true).X / 2, 150), new Color(255, 0, 68), true);
        _textRenderer.DrawString(_spriteBatch, finalScore, new Vector2(320 - _textRenderer.MeasureString(finalScore).X / 2, 220), Color.White);
        
        float pulse = (float)Math.Sin(_gameTime * 3) * 0.3f + 0.7f;
        _textRenderer.DrawString(_spriteBatch, prompt, new Vector2(320 - _textRenderer.MeasureString(prompt).X / 2, 280), 
            new Color((byte)(255 * pulse), (byte)(255 * pulse), (byte)(255 * pulse)));
        
        _textRenderer.DrawString(_spriteBatch, backHint, new Vector2(320 - _textRenderer.MeasureString(backHint).X / 2, 310), new Color(80, 80, 80));
        
        DrawHighScores(350);
    }

    private void DrawNewRecordScreen()
    {
        string newRecord = "NEW RECORD!";
        float pulse = (float)Math.Sin(_gameTime * 6) * 0.2f + 0.8f;
        _textRenderer.DrawString(_spriteBatch, newRecord, new Vector2(320 - _textRenderer.MeasureString(newRecord, true).X / 2, 80), 
            new Color((byte)(255 * pulse), (byte)(215 * pulse), 0), true);

        DrawGoldMedal(320 - 50, 180, 100);

        string scoreText = $"Score: {_score}";
        _textRenderer.DrawString(_spriteBatch, scoreText, new Vector2(320 - _textRenderer.MeasureString(scoreText).X / 2, 320), Color.White);

        string prompt = "Press ENTER to continue";
        float promptPulse = (float)Math.Sin(_gameTime * 3) * 0.3f + 0.7f;
        _textRenderer.DrawString(_spriteBatch, prompt, new Vector2(320 - _textRenderer.MeasureString(prompt).X / 2, 380), 
            new Color((byte)(255 * promptPulse), (byte)(255 * promptPulse), (byte)(255 * promptPulse)));
    }

    private void DrawGoldMedal(float x, float y, int size)
    {
        Color goldColor = new Color(255, 215, 0);
        Color goldShine = new Color(255, 255, 150);
        Color goldDark = new Color(200, 150, 0);

        for (int dy = 0; dy < size; dy++)
        {
            for (int dx = 0; dx < size; dx++)
            {
                float dist = (float)Math.Sqrt((dx - size/2) * (dx - size/2) + (dy - size/2) * (dy - size/2));
                if (dist < size/2 && dist > size/2 - 6)
                {
                    _spriteBatch.Draw(_pixelTexture, new Rectangle((int)x + dx, (int)y + dy, 1, 1), goldColor);
                }
                else if (dist < size/2 - 6)
                {
                    _spriteBatch.Draw(_pixelTexture, new Rectangle((int)x + dx, (int)y + dy, 1, 1), goldShine);
                }
            }
        }

        _spriteBatch.Draw(_pixelTexture, new Rectangle((int)x + size/2 - 10, (int)y + size - 5, 20, 8), goldDark);
        
        _spriteBatch.Draw(_pixelTexture, new Rectangle((int)x + 10, (int)y + 8, 8, 8), Color.White);
        
        string num = "1";
        _textRenderer.DrawString(_spriteBatch, num, new Vector2(x + size/2 - 8, y + size/2 - 15), goldDark, huge: true);
    }

    private void DrawPausedScreen()
    {
        string paused = "PAUSED";
        string prompt = "Press ESC to resume";
        
        _textRenderer.DrawString(_spriteBatch, paused, new Vector2(320 - _textRenderer.MeasureString(paused, true).X / 2, 200), new Color(255, 255, 0), true);
        _textRenderer.DrawString(_spriteBatch, prompt, new Vector2(320 - _textRenderer.MeasureString(prompt).X / 2, 260), Color.White);
    }

    private void DrawLevelTransitionScreen()
    {
        string levelText;
        Color levelColor;
        
        if (_levelTransitionTimer < 1.5f)
        {
            levelText = "LEVEL COMPLETE!";
            levelColor = new Color(255, 255, 0);
        }
        else if (_level >= _maxLevel)
        {
            levelText = "YOU WIN!";
            levelColor = new Color(0, 255, 0);
        }
        else
        {
            levelText = $"LEVEL {_level + 1}";
            levelColor = new Color(0, 255, 255);
        }
        
        float scale = 1f + MathF.Sin(_levelTransitionTimer * 8) * 0.1f;
        float yPos = 200;
        
        _textRenderer.DrawString(_spriteBatch, levelText, new Vector2(320 - _textRenderer.MeasureString(levelText).X / 2, yPos), levelColor, false, false, false, scale);
        
        if (_levelTransitionTimer > 1f && _level < _maxLevel)
        {
            string getReady = "GET READY!";
            float pulse = (float)Math.Sin(_gameTime * 6) * 0.3f + 0.7f;
            _textRenderer.DrawString(_spriteBatch, getReady, new Vector2(320 - _textRenderer.MeasureString(getReady).X / 2, 280), 
                new Color((byte)(255 * pulse), (byte)(255 * pulse), (byte)(255 * pulse)));
        }
    }

    private void DrawLevelSelectScreen()
    {
        string title = "SELECT LEVEL";
        _textRenderer.DrawString(_spriteBatch, title, new Vector2(320 - _textRenderer.MeasureString(title, true).X / 2, 50), new Color(0, 255, 255), true);

        string subtitle = "Use LEFT/RIGHT arrows or type number";
        _textRenderer.DrawString(_spriteBatch, subtitle, new Vector2(320 - _textRenderer.MeasureString(subtitle).X / 2, 90), new Color(150, 150, 150));

        string levelText = $"LEVEL {_selectedLevel}";
        float pulse = (float)Math.Sin(_gameTime * 4) * 0.15f + 0.85f;
        _textRenderer.DrawString(_spriteBatch, levelText, new Vector2(320 - _textRenderer.MeasureString(levelText, huge: true).X / 2, 140), 
            new Color((byte)(255), (byte)(200 * pulse), (byte)(0)), huge: true);

        var allLevelShortcuts = ShortcutDatabase.Instance.GetShortcutsForLevel(10).Where(s => s.Level == _selectedLevel).ToList();
        if (allLevelShortcuts.Count > 0)
        {
            string previewLabel = "Practice:";
            _textRenderer.DrawString(_spriteBatch, previewLabel, new Vector2(320 - _textRenderer.MeasureString(previewLabel).X / 2, 210), new Color(100, 100, 100));
            
            int maxShow = Math.Min(5, allLevelShortcuts.Count);
            for (int i = 0; i < maxShow; i++)
            {
                var sc = allLevelShortcuts[i];
                string shortcutPreview = $"{sc.Action}: {sc.GetShortcutString()}";
                _textRenderer.DrawString(_spriteBatch, shortcutPreview, new Vector2(320 - _textRenderer.MeasureString(shortcutPreview, small: true).X / 2, 235 + i * 14), new Color(160, 160, 160), small: true);
            }
        }

        int levelsPerRow = 5;
        int startY = 320;
        int spacing = 70;
        for (int i = 1; i <= _maxLevel; i++)
        {
            int row = (i - 1) / levelsPerRow;
            int col = (i - 1) % levelsPerRow;
            int x = 120 + col * spacing;
            int y = startY + row * 55;

            int levelScore = _highScoreManager.GetScoreForLevel(i);
            
            float hue = (_gameTime * 2 + i * 0.5f) % (MathF.PI * 2);
            byte r = (byte)(MathF.Sin(hue) * 127 + 128);
            byte g = (byte)(MathF.Sin(hue + MathF.PI * 2 / 3) * 127 + 128);
            byte b = (byte)(MathF.Sin(hue + MathF.PI * 4 / 3) * 127 + 128);
            
            if (i == _selectedLevel)
            {
                _spriteBatch.Draw(_pixelTexture, new Rectangle(x - 8, y - 8, 36, 36), new Color(0, 120, 120));
                _spriteBatch.Draw(_pixelTexture, new Rectangle(x - 4, y - 4, 28, 28), new Color(0, 80, 80));
                _textRenderer.DrawString(_spriteBatch, i.ToString(), new Vector2(x, y), new Color(255, 255, 0));
            }
            else
            {
                for (int glow = 3; glow >= 1; glow--)
                {
                    var glowColor = new Microsoft.Xna.Framework.Color((byte)(r * glow * 0.3f), (byte)(g * glow * 0.3f), (byte)(b * glow * 0.3f), (byte)100);
                    _textRenderer.DrawString(_spriteBatch, i.ToString(), new Vector2(x - glow, y - glow), glowColor);
                }
                _textRenderer.DrawString(_spriteBatch, i.ToString(), new Vector2(x, y), new Microsoft.Xna.Framework.Color(r, g, b));
            }
            
            if (levelScore > 0)
            {
                string scoreText = levelScore.ToString("D5");
                _textRenderer.DrawString(_spriteBatch, scoreText, new Vector2(x - 2, y + 22), new Color(255, 215, 0), small: true);
            }
        }

        string prompt = "Press ENTER to start";
        float promptPulse = (float)Math.Sin(_gameTime * 3) * 0.3f + 0.7f;
        _textRenderer.DrawString(_spriteBatch, prompt, new Vector2(320 - _textRenderer.MeasureString(prompt).X / 2, 450), 
            new Color((byte)(255 * promptPulse), (byte)(255 * promptPulse), (byte)(255 * promptPulse)));

        string backHint = "Press ESC to go back";
        _textRenderer.DrawString(_spriteBatch, backHint, new Vector2(320 - _textRenderer.MeasureString(backHint).X / 2, 470), new Color(70, 70, 70));
    }
    
    private void DrawHighScores(int startY)
    {
        var scores = _highScoreManager.GetScores().Take(3).ToList();
        
        string header = "TOP 3";
        _textRenderer.DrawString(_spriteBatch, header, new Vector2(320 - _textRenderer.MeasureString(header).X / 2, startY), new Color(255, 200, 0));
        
        for (int i = 0; i < scores.Count; i++)
        {
            string scoreText = $"{scores[i].Score:D6} (L{scores[i].Level})";
            
            DrawMedal(320 - _textRenderer.MeasureString(scoreText).X / 2 - 30, startY + 25 + i * 30, i);
            
            _textRenderer.DrawString(_spriteBatch, scoreText, new Vector2(320 - _textRenderer.MeasureString(scoreText).X / 2, startY + 25 + i * 30), 
                i == 0 ? new Color(255, 215, 0) : (i == 1 ? new Color(192, 192, 192) : new Color(205, 127, 50)));
        }
    }
    
    private void DrawMedal(float x, float y, int rank)
    {
        Color medalColor = rank == 0 ? new Color(255, 215, 0) : (rank == 1 ? new Color(192, 192, 192) : new Color(205, 127, 50));
        Color shineColor = rank == 0 ? new Color(255, 255, 150) : (rank == 1 ? new Color(255, 255, 255) : new Color(255, 200, 100));
        int size = 20;
        
        // Outer ring
        for (int dy = 0; dy < size; dy++)
        {
            for (int dx = 0; dx < size; dx++)
            {
                float dist = (float)Math.Sqrt((dx - size/2) * (dx - size/2) + (dy - size/2) * (dy - size/2));
                if (dist < size/2 && dist > size/2 - 3)
                {
                    _spriteBatch.Draw(_pixelTexture, new Rectangle((int)x + dx, (int)y + dy, 1, 1), medalColor);
                }
                else if (dist < size/2 - 3)
                {
                    _spriteBatch.Draw(_pixelTexture, new Rectangle((int)x + dx, (int)y + dy, 1, 1), shineColor);
                }
            }
        }
        
        // Inner highlight
        _spriteBatch.Draw(_pixelTexture, new Rectangle((int)x + 6, (int)y + 4, 4, 4), Color.White);
        
        string num = (rank + 1).ToString();
        _textRenderer.DrawString(_spriteBatch, num, new Vector2(x + 6, y + 4), Color.Black, small: true, scale: 0.7f);
    }

    private void DrawScanlines()
    {
        for (int y = 0; y < 480; y += 4)
        {
            _spriteBatch.Draw(_pixelTexture, new Rectangle(0, y, 640, 2), new Color(0, 0, 0, 30));
        }
    }
}
