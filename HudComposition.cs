using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SpiritOfTheSamuraiRenoDXHud
{
    // Experimental: split UI/Canvas into its own camera so RenoDX's UI brightness applies to it.
    //
    // Isolation is by layer. Everything under UI/Canvas is moved to an unused layer; the scene
    // camera drops that layer from its culling mask and the HUD camera renders only that layer.
    // 2D lights follow the same mask, so the canvas's own light stays with the HUD and world
    // lights (and their volumetric glow) never reach it. Original layers are restored on teardown.
    //
    // The HUD camera is a second base camera drawn straight onto the scene camera's output, not
    // an overlay in a camera stack. A stack would route post-processing through URP's
    // R8G8B8A8_UNorm intermediates, which RenoDX does not upgrade, clipping scene highlights
    // whenever UI brightness is below game brightness.
    public class HudComposition : MonoBehaviour
    {
        private const int RefreshFrames = 10;

        private Canvas canvas;
        private Camera scene_camera;
        private Camera hud_camera;
        private UniversalAdditionalCameraData scene_data;
        private readonly List<Transform> children = new List<Transform>();
        private readonly Dictionary<GameObject, int> original_layers = new Dictionary<GameObject, int>();
        private int hud_layer = -1;
        private int scene_mask;
        private bool setup_failed;

        private void LateUpdate()
        {
            if (!Plugin.SeparateHud.Value)
            {
                Teardown();
                setup_failed = false;
                return;
            }
            if (setup_failed) return;
            if (hud_camera == null && !Setup()) return;
            if (scene_camera == null || canvas == null)
            {
                Teardown();
                return;
            }
            if (Time.frameCount % RefreshFrames == 0) MoveCanvasToHudLayer();
            hud_camera.CopyFrom(scene_camera);
            hud_camera.transform.SetPositionAndRotation(scene_camera.transform.position, scene_camera.transform.rotation);
            hud_camera.projectionMatrix = scene_camera.projectionMatrix;
            // Keep the scene camera's output: draw after it without clearing.
            hud_camera.clearFlags = CameraClearFlags.Nothing;
            hud_camera.allowMSAA = false;
            hud_camera.depth = scene_camera.depth + 1f;
            hud_camera.targetTexture = null;
            hud_camera.cullingMask = 1 << hud_layer;
            hud_camera.enabled = scene_camera.enabled;
            scene_camera.cullingMask = scene_mask & ~(1 << hud_layer);
            canvas.worldCamera = hud_camera;
        }

        private bool Setup()
        {
            foreach (Canvas candidate in Resources.FindObjectsOfTypeAll<Canvas>())
            {
                if (!candidate.gameObject.scene.IsValid() || Plugin.GetPath(candidate.transform) != "UI/Canvas"
                    || candidate.transform.Find("InGameUI") == null || candidate.worldCamera == null) continue;
                var camera = candidate.worldCamera;
                var data = camera.GetComponent<UniversalAdditionalCameraData>();
                if (data == null || data.renderType != CameraRenderType.Base)
                {
                    Plugin.Log.LogWarning("HUD composition unavailable: scene camera is not a URP base camera.");
                    setup_failed = true;
                    return false;
                }
                hud_layer = FindFreeLayer();
                if (hud_layer < 0)
                {
                    Plugin.Log.LogWarning("HUD composition unavailable: no unused layer to isolate the HUD.");
                    setup_failed = true;
                    return false;
                }
                canvas = candidate;
                scene_camera = camera;
                scene_data = data;
                scene_mask = scene_camera.cullingMask;
                var camera_object = new GameObject("SamuraiHudCamera");
                camera_object.transform.SetParent(transform, false);
                hud_camera = camera_object.AddComponent<Camera>();
                hud_camera.CopyFrom(scene_camera);
                camera_object.AddComponent<HudCameraMarker>();
                var hud_data = camera_object.AddComponent<UniversalAdditionalCameraData>();
                hud_data.SetRenderer((int)AccessTools.Field(typeof(UniversalAdditionalCameraData), "m_RendererIndex").GetValue(scene_data));
                hud_data.renderType = CameraRenderType.Base;
                hud_data.renderPostProcessing = false;
                hud_data.antialiasing = AntialiasingMode.None;
                hud_data.volumeLayerMask = 0;
                MoveCanvasToHudLayer();
                canvas.worldCamera = hud_camera;
                Plugin.Log.LogInfo($"HUD composition active: '{Plugin.GetPath(canvas.transform)}' on layer {hud_layer} -> camera after '{Plugin.GetPath(scene_camera.transform)}'. F7 restores vanilla composition.");
                return true;
            }
            return false;
        }

        private static int FindFreeLayer()
        {
            for (int layer = 31; layer >= 8; --layer)
                if (string.IsNullOrEmpty(LayerMask.LayerToName(layer))) return layer;
            return -1;
        }

        // Runs every few frames so objects instantiated under the canvas join the HUD layer.
        private void MoveCanvasToHudLayer()
        {
            canvas.GetComponentsInChildren(true, children);
            foreach (Transform child in children)
            {
                GameObject target = child.gameObject;
                if (target.layer == hud_layer) continue;
                original_layers[target] = target.layer;
                target.layer = hud_layer;
            }
        }

        private void Teardown()
        {
            foreach (var entry in original_layers)
                if (entry.Key != null && entry.Key.layer == hud_layer) entry.Key.layer = entry.Value;
            original_layers.Clear();
            if (scene_camera != null && hud_layer >= 0) scene_camera.cullingMask = scene_mask;
            if (canvas != null && canvas.worldCamera == hud_camera) canvas.worldCamera = scene_camera;
            if (hud_camera != null)
            {
                hud_camera.enabled = false;
                Destroy(hud_camera.gameObject);
            }
            hud_camera = null;
            hud_layer = -1;
        }

        private void OnDisable()
        {
            Teardown();
        }
    }

    public class HudCameraMarker : MonoBehaviour { }

    [HarmonyPatch]
    internal static class HudPostProcessingPatch
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(AccessTools.TypeByName("UnityEngine.Rendering.Universal.Renderer2D"), "Setup");
        }

        private static void Prefix(ref RenderingData renderingData, out bool __state)
        {
            __state = renderingData.postProcessingEnabled;
            if (renderingData.cameraData.camera.GetComponent<HudCameraMarker>() != null)
                renderingData.postProcessingEnabled = false;
        }

        private static void Postfix(ref RenderingData renderingData, bool __state)
        {
            renderingData.postProcessingEnabled = __state;
        }
    }

    // URP gives a base camera its own R8G8B8A8_UNorm color texture (render scale, letterbox
    // viewport, ...) and blits it to the target afterwards, which would replace the scene with
    // an uninitialized HUD-only image. The HUD camera instead draws straight onto the target
    // the scene camera already resolved to, which RenoDX upgrades.
    [HarmonyPatch]
    internal static class HudTargetPatch
    {
        private static readonly AccessTools.FieldRef<object, bool> CreateColor =
            AccessTools.FieldRefAccess<bool>(AccessTools.TypeByName("UnityEngine.Rendering.Universal.Renderer2D"), "m_CreateColorTexture");
        private static readonly AccessTools.FieldRef<object, bool> CreateDepth =
            AccessTools.FieldRefAccess<bool>(AccessTools.TypeByName("UnityEngine.Rendering.Universal.Renderer2D"), "m_CreateDepthTexture");

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(AccessTools.TypeByName("UnityEngine.Rendering.Universal.Renderer2D"), "CreateRenderTextures");
        }

        private static bool Prefix(object __instance, ref CameraData cameraData, out RenderTargetHandle colorTargetHandle, out RenderTargetHandle depthTargetHandle)
        {
            colorTargetHandle = depthTargetHandle = RenderTargetHandle.CameraTarget;
            if (cameraData.camera.GetComponent<HudCameraMarker>() == null) return true;
            CreateColor(__instance) = false;
            CreateDepth(__instance) = false;
            return false;
        }
    }
}
