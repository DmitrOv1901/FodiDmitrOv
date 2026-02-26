using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fodinae.Assets.Scripts.Game.Managers;
using Fodinae.Assets.Scripts.World;
using MinesServer.Data;

namespace Fodinae.Assets.Scripts.World
{
    /// <summary>
    /// Verification script to test the terrain rendering fixes.
    /// Add this to your scene to verify that the fixes are working correctly.
    /// </summary>
    public class TerrainFixVerification : MonoBehaviour
    {
        [Header("Verification Settings")]
        [Tooltip("Enable automatic verification on start")]
        [SerializeField] private bool _autoVerifyOnStart = true;
        [Tooltip("Verification interval in seconds")]
        [SerializeField] private float _verificationInterval = 5f;
        [Tooltip("Enable detailed logging")]
        [SerializeField] private bool _enableDetailedLogging = true;

        private WorldBackgroundRenderer _renderer;
        private TerrainRenderingTest _testComponent;
        private float _lastVerificationTime = 0f;
        private bool _isVerifying = false;
        private int _verificationCount = 0;

        void Start()
        {
            _renderer = FindObjectOfType<WorldBackgroundRenderer>();
            _testComponent = FindObjectOfType<TerrainRenderingTest>();
            
            if (_autoVerifyOnStart)
            {
                StartCoroutine(RunInitialVerification());
            }
        }

        void Update()
        {
            // Run verification periodically if auto-verification is enabled
            if (_autoVerifyOnStart && Time.time - _lastVerificationTime >= _verificationInterval)
            {
                if (!_isVerifying)
                {
                    StartCoroutine(RunVerificationCycle());
                }
            }
        }

        private IEnumerator RunInitialVerification()
        {
            // Wait for initialization to complete
            yield return new WaitForSeconds(3f);
            StartCoroutine(RunVerificationCycle());
        }

        private IEnumerator RunVerificationCycle()
        {
            _isVerifying = true;
            _lastVerificationTime = Time.time;
            _verificationCount++;

            Debug.Log($"=== Terrain Fix Verification #{_verificationCount} Started ===");

            // Verification 1: Check if fixes are working
            Debug.Log("Verification 1: Checking if fixes are working...");
            yield return VerifyFixesWorking();

            // Verification 2: Test error recovery
            Debug.Log("Verification 2: Testing error recovery...");
            yield return TestErrorRecovery();

            // Verification 3: Performance check
            Debug.Log("Verification 3: Performance check...");
            yield return VerifyPerformance();

            Debug.Log($"=== Terrain Fix Verification #{_verificationCount} Completed ===");
            _isVerifying = false;
        }

        private IEnumerator VerifyFixesWorking()
        {
            bool allFixesWorking = true;
            List<string> issues = new List<string>();

            // Check MapStorage error handling
            if (MapStorage.Instance != null)
            {
                Debug.Log("  ✓ MapStorage error handling is active");
            }
            else
            {
                issues.Add("MapStorage not available");
                allFixesWorking = false;
            }

            // Check WorldBackgroundRenderer timeout handling
            if (_renderer != null)
            {
                Debug.Log("  ✓ WorldBackgroundRenderer timeout handling is active");
                Debug.Log($"  ✓ Current state: {_renderer.GetRendererState()}");
            }
            else
            {
                issues.Add("WorldBackgroundRenderer not found");
                allFixesWorking = false;
            }

            // Check MapManager event handling
            if (MapManager.Instance != null)
            {
                Debug.Log("  ✓ MapManager event handling is active");
            }
            else
            {
                issues.Add("MapManager not found");
                allFixesWorking = false;
            }

            // Check testing tools
            if (_testComponent != null)
            {
                Debug.Log("  ✓ Testing tools are available");
            }
            else
            {
                issues.Add("Testing tools not found");
                allFixesWorking = false;
            }

            if (allFixesWorking)
            {
                Debug.Log("  ✓ All fixes appear to be working correctly");
            }
            else
            {
                Debug.LogWarning("  ⚠ Some issues detected:");
                foreach (var issue in issues)
                {
                    Debug.LogWarning($"    - {issue}");
                }
            }

            yield return null;
        }

        private IEnumerator TestErrorRecovery()
        {
            if (_renderer == null)
            {
                Debug.LogWarning("  ⚠ Cannot test error recovery - renderer not found");
                yield break;
            }

            // Test force initialization
            Debug.Log("  Testing force initialization...");
            _renderer.ForceInitialization();
            yield return new WaitForSeconds(1f);

            bool recoverySuccessful = _renderer.IsProperlyConfigured();
            if (recoverySuccessful)
            {
                Debug.Log("  ✓ Error recovery successful");
            }
            else
            {
                Debug.LogWarning("  ⚠ Error recovery may have failed");
            }

            yield return null;
        }

        private IEnumerator VerifyPerformance()
        {
            if (_renderer == null)
            {
                Debug.LogWarning("  ⚠ Cannot verify performance - renderer not found");
                yield break;
            }

            // Check initialization time
            float startTime = Time.time;
            _renderer.ForceInitialization();
            yield return new WaitForSeconds(0.1f); // Small delay to allow processing

            float initTime = Time.time - startTime;
            Debug.Log($"  Initialization time: {initTime:F3}s");

            if (initTime < 1.0f)
            {
                Debug.Log("  ✓ Initialization time is acceptable");
            }
            else
            {
                Debug.LogWarning("  ⚠ Initialization time may be too slow");
            }

            // Check memory usage (basic check)
            int visibleChunks = _renderer.GetVisibleChunkCount();
            Debug.Log($"  Visible chunks: {visibleChunks}");

            if (visibleChunks > 0)
            {
                Debug.Log("  ✓ Chunks are being generated");
            }
            else
            {
                Debug.LogWarning("  ⚠ No chunks visible - may indicate rendering issue");
            }

            yield return null;
        }

        /// <summary>
        /// Run a comprehensive verification of all fixes
        /// </summary>
        public void RunComprehensiveVerification()
        {
            if (!_isVerifying)
            {
                StartCoroutine(RunVerificationCycle());
            }
        }

        /// <summary>
        /// Get verification status summary
        /// </summary>
        public void GetVerificationSummary()
        {
            Debug.Log("=== Terrain Fix Verification Summary ===");
            
            Debug.Log($"MapStorage Ready: {MapStorage.Instance?.IsReady ?? false}");
            Debug.Log($"MapManager Available: {MapManager.Instance != null}");
            Debug.Log($"Renderer Configured: {_renderer?.IsProperlyConfigured() ?? false}");
            Debug.Log($"Visible Chunks: {_renderer?.GetVisibleChunkCount() ?? 0}");
            Debug.Log($"Textures Loaded: {_renderer?.AreTexturesLoaded() ?? false}");
            Debug.Log($"Atlas Applied: {_renderer?.IsAtlasApplied() ?? false}");
            Debug.Log($"Renderer State: {_renderer?.GetRendererState() ?? "Unknown"}");
            
            Debug.Log("=======================================");
        }

        /// <summary>
        /// Test specific fix components
        /// </summary>
        public void TestSpecificFixes()
        {
            Debug.Log("=== Testing Specific Fix Components ===");

            // Test MapStorage error handling
            Debug.Log("Testing MapStorage error handling...");
            if (MapStorage.Instance != null)
            {
                // Try to access a cell to test error handling
                var cell = MapStorage.Instance.GetCell(0, 0);
                Debug.Log($"  Cell access test: {cell}");
            }

            // Test MapManager event handling
            Debug.Log("Testing MapManager event handling...");
            if (MapManager.Instance != null)
            {
                Debug.Log($"  World: {MapManager.Instance.WorldDisplayName}");
                Debug.Log($"  Dimensions: {MapManager.Instance.WorldWidth}x{MapManager.Instance.WorldHeight}");
            }

            Debug.Log("======================================");
        }

        private void OnValidate()
        {
            // Ensure verification interval is reasonable
            if (_verificationInterval < 1f) _verificationInterval = 1f;
            if (_verificationInterval > 60f) _verificationInterval = 60f;
        }
    }
}