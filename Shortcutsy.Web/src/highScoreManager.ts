/**
 * HighScoreManager - uses localStorage instead of file I/O
 */

const STORAGE_KEY = 'shortcutsy_highscores';

export class HighScoreManager {
  private levelScores: Map<number, number> = new Map();

  load(): void {
    try {
      const json = localStorage.getItem(STORAGE_KEY);
      if (json) {
        const data = JSON.parse(json) as Record<string, number>;
        for (const [key, value] of Object.entries(data)) {
          this.levelScores.set(parseInt(key), value);
        }
      }
    } catch {
      this.levelScores = new Map();
    }
  }

  save(): void {
    try {
      const obj: Record<string, number> = {};
      for (const [key, value] of this.levelScores) {
        obj[key.toString()] = value;
      }
      localStorage.setItem(STORAGE_KEY, JSON.stringify(obj));
    } catch { /* ignore */ }
  }

  addScore(score: number, level: number): boolean {
    const current = this.levelScores.get(level) || 0;
    let isNewRecord = false;
    if (score > current) {
      this.levelScores.set(level, score);
      isNewRecord = true;
    }
    this.save();
    return isNewRecord;
  }

  getScoreForLevel(level: number): number {
    return this.levelScores.get(level) || 0;
  }

  getScores(): { score: number; level: number }[] {
    const entries: { score: number; level: number }[] = [];
    for (const [level, score] of this.levelScores) {
      entries.push({ score, level });
    }
    return entries.sort((a, b) => b.score - a.score);
  }

  getAllScores(): Map<number, number> {
    return new Map(this.levelScores);
  }

  clear(): void {
    this.levelScores.clear();
  }
}
