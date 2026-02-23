import { ShortcutItem, type ShortcutConfig } from './shortcutItem';
import shortcutsData from './shortcuts.json';

/**
 * Maps key names from shortcuts.json to browser KeyboardEvent.key values.
 * Browser uses e.g. "Control", "Shift", "Alt", "a", "F1", etc.
 */
const KEY_MAP: Record<string, string> = {
  'Ctrl': 'Control',
  'Control': 'Control',
  'Alt': 'Alt',
  'Shift': 'Shift',
  'Enter': 'Enter',
  'Escape': 'Escape',
  'Space': ' ',
  'Tab': 'Tab',
  'Backspace': 'Backspace',
  'Delete': 'Delete',
  'Home': 'Home',
  'End': 'End',
  'PageUp': 'PageUp',
  'PageDown': 'PageDown',
  'Up': 'ArrowUp',
  'Down': 'ArrowDown',
  'Left': 'ArrowLeft',
  'Right': 'ArrowRight',
  'F1': 'F1', 'F2': 'F2', 'F3': 'F3', 'F4': 'F4',
  'F5': 'F5', 'F6': 'F6', 'F7': 'F7', 'F8': 'F8',
  'F9': 'F9', 'F10': 'F10', 'F11': 'F11', 'F12': 'F12',
  '-': '-', 'Minus': '-',
  ',': ',', 'Comma': ',',
  '.': '.', 'Period': '.',
  '/': '/', '?': '/',
  ';': ';', 'Semicolon': ';',
  "'": "'", 'Quote': "'",
  '[': '[', 'OpenBracket': '[',
  ']': ']', 'CloseBracket': ']',
  '\\': '\\', 'Backslash': '\\',
};

// Single letter keys map to lowercase
for (let c = 65; c <= 90; c++) {
  const letter = String.fromCharCode(c);
  KEY_MAP[letter] = letter.toLowerCase();
}

/**
 * Parse key strings from JSON (e.g. ["Ctrl+S"] or ["Ctrl+K", "Ctrl+C"])
 * into browser key identifiers. Following the C# version, chord sequences
 * are flattened into a single simultaneous key combo.
 */
function parseKeys(keyStrings: string[]): string[] {
  const keys: string[] = [];
  for (const combo of keyStrings) {
    const parts = combo.split('+');
    for (const part of parts) {
      const k = part.trim();
      const mapped = KEY_MAP[k];
      if (mapped && !keys.includes(mapped)) {
        keys.push(mapped);
      }
    }
  }
  return keys;
}

export class ShortcutDatabase {
  private static _instance: ShortcutDatabase | null = null;
  allShortcuts: ShortcutItem[] = [];
  byLevel: Map<number, ShortcutItem[]> = new Map();

  static get instance(): ShortcutDatabase {
    if (!this._instance) {
      this._instance = new ShortcutDatabase();
      this._instance.loadData();
    }
    return this._instance;
  }

  static initialize(): void {
    this._instance = new ShortcutDatabase();
    this._instance.loadData();
  }

  loadData(): void {
    const configs = shortcutsData as ShortcutConfig[];
    this.allShortcuts = configs
      .filter(c => c.Action && c.Keys && c.Keys.length > 0)
      .map(c => new ShortcutItem(
        c.Action,
        parseKeys(c.Keys),
        c.Level,
        c.Keys
      ));

    this.byLevel = new Map();
    for (const s of this.allShortcuts) {
      if (!this.byLevel.has(s.level)) {
        this.byLevel.set(s.level, []);
      }
      this.byLevel.get(s.level)!.push(s);
    }
  }

  getShortcutsForLevel(maxLevel: number): ShortcutItem[] {
    return this.allShortcuts.filter(s => s.level <= maxLevel);
  }

  getShortcutsForLevelExact(level: number): ShortcutItem[] {
    return this.byLevel.get(level) || [];
  }

  getMaxLevel(): number {
    return this.allShortcuts.length > 0
      ? Math.max(...this.allShortcuts.map(s => s.level))
      : 0;
  }
}
