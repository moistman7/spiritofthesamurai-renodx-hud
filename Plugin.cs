using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpiritOfTheSamuraiRenoDXHud
{
    // RenoDX HDR HUD support for The Spirit of the Samurai. The game draws its HUD into the scene target
    // before post-processing, so the Unity RenoDX addon's UI Brightness cancels out against its scene
    // brightness compensation. This plugin optionally renders UI/Canvas in a second camera after
    // post-processing.
    [BepInPlugin(Guid, "Spirit of the Samurai RenoDX HUD", "1.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "renodx.spiritofthesamurai.hdrhud";

        internal static BepInEx.Logging.ManualLogSource Log;
        internal static ConfigEntry<bool> SeparateHud;

        private void Awake()
        {
            Log = Logger;
            SeparateHud = Config.Bind("HUD", "SeparateHud", false,
                "Render UI/Canvas after scene post-processing so the Unity RenoDX UI Brightness slider affects the HUD. Experimental; F7 toggles and saves.");
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
            Logger.LogInfo($"Loaded. SeparateHud={SeparateHud.Value} (F7).");
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Scene-owned controller: the plugin's own GameObject is not a reliable Update host here.
            if (mode == LoadSceneMode.Single)
            {
                var controller = new GameObject("SamuraiRenoDXHud");
                controller.AddComponent<HotkeyController>();
                controller.AddComponent<HudComposition>();
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
        }
    }
}
