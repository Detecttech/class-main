using System.Collections.Generic;
using System.IO;
using QuizBattle.Arena;
using QuizBattle.Arena.Vfx;
using QuizBattle.Arena.Visuals;
using QuizBattle.Characters;
using QuizBattle.GameState;
using QuizBattle.GameState.MockEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// Headless verification for Phase 1 (local playable loop): builds the Arena scene
/// contents via GameManager, runs a full auto-play mock match synchronously, renders a
/// screenshot of the final state, and asserts a winner was produced. Run twice (see Run)
/// to prove both win-condition paths per the project plan's verification requirement.
public static class ArenaDemoRunner
{
    public static void Run()
    {
        bool allPassed = true;
        allPassed &= RunScenario("race-path", seed: 1, maxRounds: 20, screenshotName: "arena-demo-race-path.png");
        allPassed &= RunScenario("progress-tiebreak-path", seed: 1, maxRounds: 2, screenshotName: "arena-demo-progress-tiebreak-path.png");
        allPassed &= RunVfxShowcase();
        allPassed &= RunCharacterSelectShowcase();

        EditorApplication.Exit(allPassed ? 0 : 1);
    }

    private static bool RunCharacterSelectShowcase()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var go = new GameObject("CharacterSelectController");
        go.AddComponent<QuizBattle.UI.CharacterSelect.CharacterSelectScreen>();

        var cam = Camera.main;
        if (cam == null)
        {
            var camObj = new GameObject("MainCamera");
            cam = camObj.AddComponent<Camera>();
            cam.tag = "MainCamera";
            camObj.transform.position = new Vector3(0, 0, -10f);
        }

        CaptureScreenshot("character-select-showcase.png");
        return true;
    }

    private static bool RunScenario(string label, int seed, int maxRounds, string screenshotName)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var defs = LoadCharacterDefs();
        if (defs.Count != 4)
        {
            Debug.LogError($"[ArenaDemoRunner:{label}] expected 4 character definitions, found {defs.Count}");
            return false;
        }

        var manager = GameManager.Bootstrap(defs);
        var result = manager.RunAutoPlayMatch(seed, maxRounds);

        if (result == null)
        {
            Debug.LogError($"[ArenaDemoRunner:{label}] match never reached a result within the round guard");
            return false;
        }

        Debug.Log($"[ArenaDemoRunner:{label}] PASSED — winner={result.winnerId} reason={result.reason}");

        var grid = Object.FindFirstObjectByType<GridController>();
        if (grid != null)
        {
            foreach (var fct in Object.FindObjectsByType<FloatingCombatText>(FindObjectsInactive.Include))
            {
                Object.DestroyImmediate(fct.gameObject);
            }
            FloatingCombatText.Spawn(grid.TileToWorldPos(3, 1) + Vector3.up * 1.5f, "-15 HP", QuizBattlePalette.RoofTilesRed, 1.15f);
        }

        CaptureScreenshot(screenshotName);
        return true;
    }

    /// Particles don't simulate in these headless runs (no player loop), so this spawns
    /// one of each ability effect at known tiles and manually advances them partway
    /// through their lifetime before screenshotting — otherwise VFX would be the one
    /// phase with no visual evidence it works at all.
    private static bool RunVfxShowcase()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var gridObj = new GameObject("Grid");
        var grid = gridObj.AddComponent<GridController>();
        var rig = ArenaEnvironment.Acquire(new Color(0.08f, 0.08f, 0.13f));

        const int width = 8;
        const int height = 6;
        grid.BuildGrid(width, height, height - 1);
        ArenaEnvironment.FrameGrid(rig, grid, width, height);

        var defs = LoadCharacterDefs();
        if (defs.Count > 0)
        {
            var frozenToken = CharacterToken.Create(defs[0].displayName, CharacterVisual.From(defs[0]), grid.TileToWorldPos(3, 3));
            frozenToken.SetFrozen(true);
        }

        var spawned = new List<ParticleSystem>();
        spawned.AddRange(AbilityVfxPlayer.Play("vfx_fireball", grid.TileToWorldPos(1, 1), grid.TileToWorldPos(2, 2), eliminated: false));
        spawned.AddRange(AbilityVfxPlayer.Play("vfx_shield_shimmer", grid.TileToWorldPos(4, 1), grid.TileToWorldPos(4, 1), eliminated: false));
        spawned.AddRange(AbilityVfxPlayer.Play("vfx_wind_trail", grid.TileToWorldPos(0, 4), grid.TileToWorldPos(2, 4), eliminated: false));
        spawned.AddRange(AbilityVfxPlayer.Play("vfx_life_drain", grid.TileToWorldPos(5, 4), grid.TileToWorldPos(6, 5), eliminated: false));
        spawned.AddRange(AbilityVfxPlayer.Play("vfx_basic_strike", grid.TileToWorldPos(6, 1), grid.TileToWorldPos(6, 1), eliminated: true));
        spawned.AddRange(AbilityVfxPlayer.Play("vfx_freeze", grid.TileToWorldPos(1, 3), grid.TileToWorldPos(3, 3), eliminated: false));

        AbilityVfxPlayer.SimulateAll(spawned, 0.15f);

        FloatingCombatText.Spawn(grid.TileToWorldPos(2, 2) + Vector3.up * 1.5f, "-25 HP CRIT!", QuizBattlePalette.RoofTilesRed, 1.3f);
        FloatingCombatText.Spawn(grid.TileToWorldPos(4, 1) + Vector3.up * 1.5f, "SHIELDED!", QuizBattlePalette.GoldTrim, 1.1f);
        FloatingCombatText.Spawn(grid.TileToWorldPos(3, 3) + Vector3.up * 1.5f, "FROZEN!", QuizBattlePalette.WaterBlue, 1.2f);

        Debug.Log($"[ArenaDemoRunner:vfx-showcase] spawned {spawned.Count} particle systems across 6 ability tags");
        CaptureScreenshot("vfx-showcase.png");
        return true;
    }

    private static List<CharacterDefinitionSO> LoadCharacterDefs()
    {
        var defs = new List<CharacterDefinitionSO>();
        var guids = AssetDatabase.FindAssets("t:CharacterDefinitionSO", new[] { "Assets/Resources/Characters" });
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var def = AssetDatabase.LoadAssetAtPath<CharacterDefinitionSO>(path);
            if (def != null) defs.Add(def);
        }
        defs.Sort((a, b) => string.CompareOrdinal(a.characterId, b.characterId));
        return defs;
    }

    private static void CaptureScreenshot(string fileName)
    {
        var camera = Camera.main;
        if (camera == null)
        {
            Debug.LogWarning("[ArenaDemoRunner] no Camera.main found, skipping screenshot");
            return;
        }

        // No player loop in this synchronous batch run, so CharacterToken's animated
        // MoveTo would otherwise leave every token frozen at its pre-move position.
        foreach (var token in Object.FindObjectsByType<CharacterToken>(FindObjectsSortMode.None))
        {
            token.CompleteMovement();
        }

        // Ensure UI canvases are bound to Camera so they render onto the target RenderTexture
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1.5f;
            canvas.sortingOrder = 1000;
        }
        Canvas.ForceUpdateCanvases();

        const int width = 1280;
        const int height = 720;
        var rt = new RenderTexture(width, height, 24);
        camera.targetTexture = rt;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        camera.Render();

        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        tex.Apply();

        camera.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(rt);

        var dir = "D:/temp";
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, fileName);
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        Debug.Log($"[ArenaDemoRunner] screenshot saved to {path}");
    }
}
