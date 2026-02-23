import { Vec3, GameState } from './types';
import { Star, Asteroid, Missile, Explosion, ExplosionParticle } from './entities';
import { ShortcutDatabase } from './shortcutDatabase';
import { ShortcutItem } from './shortcutItem';
import { HighScoreManager } from './highScoreManager';
import { InputManager } from './inputManager';
import { AudioManager } from './audioManager';
import { Renderer } from './renderer';

export class Game {
  gameState: GameState = GameState.Title;
  score: number = 0;
  level: number = 1;
  lives: number = 3;
  combo: number = 0;
  spawnTimer: number = 0;
  spawnInterval: number = 6;
  lastShortcutKey: string = '';
  muted: boolean = false;
  screenShake: number = 0;
  redFlash: number = 0;
  currentTarget: Asteroid | null = null;
  muzzleFlashTimer: number = 0;
  muzzleFlashLeft: boolean = true;

  asteroids: Asteroid[] = [];
  missiles: Missile[] = [];
  explosions: Explosion[] = [];
  stars: Star[] = [];

  highScoreManager: HighScoreManager;
  input: InputManager;
  audio: AudioManager;
  renderer: Renderer;

  keyPressTimer: number = 0;
  KEY_PRESS_DELAY: number = 0.1;

  gameTime: number = 0;
  private _random: () => number = Math.random;
  debugKeys: string = '';
  reactionTimes: number[] = [];
  wrongAttemptsPerShortcut: Map<string, number> = new Map();
  appearanceCountPerShortcut: Map<string, number> = new Map();
  consecutiveHitsWithoutClue: Map<string, number> = new Map();
  pendingAsteroidSpawns: number = 0;
  CLUE_APPEARANCES: number = 3;
  baseSpeed: number = 30;
  availableShortcuts: ShortcutItem[] = [];
  masteredShortcuts: Set<string> = new Set();
  levelTransitionTimer: number = 0;
  starSpeedMultiplier: number = 1;
  levelTransitionTriggered: boolean = false;
  maxLevel: number = 1;
  selectedLevel: number = 1;

  private lastFrameTime: number = 0;

  constructor(private canvas: HTMLCanvasElement) {
    this.renderer = new Renderer(canvas);
    this.input = new InputManager();
    this.input.setGameActive(true);
    this.audio = new AudioManager();
    this.audio.loadMusic();
    this.highScoreManager = new HighScoreManager();

    this.highScoreManager.load();
    ShortcutDatabase.initialize();

    const allShortcuts = ShortcutDatabase.instance.getShortcutsForLevel(10);
    this.maxLevel = Math.max(...allShortcuts.map(s => s.level));

    this.createStarfield();

    this.lastFrameTime = performance.now();
    requestAnimationFrame((t) => this.gameLoop(t));
  }

  private gameLoop(timestamp: number): void {
    try {
      const deltaTime = Math.min((timestamp - this.lastFrameTime) / 1000, 0.1);
      this.lastFrameTime = timestamp;

      this.update(deltaTime);
      this.draw();
      this.input.endFrame();

      requestAnimationFrame((t) => this.gameLoop(t));
    } catch (e) {
      console.error('Game loop error:', e);
    }
  }

  private update(deltaTime: number): void {
    this.gameTime += deltaTime;

    if (this.input.isKeyJustPressed('Escape')) {
      if (this.gameState === GameState.Playing) {
        this.gameState = GameState.Paused;
      } else if (this.gameState === GameState.Paused) {
        this.gameState = GameState.Playing;
      } else if (this.gameState === GameState.GameOver) {
        this.audio.stopMusic();
        this.gameState = GameState.Title;
      } else if (this.gameState === GameState.LevelSelect) {
        this.gameState = GameState.Title;
      }
    }

    if (this.input.isKeyJustPressed('m')) {
      this.muted = !this.muted;
      this.audio.toggleMute();
    }

    if (this.gameState === GameState.Title || this.gameState === GameState.GameOver) {
      if (this.input.isKeyJustPressed('Enter')) {
        this.selectedLevel = 1;
        this.startGame(this.selectedLevel);
      }
      if (this.input.isKeyJustPressed('l')) {
        this.gameState = GameState.LevelSelect;
      }
    } else if (this.gameState === GameState.LevelSelect) {
      if (this.input.isKeyJustPressed('Enter')) {
        this.startGame(this.selectedLevel);
      }
      if (this.input.isKeyJustPressed('ArrowLeft')) {
        if (this.selectedLevel > 1) this.selectedLevel--;
      }
      if (this.input.isKeyJustPressed('ArrowRight')) {
        if (this.selectedLevel < this.maxLevel) this.selectedLevel++;
      }
      const digit = this.getDigitFromInput();
      if (digit > 0 && digit <= this.maxLevel) {
        this.selectedLevel = digit;
      }
    } else if (this.gameState === GameState.Playing) {
      this.keyPressTimer -= deltaTime;
      this.handleKeyboardInput();
      this.updateGame(deltaTime);
    } else if (this.gameState === GameState.LevelTransition) {
      this.updateLevelTransition(deltaTime);
    } else if (this.gameState === GameState.NewRecord) {
      if (this.input.isKeyJustPressed('Enter')) {
        this.gameState = GameState.GameOver;
      }
    }

    if (this.screenShake > 0) this.screenShake -= deltaTime * 10;
    if (this.redFlash > 0) this.redFlash -= deltaTime * 2;
    if (this.muzzleFlashTimer > 0) this.muzzleFlashTimer -= deltaTime;
  }

  private getDigitFromInput(): number {
    const pressed = this.input.getPressedKeys();
    if (pressed.includes('1')) return 1;
    if (pressed.includes('2')) return 2;
    if (pressed.includes('3')) return 3;
    if (pressed.includes('4')) return 4;
    if (pressed.includes('5')) return 5;
    if (pressed.includes('6')) return 6;
    if (pressed.includes('7')) return 7;
    if (pressed.includes('8')) return 8;
    if (pressed.includes('9')) return 9;
    if (pressed.includes('0')) return 10;
    return 0;
  }

  private handleKeyboardInput(): void {
    const pressedKeys = this.input.getPressedKeys();
    const newKeys = this.input.getNewlyPressedKeys();
    this.debugKeys = pressedKeys.join(',');

    if (this.keyPressTimer > 0 && pressedKeys.length > 0) return;
    if (newKeys.length === 0) return;

    let currentTargetLocked = false;
    if (this.currentTarget && this.currentTarget.shortcut) {
      const shortcutKey = this.currentTarget.shortcut.getShortcutString();
      const wrongForThis = this.wrongAttemptsPerShortcut.get(shortcutKey) || 0;
      const consecutiveHits = this.consecutiveHitsWithoutClue.get(shortcutKey) || 0;
      const isMastered = this.masteredShortcuts.has(shortcutKey);
      if (this.currentTarget.showShortcut || isMastered || consecutiveHits >= 3) {
        currentTargetLocked = true;
      }
    }

    if (!currentTargetLocked) {
      const activeAsteroids = this.asteroids.filter(a => a.isActive).sort((a, b) => a.position.z - b.position.z);
      this.currentTarget = activeAsteroids[0] || null;
    }

    this.keyPressTimer = this.KEY_PRESS_DELAY;

    const sortedAsteroids = this.asteroids.filter(a => a.isActive).sort((a, b) => a.position.z - b.position.z);

    for (const asteroid of sortedAsteroids) {
      if (!asteroid.shortcut) continue;

      const requiredKeys = asteroid.shortcut.keyCombo;
      const match = requiredKeys.every(rk => pressedKeys.includes(rk)) &&
                    requiredKeys.length === pressedKeys.length;

      if (match) {
        const reactionTime = this.gameTime - asteroid.spawnTime;
        this.reactionTimes.push(reactionTime);
        if (this.reactionTimes.length > 10) this.reactionTimes.shift();

        const shortcutKey = asteroid.shortcut.getShortcutString();
        this.wrongAttemptsPerShortcut.set(shortcutKey, 0);

        if (!asteroid.showShortcut) {
          const current = this.consecutiveHitsWithoutClue.get(shortcutKey) || 0;
          this.consecutiveHitsWithoutClue.set(shortcutKey, current + 1);
          if (this.consecutiveHitsWithoutClue.get(shortcutKey)! >= 3) {
            this.masteredShortcuts.add(shortcutKey);
          }
        } else {
          this.consecutiveHitsWithoutClue.set(shortcutKey, 0);
        }

        this.fireMissile(asteroid);
        this.combo++;
        asteroid.wrongAttempts = 0;
        this.checkSpeedIncrease();
        return;
      }
    }

    if (this.currentTarget && this.currentTarget.shortcut) {
      this.currentTarget.wrongAttempts++;
      const shortcutKey = this.currentTarget.shortcut.getShortcutString();
      const current = this.wrongAttemptsPerShortcut.get(shortcutKey) || 0;
      this.wrongAttemptsPerShortcut.set(shortcutKey, current + 1);
      this.consecutiveHitsWithoutClue.set(shortcutKey, 0);
    }

    this.combo = 0;
  }

  private checkSpeedIncrease(): void {
    if (this.reactionTimes.length < 6) return;

    const avg = this.reactionTimes.reduce((a, b) => a + b, 0) / this.reactionTimes.length;
    const variance = this.reactionTimes.reduce((a, b) => a + (b - avg) ** 2, 0) / this.reactionTimes.length;
    const stdDev = Math.sqrt(variance);

    if (stdDev < 0.5 && avg < 2.0) {
      this.baseSpeed *= 1.1;
      this.reactionTimes = [];
    } else if (stdDev < 0.4 && avg < 1.5) {
      this.baseSpeed *= 1.15;
      this.reactionTimes = [];
    }
  }

  private fireMissile(target: Asteroid): void {
    const fromLeft = this._random() < 0.5;

    const missile = new Missile();
    missile.position = new Vec3(fromLeft ? 50 : 590, 440, 0);
    missile.targetPosition = target.position.clone();
    missile.targetAsteroid = target;
    missile.velocity = new Vec3();
    missile.isActive = true;
    missile.trail = [];

    this.missiles.push(missile);
    this.muzzleFlashTimer = 0.1;
    this.muzzleFlashLeft = fromLeft;
    this.audio.playShoot();
  }

  private updateGame(deltaTime: number): void {
    if (this.gameState !== GameState.LevelTransition) {
      this.spawnTimer += deltaTime;
      let currentInterval = this.spawnInterval - (this.level - 1) * 0.2;
      currentInterval = Math.max(currentInterval, 2.0);

      const activeAsteroids = this.asteroids.filter(a => a.isActive && a.shortcut).length;

      if (this.spawnTimer >= currentInterval && activeAsteroids < 3) {
        this.spawnAsteroid();
        this.spawnTimer = 0;
      }
    }

    for (const asteroid of this.asteroids) {
      asteroid.update(deltaTime);

      if (asteroid.isActive && asteroid.hasReachedPlayer()) {
        asteroid.isActive = false;
        this.pendingAsteroidSpawns++;
        this.lives--;
        this.redFlash = 1;
        this.combo = 0;

        if (this.lives <= 0) {
          this.gameOver();
        }
      }
    }

    for (const missile of this.missiles) {
      missile.update(deltaTime);

      if (!missile.isActive && missile.targetAsteroid && !missile.targetAsteroid.isActive) {
        this.createExplosion(missile.targetAsteroid.position);
        this.pendingAsteroidSpawns++;

        const timeBonus = Math.max(0, 3 - (this.gameTime - missile.targetAsteroid.spawnTime)) / 3 * 50;
        const baseScore = 100;
        const comboBonus = this.combo * 25;
        const levelMultiplier = this.level;

        this.score += (baseScore + Math.floor(timeBonus) + comboBonus) * levelMultiplier;
        this.screenShake = 1;
      }
    }

    for (const explosion of this.explosions) {
      explosion.update(deltaTime);
    }

    this.missiles = this.missiles.filter(m => m.isActive);
    this.asteroids = this.asteroids.filter(a => a.isActive);
    this.explosions = this.explosions.filter(e => e.isActive);

    while (this.pendingAsteroidSpawns > 0) {
      this.spawnAsteroid();
      this.pendingAsteroidSpawns--;
    }

    if (this.currentTarget && !this.currentTarget.isActive) {
      const active = this.asteroids.filter(a => a.isActive).sort((a, b) => a.position.z - b.position.z);
      this.currentTarget = active[0] || null;
    }
  }

  private updateAvailableShortcuts(): void {
    const currentLevelShortcuts = this.availableShortcuts.filter(s => s.level === this.level);
    for (const shortcut of currentLevelShortcuts) {
      const key = shortcut.getShortcutString();
      const consecutiveHits = this.consecutiveHitsWithoutClue.get(key) || 0;

      if (consecutiveHits >= 3 && !this.masteredShortcuts.has(key)) {
        this.masteredShortcuts.add(key);
      }
    }

    if (!this.levelTransitionTriggered && this.gameState === GameState.Playing) {
      const allMastered = currentLevelShortcuts.every(s => this.masteredShortcuts.has(s.getShortcutString()));

      if (allMastered && currentLevelShortcuts.length > 0) {
        this.triggerLevelTransition();
      }
    }
  }

  private triggerLevelTransition(): void {
    this.levelTransitionTriggered = true;
    this.gameState = GameState.LevelTransition;
    this.levelTransitionTimer = 0;
    this.starSpeedMultiplier = 5;

    const mainExplosion = new Explosion();
    mainExplosion.position = new Vec3(320, 240, 50);
    mainExplosion.size = 200;
    mainExplosion.maxTime = 1.5;
    mainExplosion.time = 0;
    mainExplosion.isActive = true;
    mainExplosion.particles = [];

    for (let i = 0; i < 50; i++) {
      const particle = new ExplosionParticle();
      const angle = this._random() * Math.PI * 2;
      const speed = 50 + this._random() * 150;
      particle.position = new Vec3(320, 240, 50);
      particle.velocity = new Vec3(Math.cos(angle) * speed, Math.sin(angle) * speed, 0);
      particle.color = { r: 255, g: Math.floor(128 + this._random() * 127), b: 0 };
      particle.size = 3 + this._random() * 5;
      particle.life = 0.5 + this._random();
      particle.maxLife = 1.5;
      mainExplosion.particles.push(particle);
    }
    this.explosions.push(mainExplosion);

    for (let i = 0; i < 5; i++) {
      const explosion = new Explosion();
      explosion.position = new Vec3(320 + (this._random() - 0.5) * 200, 240 + (this._random() - 0.5) * 150, 50);
      explosion.size = 100;
      explosion.maxTime = 1.5;
      explosion.time = 0;
      explosion.isActive = true;
      explosion.particles = [];

      for (let j = 0; j < 20; j++) {
        const particle = new ExplosionParticle();
        const angle = this._random() * Math.PI * 2;
        const speed = 30 + this._random() * 100;
        particle.position = explosion.position.clone();
        particle.velocity = new Vec3(Math.cos(angle) * speed, Math.sin(angle) * speed, 0);
        particle.color = { r: 255, g: Math.floor(77 + this._random() * 178), b: 0 };
        particle.size = 2 + this._random() * 4;
        particle.life = 0.3 + this._random() * 0.8;
        particle.maxLife = 1.5;
        explosion.particles.push(particle);
      }
      this.explosions.push(explosion);
    }

    this.screenShake = 20;
    this.audio.playExplosion();
  }

  private updateLevelTransition(deltaTime: number): void {
    this.levelTransitionTimer += deltaTime;
    this.starSpeedMultiplier = Math.max(1, 5 - this.levelTransitionTimer * 4);

    if (this.levelTransitionTimer >= 2.5) {
      if (this.level < this.maxLevel) {
        this.level++;
        this.asteroids = [];
        this.missiles = [];
        this.spawnTimer = 0;

        const allShortcuts = ShortcutDatabase.instance.getShortcutsForLevel(10);
        this.availableShortcuts = allShortcuts.filter(s => s.level === this.level);
      }

      this.levelTransitionTriggered = false;
      this.starSpeedMultiplier = 1;
      this.gameState = GameState.Playing;
    }
  }

  private spawnAsteroid(): void {
    this.updateAvailableShortcuts();

    let choices = this.availableShortcuts
      .filter(s => s.level === this.level && !this.masteredShortcuts.has(s.getShortcutString()))
      .filter(s => s.getShortcutString() !== this.lastShortcutKey);

    if (choices.length === 0) {
      choices = this.availableShortcuts
        .filter(s => s.level === this.level && !this.masteredShortcuts.has(s.getShortcutString()));
    }

    const activeCount = this.asteroids.filter(a => a.isActive && a.shortcut).length;
    if (choices.length === 0 && activeCount < 4 && this.masteredShortcuts.size > 0) {
      choices = this.availableShortcuts
        .filter(s => this.masteredShortcuts.has(s.getShortcutString()))
        .filter(s => s.level === this.level)
        .filter(s => s.getShortcutString() !== this.lastShortcutKey);
    }

    // Fallback: use ALL shortcuts for current level, or all available
    if (choices.length === 0) {
      choices = this.availableShortcuts.filter(s => s.level === this.level);
    }
    if (choices.length === 0) {
      choices = [...this.availableShortcuts];
    }

    // Still no choices - abort spawn
    if (choices.length === 0) return;

    const shortcut = choices[Math.floor(this._random() * choices.length)];
    this.lastShortcutKey = shortcut.getShortcutString();

    let x: number, y: number;
    let attempts = 0;
    do {
      x = 100 + this._random() * 440;
      y = 80 + this._random() * 270;
      attempts++;
    } while (attempts < 10 && this.asteroids.some(a => a.isActive &&
      Math.abs(a.position.x - x) < 80 && Math.abs(a.position.y - y) < 60));

    const startZ = 500;
    let speed = this.baseSpeed + this.level * 5;
    speed = Math.min(speed, 120);

    const shortcutKey = shortcut.getShortcutString();
    const appearanceCount = (this.appearanceCountPerShortcut.get(shortcutKey) || 0) + 1;
    this.appearanceCountPerShortcut.set(shortcutKey, appearanceCount);

    const wrongCount = this.wrongAttemptsPerShortcut.get(shortcutKey) || 0;
    const isMastered = this.masteredShortcuts.has(shortcutKey);
    const showShortcut = !isMastered && (appearanceCount <= this.CLUE_APPEARANCES || wrongCount >= 2);

    const asteroid = new Asteroid();
    asteroid.position = new Vec3(x, y, startZ);
    asteroid.velocity = new Vec3((this._random() - 0.5) * 20, this._random() * 5, -speed);
    asteroid.shortcut = shortcut;
    asteroid.showShortcut = showShortcut;
    asteroid.size = 25 + Math.floor(this._random() * 15);
    asteroid.isActive = true;
    asteroid.spawnTime = this.gameTime;
    asteroid.wrongAttempts = wrongCount;

    this.asteroids.push(asteroid);

    if (!this.currentTarget || !this.currentTarget.isActive) {
      this.currentTarget = asteroid;
    }
  }

  private createExplosion(position: Vec3): void {
    const explosion = new Explosion();
    explosion.position = position.clone();
    explosion.maxTime = 0.8;
    explosion.size = 50;
    explosion.time = 0;
    explosion.isActive = true;
    explosion.particles = [];

    for (let i = 0; i < 50; i++) {
      const velocity = new Vec3(
        (this._random() - 0.5) * 300,
        (this._random() - 0.5) * 300,
        (this._random() - 0.5) * 100
      );

      let color: { r: number; g: number; b: number };
      const t = this._random();
      if (t < 0.3) {
        color = { r: 255, g: 255, b: 0 };
      } else if (t < 0.7) {
        color = { r: 255, g: 100, b: 0 };
      } else {
        color = { r: 255, g: 0, b: 0 };
      }

      const particle = new ExplosionParticle();
      particle.position = position.clone();
      particle.velocity = velocity;
      particle.life = this._random() * 0.5 + 0.3;
      particle.maxLife = 0.8;
      particle.color = color;
      particle.size = 3 + this._random() * 5;
      explosion.particles.push(particle);
    }

    this.explosions.push(explosion);
    this.audio.playExplosion();
  }

  private startGame(startingLevel: number = 1): void {
    this.gameState = GameState.Playing;
    this.score = 0;
    this.level = startingLevel;
    this.lives = 3;
    this.combo = 0;
    this.spawnTimer = 0;
    this.gameTime = 0;
    this.asteroids = [];
    this.missiles = [];
    this.explosions = [];
    this.wrongAttemptsPerShortcut = new Map();
    this.appearanceCountPerShortcut = new Map();
    this.consecutiveHitsWithoutClue = new Map();
    this.reactionTimes = [];
    this.currentTarget = null;
    this.lastShortcutKey = '';
    this.masteredShortcuts = new Set();
    this.levelTransitionTimer = 0;
    this.starSpeedMultiplier = 1;
    this.levelTransitionTriggered = false;

    const allShortcuts = ShortcutDatabase.instance.getShortcutsForLevel(10);
    this.availableShortcuts = allShortcuts.filter(s => s.level === startingLevel);

    this.audio.startMusic();
  }

  private gameOver(): void {
    this.audio.stopMusic();
    const isNewRecord = this.highScoreManager.addScore(this.score, this.level);

    if (isNewRecord) {
      this.gameState = GameState.NewRecord;
    } else {
      this.gameState = GameState.GameOver;
    }
  }

  private createStarfield(): void {
    this.stars = [];

    for (let i = 0; i < 25; i++) {
      const star = new Star();
      star.position = new Vec3(this._random() * 640, this._random() * 480, this._random() * 100);
      star.size = 1 + Math.floor(this._random() * 2);
      star.brightness = this._random() * 0.5 + 0.5;
      star.twinkleSpeed = 1 + Math.floor(this._random() * 5);
      star.twinkleOffset = this._random() * Math.PI * 2;
      this.stars.push(star);
    }

    for (let i = 0; i < 10; i++) {
      const star = new Star();
      star.position = new Vec3(this._random() * 640, this._random() * 480, this._random() * 80);
      star.size = 1 + Math.floor(this._random() * 2);
      star.brightness = this._random() * 0.3 + 0.3;
      star.twinkleSpeed = 2 + Math.floor(this._random() * 3);
      star.twinkleOffset = this._random() * Math.PI * 2;
      this.stars.push(star);
    }

    for (let i = 0; i < 5; i++) {
      const star = new Star();
      star.position = new Vec3(this._random() * 640, this._random() * 480, 50 + this._random() * 50);
      star.size = 2 + Math.floor(this._random() * 2);
      star.brightness = this._random() * 0.3 + 0.7;
      star.twinkleSpeed = 1 + Math.floor(this._random() * 3);
      star.twinkleOffset = this._random() * Math.PI * 2;
      this.stars.push(star);
    }
  }

  private draw(): void {
    this.renderer.clear(5, 5, 16);

    this.renderer.save();
    if (this.screenShake > 0) {
      const shakeX = (this._random() - 0.5) * 10 * this.screenShake;
      const shakeY = (this._random() - 0.5) * 10 * this.screenShake;
      this.renderer.translate(shakeX, shakeY);
    }

    this.renderer.setBlendMode('lighter');
    this.drawStarfield();

    if (this.gameState === GameState.Playing || this.gameState === GameState.Paused || this.gameState === GameState.LevelTransition) {
      this.drawLaunchers();
      this.drawAsteroids();
      this.drawMissiles();
      this.drawExplosions();
      this.drawHUD();
    }

    this.renderer.restore();
    this.renderer.setBlendMode('source-over');

    if (this.gameState === GameState.Title) {
      this.drawTitleScreen();
    } else if (this.gameState === GameState.LevelSelect) {
      this.drawLevelSelectScreen();
    } else if (this.gameState === GameState.GameOver) {
      this.drawGameOverScreen();
    } else if (this.gameState === GameState.Paused) {
      this.drawPausedScreen();
    } else if (this.gameState === GameState.LevelTransition) {
      this.drawLevelTransitionScreen();
    } else if (this.gameState === GameState.NewRecord) {
      this.drawNewRecordScreen();
    }

    if (this.redFlash > 0) {
      this.renderer.fillRect(0, 0, 640, 480, 255, 0, 0, this.redFlash * 100);
    }

    if (this.gameState !== GameState.Playing) {
      this.drawScanlines();
    }
  }

  private drawStarfield(): void {
    for (const star of this.stars) {
      star.position.z -= 50 * 0.016 * this.starSpeedMultiplier;
      if (star.position.z <= 0) {
        star.position.z = 100;
        star.position.x = 320 + (this._random() - 0.5) * 200;
        star.position.y = 240 + (this._random() - 0.5) * 150;
      }

      const scale = 100 / star.position.z;
      const x = 320 + (star.position.x - 320) * scale;
      const y = 240 + (star.position.y - 240) * scale;

      if (x < -10 || x > 650 || y < -10 || y > 490) continue;

      const twinkle = Math.sin(this.gameTime * star.twinkleSpeed + star.twinkleOffset) * 0.3 + 0.7;
      let brightness = star.brightness * twinkle * scale;
      brightness = Math.min(1, brightness);

      const size = Math.max(1, Math.floor(star.size * scale));

      const starIndex = this.stars.indexOf(star);
      let r = brightness * 255, g = brightness * 255, b = brightness * 255;

      if (starIndex >= 25) {
        r = brightness * 0.7 * 255;
        g = brightness * 0.8 * 255;
        b = brightness * 255;
      } else if (starIndex >= 35) {
        r = brightness * 0.8 * 255;
        g = brightness * 255;
        b = brightness * 255;
      }

      this.renderer.fillRect(x, y, size, size, Math.floor(r), Math.floor(g), Math.floor(b));
    }
  }

  private drawLaunchers(): void {
    this.renderer.fillRect(10, 455, 60, 25, 40, 45, 50);
    this.renderer.fillRect(570, 455, 60, 25, 40, 45, 50);

    this.drawLauncherAt(20, 400, false);
    this.drawLauncherAt(572, 400, true);

    if (this.muzzleFlashTimer > 0) {
      const flashSize = this.muzzleFlashTimer * 400;
      if (this.muzzleFlashLeft) {
        this.renderer.fillRect(35, 385, flashSize, flashSize / 2, 0, 255, 255, Math.floor(this.muzzleFlashTimer * 10 * 255));
      } else {
        this.renderer.fillRect(600 - flashSize, 385, flashSize, flashSize / 2, 0, 255, 255, Math.floor(this.muzzleFlashTimer * 10 * 255));
      }
    }
  }

  private drawLauncherAt(x: number, y: number, flipped: boolean): void {
    const w = 48, h = 64;
    const baseX = flipped ? x - 28 : x;

    this.renderer.fillRect(baseX + 8, y + 20, 32, 28, 35, 40, 45);
    this.renderer.fillRect(baseX + 8, y + 44, 32, 4, 25, 30, 35);
    this.renderer.fillRect(baseX + 10, y + 30, 28, 14, 45, 50, 55);

    for (let i = 0; i < 4; i++) {
      this.renderer.fillRect(baseX + 12 + i * 8, y + 25, 2, 10, 30, 35, 40);
    }

    this.renderer.fillRect(baseX + 18, y + 8, 12, 12, 30, 35, 40);
    this.renderer.fillRect(baseX + 19, y + 2, 10, 8, 0, 255, 255);

    this.renderer.fillRect(baseX + 10, y + 48, 4, 4, 50, 50, 50);
    this.renderer.fillRect(baseX + 34, y + 48, 4, 4, 50, 50, 50);
    this.renderer.fillRect(baseX + 10, y + 40, 4, 4, 50, 50, 50);
    this.renderer.fillRect(baseX + 34, y + 40, 4, 4, 50, 50, 50);
  }

  private drawAsteroids(): void {
    const activeAsteroids = this.asteroids
      .filter(a => a.isActive && a.shortcut)
      .sort((a, b) => a.position.z - b.position.z);

    for (const asteroid of activeAsteroids) {
      const progress = 1 - (asteroid.position.z + 100) / 600;
      const clampedProgress = Math.max(0, Math.min(1, progress));

      let scale = 0.2 + progress * 0.4;
      scale = Math.max(0.2, scale);

      const speed = Math.abs(asteroid.velocity.z);
      const secondsLeft = (asteroid.position.z + 50) / speed;

      const isDanger = secondsLeft <= 5;
      const blinkOn = !isDanger || Math.floor(this.gameTime * 8) % 2 === 0;

      const shipSize = Math.floor(96 * scale);
      const shipX = asteroid.position.x - shipSize / 2;
      const shipY = asteroid.position.y - shipSize / 2;

      const tintRed = isDanger && blinkOn;
      this.drawSpaceship(shipX, shipY, scale, tintRed);

      const textScale = 1.0 + progress * 0.5;
      const clampedTextScale = Math.max(1.0, textScale);

      const actionText = asteroid.shortcut!.action;
      const actionSize = this.renderer.measureNormal(actionText);
      const actionWidth = actionSize.width * clampedTextScale;
      const actionHeight = actionSize.height * clampedTextScale;

      const textPosY = asteroid.position.y + shipSize * 0.8;

      const bgPad = 6;
      const actionPosX = asteroid.position.x - actionWidth / 2;
      this.renderer.fillRect(actionPosX - bgPad, textPosY - actionHeight / 2 - bgPad, actionWidth + bgPad * 2, actionHeight + bgPad * 2, 0, 0, 0);

      this.renderer.drawTextOutlined(actionText, actionPosX - 1, textPosY - actionHeight / 2 - 1, 255, 255, 255, 255, 16, 'Consolas, monospace', clampedTextScale);
      this.renderer.drawTextOutlined(actionText, actionPosX + 1, textPosY - actionHeight / 2 - 1, 255, 255, 255, 255, 16, 'Consolas, monospace', clampedTextScale);
      this.renderer.drawTextOutlined(actionText, actionPosX - 1, textPosY - actionHeight / 2 + 1, 255, 255, 255, 255, 16, 'Consolas, monospace', clampedTextScale);
      this.renderer.drawTextOutlined(actionText, actionPosX + 1, textPosY - actionHeight / 2 + 1, 255, 255, 255, 255, 16, 'Consolas, monospace', clampedTextScale);
      this.renderer.drawTextOutlined(actionText, actionPosX, textPosY - actionHeight / 2, 255, 255, 255, 255, 16, 'Consolas, monospace', clampedTextScale);

      if (asteroid.showShortcut) {
        const shortcutKey = asteroid.shortcut!.getShortcutString();
        const shortcutSize = this.renderer.measureNormal(shortcutKey);
        const shortcutWidth = shortcutSize.width * clampedTextScale * 0.8;
        const shortcutHeight = shortcutSize.height * clampedTextScale * 0.8;

        const shortcutTextPosY = textPosY + actionHeight * 0.8;
        const shortcutPosX = asteroid.position.x - shortcutWidth / 2;

        this.renderer.fillRect(shortcutPosX - bgPad, shortcutTextPosY - shortcutHeight / 2 - bgPad, shortcutWidth + bgPad * 2, shortcutHeight + bgPad * 2, 0, 0, 0);

        const shortcutScale = clampedTextScale * 0.8;
        this.renderer.drawTextOutlined(shortcutKey, shortcutPosX, shortcutTextPosY - shortcutHeight / 2, 180, 180, 180, 255, 16, 'Consolas, monospace', shortcutScale);
      }
    }
  }

  private drawSpaceship(x: number, y: number, scale: number, tintRed: boolean): void {
    const s = scale;
    const r = tintRed ? 255 : undefined;
    const g = tintRed ? 100 : undefined;
    const b = tintRed ? 100 : undefined;

    const bodyColor = (rx: number, gx: number, bx: number) => {
      if (tintRed) return { r: 255, g: 100, b: 100 };
      return { r: rx, g: gx, b: bx };
    };

    this.renderer.fillRect(x + 20 * s, y + 55 * s, 88 * s, 25 * s, bodyColor(40, 60, 40).r, bodyColor(40, 60, 40).g, bodyColor(40, 60, 40).b);
    this.renderer.fillRect(x + 35 * s, y + 35 * s, 58 * s, 22 * s, bodyColor(50, 80, 50).r, bodyColor(50, 80, 50).g, bodyColor(50, 80, 50).b);
    this.renderer.fillRect(x + 45 * s, y + 20 * s, 38 * s, 18 * s, bodyColor(60, 100, 60).r, bodyColor(60, 100, 60).g, bodyColor(60, 100, 60).b);

    this.renderer.fillRect(x + 50 * s, y + 10 * s, 28 * s, 15 * s, 160, 180, 190);

    this.renderer.fillRect(x + 10 * s, y + 35 * s, 15 * s, 25 * s, bodyColor(30, 50, 30).r, bodyColor(30, 50, 30).g, bodyColor(30, 50, 30).b);
    this.renderer.fillRect(x + 73 * s, y + 35 * s, 15 * s, 25 * s, bodyColor(30, 50, 30).r, bodyColor(30, 50, 30).g, bodyColor(30, 50, 30).b);

    this.renderer.fillRect(x + 5 * s, y + 45 * s, 8 * s, 10 * s, 80, 40, 30);
    this.renderer.fillRect(x + 87 * s, y + 45 * s, 8 * s, 10 * s, 80, 40, 30);

    this.renderer.fillRect(x + 45 * s, y + 78 * s, 10 * s, 8 * s, 100, 200, 100);
    this.renderer.fillRect(x + 55 * s, y + 80 * s, 8 * s, 6 * s, 150, 255, 150);

    this.renderer.fillRect(x + 62 * s, y, 4 * s, 15 * s, 100, 100, 100);
    this.renderer.fillRect(x + 60 * s, y - 3 * s, 8 * s, 5 * s, 255, 0, 0);
  }

  private drawMissiles(): void {
    for (const missile of this.missiles.filter(m => m.isActive)) {
      for (let i = 0; i < missile.trail.length; i++) {
        const alpha = (i / missile.trail.length) * 0.5;
        const pos = missile.trail[i];
        this.renderer.fillRect(pos.x, pos.y, 2, 2, 0, 255, 255, alpha * 255);
      }
      this.renderer.fillRect(missile.position.x - 1, missile.position.y - 1, 3, 3, 0, 255, 255);
    }
  }

  private drawExplosions(): void {
    for (const explosion of this.explosions) {
      const progress = explosion.time / explosion.maxTime;

      for (const particle of explosion.particles) {
        const alpha = particle.life / particle.maxLife;
        const size = Math.floor(particle.size * (1 + progress));

        this.renderer.fillRect(
          particle.position.x - size / 2,
          particle.position.y - size / 2,
          size,
          size,
          particle.color.r,
          particle.color.g,
          particle.color.b,
          alpha * 255
        );
      }
    }
  }

  private drawHUD(): void {
    const scoreText = `SCORE: ${this.score.toString().padStart(6, '0')}`;
    const levelText = `LEVEL: ${this.level}`;
    const livesText = `LIVES: ${'♥'.repeat(this.lives)}`;

    this.renderer.drawNormal(scoreText, 10, 10, 255, 255, 255);
    this.renderer.drawNormal(levelText, 280, 10, 255, 255, 255);
    this.renderer.drawNormal(livesText, 520, 10, 255, 0, 0);

    if (this.debugKeys) {
      this.renderer.drawNormal('DEBUG: ' + this.debugKeys, 10, 55, 0, 255, 255);
    }

    if (this.combo > 1) {
      const comboText = `COMBO x${this.combo}`;
      this.renderer.drawNormal(comboText, 10, 35, 255, 255, 0);
    }

    if (this.reactionTimes.length > 0) {
      const avgReaction = this.reactionTimes.reduce((a, b) => a + b, 0) / this.reactionTimes.length;
      const reactionText = `AVG: ${avgReaction.toFixed(2)}s`;
      this.renderer.drawNormal(reactionText, 10, 75, 128, 128, 128);
    }

    const hintText = this.muted ? '[M] Unmute' : '[M] Mute';
    const hintWidth = this.renderer.measureNormal(hintText).width;
    this.renderer.drawNormal(hintText, 320 - hintWidth / 2, 460, 100, 100, 100);
  }

  private drawTitleScreen(): void {
    const title = 'SHORTCUTSY';
    const subtitle = 'Master Your Shortcuts';
    const prompt = 'Press ENTER to start';

    const titleSize = this.renderer.measureHuge(title);
    const titleX = 320 - titleSize.width / 2;

    const pulse = Math.sin(this.gameTime * 3);
    const glowIntensity = pulse * 0.3 + 0.7;

    for (let i = 3; i >= 1; i--) {
      const glowSize = i * 8;
      this.renderer.drawHuge(title, titleX - glowSize / 2, 140 - glowSize / 2, 0, Math.floor(100 * glowIntensity), Math.floor(100 * glowIntensity), Math.floor(50 * glowIntensity));
    }

    for (let ox = -2; ox <= 2; ox++) {
      for (let oy = -2; oy <= 2; oy++) {
        if (ox === 0 && oy === 0) continue;
        this.renderer.drawHuge(title, titleX + ox, 140 + oy, 0, 0, 0);
      }
    }

    const crownX = titleX + 10;
    const crownY = 120;
    this.drawCrown(crownX, crownY, true);

    this.renderer.drawHuge(title, titleX, 140, 0, 255, 255);

    const subtitleColorR = Math.floor(200 * glowIntensity);
    const subtitleColorG = Math.floor(136 * glowIntensity);
    const subtitleSize = this.renderer.measureNormal(subtitle);
    this.renderer.drawNormal(subtitle, 320 - subtitleSize.width / 2 + 1, 211, 0, 0, 0);
    this.renderer.drawNormal(subtitle, 320 - subtitleSize.width / 2, 210, 0, subtitleColorR, subtitleColorG);

    const promptPulse = Math.sin(this.gameTime * 3) * 0.3 + 0.7;
    const promptSize = this.renderer.measureNormal(prompt);
    this.renderer.drawNormal(prompt, 320 - promptSize.width / 2, 290, Math.floor(255 * promptPulse), Math.floor(255 * promptPulse), Math.floor(255 * promptPulse));

    const levelSelectHint = 'Press L to select level';
    const hintSize = this.renderer.measureNormal(levelSelectHint);
    this.renderer.drawNormal(levelSelectHint, 320 - hintSize.width / 2, 340, 80, 80, 80);
  }

  private drawCrown(x: number, y: number, tilted: boolean): void {
    if (tilted) {
      this.renderer.fillRect(x, y, 4, 8, 255, 215, 0);
      this.renderer.fillRect(x + 6, y - 2, 4, 7, 255, 220, 0);
      this.renderer.fillRect(x + 12, y - 1, 4, 6, 255, 215, 0);
      this.renderer.fillRect(x + 18, y - 2, 4, 7, 255, 220, 0);
      this.renderer.fillRect(x + 24, y, 4, 6, 255, 215, 0);
      this.renderer.fillRect(x - 2, y + 6, 32, 5, 255, 180, 0);
      this.renderer.fillRect(x, y + 11, 28, 3, 200, 150, 0);
    } else {
      for (let i = 0; i < 5; i++) {
        this.renderer.fillRect(x + i * 5, y, 3, 6, 255, 215, 0);
      }
      this.renderer.fillRect(x - 3, y + 5, 30, 5, 255, 180, 0);
      this.renderer.fillRect(x - 2, y + 10, 28, 3, 200, 150, 0);
    }
  }

  private drawGameOverScreen(): void {
    const gameOver = 'GAME OVER';
    const finalScore = `Final Score: ${this.score}`;
    const prompt = 'Press ENTER to restart';
    const backHint = 'Press ESC for title';

    const goSize = this.renderer.measureLarge(gameOver);
    this.renderer.drawLarge(gameOver, 320 - goSize.width / 2, 150, 255, 0, 68);

    const scoreSize = this.renderer.measureNormal(finalScore);
    this.renderer.drawNormal(finalScore, 320 - scoreSize.width / 2, 220, 255, 255, 255);

    const pulse = Math.sin(this.gameTime * 3) * 0.3 + 0.7;
    const promptSize = this.renderer.measureNormal(prompt);
    this.renderer.drawNormal(prompt, 320 - promptSize.width / 2, 280, Math.floor(255 * pulse), Math.floor(255 * pulse), Math.floor(255 * pulse));

    const backSize = this.renderer.measureNormal(backHint);
    this.renderer.drawNormal(backHint, 320 - backSize.width / 2, 310, 80, 80, 80);

    this.drawHighScores(350);
  }

  private drawNewRecordScreen(): void {
    const newRecord = 'NEW RECORD!';
    const pulse = Math.sin(this.gameTime * 6) * 0.2 + 0.8;

    const nrSize = this.renderer.measureLarge(newRecord);
    this.renderer.drawLarge(newRecord, 320 - nrSize.width / 2, 80, Math.floor(255 * pulse), Math.floor(215 * pulse), 0);

    this.drawGoldMedal(320 - 50, 180, 100);

    const scoreText = `Score: ${this.score}`;
    const scoreSize = this.renderer.measureNormal(scoreText);
    this.renderer.drawNormal(scoreText, 320 - scoreSize.width / 2, 320, 255, 255, 255);

    const prompt = 'Press ENTER to continue';
    const promptPulse = Math.sin(this.gameTime * 3) * 0.3 + 0.7;
    const promptSize = this.renderer.measureNormal(prompt);
    this.renderer.drawNormal(prompt, 320 - promptSize.width / 2, 380, Math.floor(255 * promptPulse), Math.floor(255 * promptPulse), Math.floor(255 * promptPulse));
  }

  private drawGoldMedal(x: number, y: number, size: number): void {
    const goldColor = { r: 255, g: 215, b: 0 };
    const goldShine = { r: 255, g: 255, b: 150 };
    const goldDark = { r: 200, g: 150, b: 0 };

    for (let dy = 0; dy < size; dy++) {
      for (let dx = 0; dx < size; dx++) {
        const dist = Math.sqrt((dx - size / 2) ** 2 + (dy - size / 2) ** 2);
        if (dist < size / 2 && dist > size / 2 - 6) {
          this.renderer.fillRect(x + dx, y + dy, 1, 1, goldColor.r, goldColor.g, goldColor.b);
        } else if (dist < size / 2 - 6) {
          this.renderer.fillRect(x + dx, y + dy, 1, 1, goldShine.r, goldShine.g, goldShine.b);
        }
      }
    }

    this.renderer.fillRect(x + size / 2 - 10, y + size - 5, 20, 8, goldDark.r, goldDark.g, goldDark.b);
    this.renderer.fillRect(x + 10, y + 8, 8, 8, 255, 255, 255);

    this.renderer.drawHuge('1', x + size / 2 - 8, y + size / 2 - 15, goldDark.r, goldDark.g, goldDark.b);
  }

  private drawPausedScreen(): void {
    const paused = 'PAUSED';
    const prompt = 'Press ESC to resume';

    const pausedSize = this.renderer.measureLarge(paused);
    this.renderer.drawLarge(paused, 320 - pausedSize.width / 2, 200, 255, 255, 0);

    const promptSize = this.renderer.measureNormal(prompt);
    this.renderer.drawNormal(prompt, 320 - promptSize.width / 2, 260, 255, 255, 255);
  }

  private drawLevelTransitionScreen(): void {
    let levelText: string;
    let r = 255, g = 255, b = 0;

    if (this.levelTransitionTimer < 1.5) {
      levelText = 'LEVEL COMPLETE!';
    } else if (this.level >= this.maxLevel) {
      levelText = 'YOU WIN!';
      r = 0; g = 255; b = 0;
    } else {
      levelText = `LEVEL ${this.level + 1}`;
      r = 0; g = 255; b = 255;
    }

    const scale = 1 + Math.sin(this.levelTransitionTimer * 8) * 0.1;
    const yPos = 200;

    const levelSize = this.renderer.measureNormal(levelText);
    this.renderer.drawNormal(levelText, 320 - levelSize.width / 2, yPos, r, g, b, 255, scale);

    if (this.levelTransitionTimer > 1 && this.level < this.maxLevel) {
      const getReady = 'GET READY!';
      const readyPulse = Math.sin(this.gameTime * 6) * 0.3 + 0.7;
      const readySize = this.renderer.measureNormal(getReady);
      this.renderer.drawNormal(getReady, 320 - readySize.width / 2, 280, Math.floor(255 * readyPulse), Math.floor(255 * readyPulse), Math.floor(255 * readyPulse));
    }
  }

  private drawLevelSelectScreen(): void {
    const title = 'SELECT LEVEL';
    const titleSize = this.renderer.measureLarge(title);
    this.renderer.drawLarge(title, 320 - titleSize.width / 2, 50, 0, 255, 255);

    const subtitle = 'Use LEFT/RIGHT arrows or type number';
    const subSize = this.renderer.measureNormal(subtitle);
    this.renderer.drawNormal(subtitle, 320 - subSize.width / 2, 90, 150, 150, 150);

    const levelText = `LEVEL ${this.selectedLevel}`;
    const pulse = Math.sin(this.gameTime * 4) * 0.15 + 0.85;
    const levelSize = this.renderer.measureHuge(levelText);
    this.renderer.drawHuge(levelText, 320 - levelSize.width / 2, 140, 255, Math.floor(200 * pulse), 0);

    const allLevelShortcuts = ShortcutDatabase.instance.getShortcutsForLevel(10).filter(s => s.level === this.selectedLevel);
    if (allLevelShortcuts.length > 0) {
      const previewLabel = 'Practice:';
      const previewSize = this.renderer.measureNormal(previewLabel);
      this.renderer.drawNormal(previewLabel, 320 - previewSize.width / 2, 210, 100, 100, 100);

      const maxShow = Math.min(5, allLevelShortcuts.length);
      for (let i = 0; i < maxShow; i++) {
        const sc = allLevelShortcuts[i];
        const shortcutPreview = `${sc.action}: ${sc.getShortcutString()}`;
        const scSize = this.renderer.measureSmall(shortcutPreview);
        this.renderer.drawSmall(shortcutPreview, 320 - scSize.width / 2, 235 + i * 14, 160, 160, 160);
      }
    }

    const levelsPerRow = 5;
    const startY = 320;
    const spacing = 70;

    for (let i = 1; i <= this.maxLevel; i++) {
      const row = Math.floor((i - 1) / levelsPerRow);
      const col = (i - 1) % levelsPerRow;
      const lx = 120 + col * spacing;
      const ly = startY + row * 55;

      const levelScore = this.highScoreManager.getScoreForLevel(i);

      const hue = (this.gameTime * 2 + i * 0.5) % (Math.PI * 2);
      const lr = Math.floor(Math.sin(hue) * 127 + 128);
      const lg = Math.floor(Math.sin(hue + Math.PI * 2 / 3) * 127 + 128);
      const lb = Math.floor(Math.sin(hue + Math.PI * 4 / 3) * 127 + 128);

      if (i === this.selectedLevel) {
        this.renderer.fillRect(lx - 8, ly - 8, 36, 36, 0, 120, 120);
        this.renderer.fillRect(lx - 4, ly - 4, 28, 28, 0, 80, 80);
        this.renderer.drawNormal(i.toString(), lx, ly, 255, 255, 0);
      } else {
        for (let glow = 3; glow >= 1; glow--) {
          this.renderer.drawNormal(i.toString(), lx - glow, ly - glow, Math.floor(lr * glow * 0.3), Math.floor(lg * glow * 0.3), Math.floor(lb * glow * 0.3), 100);
        }
        this.renderer.drawNormal(i.toString(), lx, ly, lr, lg, lb);
      }

      if (levelScore > 0) {
        const scoreText = levelScore.toString().padStart(5, '0');
        this.renderer.drawSmall(scoreText, lx - 2, ly + 22, 255, 215, 0);
      }
    }

    const prompt = 'Press ENTER to start';
    const promptPulse = Math.sin(this.gameTime * 3) * 0.3 + 0.7;
    const promptSize = this.renderer.measureNormal(prompt);
    this.renderer.drawNormal(prompt, 320 - promptSize.width / 2, 450, Math.floor(255 * promptPulse), Math.floor(255 * promptPulse), Math.floor(255 * promptPulse));

    const backHint = 'Press ESC to go back';
    const backSize = this.renderer.measureNormal(backHint);
    this.renderer.drawNormal(backHint, 320 - backSize.width / 2, 470, 70, 70, 70);
  }

  private drawHighScores(startY: number): void {
    const scores = this.highScoreManager.getScores().slice(0, 3);

    const header = 'TOP 3';
    const headerSize = this.renderer.measureNormal(header);
    this.renderer.drawNormal(header, 320 - headerSize.width / 2, startY, 255, 200, 0);

    for (let i = 0; i < scores.length; i++) {
      const scoreText = `${scores[i].score.toString().padStart(6, '0')} (L${scores[i].level})`;
      const medalColors = [
        { r: 255, g: 215, b: 0 },
        { r: 192, g: 192, b: 192 },
        { r: 205, g: 127, b: 50 }
      ];
      const color = medalColors[i] || medalColors[0];

      this.drawMedal(320 - this.renderer.measureNormal(scoreText).width / 2 - 30, startY + 25 + i * 30, i);

      const scoreSize = this.renderer.measureNormal(scoreText);
      this.renderer.drawNormal(scoreText, 320 - scoreSize.width / 2, startY + 25 + i * 30, color.r, color.g, color.b);
    }
  }

  private drawMedal(x: number, y: number, rank: number): void {
    const medalColors = [
      { r: 255, g: 215, b: 0 },
      { r: 192, g: 192, b: 192 },
      { r: 205, g: 127, b: 50 }
    ];
    const shineColors = [
      { r: 255, g: 255, b: 150 },
      { r: 255, g: 255, b: 255 },
      { r: 255, g: 200, b: 100 }
    ];
    const medalColor = medalColors[rank] || medalColors[0];
    const shineColor = shineColors[rank] || shineColors[0];
    const size = 20;

    for (let dy = 0; dy < size; dy++) {
      for (let dx = 0; dx < size; dx++) {
        const dist = Math.sqrt((dx - size / 2) ** 2 + (dy - size / 2) ** 2);
        if (dist < size / 2 && dist > size / 2 - 3) {
          this.renderer.fillRect(x + dx, y + dy, 1, 1, medalColor.r, medalColor.g, medalColor.b);
        } else if (dist < size / 2 - 3) {
          this.renderer.fillRect(x + dx, y + dy, 1, 1, shineColor.r, shineColor.g, shineColor.b);
        }
      }
    }

    this.renderer.fillRect(x + 6, y + 4, 4, 4, 255, 255, 255);

    const num = (rank + 1).toString();
    this.renderer.drawSmall(num, x + 6, y + 4, 0, 0, 0);
  }

  private drawScanlines(): void {
    for (let y = 0; y < 480; y += 4) {
      this.renderer.fillRect(0, y, 640, 2, 0, 0, 0, 30);
    }
  }
}
