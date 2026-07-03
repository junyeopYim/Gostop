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
        private const string StampDir = "Assets/Art/Cards/Stamps";
        private const string UiDir = "Assets/Art/UI";
        private const string BackgroundDir = "Assets/Art/Backgrounds";
        private const string ElementDir = "Assets/Art/Elements";
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
            db.StampEntries = CollectSprites(StampDir);     // 파일명 = 낙관 이름 (seal_red, seal_gold)
            db.UiEntries = CollectSprites(UiDir);            // 파일명 = UI atlas id
            db.BackgroundEntries = CollectSprites(BackgroundDir);
            db.ElementEntries = CollectSprites(ElementDir);
            db.BackSprite = TakeBackSprite(db.BaseEntries); // card_back은 카드 id가 아니라 전용 슬롯
            db.ClearLookupCaches();
            EditorUtility.SetDirty(db);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CardArtDatabase] base {db.BaseEntries.Count}장, overlay {db.OverlayEntries.Count}장, stamp {db.StampEntries.Count}장, "
                      + $"ui {db.UiEntries.Count}장, background {db.BackgroundEntries.Count}장, element {db.ElementEntries.Count}장, "
                      + $"back {(db.BackSprite != null ? "있음" : "없음")} → {AssetPath}");
        }

        // Base 폴더에 함께 복사되는 card_back.png을 카드 id 목록에서 빼서 전용 슬롯에 담는다
        private static Sprite TakeBackSprite(List<CardArtDatabase.Entry> entries)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Key != "card_back") continue;
                var sprite = entries[i].Sprite;
                entries.RemoveAt(i);
                return sprite;
            }
            return null;
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
