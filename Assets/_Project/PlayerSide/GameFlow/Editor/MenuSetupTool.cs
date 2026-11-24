using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using Hollow.PlayerSide.UI;

namespace Hollow.PlayerSide.GameFlow.EditorTools
{
    /// <summary>
    /// Tools > Hollow Warden > Build Menus
    /// 1. Creates (or fills in) the MenuArt asset from the menu art and sounds.
    /// 2. Title.unity: adds the title screen and an EventSystem, black camera background.
    /// 3. Arena.unity: adds the pause menu, death screen, victory screen and boss health bar, and turns off the Knight's sandbox respawn there.
    /// 4. Build order: Title first, then Arena.
    /// Running it again replaces only the "Title Menu" and "Game Menus" objects.
    /// </summary>
    public static class MenuSetupTool
    {
        const string Root = "Assets/_Project/PlayerSide/GameFlow/";
        const string ArtDir = Root + "Art/";
        const string AudioDir = Root + "Audio/";
        const string ArtAssetPath = Root + "MenuArt.asset";
        const string TitlePath = "Assets/_Project/Scenes/Title.unity";
        const string ArenaPath = "Assets/_Project/Scenes/Arena.unity";

        [UnityEditor.MenuItem("Tools/Hollow Warden/Build Menus")]
        public static void Build()
        {
            if (!EditorUtility.DisplayDialog("Build Menus",
                    "This opens Title.unity and Arena.unity, adds the menus to them and saves both scenes.\n\n" +
                    "Nothing else in the arena is changed.", "Build", "Cancel"))
                return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var art = GetOrCreateArt();
            SetUpTitle(art);
            SetUpArena(art);
            SetBuildOrder();
            EditorSceneManager.OpenScene(TitlePath, OpenSceneMode.Single);
            Debug.Log("Menus built. Title.unity is open: press Play to start from the title screen.");
        }

        // ---------- Art asset ----------

        static MenuArt GetOrCreateArt()
        {
            var art = AssetDatabase.LoadAssetAtPath<MenuArt>(ArtAssetPath);
            if (art == null)
            {
                art = ScriptableObject.CreateInstance<MenuArt>();
                AssetDatabase.CreateAsset(art, ArtAssetPath);
            }

            // Only fills empty fields, so your own changes to the asset are kept.
            if (art.titleBackground == null) art.titleBackground = LoadSprite("title_bg");
            if (art.titleFleurTop == null) art.titleFleurTop = LoadSprite("title_fleur_top");
            if (art.titleFleurBottom == null) art.titleFleurBottom = LoadSprite("title_fleur_bottom");
            if (art.menuFleurTop == null) art.menuFleurTop = LoadSprite("pause_fleur_top");
            if (art.menuFleurBottom == null) art.menuFleurBottom = LoadSprite("menu_fleur_bottom");
            if (art.divider == null) art.divider = LoadSprite("divider");
            if (art.sliderHandle == null) art.sliderHandle = LoadSprite("slider_handle");
            if (art.deathBackground == null) art.deathBackground = LoadSprite("death_bg");
            if (art.pointerFrames == null || art.pointerFrames.Length == 0 || art.pointerFrames[0] == null)
            {
                var frames = new List<Sprite>();
                for (int i = 0; i <= 10; i++)
                {
                    var s = LoadSprite($"pointer_{i:00}");
                    if (s != null) frames.Add(s);
                }
                art.pointerFrames = frames.ToArray();
            }
            if (art.titleMusic == null) art.titleMusic = LoadClip("title_theme");
            if (art.selectSound == null) art.selectSound = LoadClip("menu_click");
            if (art.confirmSound == null) art.confirmSound = LoadClip("menu_click");

            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();
            return art;
        }

        static Sprite LoadSprite(string name)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + name + ".png");
            if (s == null) Debug.LogWarning($"Menu art not found: {ArtDir}{name}.png");
            return s;
        }

        static AudioClip LoadClip(string name)
        {
            var c = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + name + ".wav");
            if (c == null) Debug.LogWarning($"Menu sound not found: {AudioDir}{name}.wav");
            return c;
        }

        // ---------- Scenes ----------

        static void SetUpTitle(MenuArt art)
        {
            var scene = EditorSceneManager.OpenScene(TitlePath, OpenSceneMode.Single);
            var go = ReplaceRoot("Title Menu");
            AssignArt(go.AddComponent<TitleMenu>(), art);
            EnsureEventSystem();

            var cam = Object.FindAnyObjectByType<Camera>();
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static void SetUpArena(MenuArt art)
        {
            var scene = EditorSceneManager.OpenScene(ArenaPath, OpenSceneMode.Single);
            var go = ReplaceRoot("Game Menus");
            AssignArt(go.AddComponent<PauseMenu>(), art);
            AssignArt(go.AddComponent<DeathScreen>(), art);
            AssignArt(go.AddComponent<VictoryScreen>(), art);
            AssignArt(go.AddComponent<BossHealthBar>(), art);
            EnsureEventSystem();

            // The death screen handles dying here, so the Knight must not respawn by himself.
            // This is a scene-only override: the Player prefab (and the sandbox) keep respawning.
            foreach (var health in Object.FindObjectsByType<PlayerHealth>())
            {
                var so = new SerializedObject(health);
                so.FindProperty("respawnAfterDeath").boolValue = false;
                so.ApplyModifiedProperties();
                PrefabUtility.RecordPrefabInstancePropertyModifications(health);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        static GameObject ReplaceRoot(string name)
        {
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                if (root.name == name) Object.DestroyImmediate(root);
            return new GameObject(name);
        }

        static void AssignArt(Object component, MenuArt art)
        {
            var so = new SerializedObject(component);
            so.FindProperty("art").objectReferenceValue = art;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        static void SetBuildOrder()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != TitlePath && s.path != ArenaPath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(TitlePath, true));
            scenes.Insert(1, new EditorBuildSettingsScene(ArenaPath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
