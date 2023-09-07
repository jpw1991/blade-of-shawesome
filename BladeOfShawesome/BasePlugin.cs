using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using BepInEx.Configuration;
using BladeOfShawesome.Items;
using ChebsValheimLibrary;
using HarmonyLib;
using Jotunn;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;
using Paths = BepInEx.Paths;

namespace BladeOfShawesome
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency(Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal class BasePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.chebgonaz.bladeofshawesome";
        public const string PluginName = "BladeOfShawesome";
        public const string PluginVersion = "0.0.2";

        private const string ConfigFileName = PluginGuid + ".cfg";
        private static readonly string ConfigFileFullPath = Path.Combine(Paths.ConfigPath, ConfigFileName);

        public readonly System.Version ChebsValheimLibraryVersion = new("2.3.1");

        private readonly Harmony harmony = new(PluginGuid);

        // if set to true, the particle effects that for some reason hurt radeon are dynamically disabled
        public static ConfigEntry<bool> RadeonFriendly;

        public static CustomLocalization Localization = LocalizationManager.Instance.GetLocalization();

        public static BladeOfShawesomeItem BladeOfShawesome = new();
        public static GreatswordOfShawesomeItem GreatswordOfShawesome = new();

        private void Awake()
        {
            if (!Base.VersionCheck(ChebsValheimLibraryVersion, out string message))
            {
                Jotunn.Logger.LogWarning(message);
            }

            CreateConfigValues();
            LoadAssetBundle();
            harmony.PatchAll();

            SetupWatcher();

            PrefabManager.OnVanillaPrefabsAvailable += DoOnVanillaPrefabsAvailable;
        }

        private void DoOnVanillaPrefabsAvailable()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= DoOnVanillaPrefabsAvailable;
            
            UpdateAllRecipes();
            
            // steal himminafl attack and give it to the swords
            var himminaflAttack = PrefabManager.Instance.GetPrefab("fx_himminafl_hit");
            if (himminaflAttack == null)
            {
                Logger.LogError("Error: failed to get fx_himminafl_hit prefab");
                return;
            }

            var swords = new List<GameObject>()
            {
                PrefabManager.Instance.GetPrefab(BladeOfShawesome.ItemName),
                PrefabManager.Instance.GetPrefab(GreatswordOfShawesome.ItemName)
            };
            foreach (var sword in swords)
            {
                if (sword == null)
                {
                    Logger.LogError("Error: sword is null");
                    continue;
                }

                if (!sword.TryGetComponent(out ItemDrop itemDrop))
                {
                    Logger.LogError("Error: failed to get sword's item drop");
                    continue;
                }

                itemDrop.m_itemData.m_shared.m_attack.m_hitEffect = new EffectList()
                {
                    m_effectPrefabs = new EffectList.EffectData[]
                    {
                        new EffectList.EffectData()
                        {
                            m_prefab = himminaflAttack,
                        }
                    }
                };
                
                itemDrop.m_itemData.m_shared.m_equipStatusEffect = ScriptableObject.CreateInstance<SE_Rested>();
            }
        }

        private void UpdateAllRecipes(bool updateItemsInScene = false)
        {
            BladeOfShawesome.UpdateRecipe();
            GreatswordOfShawesome.UpdateRecipe();
        }

        private void CreateConfigValues()
        {
            Config.SaveOnConfigSet = true;

            RadeonFriendly = Config.Bind($"{GetType().Name} (Client)", "RadeonFriendly",
                false, new ConfigDescription("ONLY set this to true if you have graphical issues with " +
                                             "the mod. It will disable all particle effects for the mod's prefabs " +
                                             "which seem to give users with Radeon cards trouble for unknown " +
                                             "reasons. If you have problems with lag it might also help to switch" +
                                             "this setting on."));
            BladeOfShawesome.CreateConfigs(this);
            GreatswordOfShawesome.CreateConfigs(this);
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.Error += (sender, e) => Jotunn.Logger.LogError($"Error watching for config changes: {e}");
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                Logger.LogInfo("Read updated config values");
                Config.Reload();
                UpdateAllRecipes(true);
            }
            catch (Exception exc)
            {
                Logger.LogError($"There was an issue loading your {ConfigFileName}: {exc}");
                Logger.LogError("Please check your config entries for spelling and format!");
            }
        }

        private void LoadAssetBundle()
        {
            // order is important (I think): items, creatures, structures
            var assetBundlePath = Path.Combine(Path.GetDirectoryName(Info.Location), "shawesome");
            var chebgonazAssetBundle = AssetUtils.LoadAssetBundle(assetBundlePath);
            try
            {
                {
                    var prefab = Base.LoadPrefabFromBundle(BladeOfShawesome.PrefabName, chebgonazAssetBundle,
                        RadeonFriendly.Value);
                    ItemManager.Instance.AddItem(BladeOfShawesome.GetCustomItemFromPrefab(prefab));
                }
                {
                    var prefab = Base.LoadPrefabFromBundle(GreatswordOfShawesome.PrefabName, chebgonazAssetBundle,
                        RadeonFriendly.Value);
                    ItemManager.Instance.AddItem(GreatswordOfShawesome.GetCustomItemFromPrefab(prefab));
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Exception caught while loading assets: {ex}");
            }
            finally
            {
                chebgonazAssetBundle.Unload(false);
            }
        }
    }
}