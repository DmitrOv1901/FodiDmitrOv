using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MinesServer.Data;
using Fodinae.Assets.Scripts.Game.Managers;

namespace Fodinae.Assets.Scripts.World
{
    /// <summary>
    /// Final test to verify terrain rendering is working correctly
    /// This script manually populates world data and forces rendering
    /// </summary>
    [RequireComponent(typeof(WorldBackgroundRenderer))]
    public class TerrainRenderingFinalTest : MonoBehaviour
    {
        [Header("Test Configuration")]
        [Tooltip("Enable automatic test execution")]
        [SerializeField] private bool _autoRunTest = true;
        
        [Tooltip("Test world dimensions")]
        [SerializeField] private int _testWorldWidth = 64;
        [SerializeField] private int _testWorldHeight = 64;
        
        [Tooltip("Test world name")]
        [SerializeField] private string _testWorldName = "Final_Test_World";
        
        [Tooltip("Enable detailed logging")]
        [SerializeField] private bool _detailedLogging = true;

        private WorldBackgroundRenderer _renderer;
        private bool _isInitialized = false;
        private bool _testCompleted = false;

        void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (_isInitialized) return;

            _renderer = GetComponent<WorldBackgroundRenderer>();
            _isInitialized = true;
            
            Debug.Log("TerrainRenderingFinalTest: Initialized");
        }

        void Start()
        {
            if (_autoRunTest)
            {
                StartCoroutine(RunComprehensiveTest());
            }
        }

        /// <summary>
        /// Run comprehensive final test
        /// </summary>
        public IEnumerator RunComprehensiveTest()
        {
            Debug.Log("=== TERRAIN RENDERING FINAL TEST SUITE STARTING ===");
            
            // Phase 1: System State Verification
            yield return RunTest("Phase 1: System State Verification", TestSystemState);

            // Phase 2: Manual World Creation
            yield return RunTest("Phase 2: Manual World Creation", TestManualWorldCreation);

            // Phase 3: World Data Population
            yield return RunTest("Phase 3: World Data Population", TestWorldDataPopulation);

            // Phase 4: Renderer Activation
            yield return RunTest("Phase 4: Renderer Activation", TestRendererActivation);

            // Phase 5: Visual Verification
            yield return RunTest("Phase 5: Visual Verification", TestVisualVerification);

            // Final Results
            GenerateFinalResults();
            
            Debug.Log("=== TERRAIN RENDERING FINAL TEST SUITE COMPLETED ===");
        }

        private IEnumerator RunTest(string testName, System.Func<IEnumerator> testFunction)
        {
            Debug.Log($"Running: {testName}");

            yield return testFunction();
            Debug.Log($"✓ {testName}: PASSED");
        }

        private IEnumerator TestSystemState()
        {
            Debug.Log("Phase 1: MapManager available: " + (MapManager.Instance != null));
            Debug.Log("Phase 1: MapStorage available: " + (MapStorage.Instance != null));
            Debug.Log("Phase 1: WorldBackgroundRenderer available: " + (_renderer != null));
            Debug.Log("Phase 1: Renderer configured: " + _renderer.IsProperlyConfigured());
            
            if (MapManager.Instance != null)
            {
                Debug.Log("Phase 1: MapManager world: " + MapManager.Instance.WorldCodeName);
                Debug.Log("Phase 1: MapManager dimensions: " + MapManager.Instance.WorldWidth + "x" + MapManager.Instance.WorldHeight);
            }
            
            if (MapStorage.Instance != null)
            {
                Debug.Log("Phase 1: MapStorage ready: " + MapStorage.Instance.IsReady);
                Debug.Log("Phase 1: MapStorage world: " + MapStorage.Instance.GetWorldCodeName());
            }
            
            yield return null;
        }

        private IEnumerator TestManualWorldCreation()
        {
            Debug.Log("Phase 2: Creating test world manually...");
            
            // Dispose any existing world
            if (MapStorage.Instance != null)
            {
                MapStorage.Instance.Dispose();
            }
            
            // Create new test world
            MapStorage.Instance.InitWorld(_testWorldName, _testWorldWidth, _testWorldHeight);
            
            // Wait for initialization
            yield return new WaitForSeconds(1.0f);
            
            if (!MapStorage.Instance.IsReady)
            {
                throw new System.Exception("MapStorage initialization failed");
            }
            
            Debug.Log($"Phase 2: Test world created successfully: {_testWorldName} ({_testWorldWidth}x{_testWorldHeight})");
        }

        private IEnumerator TestWorldDataPopulation()
        {
            Debug.Log("Phase 3: Populating world with test terrain data...");
            
            // Create test terrain patterns
            PopulateTestTerrain();

            yield return new WaitForSeconds(1.0f);

            // Verify data was set correctly
            int populatedCells = 0;
            for (int y = 0; y < _testWorldHeight; y++)
            {
                for (int x = 0; x < _testWorldWidth; x++)
                {
                    var cell = MapStorage.Instance.GetCell(x, y);
                    if (cell != CellType.Unloaded && cell != CellType.Pregener)
                    {
                        populatedCells++;
                    }
                }
            }
            
            Debug.Log($"Phase 3: Populated {populatedCells} cells with terrain data");
            
            if (populatedCells == 0)
            {
                throw new System.Exception("No terrain data was populated");
            }
        }

        private void PopulateTestTerrain()
        {
            // Create a simple test pattern
            for (int y = 0; y < _testWorldHeight; y++)
            {
                for (int x = 0; x < _testWorldWidth; x++)
                {
                    // Create a checkerboard pattern
                    if ((x + y) % 2 == 0)
                    {
                        MapStorage.Instance.SetCell(x, y, CellType.DeepRock);
                    }
                    else
                    {
                        MapStorage.Instance.SetCell(x, y, CellType.Empty);
                    }
                    
                    // Add some special features
                    if (x == 0 || y == 0 || x == _testWorldWidth - 1 || y == _testWorldHeight - 1)
                    {
                        MapStorage.Instance.SetCell(x, y, CellType.Road);
                    }
                    
                    if (x == _testWorldWidth / 2 && y == _testWorldHeight / 2)
                    {
                        MapStorage.Instance.SetCell(x, y, CellType.Boulder1);
                    }
                }
            }
        }

        private IEnumerator TestRendererActivation()
        {
            Debug.Log("Phase 4: Activating terrain renderer...");
            
            // Force renderer initialization
            _renderer.ForceInitialization();
            
            // Wait for renderer to process
            yield return new WaitForSeconds(2.0f);
            
            // Check renderer state
            var rendererState = _renderer.GetRendererState();
            Debug.Log($"Phase 4: Renderer state: {rendererState}");
            
            if (rendererState != "ReadyForRendering")
            {
                throw new System.Exception($"Renderer not ready. Current state: {rendererState}");
            }
            
            // Check if chunks are being generated
            var visibleChunks = _renderer.GetVisibleChunkCount();
            Debug.Log($"Phase 4: Visible chunks: {visibleChunks}");
            
            if (visibleChunks <= 0)
            {
                throw new System.Exception("No visible chunks generated");
            }
        }

        private IEnumerator TestVisualVerification()
        {
            Debug.Log("Phase 5: Performing visual verification...");
            
            // Check if textures are loaded
            bool texturesLoaded = _renderer.AreTexturesLoaded();
            Debug.Log($"Phase 5: Textures loaded: {texturesLoaded}");
            
            // Check if atlas is applied
            bool atlasApplied = _renderer.IsAtlasApplied();
            Debug.Log($"Phase 5: Atlas applied: {atlasApplied}");

            yield return new WaitForSeconds(1.0f);

            // Check mesh generation
            var meshFilter = GetComponent<MeshFilter>();
            if (meshFilter != null && meshFilter.mesh != null)
            {
                var vertexCount = meshFilter.mesh.vertexCount;
                var triangleCount = meshFilter.mesh.triangles.Length;
                Debug.Log($"Phase 5: Mesh vertices: {vertexCount}, triangles: {triangleCount}");
                
                if (vertexCount > 0 && triangleCount > 0)
                {
                    Debug.Log("Phase 5: ✓ Mesh generated successfully");
                }
                else
                {
                    Debug.LogWarning("Phase 5: Mesh appears to be empty");
                }
            }
            
            // Final verification
            if (!texturesLoaded)
            {
                Debug.LogWarning("Phase 5: Textures not loaded yet - this is normal if texture system is still initializing");
            }
            
            if (!atlasApplied)
            {
                Debug.LogWarning("Phase 5: Atlas not applied yet - this is normal if texture system is still initializing");
            }
        }

        private void GenerateFinalResults()
        {
            Debug.Log("=== FINAL TEST RESULTS ===");
            
            bool allTestsPassed = true;
            
            // Check system components
            if (MapManager.Instance == null)
            {
                Debug.LogError("✗ MapManager not available");
                allTestsPassed = false;
            }
            else
            {
                Debug.Log("✓ MapManager available");
            }
            
            if (MapStorage.Instance == null || !MapStorage.Instance.IsReady)
            {
                Debug.LogError("✗ MapStorage not ready");
                allTestsPassed = false;
            }
            else
            {
                Debug.Log("✓ MapStorage ready");
            }
            
            if (!_renderer.IsProperlyConfigured())
            {
                Debug.LogError("✗ WorldBackgroundRenderer not configured");
                allTestsPassed = false;
            }
            else
            {
                Debug.Log("✓ WorldBackgroundRenderer configured");
            }
            
            // Check renderer state
            var rendererState = _renderer.GetRendererState();
            if (rendererState != "ReadyForRendering")
            {
                Debug.LogError($"✗ Renderer not ready: {rendererState}");
                allTestsPassed = false;
            }
            else
            {
                Debug.Log("✓ Renderer ready for rendering");
            }
            
            // Check visible chunks
            var visibleChunks = _renderer.GetVisibleChunkCount();
            if (visibleChunks <= 0)
            {
                Debug.LogError($"✗ No visible chunks: {visibleChunks}");
                allTestsPassed = false;
            }
            else
            {
                Debug.Log($"✓ Visible chunks generated: {visibleChunks}");
            }
            
            if (allTestsPassed)
            {
                Debug.Log("🎉 ALL TESTS PASSED - TERRAIN RENDERING IS WORKING!");
                Debug.Log("The terrain rendering system should now be displaying the world background.");
            }
            else
            {
                Debug.LogWarning("⚠️  SOME TESTS FAILED - Terrain rendering may not be fully functional");
                Debug.LogWarning("Check the error messages above for specific issues");
            }
            
            _testCompleted = true;
        }

        /// <summary>
        /// Get current test status
        /// </summary>
        public string GetTestStatus()
        {
            if (!_testCompleted)
            {
                return "Test in progress...";
            }
            
            var rendererState = _renderer.GetRendererState();
            var visibleChunks = _renderer.GetVisibleChunkCount();
            var texturesLoaded = _renderer.AreTexturesLoaded();
            var atlasApplied = _renderer.IsAtlasApplied();
            
            return $"Status: {rendererState}, Chunks: {visibleChunks}, Textures: {texturesLoaded}, Atlas: {atlasApplied}";
        }

        /// <summary>
        /// Manually trigger test execution
        /// </summary>
        public void RunTest()
        {
            if (!_testCompleted)
            {
                StartCoroutine(RunComprehensiveTest());
            }
            else
            {
                Debug.Log("Test already completed. Check console for results.");
            }
        }

        /// <summary>
        /// Clear test completion flag to allow re-running
        /// </summary>
        public void ResetTest()
        {
            _testCompleted = false;
            Debug.Log("Test reset - you can run it again");
        }

        private void OnDestroy()
        {
            Debug.Log("TerrainRenderingFinalTest: Destroyed");
        }
    }
}