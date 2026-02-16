using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;

namespace Shortcutsy.Rendering;

/// <summary>
/// Renders text to textures using System.Drawing for font rendering,
/// then caches them as MonoGame Texture2D for efficient rendering.
/// </summary>
public class TextRenderer
{
    private GraphicsDevice _graphicsDevice;
    private Dictionary<string, Texture2D> _textureCache = new();
    private System.Drawing.Font _font;
    private System.Drawing.Font _largeFont;
    private System.Drawing.Font _hugeFont;
    private System.Drawing.Font _smallFont;

    public TextRenderer(GraphicsDevice gd)
    {
        _graphicsDevice = gd;
        _font = new System.Drawing.Font("Consolas", 16, System.Drawing.FontStyle.Bold);
        _largeFont = new System.Drawing.Font("Consolas", 28, System.Drawing.FontStyle.Bold);
        _hugeFont = new System.Drawing.Font("Impact", 52, System.Drawing.FontStyle.Bold);
        _smallFont = new System.Drawing.Font("Consolas", 10, System.Drawing.FontStyle.Bold);
    }

    /// <summary>
    /// Draws text to the sprite batch using cached texture.
    /// </summary>
    public void DrawString(SpriteBatch spriteBatch, string text, Vector2 position, Color color, bool large = false, bool small = false, bool huge = false, float scale = 1f)
    {
        if (string.IsNullOrEmpty(text)) return;
        var texture = GetOrCreateTexture(text, large, small, huge);
        var tint = new Microsoft.Xna.Framework.Color(color.R, color.G, color.B, color.A);
        spriteBatch.Draw(texture, position, null, tint, 0, Vector2.Zero, scale, SpriteEffects.None, 0);
    }

    /// <summary>
    /// Measures the size of rendered text.
    /// </summary>
    public Vector2 MeasureString(string text, bool large = false, bool small = false, bool huge = false)
    {
        if (string.IsNullOrEmpty(text)) return Vector2.Zero;
        var font = huge ? _hugeFont : (large ? _largeFont : (small ? _smallFont : _font));
        using (var bmp = new System.Drawing.Bitmap(1, 1))
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            var size = g.MeasureString(text, font);
            return new Vector2(size.Width, size.Height);
        }
    }

    private Texture2D GetOrCreateTexture(string text, bool large, bool small, bool huge = false)
    {
        if (string.IsNullOrEmpty(text))
        {
            var key = "empty_S";
            if (_textureCache.ContainsKey(key))
                return _textureCache[key];
            var texture = new Texture2D(_graphicsDevice, 1, 1);
            texture.SetData(new[] { Color.Transparent });
            _textureCache[key] = texture;
            return texture;
        }
        
        var cacheKey = text + "_" + (huge ? "H" : (large ? "L" : (small ? "X" : "S")));
        if (_textureCache.ContainsKey(cacheKey))
            return _textureCache[cacheKey];

        var font = huge ? _hugeFont : (large ? _largeFont : (small ? _smallFont : _font));
        
        using (var bmp = new System.Drawing.Bitmap(1, 1))
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            var size = g.MeasureString(text, font);
            int width = (int)Math.Ceiling(size.Width);
            int height = (int)Math.Ceiling(size.Height);
            
            using (var bmp2 = new System.Drawing.Bitmap(width, height))
            using (var g2 = System.Drawing.Graphics.FromImage(bmp2))
            {
                g2.Clear(System.Drawing.Color.Transparent);
                g2.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                g2.DrawString(text, font, System.Drawing.Brushes.White, 0, 0);
                
                var colors = new Color[width * height];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        var p = bmp2.GetPixel(x, y);
                        colors[y * width + x] = new Color(p.R, p.G, p.B, p.A);
                    }
                }
                
                var texture = new Texture2D(_graphicsDevice, width, height);
                texture.SetData(colors);
                _textureCache[cacheKey] = texture;
                return texture;
            }
        }
    }

    /// <summary>
    /// Clears the texture cache to free memory.
    /// </summary>
    public void ClearCache()
    {
        foreach (var tex in _textureCache.Values)
            tex.Dispose();
        _textureCache.Clear();
    }
}
