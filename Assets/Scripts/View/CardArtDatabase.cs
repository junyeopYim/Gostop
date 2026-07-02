using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hwatu.View
{
    /// <summary>
    /// 카드 id → 베이스 스프라이트, 오버레이 이름 → 스프라이트 매핑.
    /// "Tools/Hwatu/Rebuild Card Art Database" 메뉴가 Assets/Art/Cards를 스캔해 채운다.
    /// 에셋은 Resources/CardArtDatabase.asset에 두어 런타임에 로드한다.
    /// </summary>
    public sealed class CardArtDatabase : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string Key;
            public Sprite Sprite;
        }

        public List<Entry> BaseEntries = new List<Entry>();
        public List<Entry> OverlayEntries = new List<Entry>();

        private Dictionary<string, Sprite> _baseLookup;
        private Dictionary<string, Sprite> _overlayLookup;

        private static CardArtDatabase _instance;
        private static bool _searched;

        /// <summary>Resources에서 로드. 없으면 null → 뷰는 색상 사각형으로 폴백.</summary>
        public static CardArtDatabase Instance
        {
            get
            {
                if (_instance == null && !_searched)
                {
                    _instance = Resources.Load<CardArtDatabase>("CardArtDatabase");
                    _searched = true;
                }
                return _instance;
            }
        }

        public bool TryGetBase(string id, out Sprite sprite)
            => TryGet(ref _baseLookup, BaseEntries, id, out sprite);

        public bool TryGetOverlay(string overlayName, out Sprite sprite)
            => TryGet(ref _overlayLookup, OverlayEntries, overlayName, out sprite);

        public void ClearLookupCaches()
        {
            _baseLookup = null;
            _overlayLookup = null;
        }

        private static bool TryGet(ref Dictionary<string, Sprite> cache, List<Entry> entries,
                                   string key, out Sprite sprite)
        {
            if (cache == null)
            {
                cache = new Dictionary<string, Sprite>(entries.Count);
                foreach (var entry in entries)
                {
                    if (!string.IsNullOrEmpty(entry.Key) && entry.Sprite != null)
                        cache[entry.Key] = entry.Sprite;
                }
            }
            sprite = null;
            return key != null && cache.TryGetValue(key, out sprite);
        }
    }
}
