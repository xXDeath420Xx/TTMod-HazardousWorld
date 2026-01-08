using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
using TechtonicaFramework.TechTree;
using TechtonicaFramework.BuildMenu;

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
        public const string VersionString = "1.6.0";

        private static readonly Harmony Harmony = new Harmony(MyGUID);
        public static ManualLogSource Log;
        public static HazardousWorldPlugin Instance;
        public static string PluginPath;

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

        // Spore plant system
        private static List<SporePlantController> activeSporePlants = new List<SporePlantController>();
        private float sporePlantSpawnTimer = 0f;
        private const float SPORE_SPAWN_INTERVAL = 120f; // 2 minutes between new spore plants

        // Hostile flora system
        private static List<VenomousThornController> activeVenomousThorns = new List<VenomousThornController>();
        private static List<AcidSpitterController> activeAcidSpitters = new List<AcidSpitterController>();
        private static List<GraspingVineController> activeGraspingVines = new List<GraspingVineController>();
        private float hostileFloraSpawnTimer = 0f;
        private const float HOSTILE_FLORA_SPAWN_INTERVAL = 90f; // 90 seconds between spawns

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            PluginPath = Path.GetDirectoryName(Info.Location);
            Log.LogInfo($"{PluginName} v{VersionString} loading...");

            InitializeConfig();
            Harmony.PatchAll();

            // Initialize environment asset loader for 3D flora models
            EnvironmentAssetLoader.Initialize(PluginPath);

            // Load custom icons
            LoadCustomIcons();

            // Register protective equipment
            RegisterProtectiveEquipment();

            // Hook events
            EMU.Events.GameDefinesLoaded += OnGameDefinesLoaded;
            EMU.Events.GameLoaded += OnGameLoaded;
            EMU.Events.TechTreeStateLoaded += OnTechTreeStateLoaded;
            // Framework events will be enabled when TechtonicaFramework is fully operational
            // FrameworkEvents.OnHazardZoneEntered += OnZoneEntered;
            // FrameworkEvents.OnHazardZoneExited += OnZoneExited;

            Log.LogInfo($"{PluginName} v{VersionString} loaded!");
        }

        private void OnTechTreeStateLoaded()
        {
            // PT level tier mapping (from game):
            // - LIMA: Tier1-Tier4
            // - VICTOR: Tier5-Tier11
            // - XRAY: Tier12-Tier16
            // - SIERRA: Tier17-Tier24

            // Hazard Protection: VICTOR (Tier6), position 70 (mid game survival)
            ConfigureUnlock(HazmatUnlock, "Iron Frame", TechTreeState.ResearchTier.Tier6, 70);
            Log.LogInfo("Configured HazardousWorld unlock tiers in VICTOR");
        }

        private void ConfigureUnlock(string unlockName, string spriteSourceName, TechTreeState.ResearchTier tier, int position)
        {
            try
            {
                Unlock unlock = EMU.Unlocks.GetUnlockByName(unlockName);
                if (unlock == null)
                {
                    LogDebug($"Unlock '{unlockName}' not found");
                    return;
                }

                // Set the correct tier explicitly
                unlock.requiredTier = tier;

                // Set explicit position to avoid collisions
                unlock.treePosition = position;

                // Copy sprite from source for proper tech tree icon
                if (unlock.sprite == null)
                {
                    ResourceInfo sourceRes = EMU.Resources.GetResourceInfoByName(spriteSourceName);
                    if (sourceRes != null && sourceRes.sprite != null)
                    {
                        unlock.sprite = sourceRes.sprite;
                    }
                    else
                    {
                        // Try unlock
                        Unlock sourceUnlock = EMU.Unlocks.GetUnlockByName(spriteSourceName);
                        if (sourceUnlock != null && sourceUnlock.sprite != null)
                        {
                            unlock.sprite = sourceUnlock.sprite;
                        }
                    }
                }

                LogDebug($"Configured unlock '{unlockName}': tier={tier}, position={position}");
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Failed to configure unlock {unlockName}: {ex.Message}");
            }
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

            EnableSporePlants = Config.Bind("Spore Plants", "Enable Spore Plants", true,
                "Spawn hostile spore plants that create toxic clouds");

            SporePlantDamage = Config.Bind("Spore Plants", "Spore Damage Per Second", 8f,
                new ConfigDescription("Damage per second from spore cloud", new AcceptableValueRange<float>(1f, 30f)));

            SporePlantRadius = Config.Bind("Spore Plants", "Spore Cloud Radius", 6f,
                new ConfigDescription("Radius of spore cloud damage zone", new AcceptableValueRange<float>(2f, 15f)));

            MaxSporePlants = Config.Bind("Spore Plants", "Max Spore Plants", 10,
                new ConfigDescription("Maximum number of spore plants in world", new AcceptableValueRange<int>(1, 50)));

            SporeSpawnKey = Config.Bind("Debug", "Spawn Spore Plant Key", KeyCode.F10,
                "Press to spawn a spore plant near player (debug)");

            // Venomous Thorns - contact damage
            EnableVenomousThorns = Config.Bind("Venomous Thorns", "Enable Venomous Thorns", true,
                "Spawn dangerous thorn bushes that damage on contact");
            ThornDamage = Config.Bind("Venomous Thorns", "Thorn Damage Per Hit", 15f,
                new ConfigDescription("Damage dealt when touching thorns", new AcceptableValueRange<float>(1f, 50f)));
            MaxThorns = Config.Bind("Venomous Thorns", "Max Thorn Bushes", 15,
                new ConfigDescription("Maximum thorn bushes in world", new AcceptableValueRange<int>(1, 50)));

            // Acid Spitters - ranged attackers
            EnableAcidSpitters = Config.Bind("Acid Spitters", "Enable Acid Spitters", true,
                "Spawn plants that spit acid projectiles at nearby players");
            AcidDamage = Config.Bind("Acid Spitters", "Acid Damage Per Hit", 12f,
                new ConfigDescription("Damage from acid projectiles", new AcceptableValueRange<float>(1f, 40f)));
            AcidRange = Config.Bind("Acid Spitters", "Acid Spit Range", 12f,
                new ConfigDescription("Range at which acid spitters attack", new AcceptableValueRange<float>(5f, 25f)));
            MaxSpitters = Config.Bind("Acid Spitters", "Max Acid Spitters", 8,
                new ConfigDescription("Maximum acid spitters in world", new AcceptableValueRange<int>(1, 30)));

            // Grasping Vines - slow/trap
            EnableGraspingVines = Config.Bind("Grasping Vines", "Enable Grasping Vines", true,
                "Spawn vine plants that slow and trap players");
            VineSlowAmount = Config.Bind("Grasping Vines", "Vine Slow Percentage", 0.5f,
                new ConfigDescription("Movement speed reduction (0.5 = 50% slower)", new AcceptableValueRange<float>(0.1f, 0.9f)));
            VineGrabDuration = Config.Bind("Grasping Vines", "Grab Duration", 2f,
                new ConfigDescription("How long vines hold the player", new AcceptableValueRange<float>(0.5f, 5f)));
            MaxVines = Config.Bind("Grasping Vines", "Max Vine Patches", 12,
                new ConfigDescription("Maximum vine patches in world", new AcceptableValueRange<int>(1, 40)));

            // Global hostile flora settings
            MaxTotalHostileFlora = Config.Bind("Hostile Flora", "Max Total Flora", 50,
                new ConfigDescription("Maximum total hostile flora entities", new AcceptableValueRange<int>(10, 200)));
            FloraSpawnKey = Config.Bind("Debug", "Spawn Random Flora Key", KeyCode.F11,
                "Press to spawn random hostile flora near player (debug)");

            DebugMode = Config.Bind("General", "Debug Mode", false, "Enable debug logging");
        }

        // Additional config entries for spore plants
        public static ConfigEntry<bool> EnableSporePlants;
        public static ConfigEntry<float> SporePlantDamage;
        public static ConfigEntry<float> SporePlantRadius;
        public static ConfigEntry<int> MaxSporePlants;
        public static ConfigEntry<KeyCode> SporeSpawnKey;

        // Config entries for additional hostile flora
        public static ConfigEntry<bool> EnableVenomousThorns;
        public static ConfigEntry<float> ThornDamage;
        public static ConfigEntry<int> MaxThorns;
        public static ConfigEntry<bool> EnableAcidSpitters;
        public static ConfigEntry<float> AcidDamage;
        public static ConfigEntry<float> AcidRange;
        public static ConfigEntry<int> MaxSpitters;
        public static ConfigEntry<bool> EnableGraspingVines;
        public static ConfigEntry<float> VineSlowAmount;
        public static ConfigEntry<float> VineGrabDuration;
        public static ConfigEntry<int> MaxVines;
        public static ConfigEntry<int> MaxTotalHostileFlora;
        public static ConfigEntry<KeyCode> FloraSpawnKey;

        private void RegisterProtectiveEquipment()
        {
            // Register unlock for protective gear - Modded category (VICTOR zone, Tier6)
            EMUAdditions.AddNewUnlock(new NewUnlockDetails
            {
                category = ModdedTabModule.ModdedCategory,
                coreTypeNeeded = ResearchCoreDefinition.CoreType.Green,
                coreCountNeeded = 100,
                description = "Research protective equipment to survive hazardous environments.",
                displayName = HazmatUnlock,
                requiredTier = TechTreeState.ResearchTier.Tier6, // VICTOR zone
                treePosition = 70
            });

            // Hazmat Suit - protects against toxic
            EMUAdditions.AddNewResource(new NewResourceDetails
            {
                name = HazmatSuitName,
                description = "Protective suit that reduces toxic damage by 80%. Essential for exploring contaminated areas.",
                craftingMethod = CraftingMethod.Assembler,
                craftTierRequired = 0,
                headerTitle = "Modded",
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
                headerTitle = "Modded",
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
                headerTitle = "Modded",
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

        private void OnGameDefinesLoaded()
        {
            // Link unlocks to resources - CRITICAL for crafting to work
            LinkUnlockToResource(HazmatSuitName, HazmatUnlock);
            LinkUnlockToResource(RadShieldName, HazmatUnlock);
            LinkUnlockToResource("Antidote", HazmatUnlock);

            // Apply custom sprites to resources
            ApplyCustomSprites();

            Log.LogInfo("Linked HazardousWorld unlocks to resources");
        }

        private void LinkUnlockToResource(string resourceName, string unlockName)
        {
            try
            {
                ResourceInfo info = EMU.Resources.GetResourceInfoByName(resourceName);
                if (info != null)
                {
                    info.unlock = EMU.Unlocks.GetUnlockByName(unlockName);
                    LogDebug($"Linked {resourceName} to unlock {unlockName}");
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Failed to link {resourceName} to {unlockName}: {ex.Message}");
            }
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
            // Debug spawn keys
            if (Input.GetKeyDown(SporeSpawnKey.Value))
            {
                SpawnSporePlantNearPlayer();
            }
            if (Input.GetKeyDown(FloraSpawnKey.Value))
            {
                SpawnRandomHostileFloraTypeNearPlayer();
            }

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

            // Spore plant spawning system
            if (EnableSporePlants.Value)
            {
                UpdateSporePlantSystem();
            }

            // Update all hostile flora spawning
            UpdateHostileFloraSpawning();

            // Clean up destroyed entities
            activeSporePlants.RemoveAll(sp => sp == null);
            activeVenomousThorns.RemoveAll(t => t == null);
            activeAcidSpitters.RemoveAll(s => s == null);
            activeGraspingVines.RemoveAll(v => v == null);
        }

        private void UpdateHostileFloraSpawning()
        {
            hostileFloraSpawnTimer += Time.deltaTime;

            if (hostileFloraSpawnTimer < HOSTILE_FLORA_SPAWN_INTERVAL) return;
            hostileFloraSpawnTimer = 0f;

            int totalFlora = activeSporePlants.Count + activeVenomousThorns.Count +
                           activeAcidSpitters.Count + activeGraspingVines.Count;

            if (totalFlora >= MaxTotalHostileFlora.Value) return;

            // Random chance to spawn each type
            var player = Player.instance;
            if (player == null) return;

            float roll = UnityEngine.Random.value;

            if (EnableVenomousThorns.Value && activeVenomousThorns.Count < MaxThorns.Value && roll < 0.4f)
            {
                TrySpawnFloraAtRandomLocation<VenomousThornController>(SpawnVenomousThorn);
            }
            else if (EnableAcidSpitters.Value && activeAcidSpitters.Count < MaxSpitters.Value && roll < 0.6f)
            {
                TrySpawnFloraAtRandomLocation<AcidSpitterController>(SpawnAcidSpitter);
            }
            else if (EnableGraspingVines.Value && activeGraspingVines.Count < MaxVines.Value && roll < 0.8f)
            {
                TrySpawnFloraAtRandomLocation<GraspingVineController>(SpawnGraspingVine);
            }
        }

        private void TrySpawnFloraAtRandomLocation<T>(Func<Vector3, GameObject> spawnFunc) where T : Component
        {
            var player = Player.instance;
            if (player == null) return;

            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float distance = UnityEngine.Random.Range(25f, 70f);
            Vector3 offset = new Vector3(Mathf.Cos(angle) * distance, 0, Mathf.Sin(angle) * distance);
            Vector3 spawnPos = player.transform.position + offset;

            if (Physics.Raycast(spawnPos + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 100f))
            {
                spawnFunc(hit.point);
            }
        }

        private void SpawnRandomHostileFloraTypeNearPlayer()
        {
            var player = Player.instance;
            if (player == null) return;

            Vector3 spawnPos = player.transform.position + player.transform.forward * 8f;

            int type = UnityEngine.Random.Range(0, 3);
            switch (type)
            {
                case 0:
                    SpawnVenomousThorn(spawnPos);
                    Log.LogInfo($"DEBUG: Spawned Venomous Thorn at {spawnPos}");
                    break;
                case 1:
                    SpawnAcidSpitter(spawnPos);
                    Log.LogInfo($"DEBUG: Spawned Acid Spitter at {spawnPos}");
                    break;
                case 2:
                    SpawnGraspingVine(spawnPos);
                    Log.LogInfo($"DEBUG: Spawned Grasping Vine at {spawnPos}");
                    break;
            }
        }

        private void UpdateSporePlantSystem()
        {
            sporePlantSpawnTimer += Time.deltaTime;

            // Periodically spawn new spore plants if under max
            if (sporePlantSpawnTimer >= SPORE_SPAWN_INTERVAL && activeSporePlants.Count < MaxSporePlants.Value)
            {
                sporePlantSpawnTimer = 0f;
                TrySpawnRandomSporePlant();
            }
        }

        private void TrySpawnRandomSporePlant()
        {
            var player = Player.instance;
            if (player == null) return;

            // Spawn in a random direction from player, at medium distance
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float distance = UnityEngine.Random.Range(30f, 80f);
            Vector3 offset = new Vector3(Mathf.Cos(angle) * distance, 0, Mathf.Sin(angle) * distance);
            Vector3 spawnPos = player.transform.position + offset;

            // Raycast down to find ground
            if (Physics.Raycast(spawnPos + Vector3.up * 50f, Vector3.down, out RaycastHit hit, 100f))
            {
                SpawnSporePlant(hit.point);
            }
        }

        private void SpawnSporePlantNearPlayer()
        {
            var player = Player.instance;
            if (player == null) return;

            Vector3 spawnPos = player.transform.position + player.transform.forward * 8f;
            SpawnSporePlant(spawnPos);
            Log.LogInfo($"DEBUG: Spawned spore plant at {spawnPos}");
        }

        public static GameObject SpawnSporePlant(Vector3 position)
        {
            GameObject plant;

            // Try to use real 3D model from asset bundle
            var modelInstance = EnvironmentAssetLoader.InstantiateFlora(
                EnvironmentAssetLoader.FloraType.SporePlant,
                position,
                Quaternion.identity);

            if (modelInstance != null)
            {
                plant = modelInstance;
                plant.name = "SporePlant";
                LogDebug($"Spawned spore plant using 3D model at {position}");
            }
            else
            {
                // Fallback to procedural generation
                plant = new GameObject("SporePlant");
                plant.transform.position = position;

                // Create base stalk
                GameObject stalk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                stalk.transform.SetParent(plant.transform);
                stalk.transform.localPosition = Vector3.up * 0.5f;
                stalk.transform.localScale = new Vector3(0.3f, 1f, 0.3f);
                var stalkRenderer = stalk.GetComponent<Renderer>();
                stalkRenderer.material.color = new Color(0.3f, 0.5f, 0.2f);

                // Create bulbous top
                GameObject bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bulb.transform.SetParent(plant.transform);
                bulb.transform.localPosition = Vector3.up * 1.5f;
                bulb.transform.localScale = new Vector3(1.2f, 0.8f, 1.2f);
                var bulbRenderer = bulb.GetComponent<Renderer>();
                bulbRenderer.material.color = new Color(0.6f, 0.2f, 0.4f);

                // Create spore puff visual (child spheres)
                for (int i = 0; i < 6; i++)
                {
                    GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    puff.transform.SetParent(bulb.transform);
                    float puffAngle = (i / 6f) * Mathf.PI * 2f;
                    puff.transform.localPosition = new Vector3(Mathf.Cos(puffAngle) * 0.4f, 0.2f, Mathf.Sin(puffAngle) * 0.4f);
                    puff.transform.localScale = Vector3.one * 0.3f;
                    var puffRenderer = puff.GetComponent<Renderer>();
                    puffRenderer.material.color = new Color(0.8f, 0.6f, 0.2f, 0.7f);
                    UnityEngine.Object.Destroy(puff.GetComponent<Collider>());
                }
            }

            // Add controller
            var controller = plant.AddComponent<SporePlantController>();
            controller.sporeRadius = SporePlantRadius.Value;
            controller.sporeDamagePerSecond = SporePlantDamage.Value;

            activeSporePlants.Add(controller);
            LogDebug($"Spawned spore plant at {position}, total: {activeSporePlants.Count}");

            return plant;
        }

        /// <summary>
        /// Spawn a Venomous Thorn bush at the given position
        /// </summary>
        public static GameObject SpawnVenomousThorn(Vector3 position)
        {
            GameObject thorn;

            // Try to use real 3D model from asset bundle
            var modelInstance = EnvironmentAssetLoader.InstantiateFlora(
                EnvironmentAssetLoader.FloraType.VenomousThorn,
                position,
                Quaternion.identity);

            if (modelInstance != null)
            {
                thorn = modelInstance;
                thorn.name = "VenomousThorn";
                LogDebug($"Spawned venomous thorn using 3D model at {position}");
            }
            else
            {
                // Fallback to procedural generation
                thorn = new GameObject("VenomousThorn");
                thorn.transform.position = position;

                // Create sharp thorn-like branches
                Color thornColor = new Color(0.3f, 0.15f, 0.1f); // Dark brown
                Color tipColor = new Color(0.6f, 0.1f, 0.2f); // Dark red tips

                // Base
                GameObject baseObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                baseObj.transform.SetParent(thorn.transform);
                baseObj.transform.localPosition = Vector3.up * 0.2f;
                baseObj.transform.localScale = new Vector3(0.6f, 0.4f, 0.6f);
                baseObj.GetComponent<Renderer>().material.color = thornColor;
                UnityEngine.Object.Destroy(baseObj.GetComponent<Collider>());

                // Create thorny branches pointing outward
                for (int i = 0; i < 8; i++)
                {
                    float angle = (i / 8f) * Mathf.PI * 2f;
                    float heightOffset = UnityEngine.Random.Range(0.3f, 0.8f);

                    GameObject branch = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    branch.transform.SetParent(thorn.transform);
                    branch.transform.localPosition = new Vector3(
                        Mathf.Cos(angle) * 0.3f,
                        heightOffset,
                        Mathf.Sin(angle) * 0.3f
                    );
                    branch.transform.localRotation = Quaternion.Euler(
                        UnityEngine.Random.Range(30f, 60f),
                        angle * Mathf.Rad2Deg,
                        0
                    );
                    branch.transform.localScale = new Vector3(0.08f, 0.4f, 0.08f);
                    branch.GetComponent<Renderer>().material.color = Color.Lerp(thornColor, tipColor, 0.5f);
                    UnityEngine.Object.Destroy(branch.GetComponent<Collider>());
                }
            }

            var controller = thorn.AddComponent<VenomousThornController>();
            controller.damageOnContact = ThornDamage.Value;

            activeVenomousThorns.Add(controller);
            LogDebug($"Spawned venomous thorn at {position}, total: {activeVenomousThorns.Count}");

            return thorn;
        }

        /// <summary>
        /// Spawn an Acid Spitter plant at the given position
        /// </summary>
        public static GameObject SpawnAcidSpitter(Vector3 position)
        {
            GameObject spitter;

            // Try to use real 3D model from asset bundle
            var modelInstance = EnvironmentAssetLoader.InstantiateFlora(
                EnvironmentAssetLoader.FloraType.AcidSpitter,
                position,
                Quaternion.identity);

            if (modelInstance != null)
            {
                spitter = modelInstance;
                spitter.name = "AcidSpitter";
                LogDebug($"Spawned acid spitter using 3D model at {position}");
            }
            else
            {
                // Fallback to procedural generation
                spitter = new GameObject("AcidSpitter");
                spitter.transform.position = position;

                Color stemColor = new Color(0.2f, 0.4f, 0.1f); // Dark green
                Color headColor = new Color(0.5f, 0.8f, 0.2f); // Bright green
                Color sackColor = new Color(0.7f, 0.9f, 0.1f); // Yellow-green acid

                // Stem
                GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                stem.transform.SetParent(spitter.transform);
                stem.transform.localPosition = Vector3.up * 0.5f;
                stem.transform.localScale = new Vector3(0.25f, 1f, 0.25f);
                stem.GetComponent<Renderer>().material.color = stemColor;

                // Head (pitcher-like)
                GameObject head = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                head.transform.SetParent(spitter.transform);
                head.transform.localPosition = Vector3.up * 1.5f;
                head.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);
                head.transform.localRotation = Quaternion.Euler(15f, 0, 0); // Tilted forward
                head.GetComponent<Renderer>().material.color = headColor;
                UnityEngine.Object.Destroy(head.GetComponent<Collider>());

                // Acid sack bulge
                GameObject sack = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sack.transform.SetParent(head.transform);
                sack.transform.localPosition = Vector3.forward * 0.2f;
                sack.transform.localScale = new Vector3(0.6f, 0.5f, 0.4f);
                sack.GetComponent<Renderer>().material.color = sackColor;
                UnityEngine.Object.Destroy(sack.GetComponent<Collider>());
            }

            var controller = spitter.AddComponent<AcidSpitterController>();
            controller.damage = AcidDamage.Value;
            controller.attackRange = AcidRange.Value;

            activeAcidSpitters.Add(controller);
            LogDebug($"Spawned acid spitter at {position}, total: {activeAcidSpitters.Count}");

            return spitter;
        }

        /// <summary>
        /// Spawn a Grasping Vine patch at the given position
        /// </summary>
        public static GameObject SpawnGraspingVine(Vector3 position)
        {
            GameObject vine;

            // Try to use real 3D model from asset bundle
            var modelInstance = EnvironmentAssetLoader.InstantiateFlora(
                EnvironmentAssetLoader.FloraType.GraspingVine,
                position,
                Quaternion.identity);

            if (modelInstance != null)
            {
                vine = modelInstance;
                vine.name = "GraspingVine";
                LogDebug($"Spawned grasping vine using 3D model at {position}");
            }
            else
            {
                // Fallback to procedural generation
                vine = new GameObject("GraspingVine");
                vine.transform.position = position;

                Color vineColor = new Color(0.15f, 0.3f, 0.1f); // Dark green
                Color rootColor = new Color(0.3f, 0.2f, 0.1f); // Brown

                // Create a cluster of vines
                for (int i = 0; i < 5; i++)
                {
                    float angle = (i / 5f) * Mathf.PI * 2f;
                    float dist = UnityEngine.Random.Range(0.3f, 0.8f);

                    GameObject tendril = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    tendril.transform.SetParent(vine.transform);
                    tendril.transform.localPosition = new Vector3(
                        Mathf.Cos(angle) * dist,
                        0.5f,
                        Mathf.Sin(angle) * dist
                    );
                    tendril.transform.localScale = new Vector3(0.15f, 1f, 0.15f);
                    tendril.transform.localRotation = Quaternion.Euler(
                        UnityEngine.Random.Range(-20f, 20f),
                        UnityEngine.Random.Range(0f, 360f),
                        UnityEngine.Random.Range(-20f, 20f)
                    );
                    tendril.GetComponent<Renderer>().material.color = vineColor;
                    UnityEngine.Object.Destroy(tendril.GetComponent<Collider>());
                }

                // Root mass at base
                GameObject roots = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                roots.transform.SetParent(vine.transform);
                roots.transform.localPosition = Vector3.up * 0.15f;
                roots.transform.localScale = new Vector3(1.5f, 0.4f, 1.5f);
                roots.GetComponent<Renderer>().material.color = rootColor;
                UnityEngine.Object.Destroy(roots.GetComponent<Collider>());
            }

            var controller = vine.AddComponent<GraspingVineController>();
            controller.slowAmount = VineSlowAmount.Value;
            controller.grabDuration = VineGrabDuration.Value;

            activeGraspingVines.Add(controller);
            LogDebug($"Spawned grasping vine at {position}, total: {activeGraspingVines.Count}");

            return vine;
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

        // Cached sprites cloned from game assets
        private static Dictionary<string, Sprite> customSprites = new Dictionary<string, Sprite>();

        /// <summary>
        /// Clone a sprite from an existing game resource to match Techtonica's icon style
        /// </summary>
        private static Sprite CloneGameSprite(string sourceResourceName)
        {
            try
            {
                ResourceInfo sourceResource = EMU.Resources.GetResourceInfoByName(sourceResourceName);
                if (sourceResource != null && sourceResource.sprite != null)
                {
                    Log.LogInfo($"Cloned sprite from {sourceResourceName}");
                    return sourceResource.sprite;
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Failed to clone sprite from {sourceResourceName}: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Load all custom icons by cloning from existing game resources
        /// This ensures icons match Techtonica's visual style
        /// </summary>
        private void LoadCustomIcons()
        {
            // Clone sprites from existing similar items to match game aesthetic
            // Hazmat Suit - use M.O.L.E. suit icon (protective gear)
            customSprites["hazmat_suit"] = CloneGameSprite("M.O.L.E.");
            // Radiation Shield - use Exosuit icon (protective equipment)
            customSprites["radiation_shield"] = CloneGameSprite("Exosuit");
            // Antidote - use Plantmatter Fibre icon (organic consumable)
            customSprites["antidote"] = CloneGameSprite("Plantmatter Fibre");

            int loaded = customSprites.Values.Count(s => s != null);
            Log.LogInfo($"Cloned {loaded}/{customSprites.Count} sprites from game assets");
        }

        /// <summary>
        /// Set sprite on ResourceInfo using reflection (sprite property is read-only)
        /// </summary>
        private static void SetResourceSprite(ResourceInfo resource, Sprite sprite)
        {
            if (resource == null || sprite == null) return;
            try
            {
                var spriteField = typeof(ResourceInfo).GetField("_sprite", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (spriteField != null)
                {
                    spriteField.SetValue(resource, sprite);
                }
                else
                {
                    // Try property backing field
                    var backingField = typeof(ResourceInfo).GetField("<sprite>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (backingField != null)
                    {
                        backingField.SetValue(resource, sprite);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Failed to set sprite via reflection: {ex.Message}");
            }
        }

        /// <summary>
        /// Apply custom sprites to resources after they're registered
        /// </summary>
        private void ApplyCustomSprites()
        {
            try
            {
                // Apply to Hazmat Suit
                var hazmat = EMU.Resources.GetResourceInfoByName(HazmatSuitName);
                if (hazmat != null && customSprites.TryGetValue("hazmat_suit", out Sprite hazmatSprite) && hazmatSprite != null)
                {
                    SetResourceSprite(hazmat, hazmatSprite);
                    LogDebug($"Applied custom sprite to {HazmatSuitName}");
                }

                // Apply to Radiation Shield
                var radShield = EMU.Resources.GetResourceInfoByName(RadShieldName);
                if (radShield != null && customSprites.TryGetValue("radiation_shield", out Sprite radSprite) && radSprite != null)
                {
                    SetResourceSprite(radShield, radSprite);
                    LogDebug($"Applied custom sprite to {RadShieldName}");
                }

                // Apply to Antidote
                var antidote = EMU.Resources.GetResourceInfoByName("Antidote");
                if (antidote != null && customSprites.TryGetValue("antidote", out Sprite antidoteSprite) && antidoteSprite != null)
                {
                    SetResourceSprite(antidote, antidoteSprite);
                    LogDebug($"Applied custom sprite to Antidote");
                }

                // Apply to unlock
                var unlock = EMU.Unlocks.GetUnlockByName(HazmatUnlock);
                if (unlock != null && customSprites.TryGetValue("hazmat_suit", out Sprite unlockSprite) && unlockSprite != null)
                {
                    unlock.sprite = unlockSprite;
                    LogDebug($"Applied custom sprite to unlock {HazmatUnlock}");
                }
            }
            catch (Exception ex)
            {
                Log.LogWarning($"Failed to apply custom sprites: {ex.Message}");
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

    /// <summary>
    /// Controller for hostile spore plants that create damage zones
    /// </summary>
    public class SporePlantController : MonoBehaviour
    {
        public float sporeRadius = 6f;
        public float sporeDamagePerSecond = 8f;
        public float sporePulseInterval = 3f;
        public float health = 50f;
        public float maxHealth = 50f;

        private float lastPulseTime;
        private float lastDamageTime;
        private Transform sporeCloud;
        private bool isPlayerInRange = false;
        private ParticleSystem sporeParticles;

        void Start()
        {
            // Create spore cloud visual
            sporeCloud = new GameObject("SporeCloud").transform;
            sporeCloud.SetParent(transform);
            sporeCloud.localPosition = Vector3.up * 1.5f;

            // Create a particle system for spore effect
            CreateSporeParticles();

            // Add trigger collider for damage zone
            var trigger = gameObject.AddComponent<SphereCollider>();
            trigger.radius = sporeRadius;
            trigger.isTrigger = true;

            // Add a Rigidbody so OnTriggerStay works
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        void CreateSporeParticles()
        {
            GameObject particleObj = new GameObject("SporeParticles");
            particleObj.transform.SetParent(sporeCloud);
            particleObj.transform.localPosition = Vector3.zero;

            sporeParticles = particleObj.AddComponent<ParticleSystem>();

            var main = sporeParticles.main;
            main.startLifetime = 4f;
            main.startSpeed = 0.5f;
            main.startSize = 0.5f;
            main.startColor = new Color(0.6f, 0.8f, 0.2f, 0.4f);
            main.maxParticles = 100;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = sporeParticles.emission;
            emission.rateOverTime = 15f;

            var shape = sporeParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = sporeRadius * 0.8f;

            var colorOverLifetime = sporeParticles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(0.6f, 0.8f, 0.2f), 0f), new GradientColorKey(new Color(0.4f, 0.6f, 0.1f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.5f, 0.3f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

            var renderer = particleObj.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
        }

        void Update()
        {
            // Periodic spore pulse effect
            if (Time.time - lastPulseTime >= sporePulseInterval)
            {
                lastPulseTime = Time.time;
                PulseSpores();
            }

            // Check for player in range and apply damage
            var player = Player.instance;
            if (player != null)
            {
                float distance = Vector3.Distance(transform.position, player.transform.position);
                if (distance <= sporeRadius)
                {
                    if (Time.time - lastDamageTime >= 1f)
                    {
                        lastDamageTime = Time.time;
                        DamagePlayer();
                    }
                    isPlayerInRange = true;
                }
                else
                {
                    isPlayerInRange = false;
                }
            }
        }

        void PulseSpores()
        {
            // Emit a burst of spores
            if (sporeParticles != null)
            {
                sporeParticles.Emit(20);
            }

            // Animate the bulb slightly
            StartCoroutine(BulbPulseAnimation());
        }

        IEnumerator BulbPulseAnimation()
        {
            Transform bulb = transform.Find("Sphere"); // The bulb
            if (bulb == null) yield break;

            Vector3 originalScale = bulb.localScale;
            Vector3 pulsedScale = originalScale * 1.2f;

            float duration = 0.3f;
            float elapsed = 0f;

            // Expand
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                bulb.localScale = Vector3.Lerp(originalScale, pulsedScale, elapsed / duration);
                yield return null;
            }

            // Contract
            elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                bulb.localScale = Vector3.Lerp(pulsedScale, originalScale, elapsed / duration);
                yield return null;
            }

            bulb.localScale = originalScale;
        }

        void DamagePlayer()
        {
            // Check if player has hazmat suit
            bool hasProtection = false; // TODO: Check inventory for HazmatSuit

            float damage = sporeDamagePerSecond;
            if (hasProtection)
            {
                damage *= 0.2f; // 80% reduction with hazmat
            }

            HazardousWorldPlugin.Log.LogWarning($"Spore plant damages player for {damage:F1} (protection: {hasProtection})");

            // TODO: Apply actual damage when player health system is active
            // FrameworkAPI.DamagePlayer(damage, DamageType.Toxic);
        }

        public void TakeDamage(float damage)
        {
            health -= damage;
            HazardousWorldPlugin.LogDebug($"Spore plant took {damage} damage, health: {health}/{maxHealth}");

            // Flash red
            StartCoroutine(DamageFlash());

            if (health <= 0)
            {
                Die();
            }
        }

        IEnumerator DamageFlash()
        {
            var renderers = GetComponentsInChildren<Renderer>();
            var originalColors = new List<Color>();

            foreach (var r in renderers)
            {
                if (r.material != null)
                {
                    originalColors.Add(r.material.color);
                    r.material.color = Color.red;
                }
            }

            yield return new WaitForSeconds(0.15f);

            int i = 0;
            foreach (var r in renderers)
            {
                if (r.material != null && i < originalColors.Count)
                {
                    r.material.color = originalColors[i];
                    i++;
                }
            }
        }

        void Die()
        {
            HazardousWorldPlugin.Log.LogInfo("Spore plant destroyed!");

            // Death effect - burst of spores
            if (sporeParticles != null)
            {
                sporeParticles.transform.SetParent(null);
                sporeParticles.Emit(50);
                Destroy(sporeParticles.gameObject, 5f);
            }

            // TODO: Drop loot (Plantmatter, seeds, etc.)

            Destroy(gameObject);
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                HazardousWorldPlugin.Log.LogWarning("Entering spore cloud zone!");
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                HazardousWorldPlugin.Log.LogInfo("Leaving spore cloud zone");
            }
        }
    }

    /// <summary>
    /// Controller for Venomous Thorn bushes - deals damage on contact
    /// </summary>
    public class VenomousThornController : MonoBehaviour
    {
        public float damageOnContact = 15f;
        public float damageCooldown = 0.5f;
        public float health = 30f;
        public float maxHealth = 30f;
        public float damageRadius = 1.5f;

        private float lastDamageTime;

        void Start()
        {
            // Add trigger collider
            var trigger = gameObject.AddComponent<SphereCollider>();
            trigger.radius = damageRadius;
            trigger.isTrigger = true;

            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        void Update()
        {
            var player = Player.instance;
            if (player == null) return;

            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (distance <= damageRadius && Time.time - lastDamageTime >= damageCooldown)
            {
                lastDamageTime = Time.time;
                DamagePlayer();
            }
        }

        void DamagePlayer()
        {
            HazardousWorldPlugin.Log.LogWarning($"Venomous thorn damages player for {damageOnContact}!");
            // TODO: Apply actual damage via Framework
            // FrameworkAPI.DamagePlayer(damageOnContact, DamageType.Poison);

            // Visual feedback - shake the plant
            StartCoroutine(ShakeAnimation());
        }

        IEnumerator ShakeAnimation()
        {
            Vector3 originalPos = transform.position;
            float duration = 0.2f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float shake = Mathf.Sin(elapsed * 50f) * 0.1f * (1f - elapsed / duration);
                transform.position = originalPos + new Vector3(shake, 0, shake);
                yield return null;
            }
            transform.position = originalPos;
        }

        public void TakeDamage(float damage)
        {
            health -= damage;
            HazardousWorldPlugin.LogDebug($"Venomous thorn took {damage} damage, health: {health}/{maxHealth}");

            if (health <= 0)
            {
                Die();
            }
        }

        void Die()
        {
            HazardousWorldPlugin.Log.LogInfo("Venomous thorn destroyed!");
            // TODO: Drop plantmatter
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Controller for Acid Spitter plants - ranged attacks
    /// </summary>
    public class AcidSpitterController : MonoBehaviour
    {
        public float damage = 12f;
        public float attackRange = 12f;
        public float attackCooldown = 3f;
        public float projectileSpeed = 15f;
        public float health = 40f;
        public float maxHealth = 40f;

        private float lastAttackTime;
        private Transform head;

        void Start()
        {
            head = transform.Find("Capsule"); // The head piece
            lastAttackTime = Time.time - attackCooldown; // Can attack immediately
        }

        void Update()
        {
            var player = Player.instance;
            if (player == null) return;

            float distance = Vector3.Distance(transform.position, player.transform.position);

            // Rotate head to face player when in range
            if (distance <= attackRange * 1.5f && head != null)
            {
                Vector3 dirToPlayer = (player.transform.position - head.position).normalized;
                Quaternion targetRot = Quaternion.LookRotation(dirToPlayer) * Quaternion.Euler(90f, 0, 0);
                head.rotation = Quaternion.Slerp(head.rotation, targetRot, Time.deltaTime * 3f);
            }

            // Attack when in range
            if (distance <= attackRange && Time.time - lastAttackTime >= attackCooldown)
            {
                lastAttackTime = Time.time;
                StartCoroutine(SpitAcid(player.transform));
            }
        }

        IEnumerator SpitAcid(Transform target)
        {
            // Wind-up animation
            Vector3 originalScale = head != null ? head.localScale : Vector3.one;
            if (head != null)
            {
                head.localScale = originalScale * 1.3f;
                yield return new WaitForSeconds(0.3f);
                head.localScale = originalScale;
            }

            // Create projectile
            GameObject acidBlob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            acidBlob.name = "AcidProjectile";
            acidBlob.transform.position = transform.position + Vector3.up * 1.5f;
            acidBlob.transform.localScale = Vector3.one * 0.3f;
            acidBlob.GetComponent<Renderer>().material.color = new Color(0.7f, 0.9f, 0.1f);
            UnityEngine.Object.Destroy(acidBlob.GetComponent<Collider>());

            // Move towards target
            Vector3 startPos = acidBlob.transform.position;
            Vector3 targetPos = target.position + Vector3.up; // Aim at player center
            float flightTime = Vector3.Distance(startPos, targetPos) / projectileSpeed;
            float elapsed = 0f;

            while (elapsed < flightTime && acidBlob != null)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / flightTime;

                // Arc trajectory
                Vector3 currentPos = Vector3.Lerp(startPos, targetPos, t);
                currentPos.y += Mathf.Sin(t * Mathf.PI) * 2f; // Arc height
                acidBlob.transform.position = currentPos;

                yield return null;
            }

            // Impact
            if (acidBlob != null)
            {
                // Check if hit player
                float hitDistance = Vector3.Distance(acidBlob.transform.position, target.position);
                if (hitDistance < 2f)
                {
                    HazardousWorldPlugin.Log.LogWarning($"Acid spit hits player for {damage}!");
                    // TODO: FrameworkAPI.DamagePlayer(damage, DamageType.Acid);
                }

                // Splash effect
                CreateAcidSplash(acidBlob.transform.position);
                Destroy(acidBlob);
            }
        }

        void CreateAcidSplash(Vector3 position)
        {
            // Create small splash particles
            for (int i = 0; i < 5; i++)
            {
                GameObject splash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                splash.transform.position = position;
                splash.transform.localScale = Vector3.one * 0.15f;
                splash.GetComponent<Renderer>().material.color = new Color(0.7f, 0.9f, 0.1f, 0.7f);
                UnityEngine.Object.Destroy(splash.GetComponent<Collider>());

                Vector3 velocity = new Vector3(
                    UnityEngine.Random.Range(-2f, 2f),
                    UnityEngine.Random.Range(1f, 3f),
                    UnityEngine.Random.Range(-2f, 2f)
                );

                StartCoroutine(AnimateSplash(splash, velocity));
            }
        }

        IEnumerator AnimateSplash(GameObject splash, Vector3 velocity)
        {
            float lifetime = 0.5f;
            float elapsed = 0f;

            while (elapsed < lifetime && splash != null)
            {
                elapsed += Time.deltaTime;
                velocity.y -= 10f * Time.deltaTime; // Gravity
                splash.transform.position += velocity * Time.deltaTime;
                splash.transform.localScale = Vector3.one * 0.15f * (1f - elapsed / lifetime);
                yield return null;
            }

            if (splash != null) Destroy(splash);
        }

        public void TakeDamage(float damage)
        {
            health -= damage;
            HazardousWorldPlugin.LogDebug($"Acid spitter took {damage} damage, health: {health}/{maxHealth}");

            if (health <= 0)
            {
                Die();
            }
        }

        void Die()
        {
            HazardousWorldPlugin.Log.LogInfo("Acid spitter destroyed!");
            // Burst of acid on death
            CreateAcidSplash(transform.position + Vector3.up);
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Controller for Grasping Vines - slows and traps players
    /// </summary>
    public class GraspingVineController : MonoBehaviour
    {
        public float slowAmount = 0.5f; // 50% slow
        public float grabDuration = 2f;
        public float grabRadius = 2f;
        public float health = 25f;
        public float maxHealth = 25f;

        private bool isPlayerGrabbed = false;
        private float grabEndTime;
        private List<Transform> tendrils = new List<Transform>();

        void Start()
        {
            // Add trigger collider
            var trigger = gameObject.AddComponent<SphereCollider>();
            trigger.radius = grabRadius;
            trigger.isTrigger = true;

            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // Cache tendrils for animation
            foreach (Transform child in transform)
            {
                if (child.name.Contains("Capsule"))
                {
                    tendrils.Add(child);
                }
            }
        }

        void Update()
        {
            var player = Player.instance;
            if (player == null) return;

            float distance = Vector3.Distance(transform.position, player.transform.position);

            if (distance <= grabRadius)
            {
                if (!isPlayerGrabbed)
                {
                    GrabPlayer();
                }
            }
            else if (isPlayerGrabbed)
            {
                ReleasePlayer();
            }

            // Check grab duration
            if (isPlayerGrabbed && Time.time >= grabEndTime)
            {
                ReleasePlayer();
            }

            // Animate tendrils when player is grabbed
            if (isPlayerGrabbed)
            {
                AnimateTendrils();
            }
        }

        void GrabPlayer()
        {
            isPlayerGrabbed = true;
            grabEndTime = Time.time + grabDuration;

            HazardousWorldPlugin.Log.LogWarning($"Grasping vines grab player! Speed reduced by {slowAmount * 100}% for {grabDuration}s");

            // TODO: Apply slow effect via Framework
            // FrameworkAPI.ApplyStatusEffect(StatusEffectType.Slow, slowAmount, grabDuration);

            // Play grab animation
            StartCoroutine(GrabAnimation());
        }

        void ReleasePlayer()
        {
            if (!isPlayerGrabbed) return;

            isPlayerGrabbed = false;
            HazardousWorldPlugin.Log.LogInfo("Vines release player");

            // TODO: Remove slow effect
            // FrameworkAPI.RemoveStatusEffect(StatusEffectType.Slow);
        }

        IEnumerator GrabAnimation()
        {
            // Tendrils reach up when grabbing
            foreach (var tendril in tendrils)
            {
                if (tendril != null)
                {
                    Vector3 original = tendril.localScale;
                    tendril.localScale = new Vector3(original.x, original.y * 1.5f, original.z);
                }
            }

            yield return new WaitForSeconds(grabDuration);

            // Return to normal
            foreach (var tendril in tendrils)
            {
                if (tendril != null)
                {
                    Vector3 current = tendril.localScale;
                    tendril.localScale = new Vector3(current.x, current.y / 1.5f, current.z);
                }
            }
        }

        void AnimateTendrils()
        {
            // Subtle wiggle animation
            float wiggle = Mathf.Sin(Time.time * 5f) * 0.1f;
            foreach (var tendril in tendrils)
            {
                if (tendril != null)
                {
                    Vector3 rot = tendril.localEulerAngles;
                    rot.z = wiggle * 20f;
                    tendril.localEulerAngles = rot;
                }
            }
        }

        public void TakeDamage(float damage)
        {
            health -= damage;
            HazardousWorldPlugin.LogDebug($"Grasping vine took {damage} damage, health: {health}/{maxHealth}");

            if (health <= 0)
            {
                if (isPlayerGrabbed) ReleasePlayer();
                Die();
            }
        }

        void Die()
        {
            HazardousWorldPlugin.Log.LogInfo("Grasping vine destroyed!");
            Destroy(gameObject);
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                HazardousWorldPlugin.Log.LogWarning("Entering vine trap zone!");
            }
        }
    }
}
