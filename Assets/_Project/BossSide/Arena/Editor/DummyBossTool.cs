using Hollow.BossSide.Testing;
using Hollow.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hollow.BossSide.Arena.EditorTools
{
    /// <summary>
    /// Tools > Hollow Warden > Add Dummy Boss
    /// Puts a 10-health placeholder boss in the middle of the arena and turns off the gates'
    /// test auto-open (the gates now open when the dummy dies). Running it again replaces it.
    /// </summary>
    public static class DummyBossTool
    {
        const string SquarePath = "Assets/_Project/BossSide/Testing/white_square.png";
        static readonly Vector3 Position = new Vector3(23f, 0f, 0f);

        [MenuItem("Tools/Hollow Warden/Add Dummy Boss")]
        public static void Add()
        {
            var square = AssetDatabase.LoadAssetAtPath<Sprite>(SquarePath);
            if (square == null) { Debug.LogError($"Missing {SquarePath}"); return; }
            int hurtLayer = LayerMask.NameToLayer("BossHurtbox");
            int hitLayer = LayerMask.NameToLayer("BossHitbox");
            if (hurtLayer < 0 || hitLayer < 0) { Debug.LogError("Needs the BossHurtbox and BossHitbox layers."); return; }

            var old = GameObject.Find("Dummy Boss");
            if (old != null) Undo.DestroyObjectImmediate(old);

            var root = new GameObject("Dummy Boss");
            Undo.RegisterCreatedObjectUndo(root, "Add Dummy Boss");
            root.transform.position = Position;

            // Body: a plain pillar, 2.2 wide and 3.6 tall, standing on the floor.
            var body = Square(root.transform, "Body", square, new Vector2(0f, 1.8f), new Vector2(2.2f, 3.6f), "Characters", -1);

            var hurt = Box(root.transform, "Hurtbox", hurtLayer, new Vector2(2.2f, 3.6f), new Vector2(0f, 1.8f));
            hurt.AddComponent<Hurtbox>().Pogoable = true;

            var contact = Box(root.transform, "Contact Hitbox", hitLayer, new Vector2(1.8f, 3.3f), new Vector2(0f, 1.65f));
            var contactHitbox = contact.AddComponent<Hitbox>();
            contactHitbox.Team = Team.Boss;
            contactHitbox.Damage = 1;

            var slam = Box(root.transform, "Slam Hitbox", hitLayer, new Vector2(9f, 1f), new Vector2(0f, 0.5f));
            var slamHitbox = slam.AddComponent<Hitbox>();
            slamHitbox.Team = Team.Boss;
            slamHitbox.Damage = 1;

            var flash = Square(root.transform, "Slam Flash", square, new Vector2(0f, 0.15f), new Vector2(9f, 0.3f), "VFX", 0);
            flash.color = new Color(1f, 0.9f, 0.5f, 0.7f);

            // Health bar above it; the fill shrinks from the right.
            var bar = new GameObject("Health Bar").transform;
            bar.SetParent(root.transform, false);
            bar.localPosition = new Vector3(0f, 4.3f, 0f);
            Square(bar, "Back", square, Vector2.zero, new Vector2(2.4f, 0.24f), "Characters", 5).color = new Color(0f, 0f, 0f, 0.7f);
            var fillAnchor = new GameObject("Fill Anchor").transform;
            fillAnchor.SetParent(bar, false);
            fillAnchor.localPosition = new Vector3(-1.1f, 0f, 0f);
            fillAnchor.localScale = new Vector3(2.2f, 1f, 1f);
            Square(fillAnchor, "Fill", square, new Vector2(0.5f, 0f), new Vector2(1f, 0.16f), "Characters", 6).color = new Color(0.85f, 0.2f, 0.25f);

            var boss = root.AddComponent<DummyBoss>();
            var so = new SerializedObject(boss);
            so.FindProperty("body").objectReferenceValue = body;
            so.FindProperty("contactHitbox").objectReferenceValue = contactHitbox;
            so.FindProperty("slamHitbox").objectReferenceValue = slamHitbox;
            so.FindProperty("slamFlash").objectReferenceValue = flash;
            so.FindProperty("healthBarFill").objectReferenceValue = fillAnchor;
            so.ApplyModifiedPropertiesWithoutUndo();

            // The gates should now wait for the boss to die instead of reopening on a timer.
            foreach (var gate in Object.FindObjectsByType<ArenaGate>())
            {
                var g = new SerializedObject(gate);
                g.FindProperty("autoOpenAfter").floatValue = 0f;
                g.ApplyModifiedProperties();
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = root;
            Debug.Log("Dummy boss added (10 health). The gates now open only when it dies.");
        }

        static SpriteRenderer Square(Transform parent, string name, Sprite sprite, Vector2 localPos, Vector2 size,
                                     string sortingLayer, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = sortingLayer;
            sr.sortingOrder = order;
            return sr;
        }

        static GameObject Box(Transform parent, string name, int layer, Vector2 size, Vector2 offset)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.layer = layer;
            var box = go.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = size;
            box.offset = offset;
            return go;
        }
    }
}
