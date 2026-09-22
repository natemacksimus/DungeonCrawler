using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using DungeonCrawler.Core;
using DungeonCrawler.Game;

namespace DungeonCrawler.EditorTools
{
    /// <summary>
    /// Editor conveniences: put the game into a scene, and run the headless diagnostics without
    /// entering play mode.
    /// </summary>
    public static class DungeonCrawlerMenu
    {
        const string GameObjectName = "Dungeon Crawler";

        [MenuItem("Dungeon Crawler/Set Up Play Scene", priority = 0)]
        public static void SetUpPlayScene()
        {
            GameObject existing = GameObject.Find(GameObjectName);
            if (existing != null)
            {
                Selection.activeGameObject = existing;
                Debug.Log("[DungeonCrawler] scene already has " + GameObjectName + ".");
                return;
            }

            var go = new GameObject(GameObjectName);
            go.AddComponent<GameBootstrap>();
            Undo.RegisterCreatedObjectUndo(go, "Set Up Dungeon Crawler");
            Selection.activeGameObject = go;
            EditorSceneManager.MarkSceneDirty(go.scene);
            Debug.Log("[DungeonCrawler] added " + GameObjectName + " to " + go.scene.name + ". Press Play.");
        }

        [MenuItem("Dungeon Crawler/Run Self-Play Batch (10 runs)", priority = 20)]
        public static void RunSelfPlayBatch()
        {
            Debug.Log("[DungeonCrawler] self-play batch\n" + SelfPlay.RunBatch(1, 10));
        }

        [MenuItem("Dungeon Crawler/Validate 200 Floors", priority = 21)]
        public static void ValidateFloors()
        {
            int failures = 0;
            var builder = new System.Text.StringBuilder();
            for (int seed = 1; seed <= 25; seed++)
            {
                var rng = new Rng(seed);
                for (int depth = 1; depth <= 8; depth++)
                {
                    DungeonData dungeon = DungeonGenerator.Generate(depth, rng);
                    ValidationReport report = DungeonValidator.Validate(dungeon);
                    if (report.IsValid) continue;
                    failures++;
                    builder.Append("seed ").Append(seed).Append(" depth ").Append(depth)
                           .Append(": ").Append(report).Append('\n');
                }
            }

            if (failures == 0) Debug.Log("[DungeonCrawler] 200/200 floors valid and fully reachable.");
            else Debug.LogError("[DungeonCrawler] " + failures + " invalid floors\n" + builder);
        }

        [MenuItem("Dungeon Crawler/Print A Sample Floor", priority = 22)]
        public static void PrintSampleFloor()
        {
            DungeonData dungeon = DungeonGenerator.Generate(3, new Rng(Random.Range(1, 99999)));
            Debug.Log("[DungeonCrawler] " + DungeonValidator.Validate(dungeon) + "\n" + dungeon.ToAscii());
        }
    }
}
