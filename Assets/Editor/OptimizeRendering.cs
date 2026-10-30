using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

// Rendering-performance batch tool, added while investigating "laggy when facing the
// building / dense areas" reported on both desktop and WebGL builds. A forensic pass on
// GlobeFair.unity (grepping the text-serialized scene file directly) found:
//  - 0 LODGroup components across 16,431 MeshRenderers / 19,717 GameObjects - nothing in
//    the scene ever reduces detail or gets culled by distance.
//  - 8,626 GameObjects (43.8% of the scene) have no Static flags set at all, even though
//    the vast majority of exhibition geometry never moves - so Unity can't static-batch it
//    into fewer draw calls, and a lot of it was likely excluded from the occlusion bake.
//  - Desktop's default Quality tier (ProjectSettings > Quality > Standalone column) is
//    "Ultra": 150m shadow distance, 4 cascades, 4 realtime pixel lights. WebGL's default is
//    "High": 40m shadow distance, 2 cascades, 2 pixel lights. Both are expensive tiers for a
//    scene this dense with no culling/LOD to offset the cost.
//  - Occlusion Culling HAS been baked before (Assets/Scenes/GlobeFair/OcclusionCullingData.asset,
//    1.28MB) but against a scene where under half the geometry was flagged Occluder/Occludee
//    Static, so a lot of geometry that's actually hidden behind walls was probably never
//    excluded from what gets rendered.
//  - All 43 ReflectionProbes in the scene are Baked (not Realtime), so they're not a runtime
//    cost by themselves - only the "realtimeReflectionProbes" quality flag matters here.
//
// None of this touches source assets - only scene component flags/additions and Quality
// Settings tier values, both fully visible/reversible from their normal Editor windows
// (Inspector's Static dropdown, Window > Rendering > Occlusion Culling, Edit > Project
// Settings > Quality). Run the menu items below in order, 1 through 5.
public static class OptimizeRendering
{
    // Ancestors carrying any of these typed components are assumed to move at runtime -
    // never mark that renderer (or anything under it) static, and never let it get
    // distance-culled by a LODGroup either.
    private static readonly System.Type[] DynamicComponentTypes = new[]
    {
        typeof(Animator), typeof(Animation), typeof(Rigidbody), typeof(Rigidbody2D),
        typeof(CharacterController), typeof(UnityEngine.AI.NavMeshAgent),
        typeof(UnityEngine.AI.NavMeshObstacle), typeof(ParticleSystem), typeof(Cloth),
        typeof(Camera), typeof(AudioListener)
    };

    // Known custom scripts (matched by class name string, so this still compiles even if a
    // type gets renamed or moved to a different assembly) that move their own transform at
    // runtime and wouldn't be caught by the typed checks above.
    private static readonly string[] DynamicScriptNames = { "DoorRotation" };

    [MenuItem("Tools/Performance/1. Report Rendering Stats")]
    public static void ReportStats()
    {
        int meshRenderers = 0, skinnedRenderers = 0, lodGroups = 0, staticZero = 0, staticAny = 0, lights = 0;
        var scene = EditorSceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var go = t.gameObject;
                if (go.GetComponent<MeshRenderer>() != null) meshRenderers++;
                if (go.GetComponent<SkinnedMeshRenderer>() != null) skinnedRenderers++;
                if (go.GetComponent<LODGroup>() != null) lodGroups++;
                if (go.GetComponent<Light>() != null) lights++;
                var flags = GameObjectUtility.GetStaticEditorFlags(go);
                if ((int)flags == 0) staticZero++; else staticAny++;
            }
        }
        Debug.Log(string.Format(
            "[Rendering Stats] Scene: {0}\nMeshRenderers: {1}  SkinnedMeshRenderers: {2}  LODGroups: {3}  Lights: {4}\nGameObjects with NO static flags: {5}   with SOME static flag: {6}",
            scene.name, meshRenderers, skinnedRenderers, lodGroups, lights, staticZero, staticAny));
    }

    [MenuItem("Tools/Performance/2. Mark Safe Static Geometry")]
    public static void MarkSafeStaticGeometry()
    {
        var scene = EditorSceneManager.GetActiveScene();
        int marked = 0, skippedDynamic = 0, skippedLogo = 0, skippedAlready = 0, skippedNoRenderer = 0;

        var allTransforms = new List<Transform>();
        foreach (var root in scene.GetRootGameObjects())
            allTransforms.AddRange(root.GetComponentsInChildren<Transform>(true));

        foreach (var t in allTransforms)
        {
            GameObject go = t.gameObject;
            var meshRenderer = go.GetComponent<MeshRenderer>();
            if (meshRenderer == null) { skippedNoRenderer++; continue; }
            if (go.GetComponent<SkinnedMeshRenderer>() != null) { skippedDynamic++; continue; }

            var existingFlags = GameObjectUtility.GetStaticEditorFlags(go);
            if ((int)existingFlags != 0) { skippedAlready++; continue; }

            if (IsDynamic(t)) { skippedDynamic++; continue; }

            // Booth logo renderers swap their material's texture at runtime (see Booth.cs) -
            // leave batching off for these specifically so we don't silently un-batch them
            // mid-play and spam the console; occlusion flags are still safe/useful for them.
            bool isLogo = !string.IsNullOrEmpty(go.tag) && go.tag.Contains("_Logo_");

            StaticEditorFlags newFlags = StaticEditorFlags.ContributeGI
                | StaticEditorFlags.OccluderStatic
                | StaticEditorFlags.OccludeeStatic
                | StaticEditorFlags.ReflectionProbeStatic;
            if (!isLogo) newFlags |= StaticEditorFlags.BatchingStatic;
            else skippedLogo++;

            GameObjectUtility.SetStaticEditorFlags(go, newFlags);
            EditorUtility.SetDirty(go);
            marked++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log(string.Format(
            "=== Mark Safe Static Geometry Complete ===\nMarked static: {0}  Skipped (dynamic/mover): {1}  Skipped (logo, batching left off): {2}  Skipped (already flagged): {3}  Skipped (no MeshRenderer): {4}",
            marked, skippedDynamic, skippedLogo, skippedAlready, skippedNoRenderer));
    }

    private static bool IsDynamic(Transform t)
    {
        Transform cur = t;
        while (cur != null)
        {
            GameObject go = cur.gameObject;
            foreach (var type in DynamicComponentTypes)
                if (go.GetComponent(type) != null) return true;
            foreach (var name in DynamicScriptNames)
                if (go.GetComponent(name) != null) return true;
            cur = cur.parent;
        }
        return false;
    }

    // Small decorative props (signage, chairs, small fixtures) get a single-LOD LODGroup
    // that simply stops rendering once the object is small enough on screen not to matter.
    // This Unity version has no built-in mesh decimator to generate real lower-poly LOD
    // levels, but distance culling alone removes a large fraction of the 16k+ renderers from
    // the render loop once the player isn't near them - which is exactly the "lots of small
    // objects in view" scenario described.
    private const float SmallPropMaxBoundsSize = 3f; // world units, renderer.bounds.size.magnitude
    private const float CullScreenHeight = 0.02f; // object is culled once below 2% of screen height

    [MenuItem("Tools/Performance/3. Add Distance-Cull LOD To Small Props")]
    public static void AddDistanceCullLOD()
    {
        var scene = EditorSceneManager.GetActiveScene();
        int added = 0, skippedLarge = 0, skippedHasLOD = 0, skippedNoRenderer = 0, skippedDynamic = 0;

        var allTransforms = new List<Transform>();
        foreach (var root in scene.GetRootGameObjects())
            allTransforms.AddRange(root.GetComponentsInChildren<Transform>(true));

        foreach (var t in allTransforms)
        {
            GameObject go = t.gameObject;
            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer == null) { skippedNoRenderer++; continue; }
            if (go.GetComponent<LODGroup>() != null || go.GetComponentInParent<LODGroup>() != null)
            { skippedHasLOD++; continue; }
            if (IsDynamic(t)) { skippedDynamic++; continue; }

            if (renderer.bounds.size.magnitude > SmallPropMaxBoundsSize) { skippedLarge++; continue; }

            var lodGroup = go.AddComponent<LODGroup>();
            var lods = new LOD[1];
            lods[0] = new LOD(CullScreenHeight, new Renderer[] { renderer });
            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();
            EditorUtility.SetDirty(go);
            added++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log(string.Format(
            "=== Add Distance-Cull LOD Complete ===\nLODGroups added: {0}  Skipped (too large - structural): {1}  Skipped (already has LOD): {2}  Skipped (dynamic/mover): {3}  Skipped (no MeshRenderer): {4}",
            added, skippedLarge, skippedHasLOD, skippedDynamic, skippedNoRenderer));
    }

    [MenuItem("Tools/Performance/4. Rebake Occlusion Culling")]
    public static void RebakeOcclusionCulling()
    {
        Debug.Log("Starting Occlusion Culling bake - this can take a while for a scene this size (watch the progress bar). Run this AFTER step 2 so the newly-flagged static geometry is included.");
        bool ok = UnityEditor.StaticOcclusionCulling.Compute();
        Debug.Log(ok ? "=== Occlusion Culling bake complete ===" : "=== Occlusion Culling bake FAILED or was cancelled - check the Console above for details ===");
    }

    [MenuItem("Tools/Performance/5. Tighten Quality Tiers (Standalone + WebGL)")]
    public static void TightenQualityTiers()
    {
        var names = QualitySettings.names;
        int originalLevel = QualitySettings.GetQualityLevel();

        // "Ultra" (index 5) is Standalone's default per-platform quality tier - 150m shadow
        // distance / 4 cascades / 4 realtime pixel lights is console/high-end-PC territory,
        // not an appropriate default for a scene with 16k+ largely-unbatched, un-LOD'd
        // renderers. Trimmed, not gutted - shadows and reflections stay on.
        SetTierIfExists(names, "Ultra", () =>
        {
            QualitySettings.shadowDistance = 60f;
            QualitySettings.shadowCascades = 2;
            QualitySettings.pixelLightCount = 2;
        });

        // "High" (index 3) is WebGL's default - browsers have a much tighter CPU/GPU budget
        // than the desktop build, so trim it further than desktop's tier. Reflection probes
        // are all baked in this scene already (see header notes), so disabling the realtime
        // flag here costs nothing visually.
        SetTierIfExists(names, "High", () =>
        {
            QualitySettings.shadowDistance = 25f;
            QualitySettings.shadowCascades = 1;
            QualitySettings.pixelLightCount = 1;
            QualitySettings.realtimeReflectionProbes = false;
        });

        QualitySettings.SetQualityLevel(originalLevel, true);
        Debug.Log("=== Quality tier tightening complete === (Ultra: shadowDistance 150->60, cascades 4->2, pixelLights 4->2 | High: shadowDistance 40->25, cascades 2->1, pixelLights 2->1, realtime reflection probes off)");
        AssetDatabase.SaveAssets();
    }

    private static void SetTierIfExists(string[] names, string tierName, System.Action apply)
    {
        int idx = System.Array.IndexOf(names, tierName);
        if (idx < 0) { Debug.LogWarning("Quality tier not found: " + tierName); return; }
        QualitySettings.SetQualityLevel(idx, true);
        apply();
    }
}
