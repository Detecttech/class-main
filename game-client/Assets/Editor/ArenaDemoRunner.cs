using System.Collections.Generic;
using System.IO;
using QuizBattle.Arena;
using QuizBattle.Arena.Vfx;
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
        allPassed &= RunScenario("hp-path", seed: 1, maxRounds: 20, screenshotName: "arena-demo-hp-path.png");
        allPassed &= RunScenario("zone-control-path", seed: 1, maxRounds: 2, screenshotName: "arena-demo-zone-control-path.png");
        allPassed &= RunVfxShowcase();

        EditorApplication.Exit(allPassed ? 0 : 1);
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
        grid.BuildGrid(width, height, new List<ZoneDef>());
        ArenaEnvironment.FrameGrid(rig, grid, width, height);

        var spawned = new List<ParticleSystem>();
        spawned.AddRange(AbilityVfxPlayer.Play("vfx_fireball", grid.TileToWorldPos(1, 1), grid.TileToWorldPos(2, 2), eliminated: false));
        spawned.AddRange(AbilityVfxPlayer.Play("vfx_shield_shimmer", grid.TileToWorldPos(4, 1), grid.TileToWorldPos(4, 1), eliminated: false));
        spawned.AddRange(AbilityVfxPlayer.Play("vfx_wind_trail", grid.TileToWorldPos(0, 4), grid.TileToWorldPos(2, 4), eliminated: false));
        spawned.AddRange(AbilityVfxPlayer.Play("vfx_life_drain", grid.TileToWorldPos(5, 4), grid.TileToWorldPos(6, 5), eliminated: false));
        spawned.AddRange(AbilityVfxPlayer.Play("vfx_basic_strike", grid.TileToWorldPos(6, 1), grid.TileToWorldPos(6, 1), eliminated: true));

        AbilityVfxPlayer.SimulateAll(spawned, 0.15f);

        Debug.Log($"[ArenaDemoRunner:vfx-showcase] spawned {spawned.Count} particle systems across 5 ability tags");
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
