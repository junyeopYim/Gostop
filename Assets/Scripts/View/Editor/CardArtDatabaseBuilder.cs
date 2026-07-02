using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hwatu.View.Editor
{
    /// <summary>Assets/Art/Cards를 스캔해 CardArtDatabase를 다시 채우는 에디터 메뉴.</summary>
    public static class CardArtDatabaseBuilder
    {
        private const string BaseDir = "Assets/Art/Cards/Base";
        private const string OverlayDir = "Assets/Art/Cards/Overlays";
        private const string ResourcesDir = "Assets/Resources";
        private const string AssetPath = ResourcesDir + "/CardArtDatabase.asset";

        [MenuItem("Tools/Hwatu/Rebuild Card Art Database")]
        public static void Rebuild()
        {
            var db = AssetDatabase.LoadAssetAtPath<CardArtDatabase>(AssetPath);
            if (db == null)
            {
                if (!AssetDatabase.IsValidFolder(ResourcesDir))
                    AssetDatabase.CreateFolder("Assets", "Resources");
                db = ScriptableObject.CreateInstance<CardArtDatabase>();
                AssetDatabase.CreateAsset(db, AssetPath);
            }

            db.BaseEntries = CollectSprites(BaseDir);       // 파일명 = 카드 id
            db.OverlayEntries = CollectSprites(OverlayDir); // 파일명 = 오버레이 이름 (frame_*, badge_*)
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CardArtDatabase] base {db.BaseEntries.Count}장, overlay {db.OverlayEntries.Count}장 → {AssetPath}");
        }

        private static List<CardArtDatabase.Entry> CollectSprites(string dir)
        {
            var entries = new List<CardArtDatabase.Entry>();
            if (!AssetDatabase.IsValidFolder(dir)) return entries;

            foreach (var guid in AssetDatabase.FindAssets("t:Sprite", new[] { dir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null) continue;
                entries.Add(new CardArtDatabase.Entry
                {
                    Key = Path.GetFileNameWithoutExtension(path),
                    Sprite = sprite,
                });
            }
            entries.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return entries;
        }
    }
}
