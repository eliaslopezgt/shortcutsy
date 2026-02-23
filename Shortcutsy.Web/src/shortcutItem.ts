// Shortcut data models - port of ShortcutItem.cs and ShortcutConfig

export interface ShortcutConfig {
  Action: string;
  Keys: string[];
  Level: number;
}

export class ShortcutItem {
  action: string;
  keyCombo: string[]; // Browser key codes (e.g. ["Control", "s"])
  level: number;
  /** Original display strings from JSON (e.g. ["Ctrl+S"]) */
  displayKeys: string[];

  constructor(action: string, keyCombo: string[], level: number, displayKeys: string[]) {
    this.action = action;
    this.keyCombo = keyCombo;
    this.level = level;
    this.displayKeys = displayKeys;
  }

  getShortcutString(): string {
    return this.displayKeys.join(', ');
  }
}
