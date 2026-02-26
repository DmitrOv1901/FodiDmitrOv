using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;
using MinesServer.Data;
using Fodinae.Assets.Scripts.Game.Managers;
using Fodinae.Assets.Scripts.World;

/// <summary>
/// Simple verification test for terrain rendering fixes
/// </summary>
public class TerrainRenderingVerificationTest : MonoBehaviour
{
    [Header("Test Configuration")]
    [Tooltip("Size of test world")]
    [SerializeField] private int testWorldWidth = 64;
    [SerializeField] private int testWorldHeight = 64;
    [SerializeField] private int testChunkSize = 32;
    
    [Header("Test Results")]
    [SerializeField] private bool testCompleted = false;
    [SerializeField] private bool mapStorageInitialized = false;
    [SerializeField] private bool worldLayerCreated = false;
    [SerializeField] private bool chunksLoaded = false;
    [SerializeField] private bool backgroundRendererWorking = false;
    
    private MapStorage _testMapStorage;
    private WorldLayer<CellType> _testWorldLayer;
    private WorldBackgroundRenderer _testRenderer;
    
    private async void Start()
    {
        Debug.Log("=== Starting Terrain Rendering Verification Test ===");
        
        try
        {
            // Test 1: Initialize MapStorage
            Debug.Log("Test 1: Initializing MapStorage...");
            await InitializeTestMapStorage();
            
            // Test 2: Create WorldLayer
            Debug.Log("Test 2: Creating WorldLayer...");
            await CreateTestWorldLayer();
            
            // Test 3: Load chunks
            Debug.Log("Test 3: Loading chunks...");
            await LoadTestChunks();
            
            // Test 4: Test background renderer
            Debug.Log("Test 4: Testing background renderer...");
            await TestBackgroundRenderer();
            
            // Test 5: Verify mesh generation
            Debug.Log("Test 5: Verifying mesh generation...");
            await VerifyMeshGeneration();
            
            testCompleted = true;
            Debug.Log("=== Terrain Rendering Verification Test COMPLETED ===");
            Debug.Log($"MapStorage Initialized: {mapStorageInitialized}");
            Debug.Log($"WorldLayer Created: {worldLayerCreated}");
            Debug.Log($"Chunks Loaded: {chunksLoaded}");
            Debug.Log($"Background Renderer Working: {backgroundRendererWorking}");
            
        }
        catch (Exception ex)
        {
            Debug.LogError($"Terrain Rendering Verification Test FAILED: {ex.Message}");
            Debug.LogException(ex);
        }
    }
    
    private async Task InitializeTestMapStorage()
    {
        // Dispose existing MapStorage if it exists
        if (MapStorage.Instance != null)
        {
            MapStorage.Instance.Dispose();
        }
        
        // Create test world
        string testWorldName = "terrain_test_world";
        MapStorage.Instance.InitWorld(testWorldName, testWorldWidth, testWorldHeight);
        
        // Wait for initialization
        await UniTask.Delay(1000);
        
        if (MapStorage.Instance.IsReady)
        {
            mapStorageInitialized = true;
            Debug.Log("✓ MapStorage initialized successfully");
        }
        else
        {
            throw new Exception("MapStorage initialization failed");
        }
    }
    
    private async Task CreateTestWorldLayer()
    {
        if (MapStorage.Instance?.cellLayer == null)
        {
            throw new Exception("WorldLayer not created");
        }
        
        _testWorldLayer = MapStorage.Instance.cellLayer;
        worldLayerCreated = true;
        Debug.Log("✓ WorldLayer created successfully");
        
        // Initialize some test data
        for (int y = 0; y < testWorldHeight; y++)
        {
            for (int x = 0; x < testWorldWidth; x++)
            {
                // Create a checkerboard pattern
                CellType cellType = (x + y) % 2 == 0 ? CellType.Road : CellType.Empty;
                _testWorldLayer[x, y] = cellType;
            }
        }
        
        Debug.Log("✓ Test world data initialized");
    }
    
    private async Task LoadTestChunks()
    {
        if (_testWorldLayer == null)
        {
            throw new Exception("WorldLayer not available for chunk loading");
        }
        
        // Test chunk loading for all chunks in the world
        int chunksLoaded = 0;
        int totalChunks = _testWorldLayer.WidthChunks * _testWorldLayer.HeightChunks;
        
        for (int chunkY = 0; chunkY < _testWorldLayer.HeightChunks; chunkY++)
        {
            for (int chunkX = 0; chunkX < _testWorldLayer.WidthChunks; chunkX++)
            {
                try
                {
                    // This should now work with our fix
                    var chunk = _testWorldLayer.GetChunk(chunkY + chunkX * _testWorldLayer._heightChunks);
                    
                    if (chunk != null)
                    {
                        chunksLoaded++;
                        Debug.Log($"✓ Chunk ({chunkX}, {chunkY}) loaded successfully");
                    }
                    else
                    {
                        Debug.LogWarning($"Chunk ({chunkX}, {chunkY}) not loaded");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to load chunk ({chunkX}, {chunkY}): {ex.Message}");
                }
            }
        }
        
        if (chunksLoaded > 0)
        {
            this.chunksLoaded = true;
            Debug.Log($"✓ {chunksLoaded}/{totalChunks} chunks loaded successfully");
        }
        else
        {
            throw new Exception("No chunks were loaded");
        }
    }
    
    private async Task TestBackgroundRenderer()
    {
        // Find or create a WorldBackgroundRenderer
        _testRenderer = FindObjectOfType<WorldBackgroundRenderer>();
        
        if (_testRenderer == null)
        {
            // Create a test renderer
            GameObject rendererObject = new GameObject("TestBackgroundRenderer");
            _testRenderer = rendererObject.AddComponent<WorldBackgroundRenderer>();
            
            // Configure test settings
            _testRenderer._chunkSize = testChunkSize;
            _testRenderer._renderDistance = 5;
            _testRenderer._cellSize = 1.0f;
            _testRenderer._debugMode = true;
        }
        
        // Wait for renderer initialization
        await UniTask.Delay(2000);
        
        // Check if renderer is properly configured
        if (_testRenderer.IsProperlyConfigured())
        {
            backgroundRendererWorking = true;
            Debug.Log("✓ Background renderer properly configured");
        }
        else
        {
            Debug.LogWarning("Background renderer not properly configured");
        }
        
        // Test renderer state
        string rendererState = _testRenderer.GetRendererState();
        Debug.Log($"Background renderer state: {rendererState}");
        
        // Test visible chunk count
        int visibleChunks = _testRenderer.GetVisibleChunkCount();
        Debug.Log($"Visible chunks: {visibleChunks}");
    }
    
    private async Task VerifyMeshGeneration()
    {
        if (_testRenderer == null)
        {
            throw new Exception("Background renderer not available for mesh verification");
        }
        
        // Wait a bit for mesh generation
        await UniTask.Delay(1000);
        
        // Check if mesh has been generated
        MeshFilter meshFilter = _testRenderer.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.mesh != null)
        {
            Mesh mesh = meshFilter.mesh;
            int vertexCount = mesh.vertices.Length;
            int triangleCount = mesh.triangles.Length;
            
            Debug.Log($"✓ Mesh generated: {vertexCount} vertices, {triangleCount} triangles");
            
            if (vertexCount > 0 && triangleCount > 0)
            {
                Debug.Log("✓ Mesh generation successful - terrain should be visible");
            }
            else
            {
                Debug.LogWarning("Mesh generated but has no vertices or triangles");
            }
        }
        else
        {
            Debug.LogWarning("No mesh found in background renderer");
        }
    }
    
    private void OnDestroy()
    {
        // Clean up test data
        if (_testMapStorage != null)
        {
            _testMapStorage.Dispose();
        }
    }
}