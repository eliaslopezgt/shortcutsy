/**
 * Keyboard input handler for the browser.
 * Tracks currently pressed keys and provides edge-detection (just pressed).
 */
export class InputManager {
  private currentKeys: Set<string> = new Set();
  private previousKeys: Set<string> = new Set();

  constructor() {
    // Prevent accidental tab close warning - we'll handle it ourselves
    window.addEventListener('beforeunload', (e) => {
      // We'll set this only when game is running to warn users
      if ((this as any)._gameActive) {
        e.preventDefault();
        e.returnValue = '';
      }
    });

    // Use document for earliest interception
    document.addEventListener('keydown', (e) => {
      // ALWAYS prevent default for any Ctrl/Meta key combos - browser shortcuts are dangerous
      if (e.ctrlKey || e.metaKey) {
        e.preventDefault();
        e.stopPropagation();
        e.stopImmediatePropagation();
        // Add the key anyway so game can use it
        this.currentKeys.add(this.normalizeKey(e));
        return;
      }
      
      // Prevent browser defaults for other game keys
      if (this.shouldPreventDefault(e)) {
        e.preventDefault();
      }
      this.currentKeys.add(this.normalizeKey(e));
    }, { passive: false });

    document.addEventListener('keyup', (e) => {
      this.currentKeys.delete(this.normalizeKey(e));
    }, { passive: false });

    // Handle losing focus
    window.addEventListener('blur', () => {
      this.currentKeys.clear();
    });
    
    // Also prevent context menu on right-click during game
    document.addEventListener('contextmenu', (e) => {
      e.preventDefault();
    });
  }
  
  setGameActive(active: boolean): void {
    (this as any)._gameActive = active;
  }

  private shouldPreventDefault(e: KeyboardEvent): boolean {
    // Prevent ALL Ctrl+ combinations - browser shortcuts like Ctrl+W, Ctrl+N, Ctrl+T, etc.
    if (e.ctrlKey || e.metaKey) {
      return true;
    }
    // Prevent defaults for game-relevant shortcuts
    // Allow F11 (fullscreen) and F12 (devtools) to pass through
    if (e.key === 'F11' || e.key === 'F12') return false;
    if (e.key.startsWith('F') && e.key.length <= 3) return true;
    if (e.key === 'Tab' || e.key === 'Escape') return true;
    return false;
  }

  /**
   * Normalizes a keyboard event to a consistent key string.
   * Returns the key in lowercase for letter keys, or the key identifier for special keys.
   */
  private normalizeKey(e: KeyboardEvent): string {
    // For modifier keys, use the generic name
    if (e.key === 'Control' || e.key === 'Meta') return 'Control';
    if (e.key === 'Alt') return 'Alt';
    if (e.key === 'Shift') return 'Shift';

    // For letter keys, always use lowercase
    if (e.key.length === 1 && e.key >= 'a' && e.key <= 'z') return e.key;
    if (e.key.length === 1 && e.key >= 'A' && e.key <= 'Z') return e.key.toLowerCase();

    // Handle arrow keys - normalize to match game code expectations
    if (e.key === 'ArrowUp') return 'ArrowUp';
    if (e.key === 'ArrowDown') return 'ArrowDown';
    if (e.key === 'ArrowLeft') return 'ArrowLeft';
    if (e.key === 'ArrowRight') return 'ArrowRight';

    // For function keys and other special keys, use the key directly
    return e.key;
  }

  /** Call at the end of each frame to snapshot state */
  endFrame(): void {
    this.previousKeys = new Set(this.currentKeys);
  }

  /** Check if a key is currently down */
  isKeyDown(key: string): boolean {
    return this.currentKeys.has(key);
  }

  /** Check if a key was just pressed this frame (edge detection) */
  isKeyJustPressed(key: string): boolean {
    return this.currentKeys.has(key) && !this.previousKeys.has(key);
  }

  /** Get all currently pressed keys */
  getPressedKeys(): string[] {
    return Array.from(this.currentKeys);
  }

  /** Get keys that were just pressed this frame */
  getNewlyPressedKeys(): string[] {
    return Array.from(this.currentKeys).filter(k => !this.previousKeys.has(k));
  }

  /** Check if there are any modifier keys held */
  hasModifiers(): boolean {
    return this.currentKeys.has('Control') || this.currentKeys.has('Alt') || this.currentKeys.has('Shift');
  }
}
