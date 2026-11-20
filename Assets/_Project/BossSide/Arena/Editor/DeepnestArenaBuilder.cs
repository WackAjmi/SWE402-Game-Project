using Hollow.Core;
using Hollow.BossSide.Arena;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Hollow.BossSide.Arena.EditorTools
{
    /// <summary>
    /// Tools > Hollow Warden > Build Deepnest Arena
    /// Builds the whole encounter space into the open scene in one click:
    /// entrance tunnel, flat-floored boss room, exit tunnel, collision, gates, trigger,
    /// camera limits, Deepnest art in parallax layers and 2D lights.
    /// Running it again replaces the previous "Arena" object.
    ///
    /// Layout (world units, floor at y = 0):
    ///   entrance tunnel  x -32..0   height 6
    ///   boss room        x   0..46  height 18
    ///   exit tunnel      x  46..58  height 6
    /// </summary>
    public static class DeepnestArenaBuilder
    {
        const string ArtPath = "Assets/_Project/BossSide/Arena/Art/";
        const string AudioPath = "Assets/_Project/BossSide/Arena/Audio/";
        const string PlayerPrefab = "Assets/_Project/PlayerSide/Player/Player.prefab";
        const string CameraRigPrefab = "Assets/_Project/PlayerSide/Camera/CameraRig.prefab";
        const string HudPrefab = "Assets/_Project/PlayerSide/UI/HUD.prefab";

        static readonly Vector2 ArenaCentre = new Vector2(23f, 9f);
        static readonly Vector3 PlayerSpawn = new Vector3(-28f, 0f, 0f);
        static readonly Color SolidColour = new Color(0.02f, 0.02f, 0.035f);

        static Transform root;
        static int groundLayer;

        [MenuItem("Tools/Hollow Warden/Build Deepnest Arena")]
        public static void Build()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!EditorUtility.DisplayDialog("Build Deepnest Arena",
                    $"Build the Deepnest arena into the open scene '{scene.name}'?\n\nAn existing 'Arena' object will be replaced.",
                    "Build", "Cancel"))
                return;

            groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer < 0) { Debug.LogError("No 'Ground' layer. Add it in Tags and Layers first."); return; }

            var old = GameObject.Find("Arena");
            if (old != null) Undo.DestroyObjectImmediate(old);

            root = new GameObject("Arena").transform;
            Undo.RegisterCreatedObjectUndo(root.gameObject, "Build Deepnest Arena");

            BuildSolids();
            BuildEdges();
            BuildBackground();
            BuildDecor();
            BuildForeground();
            BuildGates();
            BuildTrigger();
            var bounds = BuildCameraBounds();
            BuildLights();
            BuildMusic(root);
            SetUpSceneObjects(bounds);

            EditorSceneManager.MarkSceneDirty(scene);
            Selection.activeGameObject = root.gameObject;
            Debug.Log("Deepnest arena built. Press Play: walk right from the tunnel to start the encounter.");
        }

        // ---------- Collision and solid rock ----------

        static void BuildSolids()
        {
            var solids = Group("Solids");
            Solid(solids, "Ground", -40f, -10f, 64f, 0f);
            Solid(solids, "Entrance Ceiling", -40f, 6f, 0f, 30f);
            Solid(solids, "Arena Ceiling", 0f, 18f, 46f, 30f);
            Solid(solids, "Exit Ceiling", 46f, 6f, 64f, 30f);
            Solid(solids, "Far Left Wall", -40f, 0f, -32f, 6f);
            Solid(solids, "Far Right Wall", 58f, 0f, 64f, 6f);
        }

        static void Solid(Transform parent, string name, float x0, float y0, float x1, float y1)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = groundLayer;
            go.transform.position = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, 0f);
            go.AddComponent<BoxCollider2D>().size = new Vector2(x1 - x0, y1 - y0);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Load("deepnest_black_solid");
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(x1 - x0, y1 - y0);
            sr.color = SolidColour;
            sr.sortingLayerName = "Midground";
            sr.sortingOrder = 10;
        }

        // ---------- Rock edges along floor, ceilings and walls ----------

        static void BuildEdges()
        {
            var edges = Group("Edges");

            // Floor: dripping ledges whose top is the walkable surface.
            bool flip = false;
            for (float x = -31f; x <= 58f; x += 7.2f, flip = !flip)
                PlaceTop(edges, "bone_deep_0000_Layer-124", x, 0.35f, "Midground", 12, 1f, flip);

            // Boss room ceiling: drips hanging down.
            int i = 0;
            for (float x = 1.5f; x <= 45f; x += 7f, i++)
                PlaceTop(edges, i % 2 == 0 ? "bone_deep_0001_Layer-125" : "bone_deep_0002_Layer-126", x, 18.6f, "Midground", 12, 1f, i % 3 == 0);

            // Tunnel ceilings: smaller drips so the Knight's jump still has room.
            for (float x = -31f; x <= -1f; x += 4.4f)
                PlaceTop(edges, "bone_deep_0002_Layer-126", x, 6.4f, "Midground", 12, 0.55f, x % 2 > 1);
            for (float x = 47.5f; x <= 57.5f; x += 4.4f)
                PlaceTop(edges, "bone_deep_0002_Layer-126", x, 6.4f, "Midground", 12, 0.55f, true);

            // Stalactite clusters in the boss room.
            PlaceTop(edges, "deepnest_stalactites", 12f, 18.4f, "Midground", 13);
            PlaceTop(edges, "deepnest_stalactites", 34f, 18.4f, "Midground", 13, 1f, true);

            // Wall faces above the tunnel openings.
            Place(edges, "bone_deep_0008_Layer-132", 0.4f, 9.8f, "Midground", 11);
            Place(edges, "bone_deep_0008_Layer-132", 0.4f, 15.6f, "Midground", 11, 1f, true);
            Place(edges, "bone_deep_0008_Layer-132", 45.6f, 9.8f, "Midground", 11, 1f, true);
            Place(edges, "bone_deep_0008_Layer-132", 45.6f, 15.6f, "Midground", 11);

            // Tunnel ends.
            PlaceBottom(edges, "bone_deep_0010_Layer-134", -32.6f, -0.2f, "Midground", 11);
            PlaceBottom(edges, "bone_deep_0010_Layer-134", 58.6f, -0.2f, "Midground", 11, 1f, true);
        }

        // ---------- Background (parallax, darker the further away) ----------

        static void BuildBackground()
        {
            var bg = Group("Background");

            var fill = Place(bg, "deepnest_black_solid", 12f, 10f, "Background", -100);
            fill.drawMode = SpriteDrawMode.Sliced;
            fill.size = new Vector2(140f, 60f);
            fill.color = new Color(0.09f, 0.1f, 0.15f);

            var far = ParallaxGroup(bg, "Far (0.6)", 0.6f);
            Color farTint = new Color(0.3f, 0.33f, 0.45f);
            PlaceBottom(far, "deepnest_spider_pieces_0066_a", 12f, -0.5f, "Background", -60, 1f, false, farTint);
            PlaceBottom(far, "deepnest_spider_pieces_0066_a", 36f, -0.5f, "Background", -60, 1f, true, farTint);
            Place(far, "deep_spider_back", 23f, 11f, "Background", -62, 1.5f, false, new Color(0.32f, 0.36f, 0.5f));
            Color shaft = new Color(0.6f, 0.65f, 0.8f, 0.25f);
            Place(far, "deepnest_spider_pieces_0047_a", 6f, 9f, "Background", -58, 1f, false, shaft);
            Place(far, "deepnest_spider_pieces_0044_a", 40f, 9f, "Background", -58, 1f, true, shaft);
            PlaceBottom(far, "enoki_0009_bg6", -20f, 0f, "Background", -57, 1f, false, new Color(0.45f, 0.48f, 0.6f));

            var mid = ParallaxGroup(bg, "Mid (0.3)", 0.3f);
            Color midTint = new Color(0.45f, 0.5f, 0.62f);
            PlaceBottom(mid, "deepnest_cornifer_mound", 8f, -0.2f, "Background", -40, 1f, false, midTint);
            PlaceBottom(mid, "deepnest_cornifer_mound", 38f, -0.2f, "Background", -40, 1f, true, midTint);
            PlaceBottom(mid, "deepnest_Mid_eggs_BGs", 24f, 0f, "Background", -42, 1f, false, midTint);
            Place(mid, "bone_deep_0012_Layer-136", 15f, 10f, "Background", -44, 1f, false, midTint * 0.9f);
            Place(mid, "bone_deep_0038_Layer-118", 31f, 12.5f, "Background", -44, 1f, false, midTint * 0.9f);
            PlaceBottom(mid, "enoki_0004_bg1", -12f, 0f, "Background", -41, 1f, false, new Color(0.5f, 0.53f, 0.65f));
            Color fog = new Color(0.7f, 0.75f, 0.9f, 0.35f);
            Place(mid, "deepnest_spider_pieces_0013_f", 12f, 0.8f, "Background", -20, 7f, false, fog);
            Place(mid, "deepnest_spider_pieces_0013_f", 34f, 1f, "Background", -20, 7f, true, fog);
        }

        // ---------- Webs, lamps and floor props (part of the world) ----------

        static void BuildDecor()
        {
            var decor = Group("Decor");
            Color web = new Color(0.85f, 0.9f, 1f, 0.85f);
            PlaceTop(decor, "deep_nest_webs_0000_Layer-1", 3.3f, 18.3f, "Midground", 3, 1f, false, web);
            PlaceTop(decor, "deep_nest_webs_0000_Layer-1", 42.7f, 18.3f, "Midground", 3, 1f, true, web);
            PlaceTop(decor, "deep_nest_webs_0004_Layer-5", -3f, 6.3f, "Midground", 3, 0.7f, false, web);
            Color hanging = new Color(0.85f, 0.9f, 1f, 0.7f);
            PlaceTop(decor, "cut_webs_0000_1", 19f, 18.5f, "Midground", 2, 1f, false, hanging);
            PlaceTop(decor, "cut_webs_0000_1", 28.5f, 18.5f, "Midground", 2, 1f, true, hanging);

            Lamp(decor, 14f, false);
            Lamp(decor, 33f, true);

            Mushroom(decor, "mush_set_040000", 3f, false);
            Mushroom(decor, "mush_set_040000", 43f, true);
            Mushroom(decor, "mush_set_010000", 19f, false);
            Mushroom(decor, "mush_set_010000", -14f, true);

        }

        static void Lamp(Transform parent, float x, bool flip)
        {
            var lamp = PlaceTop(parent, "d_break_0044_deep_lamp5", x, 18.3f, "Midground", 4, 1f, flip);
            Vector3 globePos = lamp.transform.position + new Vector3(0f, -0.6f, 0f);
            var globe = Place(parent, "d_break_0044_deep_lamp5_globe", globePos.x, globePos.y, "Midground", 5);
            PointLight(globe.transform, "Lamp Light", 7f, 1.3f, new Color(0.8f, 0.88f, 1f));
        }

        static void Mushroom(Transform parent, string art, float x, bool flip)
        {
            var m = PlaceBottom(parent, art, x, 0f, "Midground", 2, 1f, flip);
            PointLight(m.transform, "Glow", 3.5f, 0.7f, new Color(0.75f, 0.85f, 1f));
        }

        // ---------- Foreground silhouettes (move faster than the world) ----------

        static void BuildForeground()
        {
            var fg = ParallaxGroup(Group("Foreground"), "Near (-0.15)", -0.15f);
            Color sil = new Color(0.02f, 0.02f, 0.03f);
            PlaceBottom(fg, "deep_branches_0001_fg3", -1.2f, -1f, "Foreground", 0, 1f, false, sil);
            PlaceBottom(fg, "deep_branches_0000_fg4", 47.2f, -1f, "Foreground", 0, 1f, true, sil);
            PlaceBottom(fg, "deepnest_spider_pieces_0025_a", -26f, -1.5f, "Foreground", 0, 1f, false, sil);
            PlaceBottom(fg, "deepnest_spider_pieces_0038_a", -15f, -1f, "Foreground", 0, 1f, true, sil);
            PlaceBottom(fg, "deep_branch_FG", 55f, -1f, "Foreground", 0, 1f, false, sil);
        }

        // ---------- Gates and the encounter trigger ----------

        static void BuildGates()
        {
            var gates = Group("Gates");
            Gate(gates, "Gate Left", -0.5f, false);
            Gate(gates, "Gate Right", 46.5f, true);
        }

        static void Gate(Transform parent, string name, float x, bool flip)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, 0f, 0f);
            go.layer = groundLayer;
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1f, 6f);
            box.offset = new Vector2(0f, 3f);

            // arena_gate has its pivot at the bottom (100 px per unit, about 7 units tall).
            var visual = Place(go.transform, "arena_gate", x, 0f, "Midground", 8, 1f, flip);
            visual.name = "Visual";

            var gate = go.AddComponent<ArenaGate>();
            var so = new SerializedObject(gate);
            so.FindProperty("visual").objectReferenceValue = visual.transform;
            so.FindProperty("closeSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath + "gate_slam.wav");
            so.FindProperty("openSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(AudioPath + "gate_open.wav");
            so.FindProperty("autoOpenAfter").floatValue = 8f;   // testing only until the boss exists
            so.ApplyModifiedPropertiesWithoutUndo();

            AddTinkSurface(visual.transform);
        }

        const string TinkSoundPath = "Assets/_Project/BossSide/Arena/Audio/gate_tink.wav";
        const string SparkPath = "Assets/_Project/PlayerSide/Player/Art/Effects/HitSpark/HitSpark.png";

        /// Makes the gates in the open scene pogoable and tink when hit, without rebuilding the arena.
        [MenuItem("Tools/Hollow Warden/Make Gates Tinkable")]
        public static void MakeGatesTinkable()
        {
            int count = 0;
            foreach (var gate in Object.FindObjectsByType<ArenaGate>())
            {
                var visual = new SerializedObject(gate).FindProperty("visual").objectReferenceValue as Transform;
                if (visual == null) { Debug.LogWarning($"{gate.name} has no Visual assigned.", gate); continue; }
                AddTinkSurface(visual);
                count++;
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"Tink surfaces added to {count} gate(s).");
        }

        static void AddTinkSurface(Transform visual)
        {
            var old = visual.Find("Tink Surface");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

            int hurtLayer = LayerMask.NameToLayer("BossHurtbox");
            if (hurtLayer < 0) { Debug.LogError("No 'BossHurtbox' layer."); return; }

            var go = new GameObject("Tink Surface");
            Undo.RegisterCreatedObjectUndo(go, "Add Tink Surface");
            go.transform.SetParent(visual, false);
            go.layer = hurtLayer;

            // The gate sprite's pivot is at its bottom and it is about 7 units tall.
            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(1.2f, 6.8f);
            box.offset = new Vector2(0f, 3.4f);

            go.AddComponent<Hurtbox>().Pogoable = true;   // no IDamageable above it: pogo-only, no soul

            var tink = go.AddComponent<NailTink>();
            var so = new SerializedObject(tink);
            so.FindProperty("tinkSound").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(TinkSoundPath);
            so.FindProperty("sparkSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(SparkPath);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void BuildTrigger()
        {
            var go = new GameObject("Encounter Trigger");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(3.5f, 0f, 0f);
            go.AddComponent<ArenaTrigger>();
        }

        // ---------- Music ----------

        const string MusicPath = "Assets/_Project/BossSide/Arena/Audio/Music/";

        /// Adds (or replaces) only the music object, without touching the rest of the arena.
        [MenuItem("Tools/Hollow Warden/Add Arena Music")]
        public static void AddMusic()
        {
            var arena = GameObject.Find("Arena");
            BuildMusic(arena != null ? arena.transform : null);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        static void BuildMusic(Transform parent)
        {
            var old = GameObject.Find("Arena Music");
            if (old != null) Undo.DestroyObjectImmediate(old);

            var go = new GameObject("Arena Music");
            Undo.RegisterCreatedObjectUndo(go, "Add Arena Music");
            if (parent != null) go.transform.SetParent(parent, false);
            var music = go.AddComponent<ArenaMusic>();
            var so = new SerializedObject(music);
            so.FindProperty("areaTheme").objectReferenceValue = Clip("deepnest_theme");
            so.FindProperty("ambience").objectReferenceValue = Clip("deepnest_ambience");
            so.FindProperty("bossMusic").objectReferenceValue = Clip("boss_battle");
            so.FindProperty("victorySting").objectReferenceValue = Clip("boss_defeat");
            so.ApplyModifiedPropertiesWithoutUndo();
            Selection.activeGameObject = go;
        }

        static AudioClip Clip(string name)
        {
            var c = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath + name + ".wav");
            if (c == null) Debug.LogWarning($"Music not found: {MusicPath}{name}.wav");
            return c;
        }

        // ---------- Camera limits ----------

        static PolygonCollider2D BuildCameraBounds()
        {
            var go = new GameObject("Camera Bounds");
            go.transform.SetParent(root, false);
            go.layer = LayerMask.NameToLayer("Ignore Raycast");
            var poly = go.AddComponent<PolygonCollider2D>();
            poly.isTrigger = true;
            poly.points = new[]
            {
                // Every part must be taller than the camera view (15 units at ortho size 7.5),
                // or the confiner shrinks that part away and the camera can't follow into it.
                new Vector2(-32f, -3f), new Vector2(58f, -3f), new Vector2(58f, 14f), new Vector2(46f, 14f),
                new Vector2(46f, 20f), new Vector2(0f, 20f), new Vector2(0f, 14f), new Vector2(-32f, 14f),
            };
            return poly;
        }

        // ---------- Lights ----------

        static void BuildLights()
        {
            Light2D global = null;
            foreach (var l in Object.FindObjectsByType<Light2D>())
                if (l.lightType == Light2D.LightType.Global) { global = l; break; }

            if (global == null)
            {
                var go = new GameObject("Global Light 2D");
                go.transform.SetParent(root, false);
                global = go.AddComponent<Light2D>();
                global.lightType = Light2D.LightType.Global;
            }
            Undo.RecordObject(global, "Arena light");
            // The template's light only lights the Default sorting layer; the arena art is on the others.
            TargetAllSortingLayers(global);
            global.intensity = 0.8f;
            global.color = new Color(0.72f, 0.77f, 0.95f);
        }

        static void PointLight(Transform parent, string name, float radius, float intensity, Color colour)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var l = go.AddComponent<Light2D>();
            l.lightType = Light2D.LightType.Point;
            l.pointLightOuterRadius = radius;
            l.pointLightInnerRadius = radius * 0.1f;
            l.intensity = intensity;
            l.color = colour;
            TargetAllSortingLayers(l);
        }

        static void TargetAllSortingLayers(Light2D light)
        {
            var so = new SerializedObject(light);
            var prop = so.FindProperty("m_ApplyToSortingLayers");
            if (prop == null) return;
            var layers = SortingLayer.layers;
            prop.arraySize = layers.Length;
            for (int i = 0; i < layers.Length; i++) prop.GetArrayElementAtIndex(i).intValue = layers[i].id;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---------- Knight, camera rig and HUD ----------

        static void SetUpSceneObjects(PolygonCollider2D bounds)
        {
            if (GameObject.Find("Player") == null)
                Spawn(PlayerPrefab, PlayerSpawn);

            var rig = GameObject.Find("CameraRig");
            if (rig == null)
            {
                rig = Spawn(CameraRigPrefab, Vector3.zero);
                // The template's own camera would fight the rig's camera.
                foreach (var cam in Object.FindObjectsByType<Camera>())
                    if (rig != null && !cam.transform.IsChildOf(rig.transform))
                    {
                        Undo.RecordObject(cam.gameObject, "Disable template camera");
                        cam.gameObject.SetActive(false);
                    }
            }

            if (GameObject.Find("HUD") == null)
                Spawn(HudPrefab, Vector3.zero);

            var vcam = Object.FindAnyObjectByType<CinemachineCamera>();
            if (vcam != null)
            {
                var confiner = vcam.GetComponent<CinemachineConfiner2D>();
                if (confiner == null) confiner = Undo.AddComponent<CinemachineConfiner2D>(vcam.gameObject);
                Undo.RecordObject(confiner, "Arena camera bounds");
                confiner.BoundingShape2D = bounds;
            }
            else Debug.LogWarning("No CinemachineCamera found: add the CameraRig and run the builder again for camera limits.");
        }

        static GameObject Spawn(string path, Vector3 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { Debug.LogWarning($"Prefab not found: {path}"); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = position;
            Undo.RegisterCreatedObjectUndo(go, "Spawn " + prefab.name);
            return go;
        }

        // ---------- Placement helpers ----------

        static Transform Group(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            return go.transform;
        }

        static Transform ParallaxGroup(Transform parent, string name, float factor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<ParallaxLayer>().Setup(factor, ArenaCentre);
            return go.transform;
        }

        static Sprite Load(string art)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath + art + ".png");
            if (s == null) Debug.LogWarning($"Sprite not found: {ArtPath}{art}.png");
            return s;
        }

        static SpriteRenderer Place(Transform parent, string art, float x, float y, string sortingLayer, int order,
                                    float scale = 1f, bool flipX = false, Color? tint = null)
        {
            var go = new GameObject(art);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, y, 0f);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Load(art);
            sr.flipX = flipX;
            sr.sortingLayerName = sortingLayer;
            sr.sortingOrder = order;
            if (tint.HasValue) sr.color = tint.Value;
            return sr;
        }

        static float Height(string art, float scale)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath + art + ".png");
            return s != null ? s.bounds.size.y * scale : 0f;
        }

        // Places a sprite so that its top edge sits at topY.
        static SpriteRenderer PlaceTop(Transform parent, string art, float x, float topY, string sortingLayer, int order,
                                       float scale = 1f, bool flipX = false, Color? tint = null)
            => Place(parent, art, x, topY - Height(art, scale) * 0.5f, sortingLayer, order, scale, flipX, tint);

        // Places a sprite so that its bottom edge sits at bottomY.
        static SpriteRenderer PlaceBottom(Transform parent, string art, float x, float bottomY, string sortingLayer, int order,
                                          float scale = 1f, bool flipX = false, Color? tint = null)
            => Place(parent, art, x, bottomY + Height(art, scale) * 0.5f, sortingLayer, order, scale, flipX, tint);
    }
}
