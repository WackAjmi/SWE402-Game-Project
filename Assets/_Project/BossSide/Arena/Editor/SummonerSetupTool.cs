using System.Collections.Generic;
using System.Linq;
using Hollow.BossSide.Boss;
using Hollow.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hollow.BossSide.Arena.EditorTools
{
    /// <summary>
    /// Tools > Hollow Warden > Add Hallowed Summoner
    /// 1. Creates (or refreshes) the BossSprites asset from Boss/Art/Frames and the flame sprite.
    /// 2. Removes the Dummy Boss and puts the Hallowed Summoner in the arena, floating over the middle.
    /// Running it again replaces the boss object; nothing else in the scene changes.
    /// </summary>
    public static class SummonerSetupTool
    {
        const string BossDir = "Assets/_Project/BossSide/Boss/";
        const string FramesDir = BossDir + "Art/Frames/";
        const string FlamePath = BossDir + "Art/boss_flame.png";
        const string SpritesAsset = BossDir + "BossSprites.asset";
        static readonly Vector3 StartPosition = new Vector3(23f, 11f, 0f);

        [MenuItem("Tools/Hollow Warden/Add Hallowed Summoner")]
        public static void Add()
        {
            int hurtLayer = LayerMask.NameToLayer("BossHurtbox");
            int hitLayer = LayerMask.NameToLayer("BossHitbox");
            if (hurtLayer < 0 || hitLayer < 0) { Debug.LogError("Needs the BossHurtbox and BossHitbox layers."); return; }

            var sprites = GetOrCreateSprites();
            if (sprites == null) return;

            foreach (var name in new[] { "Dummy Boss", "The Hallowed Summoner" })
            {
                var old = GameObject.Find(name);
                if (old != null) Undo.DestroyObjectImmediate(old);
            }

            var root = new GameObject("The Hallowed Summoner");
            Undo.RegisterCreatedObjectUndo(root, "Add Hallowed Summoner");
            root.transform.position = StartPosition;

            // Visual: sprites face right; the pivot is the tail tip (the floor during the slam).
            var visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            var body = visual.AddComponent<SpriteRenderer>();
            body.sprite = sprites.idle.frames[0];
            body.sortingLayerName = "Characters";
            body.sortingOrder = -1;
            var animator = visual.AddComponent<SpriteAnimator>();

            // Damage receiver (pogo-able) and contact damage.
            var hurt = Box(root.transform, "Hurtbox", hurtLayer, new Vector2(1.7f, 2.9f), new Vector2(0f, 2.7f));
            hurt.gameObject.AddComponent<Hurtbox>().Pogoable = true;

            var contact = Box(root.transform, "Contact Hitbox", hitLayer, new Vector2(1.3f, 2.2f), new Vector2(0f, 2.7f));
            var contactHitbox = contact.gameObject.AddComponent<Hitbox>();
            contactHitbox.Team = Team.Boss;
            contactHitbox.Damage = 1;

            // Charging flame shown at the hands during the throw wind-up.
            var glowGo = new GameObject("Charge Glow");
            glowGo.transform.SetParent(root.transform, false);
            var glow = glowGo.AddComponent<SpriteRenderer>();
            glow.sprite = sprites.flame;
            glow.sortingLayerName = "VFX";
            glow.sortingOrder = 2;
            glow.enabled = false;

            // Flame projectile template (cloned at runtime into a pool).
            var flameGo = new GameObject("Flame Template");
            flameGo.transform.SetParent(root.transform, false);
            flameGo.layer = hitLayer;
            var flameArt = new GameObject("Sprite");
            flameArt.transform.SetParent(flameGo.transform, false);
            flameArt.transform.localScale = Vector3.one * 0.3f;   // about 1.5 x 0.7 units
            var flameSr = flameArt.AddComponent<SpriteRenderer>();
            flameSr.sprite = sprites.flame;
            flameSr.sortingLayerName = "VFX";
            flameSr.sortingOrder = 1;
            var circle = flameGo.AddComponent<CircleCollider2D>();
            circle.isTrigger = true;
            circle.radius = 0.3f;
            circle.offset = new Vector2(0.35f, 0f);
            var rb = flameGo.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            var flameHitbox = flameGo.AddComponent<Hitbox>();
            flameHitbox.Team = Team.Boss;
            flameHitbox.Damage = 1;
            var flame = flameGo.AddComponent<BossFlame>();
            flameGo.SetActive(false);

            // Floor marker template for the slam's eruption spots: a small upright flame that pulses.
            var markerGo = new GameObject("Marker Template");
            markerGo.transform.SetParent(root.transform, false);
            markerGo.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            markerGo.transform.localScale = Vector3.one * 0.16f;
            var marker = markerGo.AddComponent<SpriteRenderer>();
            marker.sprite = sprites.flame;
            marker.color = new Color(0.75f, 0.9f, 1f, 0.7f);
            marker.sortingLayerName = "VFX";
            marker.sortingOrder = 0;
            markerGo.SetActive(false);

            var boss = root.AddComponent<HallowedSummoner>();
            var so = new SerializedObject(boss);
            so.FindProperty("sprites").objectReferenceValue = sprites;
            so.FindProperty("anim").objectReferenceValue = animator;
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("hurtCollider").objectReferenceValue = hurt;
            so.FindProperty("contactHitbox").objectReferenceValue = contactHitbox;
            so.FindProperty("flameTemplate").objectReferenceValue = flame;
            so.FindProperty("chargeGlow").objectReferenceValue = glow;
            so.FindProperty("markerTemplate").objectReferenceValue = marker;
            so.ApplyModifiedPropertiesWithoutUndo();

            // The gates wait for the boss to die.
            foreach (var gate in Object.FindObjectsByType<ArenaGate>())
            {
                var g = new SerializedObject(gate);
                g.FindProperty("autoOpenAfter").floatValue = 0f;
                g.ApplyModifiedProperties();
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = root;
            Debug.Log("The Hallowed Summoner is in the arena (40 HP). The Dummy Boss was removed.");
        }

        static BossSprites GetOrCreateSprites()
        {
            var frames = new Sprite[31];
            for (int i = 0; i < frames.Length; i++)
            {
                frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>($"{FramesDir}boss_{i:00}.png");
                if (frames[i] == null) { Debug.LogError($"Missing frame {FramesDir}boss_{i:00}.png"); return null; }
            }
            var flame = AssetDatabase.LoadAllAssetsAtPath(FlamePath).OfType<Sprite>().FirstOrDefault();
            if (flame == null) { Debug.LogError($"Missing flame sprite at {FlamePath}"); return null; }

            var asset = AssetDatabase.LoadAssetAtPath<BossSprites>(SpritesAsset);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<BossSprites>();
                AssetDatabase.CreateAsset(asset, SpritesAsset);
            }

            // Frame numbers as agreed: idle 0-5, notice 6-10, throw 11-16, rise 17-20, charge 21-26, slam 27-30.
            asset.idle.frames = Range(frames, 0, 5);
            asset.notice.frames = Range(frames, 6, 10);
            asset.throwCharge.frames = Range(frames, 11, 12);
            asset.throwRelease.frames = Range(frames, 13, 15);
            asset.throwRecover.frames = Range(frames, 16, 16);
            asset.rise.frames = Range(frames, 17, 20);
            asset.slamCharge.frames = Range(frames, 21, 26);
            asset.slam.frames = Range(frames, 27, 30);
            asset.flame = flame;

            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        static Sprite[] Range(Sprite[] all, int first, int last)
        {
            var list = new List<Sprite>();
            for (int i = first; i <= last; i++) list.Add(all[i]);
            return list.ToArray();
        }

        static BoxCollider2D Box(Transform parent, string name, int layer, Vector2 size, Vector2 offset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = layer;
            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = size;
            box.offset = offset;
            return box;
        }
    }
}
