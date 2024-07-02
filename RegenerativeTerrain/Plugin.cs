using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using JetBrains.Annotations;
using ServerSync;
using UnityEngine;

namespace RegenerativeTerrain
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class RegenerativeTerrainPlugin : BaseUnityPlugin
    {
        internal const string ModName = "RegenerativeTerrain";
        internal const string ModVersion = "1.0.1";
        internal const string Author = "RustyMods";
        private const string ModGUID = Author + "." + ModName;
        private static readonly string ConfigFileName = ModGUID + ".cfg";
        private static readonly string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        internal static string ConnectionError = "";
        private readonly Harmony _harmony = new(ModGUID);
        public static readonly ManualLogSource RegenerativeTerrainLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);
        private static readonly ConfigSync ConfigSync = new(ModGUID) { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };
        public enum Toggle { On = 1, Off = 0 }

        public static GameObject _Root = null!;
        
        private static ConfigEntry<Toggle> _serverConfigLocked = null!;
        public static ConfigEntry<Toggle> _Enabled = null!;
        public static ConfigEntry<int> _RegenerationTime = null!;
        public static ConfigEntry<int> _CraftingStationRadius = null!;
        public static ConfigEntry<Toggle> _CraftingStationBlocks = null!;

        public static ConfigEntry<Toggle> _ResetDirt = null!;
        public static ConfigEntry<Toggle> _ResetPaved = null!;
        public static ConfigEntry<Toggle> _ResetCultivate = null!;

        public static ConfigEntry<float> _UpdateDelay = null!;

        public static ConfigEntry<int> _RespawnTime = null!;
        public static ConfigEntry<Toggle> _RegenerateVegetation = null!;
        public static ConfigEntry<float> _GrowthRate = null!;
        public static ConfigEntry<Toggle> _ExcludeOres = null!;

        public static ConfigEntry<string> _ExclusionMap = null!;

        public void Awake()
        {
            _Root = new GameObject("root");
            DontDestroyOnLoad(_Root);
            _Root.SetActive(false);
            
            InitConfigs();
            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();
        }

        private void InitConfigs()
        {
            _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On,
                "If on, the configuration is locked and can be changed by server admins only.");
            _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);
            _Enabled = config("2 - Terrain", "Enabled", Toggle.On, "If on, plugin will regenerate terrain");
            _RegenerationTime = config("2 - Terrain", "Duration", 30, "Set the minutes until complete reset of terrain");
            _CraftingStationBlocks = config("1 - General", "Stations Block", Toggle.On, "If on, crafting stations prevent terrain from regenerating");
            _CraftingStationRadius = config("1 - General", "Radius", 25,
                new ConfigDescription("Set the radius to check for crafting station",
                    new AcceptableValueRange<int>(0, 10000)));
            _ResetDirt = config("2 - Terrain", "Reset Dirt", Toggle.On, "If on, plugin will regenerate dirt to grass");
            _ResetPaved = config("2 - Terrain", "Reset Paved", Toggle.Off, "If on, plugin will regenerate paved to grass");
            _ResetCultivate = config("2 - Terrain", "Reset Cultivated", Toggle.Off, "If on, plugin will regenerate cultivated to grass");
            _UpdateDelay = config("2 - Terrain", "Update Frequency", 1f, "set the update frequency in seconds");

            _RespawnTime = config("3 - Vegetation", "Respawn Time", 60, "Set the time for vegetation to respawn");
            _RegenerateVegetation = config("3 - Vegetation", "Enabled", Toggle.Off,
                "If on, vegetation will respawn if not completely destroyed");
            _GrowthRate = config("3 - Vegetation", "Growth Duration", 30f, "Set growth rate in minutes");
            _ExcludeOres = config("3 - Vegetation", "Exclude Ores", Toggle.On,
                "If on, any vegetation that drops ores are excluded from regeneration");
            _ExclusionMap = config("3 - Vegetation", "Exclusion", "",
                "Set the prefabs you wish to excluded from behavior, ex: MineRock_Tin:MineRock_Copper");
        }

        private void OnDestroy()
        {
            Config.Save();
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                RegenerativeTerrainLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                RegenerativeTerrainLogger.LogError($"There was an issue loading your {ConfigFileName}");
                RegenerativeTerrainLogger.LogError("Please check your config entries for spelling and format!");
            }
        }
        
        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
            bool synchronizedSetting = true)
        {
            ConfigDescription extendedDescription =
                new(
                    description.Description +
                    (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"),
                    description.AcceptableValues, description.Tags);
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);
            //var configEntry = Config.Bind(group, name, value, description);

            SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description,
            bool synchronizedSetting = true)
        {
            return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        }

        private class ConfigurationManagerAttributes
        {
            [UsedImplicitly] public int? Order;
            [UsedImplicitly] public bool? Browsable;
            [UsedImplicitly] public string? Category;
            [UsedImplicitly] public Action<ConfigEntryBase>? CustomDrawer;
        }
    }
}