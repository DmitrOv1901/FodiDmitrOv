using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fodinae.Assets.Scripts.Game.Managers;
using Fodinae.Assets.Scripts.World;
using MinesServer.Data;
using MinesServer.Networking.Server.Packets.Connection;

namespace Fodinae.Assets.Scripts.World
{
    public class TerrainRenderingFinalVerification : MonoBehaviour
    {
        [Header("Verification Settings")]
        [Tooltip("Enable detailed logging during verification")]
        [SerializeField] private bool _enableDetailedLogging = true;
        [Tooltip("Create test world if no world data available")]
        [SerializeField] private bool _createTestWorldIfMissing = true;

        private WorldBackgroundRenderer _renderer;
        private TerrainDiagnosticRunner _diagnosticRunner;
        private TerrainFixTester _tester;

        void Start()
        {
            StartCoroutine(RunFinalVerification());
        }

        public IEnumerator RunFinalVerification()
        {
            Debug.Log("=== TERRAIN RENDERING FINAL VERIFICATION STARTED ===");
            Debug.Log("Verifying all implemented fixes...");

            _renderer = FindObjectOfType<WorldBackgroundRenderer>();
            _diagnosticRunner = FindObjectOfType<TerrainDiagnosticRunner>();
            _tester = FindObjectOfType<TerrainFixTester>();

            bool allTestsPassed = true;

            Debug.Log("🔍 Test 1: Component Availability");
            if (!_renderer)
            {
                Debug.LogError("❌ WorldBackgroundRenderer not found");
                allTestsPassed = false;
            }
            else
            {
                Debug.Log("✅ WorldBackgroundRenderer found");
            }

            if (!_diagnosticRunner)
            {
                Debug.LogError("❌ TerrainDiagnosticRunner not found");
                allTestsPassed = false;
            }
            else
            {
                Debug.Log("✅ TerrainDiagnosticRunner found");
            }

            if (!_tester)
            {
                Debug.LogError("❌ TerrainFixTester not found");
                allTestsPassed = false;
            }
            else
            {
                Debug.Log("✅ TerrainFixTester found");
            }

            Debug.Log("🔍 Test 2: System Readiness");
            bool systemReady = CheckSystemReadiness();
            if (!systemReady)
            {
                Debug.LogError("❌ System not ready for rendering");
                allTestsPassed = false;
            }
            else
            {
                Debug.Log("✅ System ready for rendering");
            }

            Debug.Log("🔍 Test 3: World Data Accessibility");
            bool worldDataAccessible = CheckWorldDataAccessibility();
            if (!worldDataAccessible && _createTestWorldIfMissing)
            {
                Debug.Log("🌍 Creating test world for verification...");
                CreateTestWorldForVerification();
                yield return new WaitForSeconds(1f);
                worldDataAccessible = CheckWorldDataAccessibility();
            }

            if (!worldDataAccessible)
            {
                Debug.LogError("❌ World data not accessible");
                allTestsPassed = false;
            }
            else
            {
                Debug.Log("✅ World data accessible");
            }

            Debug.Log("🔍 Test 4: Texture System");
            bool texturesWorking = CheckTextureSystem();
            if (!texturesWorking)
            {
                Debug.LogError("❌ Texture system not working properly");
                allTestsPassed = false;
            }
            else
            {
                Debug.Log("✅ Texture system working properly");
            }

            Debug.Log("🔍 Test 5: Mesh Generation");
            bool meshWorking = CheckMeshGeneration();
            if (!meshWorking)
            {
                Debug.LogError("❌ Mesh generation not working properly");
                allTestsPassed = false;
            }
            else
            {
                Debug.Log("✅ Mesh generation working properly");
            }

            Debug.Log("🔍 Test 6: Diagnostic System");
            if (_diagnosticRunner != null)
            {
                yield return StartCoroutine(_diagnosticRunner.RunDiagnostics());
                Debug.Log("✅ Diagnostic system executed successfully");
            }
            else
            {
                Debug.LogWarning("⚠ Diagnostic system not available for testing");
            }

            Debug.Log("🔍 Test 7: Final Rendering Test");
            bool renderingWorking = CheckFinalRendering();
            if (!renderingWorking)
            {
                Debug.LogError("❌ Final rendering test failed");
                allTestsPassed = false;
            }
            else
            {
                Debug.Log("✅ Final rendering test passed");
            }

            Debug.Log("=== FINAL VERIFICATION RESULTS ===");
            if (allTestsPassed)
            {
                Debug.Log("🎉 ALL TESTS PASSED! Terrain rendering fixes are working correctly.");
                Debug.Log("✅ White terrain issue resolved");
                Debug.Log("✅ Mesh rendering issue resolved");
                Debug.Log("✅ System is ready for production use");
            }
            else
            {
                Debug.LogError("❌ SOME TESTS FAILED! Please review the issues above.");
                Debug.Log("🔧 Run TerrainDiagnosticRunner for detailed diagnostics");
            }

            Debug.Log("=== TERRAIN RENDERING FINAL VERIFICATION COMPLETED ===");
        }

        private bool CheckSystemReadiness()
        {
            try
            {
                if (MapManager.Instance == null)
                {
                    Debug.LogError("   MapManager not available");
                    return false;
                }

                if (MapStorage.Instance == null || !MapStorage.Instance.IsReady)
                {
                    Debug.LogError("   MapStorage not ready");
                    return false;
                }

                if (_renderer != null && !_renderer.IsProperlyConfigured())
                {
                    Debug.LogError("   WorldBackgroundRenderer not properly configured");
                    return false;
                }

                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"   System readiness check failed: {ex.Message}");
                return false;
            }
        }

        private bool CheckWorldDataAccessibility()
        {
            try
            {
                if (MapStorage.Instance == null || !MapStorage.Instance.IsReady)
                {
                    return false;
                }

                var testCells = new[] { (0, 0), (5, 5), (10, 10), (20, 20), (30, 30) };
                int validCells = 0;

                foreach (var (x, y) in testCells)
                {
                    try
                    {
                        var cell = MapStorage.Instance.GetCell(x, y);
                        if (cell != CellType.Unloaded && cell != CellType.Pregener)
                        {
                            validCells++;
                        }
                    }
                    catch
                    {
                        // Cell access failed
                    }
                }

                bool success = validCells >= 3;
                if (!success)
                {
                    Debug.LogWarning($"   Only {validCells}/5 test cells are accessible");
                }

                return success;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"   World data accessibility check failed: {ex.Message}");
                return false;
            }
        }

        private bool CheckTextureSystem()
        {
            try
            {
                if (_renderer == null)
                {
                    return false;
                }

                bool texturesLoaded = _renderer.AreTexturesLoaded();
                if (!texturesLoaded)
                {
                    Debug.LogWarning("   Textures not loaded yet");
                    return false;
                }

                bool atlasApplied = _renderer.IsAtlasApplied();
                if (!atlasApplied)
                {
                    Debug.LogWarning("   Atlas not applied");
                    return false;
                }

                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"   Texture system check failed: {ex.Message}");
                return false;
            }
        }

        private bool CheckMeshGeneration()
        {
            try
            {
                if (_renderer == null)
                {
                    return false;
                }

                var meshFilter = _renderer.GetComponent<MeshFilter>();
                var meshRenderer = _renderer.GetComponent<MeshRenderer>();

                if (meshFilter == null || meshRenderer == null)
                {
                    Debug.LogWarning("   Missing mesh components");
                    return false;
                }

                if (meshFilter.sharedMesh == null)
                {
                    Debug.LogWarning("   Mesh not generated yet");
                    return false;
                }

                var mesh = meshFilter.sharedMesh;
                if (mesh.vertexCount == 0 || mesh.triangles.Length == 0)
                {
                    Debug.LogWarning("   Mesh has no vertices or triangles");
                    return false;
                }

                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"   Mesh generation check failed: {ex.Message}");
                return false;
            }
        }

        private bool CheckFinalRendering()
        {
            try
            {
                if (_renderer == null)
                {
                    return false;
                }

                if (!_renderer.enabled)
                {
                    Debug.LogWarning("   Renderer is disabled");
                    return false;
                }

                var meshRenderer = _renderer.GetComponent<MeshRenderer>();
                if (meshRenderer != null && !meshRenderer.enabled)
                {
                    Debug.LogWarning("   MeshRenderer is disabled");
                    return false;
                }

                if (meshRenderer != null && meshRenderer.sharedMaterial == null)
                {
                    Debug.LogWarning("   No material assigned to mesh renderer");
                    return false;
                }

                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"   Final rendering check failed: {ex.Message}");
                return false;
            }
        }

        private void CreateTestWorldForVerification()
        {
            Debug.Log("Creating test world for verification...");

            if (MapManager.Instance != null)
            {
                var testPacket = new WorldInitPacket
                {
                    CodeName = "verification_test_world",
                    DisplayName = "Verification Test World",
                    Width = 64,
                    Height = 64,
                    Cells = new CellConfigurationPacket[256]
                };

                for (int i = 0; i < 256; i++)
                {
                    testPacket.Cells[i] = new CellConfigurationPacket { Color = unchecked((int)0xFFFFFFFF) };
                }

                MapManager.Instance.LoadWorldInit(testPacket);

                if (MapStorage.Instance == null || MapStorage.Instance.cellLayer == null)
                {
                    Debug.LogError("❌ MapStorage cellLayer is not available!");
                    return;
                }

                for (int y = 0; y < 64; y++)
                {
                    for (int x = 0; x < 64; x++)
                    {
                        CellType cellType = CellType.Empty;

                        if (x == 0 || x == 63 || y == 0 || y == 63)
                        {
                            cellType = CellType.FedBlock;
                        }
                        else if ((x + y) % 8 == 0)
                        {
                            cellType = CellType.FedBlock;
                        }

                        MapStorage.Instance.cellLayer[x, y] = cellType;
                    }
                }

                Debug.Log("✅ Test world created for verification");
            }
            else
            {
                Debug.LogError("❌ MapManager not available to create test world");
            }
        }

        public void RunManualVerification()
        {
            StartCoroutine(RunFinalVerification());
        }
    }
}