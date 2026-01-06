using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using EquinoxsModUtils;
using EquinoxsModUtils.Additions;
using HarmonyLib;
using UnityEngine;
using TechtonicaFramework.API;
using TechtonicaFramework.Environment;
using TechtonicaFramework.Core;

namespace HazardousWorld
{
    /// <summary>
    /// HazardousWorld - Adds environmental hazards to Techtonica
    /// Features: Toxic zones, radiation areas, unstable terrain, hostile flora
    /// </summary>
    [BepInPlugin(MyGUID, PluginName, VersionString)]
    [BepInDependency("com.equinox.EquinoxsModUtils")]
    [BepInDependency("com.equinox.EMUAdditions")]
    [BepInDependency("com.certifired.TechtonicaFramework")]
    public class HazardousWorldPlugin : BaseUnityPlugin
    {
        public const string MyGUID = "com.certifired.HazardousWorld";
        public const string PluginName = "HazardousWorld";
        public const string VersionString = "1.0.2";

        private static readonly Harmony Harmony = new Harmony(MyGUID);
        public static ManualLogSource Log;
        public static HazardousWorldPlugin Instance;

        // Configuration
        public static ConfigEntry<bool> EnableToxicZones;
        public static ConfigEntry<bool> EnableRadiationZones;
        public static ConfigEntry<bool> EnableHostileFlora;
        public static ConfigEntry<float> ToxicDamageRate;
        public static ConfigEntry<float> RadiationDamageRate;
        public static ConfigEntry<float> FloraDamageRate;
        public static ConfigEntry<bool> EnableReactorRadiation;
        public static ConfigEntry<float> ReactorRadiationRadius;
        public static ConfigEntry<bool> DebugMode;

        // Protective Equipment
        public const string HazmatSuitName = "Hazmat Suit";
        public const string RadShieldName = "Radiation Shield";
        public const string HazmatUnlock = "Hazard Protection";

        // Track active hazard zones
        private static Dictionary<string, HazardZoneData> activeZones = new Dictionary<string, HazardZoneData>();

        // Hazard zone spawning
        private bool zonesInitialized = false;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            Log.LogInfo($"{PluginName} v{VersionString} loading...");

            InitializeConfig();
            Harmony.PatchAll();

            // Register protective equipment
            RegisterProtectiveEquipment();

            // Hook events
            EMU.Events.GameLoaded += OnGameLoaded;
            // Framework events will be enabled when TechtonicaFramework is fully operational
            // FrameworkEvents.OnHazardZoneEntered += OnZoneEntered;
            // FrameworkEvents.OnHazardZoneExited += OnZoneExited;

            Log.LogInfo($"{PluginName} v{VersionString} loaded!");
        }

        private void InitializeConfig()
        {
            EnableToxicZones = Config.Bind("Toxic Zones", "Enable Toxic Zones", true,
                "Enable toxic gas zones in certain areas");

            ToxicDamageRate = Config.Bind("Toxic Zones", "Toxic Damage Per Second", 5f,
                new ConfigDescription("Damage per second in toxic zones", new AcceptableValueRange<float>(0.1f, 50f)));

            EnableRadiationZones = Config.Bind("Radiation", "Enable Radiation Zones", true,
                "Enable radiation hazard zones");

            RadiationDamageRate = Config.Bind("Radiation", "Radiation Damage Per Second", 8f,
                new ConfigDescription("Damage per second in radiation zones", new AcceptableValueRange<float>(0.1f, 50f)));

            EnableReactorRadiation = Config.Bind("Radiation", "Reactors Emit Radiation", true,
                "Power generators emit a small radiation field");

            ReactorRadiationRadius = Config.Bind("Radiation", "Reactor Radiation Radius", 5f,
                new ConfigDescription("Radius of reactor radiation zone", new AcceptableValueRange<float>(1f, 20f)));

            EnableHostileFlora = Config.Bind("Flora", "Enable Hostile Flora", true,
                "Some plants can damage the player on contact");

            FloraDamageRate = Config.Bind("Flora", "Flora Damage Per Second", 3f,
                new ConfigDescription("Damage per second from hostile flora", new AcceptableValueRange<float>(0.1f, 20f)));

            DebugMode = Config.Bind("General", "Debug Mode", false, "Enable debug logging");
        }

        private void RegisterProtectiveEquipment()
        {
            // Register unlock for protective gear
            EMUAdditions.AddNewUnlock(new NewUnlockDetails
            {
                category = Unlock.TechCategory.Logistics,
                coreTypeNeeded = ResearchCoreDefinition.CoreType.Green,
                coreCountNeeded = 100,
                description = "Research protective equipment to survive hazardous environments.",
                displayName = HazmatUnlock,
                requiredTier = TechTreeState.ResearchTier.Tier0,
                treePosition = 0
            });

            // Hazmat Suit - protects against toxic
            EMUAdditions.AddNewResource(new NewResourceDetails
            {
                name = HazmatSuitName,
                description = "Protective suit that reduces toxic damage by 80%. Essential for exploring contaminated areas.",
                craftingMethod = CraftingMethod.Assembler,
                craftTierRequired = 0,
                headerTitle = "Equipment",
                // subHeaderTitle inherited from parent
                maxStackCount = 1,
                sortPriority = 110,
                unlockName = HazmatUnlock,
                parentName = "Iron Frame" // Use as visual placeholder
            });

            EMUAdditions.AddNewRecipe(new NewRecipeDetails
            {
                GUID = MyGUID + "_hazmat",
                craftingMethod = CraftingMethod.Assembler,
                craftTierRequired = 0,
                duration = 20f,
                unlockName = HazmatUnlock,
                ingredients = new List<RecipeResourceInfo>
                {
                    new RecipeResourceInfo("Plantmatter Fiber", 20),
                    new RecipeResourceInfo("Biobrick", 10),
                    new RecipeResourceInfo("Iron Frame", 5)
                },
                outputs = new List<RecipeResourceInfo>
                {
                    new RecipeResourceInfo(HazmatSuitName, 1)
                },
                sortPriority = 110
            });

            // Radiation Shield - protects against radiation
            EMUAdditions.AddNewResource(new NewResourceDetails
            {
                name = RadShieldName,
                description = "Lead-lined shield that reduces radiation damage by 90%. Critical for working near reactors.",
                craftingMethod = CraftingMethod.Assembler,
                craftTierRequired = 0,
                headerTitle = "Equipment",
                // subHeaderTitle inherited from parent
                maxStackCount = 1,
                sortPriority = 111,
                unlockName = HazmatUnlock,
                parentName = "Steel Frame" // Use as visual placeholder
            });

            EMUAdditions.AddNewRecipe(new NewRecipeDetails
            {
                GUID = MyGUID + "_radshield",
                craftingMethod = CraftingMethod.Assembler,
                craftTierRequired = 0,
                duration = 30f,
                unlockName = HazmatUnlock,
                ingredients = new List<RecipeResourceInfo>
                {
                    new RecipeResourceInfo("Steel Frame", 10),
                    new RecipeResourceInfo("Copper Ingot", 20),
                    new RecipeResourceInfo("Iron Components", 10)
                },
                outputs = new List<RecipeResourceInfo>
                {
                    new RecipeResourceInfo(RadShieldName, 1)
                },
                sortPriority = 111
            });

            // Antidote - cures toxic status
            EMUAdditions.AddNewResource(new NewResourceDetails
            {
                name = "Antidote",
                description = "Instantly removes toxic status effects. Crafted from Shiverthorn.",
                craftingMethod = CraftingMethod.Assembler,
                craftTierRequired = 0,
                headerTitle = "Equipment",
                // subHeaderTitle inherited from parent
                maxStackCount = 20,
                sortPriority = 120,
                unlockName = HazmatUnlock,
                parentName = "Kindlevine Extract" // Use as visual placeholder
            });

            EMUAdditions.AddNewRecipe(new NewRecipeDetails
            {
                GUID = MyGUID + "_antidote",
                craftingMethod = CraftingMethod.Assembler,
                craftTierRequired = 0,
                duration = 5f,
                unlockName = HazmatUnlock,
                ingredients = new List<RecipeResourceInfo>
                {
                    new RecipeResourceInfo("Shiverthorn", 5),
                    new RecipeResourceInfo("Plantmatter", 10)
                },
                outputs = new List<RecipeResourceInfo>
                {
                    new RecipeResourceInfo("Antidote", 3)
                },
                sortPriority = 120
            });

            LogDebug("Protective equipment registered");
        }

        private void OnGameLoaded()
        {
            // NOTE: Hazard zone initialization disabled until TechtonicaFramework is fully operational
            // if (!zonesInitialized)
            // {
            //     InitializeHazardZones();
            //     zonesInitialized = true;
            // }
            Log.LogInfo("HazardousWorld: Game loaded (hazard zones disabled in stub mode)");
        }

        private void InitializeHazardZones()
        {
            LogDebug("Initializing hazard zones...");

            // Create some fixed hazard zones at interesting locations
            // These are example positions - would need to be adjusted for actual game world

            if (EnableToxicZones.Value)
            {
                // Toxic zones near certain cave areas
                CreateHazardZone("toxic_cave_1", new Vector3(100, -50, 200), 15f, HazardType.Toxic, ToxicDamageRate.Value);
                CreateHazardZone("toxic_cave_2", new Vector3(-150, -80, 300), 20f, HazardType.Toxic, ToxicDamageRate.Value);
                CreateHazardZone("toxic_swamp_1", new Vector3(250, -30, -100), 25f, HazardType.Toxic, ToxicDamageRate.Value * 0.5f);
            }

            if (EnableRadiationZones.Value)
            {
                // Radiation zones near ore deposits
                CreateHazardZone("rad_deposit_1", new Vector3(-200, -100, 150), 10f, HazardType.Radiation, RadiationDamageRate.Value);
                CreateHazardZone("rad_core_1", new Vector3(0, -150, 0), 30f, HazardType.Radiation, RadiationDamageRate.Value * 1.5f);
            }

            LogDebug($"Created {activeZones.Count} hazard zones");
        }

        private void CreateHazardZone(string id, Vector3 position, float radius, HazardType type, float dps)
        {
            FrameworkAPI.CreateHazardZone(id, position, radius, type, dps);

            activeZones[id] = new HazardZoneData
            {
                Id = id,
                Position = position,
                Radius = radius,
                Type = type,
                DamagePerSecond = dps
            };

            LogDebug($"Created hazard zone: {id} ({type}) at {position} with radius {radius}");
        }

        private void Update()
        {
            // Check for reactor radiation zones if enabled
            if (EnableReactorRadiation.Value)
            {
                UpdateReactorRadiationZones();
            }

            // Check for hostile flora near player
            if (EnableHostileFlora.Value)
            {
                CheckHostileFlora();
            }
        }

        private void UpdateReactorRadiationZones()
        {
            // This would dynamically create/update radiation zones around power generators
            // For now, this is a placeholder - would need to track all placed reactors
        }

        private void CheckHostileFlora()
        {
            var player = Player.instance;
            if (player == null) return;

            Vector3 playerPos = player.transform.position;

            // Check for nearby hostile plants (simplified - would need actual plant detection)
            // This could hook into the game's plant system
        }

        private void OnZoneEntered(string zoneId, Vector3 position)
        {
            if (!activeZones.TryGetValue(zoneId, out var zone)) return;

            Log.LogWarning($"DANGER: Entered {zone.Type} zone!");

            // Check for protective equipment
            bool hasProtection = CheckProtection(zone.Type);
            if (hasProtection)
            {
                Log.LogInfo("Protective equipment is reducing damage.");
            }
        }

        private void OnZoneExited(string zoneId, Vector3 position)
        {
            if (!activeZones.TryGetValue(zoneId, out var zone)) return;

            Log.LogInfo($"Left {zone.Type} zone - status effects will fade.");
        }

        private bool CheckProtection(HazardType type)
        {
            // Check player inventory for protective equipment
            var inventory = Player.instance?.inventory;
            if (inventory == null) return false;

            // This would check for Hazmat Suit or Radiation Shield
            // Simplified - would need to actually check inventory contents
            return false;
        }

        /// <summary>
        /// Create a temporary hazard zone (e.g., from an explosion or spill)
        /// </summary>
        public static void CreateTemporaryHazard(Vector3 position, HazardType type, float radius, float duration, float dps)
        {
            string id = $"temp_{type}_{UnityEngine.Random.Range(0, 10000)}";

            FrameworkAPI.CreateHazardZone(id, position, radius, type, dps);

            activeZones[id] = new HazardZoneData
            {
                Id = id,
                Position = position,
                Radius = radius,
                Type = type,
                DamagePerSecond = dps,
                IsTemporary = true,
                ExpirationTime = Time.time + duration
            };

            // Schedule removal
            Instance.StartCoroutine(RemoveZoneAfterDelay(id, duration));
        }

        private static System.Collections.IEnumerator RemoveZoneAfterDelay(string id, float delay)
        {
            yield return new WaitForSeconds(delay);

            FrameworkAPI.RemoveHazardZone(id);
            activeZones.Remove(id);
            LogDebug($"Temporary hazard zone {id} expired");
        }

        public static void LogDebug(string message)
        {
            if (DebugMode != null && DebugMode.Value)
            {
                Log.LogInfo($"[DEBUG] {message}");
            }
        }
    }

    /// <summary>
    /// Hazard zone data for tracking
    /// </summary>
    public class HazardZoneData
    {
        public string Id;
        public Vector3 Position;
        public float Radius;
        public HazardType Type;
        public float DamagePerSecond;
        public bool IsTemporary;
        public float ExpirationTime;
    }

    /// <summary>
    /// Visual effects for hazard zones
    /// </summary>
    [HarmonyPatch]
    public static class HazardVisualPatches
    {
        // Colors for different hazard types
        private static readonly Color ToxicColor = new Color(0.2f, 0.8f, 0.2f, 0.3f); // Green fog
        private static readonly Color RadiationColor = new Color(0.8f, 0.8f, 0.2f, 0.3f); // Yellow glow
        private static readonly Color FireColor = new Color(1f, 0.4f, 0.1f, 0.5f); // Orange
        private static readonly Color FrostColor = new Color(0.3f, 0.6f, 1f, 0.4f); // Light blue

        /// <summary>
        /// Apply post-processing effects when in hazard zones
        /// </summary>
        public static void ApplyHazardVisualEffect(HazardType type, float intensity)
        {
            // This would modify camera post-processing or add screen overlay
            // Requires integration with Unity's post-processing stack
            Color effectColor = type switch
            {
                HazardType.Toxic => ToxicColor,
                HazardType.Radiation => RadiationColor,
                HazardType.Fire => FireColor,
                HazardType.Frost => FrostColor,
                _ => Color.clear
            };

            // TODO: Apply screen tint/fog effect
        }
    }
}
