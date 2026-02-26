using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using MinesServer.Data;
using Fodinae.Assets.Scripts.Game.Managers;
using UnityEngine.Rendering;

namespace Fodinae.Assets.Scripts.World
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class WorldBackgroundRenderer : MonoBehaviour
    {
        [Header("Configuration")]
        public int _chunkSize = 32;
        public int _renderDistance = 15;
        public float _cellSize = 1.0f;
        public bool _debugMode = false;

        [Header("Background Settings")]
        // FIX: Default Z to 0 so it appears in front of the camera (assuming 2D setup) or at least not behind the near clip
        [SerializeField] private float _backgroundZ = 0f;
        [SerializeField] private int _sortingOrder = -1000;

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Mesh _mesh;
        private Material _backgroundMaterial;

        private WorldLayer<CellType> _worldLayer;
        private readonly ConcurrentDictionary<Vector2Int, ChunkMesh> _chunkMeshes = new();
        private readonly HashSet<Vector2Int> _visibleChunks = new();

        private Camera _mainCamera;
        // FIX: Initialize to impossible value so it updates on first frame
        private Vector2Int _lastCameraChunk = new Vector2Int(int.MinValue, int.MinValue);

        private bool _isInitialized = false;
        private bool _worldInitialized = false;
        private bool _texturesLoaded = false;
        private bool _atlasTextureApplied = false;

        private enum InitializationState { Uninitialized, WaitingForWorldInit, WaitingForWorldData, ReadyForRendering, Rendering, Failed }
        private InitializationState _currentState = InitializationState.Uninitialized;

        private void Awake() => Initialize();

        private void Initialize()
        {
            if (_isInitialized) return;

            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();

            if (_meshFilter == null || _meshRenderer == null) 
            {
                Debug.LogError("WorldBackgroundRenderer: Missing MeshFilter or MeshRenderer component");
                return;
            }

            _mesh = new Mesh();
            _meshFilter.mesh = _mesh;
            _mainCamera = Camera.main;

            ConfigureBackgroundRendering();

            WorldTextureManager.Instance.OnTextureLoaded += OnTextureLoaded;

            if (MapManager.Instance != null)
            {
                MapManager.Instance.OnWorldInitialized += OnWorldInitialized;
                MapManager.Instance.OnWorldDataLoaded += OnWorldDataLoaded;
                Debug.Log("WorldBackgroundRenderer: Registered for MapManager events");
            }
            else
            {
                Debug.LogWarning("WorldBackgroundRenderer: MapManager not found - may affect initialization");
            }

            _isInitialized = true;
            _currentState = InitializationState.WaitingForWorldInit;
            
            Debug.Log("WorldBackgroundRenderer: Initialization started");

            // Start legacy coroutines for fallback
            StartCoroutine(ImmediateMapStorageCheck());
        }

        private void ConfigureBackgroundRendering()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader) shader = Shader.Find("Unlit/Texture");

            _backgroundMaterial = new Material(shader);
            _backgroundMaterial.name = "WorldBackgroundMaterial";
            
            // Ensure proper material properties for texture rendering
            _backgroundMaterial.SetColor("_Color", Color.white);
            _backgroundMaterial.SetFloat("_Surface", 0f); // Opaque
            _backgroundMaterial.SetFloat("_Cutoff", 0.5f);
            _backgroundMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            _backgroundMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            _backgroundMaterial.SetFloat("_ZWrite", 1f);
            _backgroundMaterial.EnableKeyword("_ALPHATEST_ON");
            _backgroundMaterial.DisableKeyword("_ALPHABLEND_ON");
            _backgroundMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            _backgroundMaterial.renderQueue = 2000;

            _meshRenderer.material = _backgroundMaterial;
            _meshRenderer.sortingOrder = _sortingOrder;
            _meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;
            _meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            // FIX: Set Z position
            var pos = transform.position;
            pos.z = _backgroundZ;
            transform.position = pos;
            
            Debug.Log("WorldBackgroundRenderer: Material configured for texture rendering");
        }

        private void Update()
        {
            if (!_isInitialized) return;

            if (!_worldInitialized) InitializeWorldLayer();

            if (_worldLayer != null && _currentState == InitializationState.ReadyForRendering)
            {
                UpdateVisibleChunks();
                UpdateMesh();
            }
        }

        public void ForceInitialization()
        {
            _worldInitialized = false;
            _worldLayer = null;
            _chunkMeshes.Clear();
            _visibleChunks.Clear();
            _mesh.Clear();
            InitializeWorldLayer();

            if (_worldLayer != null)
            {
                _currentState = InitializationState.ReadyForRendering;
                _worldInitialized = true;
                _lastCameraChunk = new Vector2Int(int.MinValue, int.MinValue); // Force update next frame
            }
        }

        public void OnDrawGizmosSelected()
        {
            if (!_debugMode || _worldLayer == null) return;
            Gizmos.color = Color.yellow;
            foreach (var chunkPos in _visibleChunks)
            {
                var center = new Vector3(
                    chunkPos.x * _chunkSize * _cellSize + (_chunkSize * _cellSize / 2),
                    chunkPos.y * _chunkSize * _cellSize + (_chunkSize * _cellSize / 2),
                    0
                );
                Gizmos.DrawWireCube(center, new Vector3(_chunkSize * _cellSize, _chunkSize * _cellSize, 0.1f));
            }
        }

        private void InitializeWorldLayer()
        {
            if (MapStorage.Instance?.cellLayer != null)
            {
                _worldLayer = MapStorage.Instance.cellLayer;
                _worldInitialized = true;
                _currentState = InitializationState.ReadyForRendering;
                // Force update
                _lastCameraChunk = new Vector2Int(int.MinValue, int.MinValue);
            }
        }

        private async void UpdateVisibleChunks()
        {
            if (_worldLayer == null || _mainCamera == null) return;

            var camPos = _mainCamera.transform.position;
            int cx = Mathf.FloorToInt(camPos.x / (_chunkSize * _cellSize));
            int cy = Mathf.FloorToInt(camPos.y / (_chunkSize * _cellSize));
            var currentChunk = new Vector2Int(cx, cy);

            if (currentChunk == _lastCameraChunk) return;
            _lastCameraChunk = currentChunk;

            var newVisible = new HashSet<Vector2Int>();
            for (int y = cy - _renderDistance; y <= cy + _renderDistance; y++)
            {
                for (int x = cx - _renderDistance; x <= cx + _renderDistance; x++)
                {
                    newVisible.Add(new Vector2Int(x, y));
                }
            }

            // Unload old
            foreach (var chunk in _visibleChunks)
            {
                if (!newVisible.Contains(chunk)) _chunkMeshes.TryRemove(chunk, out _);
            }
            _visibleChunks.Clear();
            _visibleChunks.UnionWith(newVisible);

            // Load new
            var loadTasks = new List<UniTask>();
            foreach (var chunk in newVisible)
            {
                if (!_chunkMeshes.ContainsKey(chunk))
                {
                    loadTasks.Add(GenerateChunkMeshAsync(chunk));
                }
            }

            if (loadTasks.Count > 0) await UniTask.WhenAll(loadTasks);
        }

        private async UniTask GenerateChunkMeshAsync(Vector2Int chunkPos)
        {
            var chunkMesh = new ChunkMesh(chunkPos);
            await GenerateGeometry(chunkMesh);
            await GenerateTextures(chunkMesh);
            _chunkMeshes[chunkPos] = chunkMesh;
        }

        private UniTask GenerateGeometry(ChunkMesh mesh)
        {
            int vertexIndex = 0;
            for (int y = 0; y < _chunkSize; y++)
            {
                for (int x = 0; x < _chunkSize; x++)
                {
                    int wx = mesh.ChunkPosition.x * _chunkSize + x;
                    int wy = mesh.ChunkPosition.y * _chunkSize + y;

                    CellType cell = CellType.Unloaded;
                    try { cell = MapStorage.Instance.GetCell(wx, wy); } catch { }

                    // 4 Vertices per quad
                    float gx = x * _cellSize;
                    float gy = y * _cellSize;

                    mesh.Vertices.Add(new Vector3(gx, gy, 0));
                    mesh.Vertices.Add(new Vector3(gx + _cellSize, gy, 0));
                    mesh.Vertices.Add(new Vector3(gx + _cellSize, gy + _cellSize, 0));
                    mesh.Vertices.Add(new Vector3(gx, gy + _cellSize, 0));

                    mesh.UVs.Add(Vector2.zero);
                    mesh.UVs.Add(Vector2.zero);
                    mesh.UVs.Add(Vector2.zero);
                    mesh.UVs.Add(Vector2.zero);

                    mesh.Triangles.Add(vertexIndex);
                    mesh.Triangles.Add(vertexIndex + 2);
                    mesh.Triangles.Add(vertexIndex + 1);
                    mesh.Triangles.Add(vertexIndex);
                    mesh.Triangles.Add(vertexIndex + 3);
                    mesh.Triangles.Add(vertexIndex + 2);

                    mesh.Cells.Add(new CellInfo { CellType = cell, VertexStartIndex = vertexIndex, WorldPosition = new Vector2Int(wx, wy) });
                    vertexIndex += 4;
                }
            }
            return UniTask.CompletedTask;
        }

        private async UniTask GenerateTextures(ChunkMesh mesh)
        {
            var coords = await UniTask.WhenAll(mesh.Cells.Select(c =>
                WorldTextureManager.Instance.GetCellTextureCoordinate(c.CellType, c.WorldPosition.x, c.WorldPosition.y)));

            for (int i = 0; i < mesh.Cells.Count; i++)
            {
                var c = mesh.Cells[i];
                var uv = coords[i];
                if (uv == AtlasCoordinate.Empty) continue;

                mesh.UVs[c.VertexStartIndex] = new Vector2(uv.U1, uv.V1);
                mesh.UVs[c.VertexStartIndex + 1] = new Vector2(uv.U2, uv.V1);
                mesh.UVs[c.VertexStartIndex + 2] = new Vector2(uv.U2, uv.V2);
                mesh.UVs[c.VertexStartIndex + 3] = new Vector2(uv.U1, uv.V2);
            }
        }

        private void UpdateMesh()
        {
            if (_chunkMeshes.Count == 0) return;

            var verts = new List<Vector3>();
            var tris = new List<int>();
            var uvs = new List<Vector2>();

            int vOffset = 0;
            foreach (var kvp in _chunkMeshes)
            {
                var chunk = kvp.Value;
                var offset = new Vector3(chunk.ChunkPosition.x * _chunkSize * _cellSize, chunk.ChunkPosition.y * _chunkSize * _cellSize, 0);

                foreach (var v in chunk.Vertices) verts.Add(v + offset);
                foreach (var t in chunk.Triangles) tris.Add(t + vOffset);
                uvs.AddRange(chunk.UVs);

                vOffset += chunk.Vertices.Count;
            }

            _mesh.Clear();
            // Use SetVertices for large lists if needed, but simple assignment works for < 65k verts. 
            // Since we have >65k verts (131k in logs), we must set indexFormat.
            _mesh.indexFormat = IndexFormat.UInt32;
            _mesh.SetVertices(verts);
            _mesh.SetTriangles(tris, 0);
            _mesh.SetUVs(0, uvs);
            _mesh.RecalculateBounds();
            _mesh.RecalculateNormals();
        }

        private void OnTextureLoaded(string name, Texture2D tex)
        {
            _texturesLoaded = true;
            Debug.Log($"WorldBackgroundRenderer: Texture loaded: {name}");
            ApplyAtlas();
        }

        private async void ApplyAtlas()
        {
            if (_atlasTextureApplied) return;
            
            var atlases = WorldTextureManager.Instance.GetAllAtlases();
            Debug.Log($"WorldBackgroundRenderer: Found {atlases.Count} atlas(es)");
            
            if (atlases.Count > 0)
            {
                var tex = await atlases[0].GetAtlasTexture();
                if (tex != null && _backgroundMaterial != null)
                {
                    _backgroundMaterial.mainTexture = tex;
                    _atlasTextureApplied = true;
                    Debug.Log($"WorldBackgroundRenderer: Atlas applied successfully. Texture size: {tex.width}x{tex.height}");
                    // Force refresh to update UVs if they changed
                    _lastCameraChunk = new Vector2Int(int.MinValue, int.MinValue);
                }
                else
                {
                    Debug.LogWarning($"WorldBackgroundRenderer: Failed to get atlas texture or material is null");
                }
            }
            else
            {
                Debug.LogWarning("WorldBackgroundRenderer: No atlases available for texture application");
            }
        }

        // Standard helpers
        public void ForceReinitialize() { _worldInitialized = false; Initialize(); }
        public bool IsProperlyConfigured() => _isInitialized;
        public int GetVisibleChunkCount() => _chunkMeshes.Count;
        public bool AreTexturesLoaded() => _texturesLoaded;
        public bool IsAtlasApplied() => _atlasTextureApplied;
        public string GetRendererState() => _currentState.ToString();
        private void OnWorldInitialized() => _currentState = InitializationState.WaitingForWorldData;
        private void OnWorldDataLoaded() { _currentState = InitializationState.ReadyForRendering; InitializeWorldLayer(); }

        /// <summary>
        /// Get detailed diagnostic information about the renderer state
        /// </summary>
        public string GetDiagnosticInfo()
        {
            var info = new System.Text.StringBuilder();
            info.AppendLine("=== WorldBackgroundRenderer Diagnostic Info ===");
            info.AppendLine($"State: {_currentState}");
            info.AppendLine($"Initialized: {_isInitialized}");
            info.AppendLine($"World Initialized: {_worldInitialized}");
            info.AppendLine($"Textures Loaded: {_texturesLoaded}");
            info.AppendLine($"Atlas Applied: {_atlasTextureApplied}");
            info.AppendLine($"Visible Chunks: {_chunkMeshes.Count}");
            info.AppendLine($"World Layer: {_worldLayer != null}");
            
            if (_backgroundMaterial != null)
            {
                info.AppendLine($"Material: {_backgroundMaterial.name}");
                info.AppendLine($"Main Texture: {_backgroundMaterial.mainTexture?.name ?? "None"}");
                info.AppendLine($"Texture Size: {_backgroundMaterial.mainTexture?.width ?? 0}x{_backgroundMaterial.mainTexture?.height ?? 0}");
            }
            else
            {
                info.AppendLine("Material: Null");
            }
            
            if (WorldTextureManager.Instance != null)
            {
                info.AppendLine($"Texture Manager: Available");
                info.AppendLine($"Atlas Count: {WorldTextureManager.Instance.GetAllAtlases().Count}");
            }
            else
            {
                info.AppendLine("Texture Manager: Not Available");
            }
            
            if (MapStorage.Instance != null)
            {
                info.AppendLine($"MapStorage: Ready = {MapStorage.Instance.IsReady}");
            }
            else
            {
                info.AppendLine("MapStorage: Not Available");
            }
            
            return info.ToString();
        }

        // Stub for existing coroutine reference
        private System.Collections.IEnumerator ImmediateMapStorageCheck() { yield break; }
        private System.Collections.IEnumerator AggressiveFallbackInitialization() { yield break; }
        private System.Collections.IEnumerator PeriodicInitializationCheck() { yield break; }
        private System.Collections.IEnumerator CheckStandaloneInitialization() { yield break; }
        private void TryEmergencyInitialization() { }

        private class CellInfo { public Vector2Int LocalPosition, WorldPosition; public CellType CellType; public int VertexStartIndex; }
        private class ChunkMesh
        {
            public Vector2Int ChunkPosition;
            public List<Vector3> Vertices = new();
            public List<int> Triangles = new();
            public List<Vector2> UVs = new();
            public List<CellInfo> Cells = new();
            public ChunkMesh(Vector2Int p) { ChunkPosition = p; }
        }
    }
}