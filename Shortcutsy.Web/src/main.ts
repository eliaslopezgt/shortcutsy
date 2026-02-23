import { Game } from './game';

const canvas = document.getElementById('game') as HTMLCanvasElement;

if (!canvas) {
  throw new Error('Canvas element not found');
}

try {
  new Game(canvas);
  console.log('Shortcutsy loaded successfully');
} catch (e) {
  console.error('Error starting Shortcutsy:', e);
}

console.log('Click or press a key to enable audio');
