using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using MinesServer.Data;
using Fodinae.Assets.Scripts.Game.Managers;

namespace Fodinae.Assets.Scripts.World
{
    public class TextureAtlas
    {
        public int Size { get; }
        public int CellSize { get; }
        public int Padding { get; }

        private Texture2D _atlasTexture;
        private Color32[] _atlasPixels;
        private readonly ConcurrentDictionary<CellType, AtlasCell> _cells = new();
        private readonly List<Rectangle> _freeRectangles = new();
        private readonly List<Rectangle> _usedRectangles = new();

        private bool _isDirty = false;
        private readonly object _lock = new object();

        public TextureAtlas(int size, int cellSize, int padding)
        {
            Size = size;
            CellSize = cellSize;
            Padding = padding;

            _atlasTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            _atlasTexture.filterMode = FilterMode.Point; // Changed to Point for pixel art
            _atlasTexture.wrapMode = TextureWrapMode.Clamp;
            _atlasPixels = new Color32[size * size];

            for (int i = 0; i < _atlasPixels.Length; i++)
            {
                _atlasPixels[i] = new Color32(0, 0, 0, 0);
            }

            _atlasTexture.SetPixels32(_atlasPixels);
            _atlasTexture.Apply();

            _freeRectangles.Add(new Rectangle(0, 0, size, size));
        }

        public void Clear()
        {
            lock (_lock)
            {
                _cells.Clear();
                _usedRectangles.Clear();
                _freeRectangles.Clear();
                _freeRectangles.Add(new Rectangle(0, 0, Size, Size));

                // Clear atlas texture
                for (int i = 0; i < _atlasPixels.Length; i++)
                {
                    _atlasPixels[i] = new Color32(0, 0, 0, 0);
                }
                _atlasTexture.SetPixels32(_atlasPixels);
                _atlasTexture.Apply();

                _isDirty = false;
            }
        }

        public AtlasCoordinate GetCoordinate(CellType cellType, CellVariation variation)
        {
            if (!_cells.TryGetValue(cellType, out var cell))
            {
                return AtlasCoordinate.Empty;
            }
            return cell.BaseCoordinate;
        }

        public AtlasCoordinate GetCoordinate(CellType cellType)
        {
            return GetCoordinate(cellType, CellVariation.None);
        }

        public bool TryAddTexture(CellType cellType, Texture2D texture, out AtlasCoordinate coordinate)
        {
            coordinate = AtlasCoordinate.Empty;

            lock (_lock)
            {
                var bestFit = FindBestFit(texture.width, texture.height);
                if (bestFit == null) return false;

                var atlasCell = new AtlasCell
                {
                    CellType = cellType,
                    Rectangle = bestFit.Value,
                    BaseCoordinate = new AtlasCoordinate(
                        bestFit.Value.X, bestFit.Value.Y,
                        texture.width, texture.height,
                        Size, Size)
                };

                _usedRectangles.Add(bestFit.Value);
                SplitFreeRectangles(bestFit.Value);
                _cells.TryAdd(cellType, atlasCell);
                _isDirty = true;

                coordinate = atlasCell.BaseCoordinate;
                return true;
            }
        }

        public async UniTask<Texture2D> GetAtlasTexture()
        {
            if (_isDirty) await UpdateAtlasTexture();
            return _atlasTexture;
        }

        public async UniTask UpdateAtlasTexture()
        {
            // Ensure we start on Main Thread
            await UniTask.SwitchToMainThread();

            if (!_isDirty) return;

            List<(Texture2D texture, Rectangle rect)> texturesToCopy;

            lock (_lock)
            {
                if (!_isDirty) return;

                texturesToCopy = new List<(Texture2D texture, Rectangle rect)>();

                foreach (var cell in _cells.Values)
                {
                    var baseTexture = GetBaseTexture(cell.CellType);
                    if (baseTexture != null)
                    {
                        texturesToCopy.Add((baseTexture, cell.Rectangle));
                    }
                }
            }

            await CopyTexturesToAtlas(texturesToCopy);

            lock (_lock)
            {
                _isDirty = false;
            }
        }

        private async UniTask CopyTexturesToAtlas(List<(Texture2D texture, Rectangle rect)> textures)
        {
            const int batchSize = 10;

            for (int i = 0; i < textures.Count; i += batchSize)
            {
                var batch = textures.Skip(i).Take(batchSize).ToList();

                // 1. READ ON MAIN THREAD
                // We extract the raw pixel data here because Texture2D.GetPixels32() is Main Thread Only
                var pixelDataList = new List<(Color32[] pixels, int width, int height, Rectangle rect)>();

                foreach (var (tex, rect) in batch)
                {
                    if (tex != null)
                    {
                        pixelDataList.Add((tex.GetPixels32(), tex.width, tex.height, rect));
                    }
                }

                // 2. PROCESS ON BACKGROUND THREAD
                // Now we switch threads to do the heavy array copying
                await UniTask.SwitchToThreadPool();

                foreach (var data in pixelDataList)
                {
                    CopyPixelsToAtlasArray(data.pixels, data.width, data.height, data.rect);
                }

                // 3. BACK TO MAIN THREAD
                await UniTask.SwitchToMainThread();
            }

            _atlasTexture.SetPixels32(_atlasPixels);
            _atlasTexture.Apply();
        }

        private void CopyPixelsToAtlasArray(Color32[] sourcePixels, int width, int height, Rectangle destination)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int sourceIndex = y * width + x;
                    int destX = destination.X + x;
                    int destY = destination.Y + y;
                    int destIndex = destY * Size + destX;

                    if (destIndex >= 0 && destIndex < _atlasPixels.Length && sourceIndex < sourcePixels.Length)
                    {
                        _atlasPixels[destIndex] = sourcePixels[sourceIndex];
                    }
                }
            }
        }

        private Texture2D GetBaseTexture(CellType cellType)
        {
            if (WorldTextureManager.Instance != null)
            {
                // Reflection hack to access private cache if needed, or rely on public API
                // Assuming WorldTextureManager has public access or we fall back to placeholder
                // For safety, we use the placeholder if we can't easily reach back
            }
            return CreatePlaceholderTexture(cellType);
        }

        private Texture2D CreatePlaceholderTexture(CellType cellType)
        {
            var texture = new Texture2D(CellSize, CellSize);
            var color = GetCellColor(cellType);
            var pixels = new Color[CellSize * CellSize];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private Color GetCellColor(CellType cellType)
        {
            if (MapManager.Instance != null)
            {
                var serverColor = MapManager.Instance.GetCellMinimapColor(cellType);
                if (serverColor.a > 0) return serverColor;
            }
            return cellType switch
            {
                CellType.Empty => new Color(0.2f, 0.2f, 0.2f),
                CellType.Road => new Color(0.8f, 0.8f, 0.8f),
                CellType.Boulder1 => Color.black,
                CellType.WhiteSand => new Color(1f, 0.92f, 0.8f),
                CellType.GrayAcid => new Color(0f, 1f, 0f),
                _ => Color.magenta
            };
        }

        // Helper structs/methods
        private Rectangle? FindBestFit(int width, int height)
        {
            Rectangle? bestFit = null;
            int bestScore = int.MaxValue;
            foreach (var freeRect in _freeRectangles)
            {
                if (freeRect.Width >= width + Padding && freeRect.Height >= height + Padding)
                {
                    int score = (freeRect.Width - width) * (freeRect.Height - height);
                    if (score < bestScore) { bestScore = score; bestFit = new Rectangle(freeRect.X, freeRect.Y, width, height); }
                }
            }
            return bestFit;
        }

        private void SplitFreeRectangles(Rectangle usedRect)
        {
            var newFree = new List<Rectangle>();
            foreach (var free in _freeRectangles)
            {
                if (Intersects(free, usedRect)) SplitRectangle(free, usedRect, newFree);
                else newFree.Add(free);
            }
            _freeRectangles.Clear();
            _freeRectangles.AddRange(newFree);
        }

        private void SplitRectangle(Rectangle free, Rectangle used, List<Rectangle> newFree)
        {
            if (used.Y > free.Y) newFree.Add(new Rectangle(free.X, free.Y, free.Width, used.Y - free.Y));
            if (used.Y + used.Height < free.Y + free.Height) newFree.Add(new Rectangle(free.X, used.Y + used.Height, free.Width, (free.Y + free.Height) - (used.Y + used.Height)));
            if (used.X > free.X) newFree.Add(new Rectangle(free.X, free.Y, used.X - free.X, free.Height));
            if (used.X + used.Width < free.X + free.Width) newFree.Add(new Rectangle(used.X + used.Width, free.Y, (free.X + free.Width) - (used.X + used.Width), free.Height));
        }

        private bool Intersects(Rectangle a, Rectangle b) => a.X < b.X + b.Width && a.X + a.Width > b.X && a.Y < b.Y + b.Height && a.Y + a.Height > b.Y;
    }

    public struct Rectangle
    {
        public int X, Y, Width, Height;
        public Rectangle(int x, int y, int width, int height) { X = x; Y = y; Width = width; Height = height; }
    }

    internal struct AtlasCell
    {
        public CellType CellType;
        public Rectangle Rectangle;
        public AtlasCoordinate BaseCoordinate;
    }
}