using System.IO;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SpiritOfTheSamuraiRenoDXHud
{
    // RenoDX companion for The Spirit of the Samurai. The game draws its HUD into the scene target
    // before post-processing, so the Unity RenoDX addon's UI Brightness cancels out against its scene
    // brightness compensation. This plugin optionally renders UI/Canvas in a second camera after
    // post-processing, and optionally hides the UI/Canvas/vignette sprite that RenoDX's Vignette
    // slider cannot reach. Both are independent of the ultrawide plugin.
    // Soft dependency only orders loading, so the ultrawide version check below can see it.
    [BepInPlugin(Guid, "Spirit of the Samurai RenoDX HUD", "1.0.0")]
    [BepInDependency(UltrawideGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "renodx.spiritofthesamurai.hdrhud";
        private const string UltrawideGuid = "renodx.spiritofthesamurai.ultrawide";

        internal static BepInEx.Logging.ManualLogSource Log;
        internal static ConfigEntry<bool> SeparateHud;
        internal static ConfigEntry<bool> DisableVignette;
        private static bool active = true;

        private void Awake()
        {
            Log = Logger;
            bool fresh = !File.Exists(Config.ConfigFilePath);
            SeparateHud = Config.Bind("HUD", "SeparateHud", false,
                "Render UI/Canvas after scene post-processing so the Unity RenoDX UI Brightness slider affects the HUD. Experimental; F7 toggles and saves.");
            DisableVignette = Config.Bind("Vignette", "DisableVignette", false,
                "Hide the game's UI/Canvas/vignette sprite. F8 toggles and saves.");
            if (fresh) MigrateUltrawideSettings();

            // Ultrawide 1.7.x carries its own copy of both features; running two HUD cameras or two
            // vignette toggles on the same objects is not supported.
            if (Chainloader.PluginInfos.TryGetValue(UltrawideGuid, out PluginInfo ultrawide)
                && ultrawide.Metadata.Version < new System.Version(1, 8, 0))
            {
                active = false;
                Logger.LogWarning($"Inactive: Spirit of the Samurai Ultrawide {ultrawide.Metadata.Version} already provides HUD/vignette control. Update it to 1.8.0 or later.");
                return;
            }

            System.Type renderer_2d = AccessTools.TypeByName("UnityEngine.Rendering.Universal.Renderer2D");
            if (AccessTools.Method(renderer_2d, "Setup") != null && AccessTools.Method(renderer_2d, "CreateRenderTextures") != null
                && AccessTools.Field(renderer_2d, "m_CreateColorTexture") != null && AccessTools.Field(renderer_2d, "m_CreateDepthTexture") != null)
            {
                var harmony = new Harmony(Guid);
                harmony.PatchAll(typeof(HudPostProcessingPatch));
                harmony.PatchAll(typeof(HudTargetPatch));
            }
            else
            {
                SeparateHud.Value = false;
                Logger.LogWarning("HUD composition disabled: expected URP Renderer2D members were not found.");
            }
            SceneManager.sceneLoaded += OnSceneLoaded;
            Logger.LogInfo($"Loaded. SeparateHud={SeparateHud.Value} (F7), DisableVignette={DisableVignette.Value} (F8).");
        }

        // Carry over preferences saved by ultrawide 1.7.x so moving to this plugin keeps behaviour.
        private void MigrateUltrawideSettings()
        {
            string path = Path.Combine(Paths.ConfigPath, UltrawideGuid + ".cfg");
            if (!File.Exists(path)) return;
            var old_config = new ConfigFile(path, false) { SaveOnConfigSet = false };
            SeparateHud.Value = old_config.Bind("Experimental", "SeparateHud", false).Value;
            DisableVignette.Value = old_config.Bind("Diagnostics", "DisableVignette", false).Value;
            Logger.LogInfo($"Imported settings from {Path.GetFileName(path)}: SeparateHud={SeparateHud.Value}, DisableVignette={DisableVignette.Value}.");
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!active) return;
            // Scene-owned controller: the plugin's own GameObject is not a reliable Update host here.
            if (mode == LoadSceneMode.Single)
            {
                var controller = new GameObject("SamuraiRenoDXHud");
                controller.AddComponent<HotkeyController>();
                controller.AddComponent<HudComposition>();
            }
            foreach (Transform target in Resources.FindObjectsOfTypeAll<Transform>())
            {
                if (target.gameObject.scene.IsValid() && target.name == "vignette" && GetPath(target) == "UI/Canvas/vignette"
                    && target.GetComponent<VignetteControl>() == null)
                {
                    target.gameObject.AddComponent<VignetteControl>();
                    Logger.LogInfo($"Vignette control attached to '{GetPath(target)}'.");
                }
            }
        }

        internal static string GetPath(Transform transform)
        {
            string path = transform.name;
            for (Transform parent = transform.parent; parent != null; parent = parent.parent)
            {
                path = parent.name + "/" + path;
            }
            return path;
        }
    }

    public class HotkeyController : MonoBehaviour
    {
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F7))
            {
                Plugin.SeparateHud.Value = !Plugin.SeparateHud.Value;
                Plugin.Log.LogInfo($"HUD composition requested={Plugin.SeparateHud.Value}.");
            }
            if (Input.GetKeyDown(KeyCode.F8))
            {
                Plugin.DisableVignette.Value = !Plugin.DisableVignette.Value;
                Plugin.Log.LogInfo($"Vignette disabled={Plugin.DisableVignette.Value}.");
            }
        }
    }

    public class VignetteControl : MonoBehaviour
    {
        private Graphic graphic;
        private Renderer renderer;
        private bool graphic_suppressed;
        private bool renderer_suppressed;

        private void Awake()
        {
            graphic = GetComponent<Graphic>();
            renderer = GetComponent<Renderer>();
        }

        private void LateUpdate()
        {
            if (Plugin.DisableVignette.Value)
            {
                if (renderer != null && renderer.enabled)
                {
                    renderer.enabled = false;
                    renderer_suppressed = true;
                }
                if (graphic != null && graphic.enabled)
                {
                    graphic.enabled = false;
                    graphic_suppressed = true;
                }
            }
            else
            {
                Restore();
            }
        }

        private void Restore()
        {
            if (renderer_suppressed && renderer != null) renderer.enabled = true;
            if (graphic_suppressed && graphic != null) graphic.enabled = true;
            renderer_suppressed = graphic_suppressed = false;
        }

        private void OnDisable()
        {
            Restore();
        }
    }
}
