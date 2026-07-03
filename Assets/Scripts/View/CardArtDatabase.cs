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

        [Serializable]
        public struct TextureEntry
        {
            public string Key;
            public Texture2D Texture;
        }

        public List<Entry> BaseEntries = new List<Entry>();
        public List<Entry> OverlayEntries = new List<Entry>();
        public List<Entry> StampEntries = new List<Entry>();
        public List<Entry> UiEntries = new List<Entry>();
        public List<Entry> BackgroundEntries = new List<Entry>();
        public List<Entry> ElementEntries = new List<Entry>();

        /// <summary>요소 id → 잉크순서 마스크 텍스처 (단채널 R8). PaintInEffect.PlayDrawn이 소비.</summary>
        public List<TextureEntry> ElementInkMaskEntries = new List<TextureEntry>();

        /// <summary>카드 뒷면 스프라이트 (card_back.png). 없으면 뷰가 기존 단색 표현으로 폴백.</summary>
        public Sprite BackSprite;

        private Dictionary<string, Sprite> _baseLookup;
        private Dictionary<string, Sprite> _overlayLookup;
        private Dictionary<string, Sprite> _stampLookup;
        private Dictionary<string, Sprite> _uiLookup;
        private Dictionary<string, Sprite> _backgroundLookup;
        private Dictionary<string, Sprite> _elementLookup;
        private Dictionary<string, Texture2D> _elementInkMaskLookup;

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

        public bool TryGetStamp(string stampName, out Sprite sprite)
            => TryGet(ref _stampLookup, StampEntries, stampName, out sprite);

        public bool TryGetUi(string uiName, out Sprite sprite)
            => TryGet(ref _uiLookup, UiEntries, uiName, out sprite);

        public bool TryGetBackground(string backgroundName, out Sprite sprite)
            => TryGet(ref _backgroundLookup, BackgroundEntries, backgroundName, out sprite);

        public bool TryGetElement(string elementName, out Sprite sprite)
            => TryGet(ref _elementLookup, ElementEntries, elementName, out sprite);

        public bool TryGetElementInkMask(string elementName, out Texture2D texture)
            => TryGetTexture(ref _elementInkMaskLookup, ElementInkMaskEntries, elementName, out texture);

        public void ClearLookupCaches()
        {
            _baseLookup = null;
            _overlayLookup = null;
            _stampLookup = null;
            _uiLookup = null;
            _backgroundLookup = null;
            _elementLookup = null;
            _elementInkMaskLookup = null;
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

        private static bool TryGetTexture(ref Dictionary<string, Texture2D> cache, List<TextureEntry> entries,
                                          string key, out Texture2D texture)
        {
            if (cache == null)
            {
                cache = new Dictionary<string, Texture2D>(entries.Count);
                foreach (var entry in entries)
                {
                    if (!string.IsNullOrEmpty(entry.Key) && entry.Texture != null)
                        cache[entry.Key] = entry.Texture;
                }
            }
            texture = null;
            return key != null && cache.TryGetValue(key, out texture);
        }
    }
}
