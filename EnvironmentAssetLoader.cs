using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace HazardousWorld
{
    /// <summary>
    /// Loads environment prefabs from AssetBundles for hostile flora and hazard decorations
    /// </summary>
    public static class EnvironmentAssetLoader
    {
        private static Dictionary<string, AssetBundle> loadedBundles = new Dictionary<string, AssetBundle>();
        private static Dictionary<string, GameObject> loadedPrefabs = new Dictionary<string, GameObject>();
        private static string bundlesPath;
        private static bool initialized = false;

        // Bundle names
        private const string BUNDLE_MUSHROOMS = "mushroom_forest";
        private const string BUNDLE_LAVA_PLANTS = "lava_plants";
        private const string BUNDLE_ALIEN_BUILDINGS = "alien_buildings";

        // Prefab categories for hostile flora
        public enum FloraType
        {
            SporePlant,      // Use mushroom prefabs
            VenomousThorn,   // Use alien plant prefabs
            AcidSpitter,     // Use pitcher-like plants
            GraspingVine     // Use vine/tendril prefabs
        }

        // Mapping flora types to bundle prefab patterns
        private static readonly Dictionary<FloraType, (string bundle, string[] patterns)> floraMappings = new Dictionary<FloraType, (string, string[])>
        {
            { FloraType.SporePlant, (BUNDLE_MUSHROOMS, new[] { "mushroom_spiral", "mushroom_bell", "mushroom_round" }) },
            { FloraType.VenomousThorn, (BUNDLE_MUSHROOMS, new[] { "mushroom_pointy", "mushroom_tall" }) },
            { FloraType.AcidSpitter, (BUNDLE_MUSHROOMS, new[] { "mushroom_simple", "mushroom_wavy" }) },
            { FloraType.GraspingVine, (BUNDLE_LAVA_PLANTS, new[] { "plant", "vine", "tendril" }) }
        };

        // Cached prefabs by flora type for quick random selection
        private static Dictionary<FloraType, List<GameObject>> floraPrefabCache = new Dictionary<FloraType, List<GameObject>>();

        /// <summary>
        /// Initialize the asset loader
        /// </summary>
        public static void Initialize(string pluginPath)
        {
            if (initialized) return;

            bundlesPath = Path.Combine(pluginPath, "Bundles");
            HazardousWorldPlugin.Log.LogInfo($"EnvironmentAssetLoader: Looking for bundles in {bundlesPath}");

            if (!Directory.Exists(bundlesPath))
            {
                HazardousWorldPlugin.Log.LogWarning($"EnvironmentAssetLoader: Bundles folder not found at {bundlesPath}");
                Directory.CreateDirectory(bundlesPath);
                HazardousWorldPlugin.Log.LogInfo("EnvironmentAssetLoader: Created Bundles folder - add environment bundles for 3D flora models");
                return;
            }

            LoadAllBundles();
            CachePrefabsByType();
            initialized = true;
        }

        /// <summary>
        /// Load all available bundles
        /// </summary>
        private static void LoadAllBundles()
        {
            string[] bundleNames = { BUNDLE_MUSHROOMS, BUNDLE_LAVA_PLANTS, BUNDLE_ALIEN_BUILDINGS };
            int loadedCount = 0;

            foreach (var bundleName in bundleNames)
            {
                string bundlePath = Path.Combine(bundlesPath, bundleName);

                if (!File.Exists(bundlePath))
                {
                    HazardousWorldPlugin.LogDebug($"EnvironmentAssetLoader: Bundle not found: {bundleName}");
                    continue;
                }

                try
                {
                    var bundle = AssetBundle.LoadFromFile(bundlePath);
                    if (bundle != null)
                    {
                        loadedBundles[bundleName] = bundle;
                        loadedCount++;

                        // Pre-load all prefabs from this bundle
                        LoadPrefabsFromBundle(bundleName, bundle);

                        HazardousWorldPlugin.Log.LogInfo($"EnvironmentAssetLoader: Loaded bundle '{bundleName}'");
                    }
                }
                catch (Exception ex)
                {
                    HazardousWorldPlugin.Log.LogError($"EnvironmentAssetLoader: Failed to load bundle '{bundleName}': {ex.Message}");
                }
            }

            HazardousWorldPlugin.Log.LogInfo($"EnvironmentAssetLoader: Loaded {loadedCount} bundles, {loadedPrefabs.Count} prefabs");
        }

        /// <summary>
        /// Load all prefabs from a bundle
        /// </summary>
        private static void LoadPrefabsFromBundle(string bundleName, AssetBundle bundle)
        {
            try
            {
                var allAssets = bundle.GetAllAssetNames();
                foreach (var assetPath in allAssets)
                {
                    if (assetPath.EndsWith(".prefab"))
                    {
                        try
                        {
                            var prefab = bundle.LoadAsset<GameObject>(assetPath);
                            if (prefab != null)
                            {
                                string prefabName = Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
                                string key = $"{bundleName}:{prefabName}";
                                loadedPrefabs[key] = prefab;
                                HazardousWorldPlugin.LogDebug($"EnvironmentAssetLoader: Loaded prefab '{prefabName}' from '{bundleName}'");
                            }
                        }
                        catch (Exception ex)
                        {
                            HazardousWorldPlugin.LogDebug($"EnvironmentAssetLoader: Failed to load prefab {assetPath}: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                HazardousWorldPlugin.Log.LogError($"EnvironmentAssetLoader: Error loading prefabs from {bundleName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Cache prefabs by flora type for quick random selection
        /// </summary>
        private static void CachePrefabsByType()
        {
            foreach (var mapping in floraMappings)
            {
                var floraType = mapping.Key;
                var (bundle, patterns) = mapping.Value;
                var prefabList = new List<GameObject>();

                foreach (var kvp in loadedPrefabs)
                {
                    // Check if this prefab matches the bundle and patterns
                    if (!kvp.Key.StartsWith(bundle + ":")) continue;

                    string prefabName = kvp.Key.Substring(bundle.Length + 1);
                    foreach (var pattern in patterns)
                    {
                        if (prefabName.Contains(pattern.ToLowerInvariant()))
                        {
                            prefabList.Add(kvp.Value);
                            break;
                        }
                    }
                }

                floraPrefabCache[floraType] = prefabList;
                HazardousWorldPlugin.LogDebug($"EnvironmentAssetLoader: Cached {prefabList.Count} prefabs for {floraType}");
            }
        }

        /// <summary>
        /// Get a random prefab for the specified flora type
        /// </summary>
        public static GameObject GetRandomPrefab(FloraType floraType)
        {
            if (!initialized) return null;

            if (floraPrefabCache.TryGetValue(floraType, out var prefabs) && prefabs.Count > 0)
            {
                return prefabs[UnityEngine.Random.Range(0, prefabs.Count)];
            }

            return null;
        }

        /// <summary>
        /// Get a specific prefab by name
        /// </summary>
        public static GameObject GetPrefab(string bundleName, string prefabName)
        {
            if (!initialized) return null;

            string key = $"{bundleName}:{prefabName.ToLowerInvariant()}";
            if (loadedPrefabs.TryGetValue(key, out var prefab))
            {
                return prefab;
            }

            // Try partial match
            foreach (var kvp in loadedPrefabs)
            {
                if (kvp.Key.Contains(prefabName.ToLowerInvariant()))
                {
                    return kvp.Value;
                }
            }

            return null;
        }

        /// <summary>
        /// Instantiate a flora model at the specified position
        /// </summary>
        public static GameObject InstantiateFlora(FloraType floraType, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            var prefab = GetRandomPrefab(floraType);

            if (prefab == null)
            {
                HazardousWorldPlugin.LogDebug($"EnvironmentAssetLoader: No prefab for {floraType}, using procedural fallback");
                return null;
            }

            try
            {
                var instance = UnityEngine.Object.Instantiate(prefab, position, rotation, parent);

                if (instance != null)
                {
                    // Clean up the instance
                    CleanupInstance(instance);

                    // Scale appropriately for Techtonica's world scale
                    float scale = GetScaleForFloraType(floraType);
                    instance.transform.localScale = Vector3.one * scale;

                    HazardousWorldPlugin.LogDebug($"EnvironmentAssetLoader: Instantiated {floraType} model ({prefab.name})");
                    return instance;
                }
            }
            catch (Exception ex)
            {
                HazardousWorldPlugin.Log.LogError($"EnvironmentAssetLoader: Failed to instantiate {floraType}: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Get appropriate scale for flora type
        /// </summary>
        private static float GetScaleForFloraType(FloraType floraType)
        {
            return floraType switch
            {
                FloraType.SporePlant => 1.5f,
                FloraType.VenomousThorn => 0.8f,
                FloraType.AcidSpitter => 1.2f,
                FloraType.GraspingVine => 2.0f,
                _ => 1.0f
            };
        }

        /// <summary>
        /// Clean up an instantiated model
        /// </summary>
        private static void CleanupInstance(GameObject instance)
        {
            try
            {
                // Remove any scripts that might cause issues
                var monoBehaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);
                foreach (var mb in monoBehaviours)
                {
                    if (mb == null) continue;

                    string typeName = mb.GetType().FullName ?? "";

                    // Remove demo/sample scripts
                    if (typeName.Contains("Demo") ||
                        typeName.Contains("Sample") ||
                        typeName.Contains("Example"))
                    {
                        try
                        {
                            UnityEngine.Object.Destroy(mb);
                        }
                        catch { }
                    }
                }

                // Remove existing colliders (we'll add our own)
                var colliders = instance.GetComponentsInChildren<Collider>(true);
                foreach (var col in colliders)
                {
                    if (col != null)
                    {
                        try
                        {
                            UnityEngine.Object.Destroy(col);
                        }
                        catch { }
                    }
                }

                // Remove Rigidbodies
                var rigidbodies = instance.GetComponentsInChildren<Rigidbody>(true);
                foreach (var rb in rigidbodies)
                {
                    if (rb != null)
                    {
                        try
                        {
                            UnityEngine.Object.Destroy(rb);
                        }
                        catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                HazardousWorldPlugin.LogDebug($"EnvironmentAssetLoader: Cleanup error: {ex.Message}");
            }
        }

        /// <summary>
        /// Check if bundles are loaded and models are available
        /// </summary>
        public static bool HasLoadedModels => loadedPrefabs.Count > 0;

        /// <summary>
        /// Get count of loaded prefabs
        /// </summary>
        public static int LoadedPrefabCount => loadedPrefabs.Count;

        /// <summary>
        /// Get count for a specific flora type
        /// </summary>
        public static int GetPrefabCountForType(FloraType floraType)
        {
            if (floraPrefabCache.TryGetValue(floraType, out var prefabs))
            {
                return prefabs.Count;
            }
            return 0;
        }

        /// <summary>
        /// Get all loaded mushroom prefab names
        /// </summary>
        public static List<string> GetMushroomPrefabNames()
        {
            var names = new List<string>();
            foreach (var kvp in loadedPrefabs)
            {
                if (kvp.Key.StartsWith(BUNDLE_MUSHROOMS + ":"))
                {
                    names.Add(kvp.Key.Substring(BUNDLE_MUSHROOMS.Length + 1));
                }
            }
            return names;
        }

        /// <summary>
        /// Unload all bundles
        /// </summary>
        public static void Cleanup()
        {
            foreach (var bundle in loadedBundles.Values)
            {
                try
                {
                    bundle?.Unload(false);
                }
                catch { }
            }
            loadedBundles.Clear();
            loadedPrefabs.Clear();
            floraPrefabCache.Clear();
            initialized = false;
        }
    }
}
