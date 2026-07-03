using System.Collections;
using System.Collections.Generic;
using Hwatu.Core;
using Hwatu.Run;
using Hwatu.View.Flow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hwatu.View.Screens
{
    public sealed class JumakScreen : ScreenBase
    {
        protected override string ScreenName => "JumakScreen";

        private readonly System.Action _onClosed;
        private readonly bool[] _soldOut = new bool[JumakShop.OfferSlotCount];

        private List<ShopOffer> _offers;
        private RectTransform _panel;
        private RectTransform _content;
        private TextMeshProUGUI _nojatdonText;
        private TextMeshProUGUI _messageText;
        private RectTransform[] _shopSlots;
        private int _selectedCardId = -1;
        private TextMeshProUGUI _selectedCardText;
        private Button _cutButton;
        private bool _leaving;

        private RunController Run => Flow.CurrentRun;

        public JumakScreen(System.Action onClosed)
        {
            _onClosed = onClosed;
        }

        protected override void Build(Transform canvasRoot)
        {
            _offers = JumakShop.GetOffers(Run.State);

            var column = BuildCenterColumn(canvasRoot, "주막");
            var panelImage = UIStyles.CreatePanel(column, "JumakPanel", new Vector2(980f, 740f));
            _panel = (RectTransform)panelImage.transform;
            panelImage.gameObject.AddComponent<RectMask2D>();

            var layout = panelImage.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(30, 30, 22, 22);
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            BuildHeader(panelImage.transform);

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(panelImage.transform, false);
            _content = (RectTransform)contentGo.transform;
            UIBuilder.SetPreferred(contentGo, 920f, 500f);

            _messageText = UIStyles.CreateText(panelImage.transform, "Message", UITextPreset.Body, "",
                20, UIStyles.Ash, TextAnchor.MiddleCenter);
            UIBuilder.SetPreferred(_messageText.gameObject, 900f, 32f);

            BuildShopContent();
        }

        private void BuildHeader(Transform parent)
        {
            var header = new GameObject("Header", typeof(RectTransform));
            header.transform.SetParent(parent, false);
            UIBuilder.SetPreferred(header, 920f, 86f);
            var row = header.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 18f;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;

            var portrait = UIStyles.CreatePanel(header.transform, "PortraitSlot", new Vector2(66f, 66f));
            var portraitText = UIStyles.CreateText(portrait.transform, "EmptyPortrait", UITextPreset.Body,
                "", 18, UIStyles.Ash, TextAnchor.MiddleCenter);
            UIBuilder.Stretch((RectTransform)portraitText.transform, 8f, 8f);

            var speech = UIStyles.CreateText(header.transform, "JumoLine", UITextPreset.Body,
                "주모: 쉬어 갈 손님이면 조용히 고르고, 떠날 손님이면 문부터 닫아 주시오.",
                21, UIStyles.Ink, TextAnchor.MiddleLeft);
            speech.overflowMode = TextOverflowModes.Ellipsis;
            UIBuilder.SetPreferred(speech.gameObject, 630f, 66f);

            var money = new GameObject("Nojatdon", typeof(RectTransform));
            money.transform.SetParent(header.transform, false);
            var moneyRow = money.AddComponent<HorizontalLayoutGroup>();
            moneyRow.spacing = 5f;
            moneyRow.childAlignment = TextAnchor.MiddleCenter;
            moneyRow.childControlWidth = false;
            moneyRow.childControlHeight = false;
            moneyRow.childForceExpandWidth = false;
            moneyRow.childForceExpandHeight = false;
            UIBuilder.SetPreferred(money, 150f, 66f);
            UIStyles.CreateIcon(money.transform, "yeopjeon", new Vector2(34f, 34f));
            _nojatdonText = UIStyles.CreateText(money.transform, "NojatdonText", UITextPreset.Numeral,
                Run.State.nojatdon.ToString(), 26, UIStyles.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIBuilder.SetPreferred(_nojatdonText.gameObject, 90f, 40f);
        }

        private void BuildShopContent()
        {
            ClearChildren(_content);
            _selectedCardId = -1;
            _shopSlots = new RectTransform[JumakShop.OfferSlotCount];

            var layout = _content.gameObject.GetComponent<VerticalLayoutGroup>();
            if (layout == null) layout = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var offersRow = new GameObject("OfferRow", typeof(RectTransform));
            offersRow.transform.SetParent(_content, false);
            UIBuilder.SetPreferred(offersRow, 920f, 330f);
            var offersLayout = offersRow.AddComponent<HorizontalLayoutGroup>();
            offersLayout.spacing = 18f;
            offersLayout.childAlignment = TextAnchor.MiddleCenter;
            offersLayout.childControlWidth = false;
            offersLayout.childControlHeight = false;
            offersLayout.childForceExpandWidth = false;
            offersLayout.childForceExpandHeight = false;

            for (int i = 0; i < _offers.Count; i++)
                BuildOfferSlot(offersRow.transform, i, _offers[i]);

            var actions = new GameObject("Actions", typeof(RectTransform));
            actions.transform.SetParent(_content, false);
            UIBuilder.SetPreferred(actions, 920f, 78f);
            var actionLayout = actions.AddComponent<HorizontalLayoutGroup>();
            actionLayout.spacing = 20f;
            actionLayout.childAlignment = TextAnchor.MiddleCenter;
            actionLayout.childControlWidth = false;
            actionLayout.childControlHeight = false;
            actionLayout.childForceExpandWidth = false;
            actionLayout.childForceExpandHeight = false;

            int salpuriCost = JumakShop.GetSalpuriCost(Run.State);
            var salpuri = UIStyles.CreateButton(actions.transform, "SalpuriButton",
                $"살풀이 — {salpuriCost}닢", new Vector2(260f, 62f), 24, ShowSalpuriContent);
            string salpuriReason = SalpuriBlockReason();
            salpuri.interactable = salpuriReason == null;

            UIStyles.CreateButton(actions.transform, "LeaveButton", "떠나기",
                new Vector2(220f, 62f), 24, LeaveJumak);

            RefreshHeader();
            if (salpuriReason != null)
                _messageText.text = salpuriReason;
        }

        private void BuildOfferSlot(Transform parent, int index, ShopOffer offer)
        {
            var slotImage = UIStyles.CreatePanel(parent, $"OfferSlot_{index}", new Vector2(292f, 315f));
            var slot = slotImage.gameObject;
            slot.AddComponent<RectMask2D>();
            _shopSlots[index] = (RectTransform)slot.transform;
            var layout = slot.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 18, 16, 16);
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            UIStyles.CreateIcon(slot.transform, "bujeok", new Vector2(62f, 30f));

            if (offer.Definition == null)
            {
                var empty = UIStyles.CreateText(slot.transform, "Empty", UITextPreset.Body,
                    "비어 있음", 24, UIStyles.Ash, TextAnchor.MiddleCenter);
                UIBuilder.SetPreferred(empty.gameObject, 240f, 116f);
                return;
            }

            var name = UIStyles.CreateText(slot.transform, "Name", UITextPreset.Hwaje,
                offer.Definition.DisplayName, 28, UIStyles.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            name.enableAutoSizing = true;
            name.fontSizeMin = 22f;
            name.fontSizeMax = 28f;
            name.overflowMode = TextOverflowModes.Ellipsis;
            UIBuilder.SetPreferred(name.gameObject, 246f, 36f);

            var desc = UIStyles.CreateText(slot.transform, "Desc", UITextPreset.Body,
                offer.Definition.Description, 16, UIStyles.Ash, TextAnchor.UpperCenter);
            desc.enableAutoSizing = true;
            desc.fontSizeMin = 12f;
            desc.fontSizeMax = 16f;
            desc.overflowMode = TextOverflowModes.Ellipsis;
            UIBuilder.SetPreferred(desc.gameObject, 246f, 96f);

            var price = UIStyles.CreateText(slot.transform, "Price", UITextPreset.Numeral,
                $"{TierLabel(offer.Definition.Tier)} · {offer.Definition.Price}닢",
                20, UIStyles.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            price.overflowMode = TextOverflowModes.Ellipsis;
            UIBuilder.SetPreferred(price.gameObject, 246f, 28f);

            string reason = BuyBlockReason(index, offer);
            var buy = UIStyles.CreateButton(slot.transform, "BuyButton",
                _soldOut[index] ? "품절" : "산다", new Vector2(170f, 44f), 22,
                () => BuyOffer(index));
            buy.interactable = reason == null;

            if (_soldOut[index])
                DrawSoldOut(slot.transform);
        }

        private void DrawSoldOut(Transform parent)
        {
            var overlay = UIStyles.CreateSolidImage(parent, "SoldOutInk", new Color(0f, 0f, 0f, 0.48f));
            UIBuilder.Stretch((RectTransform)overlay.transform, 0f, 0f);
            overlay.transform.SetAsLastSibling();
            var sold = UIStyles.CreateText(overlay.transform, "SoldOutText", UITextPreset.Hwaje,
                "먹으로 지움", 32, UIStyles.Paper, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIBuilder.Stretch((RectTransform)sold.transform, 10f, 10f);
        }

        private void BuyOffer(int index)
        {
            var offer = _offers[index];
            string reason = BuyBlockReason(index, offer);
            if (reason != null)
            {
                _messageText.text = reason;
                return;
            }

            if (!JumakShop.TryPurchaseRelic(Run.State, offer.EffectId, out reason))
            {
                _messageText.text = reason;
                BuildShopContent();
                return;
            }

            _soldOut[index] = true;
            BuildShopContent();
            _messageText.text = $"{offer.Definition.DisplayName}을 지녔다.";
            if (_shopSlots[index] != null)
                SealStampEffect.Play(_shopSlots[index], SealStampKind.Red);
        }

        private void ShowSalpuriContent()
        {
            ClearChildren(_content);
            _selectedCardId = -1;

            var layout = _content.gameObject.GetComponent<VerticalLayoutGroup>();
            if (layout == null) layout = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            _selectedCardText = UIStyles.CreateText(_content, "SelectedCard", UITextPreset.Body,
                "베어낼 카드를 고르시오.", 22, UIStyles.Ink, TextAnchor.MiddleCenter);
            UIBuilder.SetPreferred(_selectedCardText.gameObject, 880f, 34f);

            var scrollGo = new GameObject("DeckScroll", typeof(RectTransform));
            scrollGo.transform.SetParent(_content, false);
            UIBuilder.SetPreferred(scrollGo, 890f, 350f);
            var scrollImage = scrollGo.AddComponent<Image>();
            scrollImage.color = new Color(UIStyles.Ink.r, UIStyles.Ink.g, UIStyles.Ink.b, 0.10f);
            scrollImage.raycastTarget = true;
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scrollGo.AddComponent<RectMask2D>();

            var gridGo = new GameObject("DeckGrid", typeof(RectTransform));
            gridGo.transform.SetParent(scrollGo.transform, false);
            var gridRt = (RectTransform)gridGo.transform;
            gridRt.anchorMin = new Vector2(0f, 1f);
            gridRt.anchorMax = new Vector2(1f, 1f);
            gridRt.pivot = new Vector2(0.5f, 1f);
            gridRt.anchoredPosition = Vector2.zero;
            gridRt.sizeDelta = new Vector2(0f, 0f);
            var grid = gridGo.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(58f, 86f);
            grid.spacing = new Vector2(7f, 8f);
            grid.padding = new RectOffset(12, 12, 12, 12);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 12;
            var fitter = gridGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = gridRt;

            foreach (var spec in Run.State.deck)
            {
                int cardId = spec.id;
                var view = CardView.Create(gridGo.transform, CardSpecs.ToCard(spec),
                    new Vector2(58f, 86f), SelectSalpuriCard);
                if (cardId == _selectedCardId)
                    view.gameObject.AddComponent<Outline>().effectColor = UIStyles.Vermilion;
            }

            var actions = new GameObject("SalpuriActions", typeof(RectTransform));
            actions.transform.SetParent(_content, false);
            UIBuilder.SetPreferred(actions, 880f, 74f);
            var row = actions.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 18f;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = false;
            row.childControlHeight = false;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;

            _cutButton = UIStyles.CreateButton(actions.transform, "CutButton",
                "베어낸다", new Vector2(220f, 58f), 24, ConfirmSalpuri);
            _cutButton.interactable = false;
            UIStyles.CreateButton(actions.transform, "BackButton", "돌아가기",
                new Vector2(220f, 58f), 24, BuildShopContent);

            _messageText.text = "";
        }

        private void SelectSalpuriCard(int cardId)
        {
            _selectedCardId = cardId;
            var card = CardSpecs.ToCard(Run.State.deck.Find(c => c.id == cardId));
            _selectedCardText.text = $"{card.DebugName}을 벨 준비가 되었다.";
            if (_cutButton != null) _cutButton.interactable = true;
        }

        private void ConfirmSalpuri()
        {
            if (_selectedCardId < 0)
            {
                _messageText.text = "먼저 카드를 고르시오.";
                return;
            }

            if (!JumakShop.TrySalpuri(Run.State, _selectedCardId, out var reason))
            {
                _messageText.text = reason;
                return;
            }

            BuildShopContent();
            _messageText.text = $"살풀이를 마쳤다. 덱 {Run.State.deck.Count}장.";
            SealStampEffect.PlayInsideParentTopRight((RectTransform)_messageText.transform, SealStampKind.Red);
        }

        private string BuyBlockReason(int index, ShopOffer offer)
        {
            if (_soldOut[index]) return "이미 품절입니다.";
            if (offer == null || offer.Definition == null) return "빈 진열입니다.";
            if (Run.State.relicIds.Contains(offer.EffectId)) return "이미 지닌 부적입니다.";
            int limit = Run.State.relicSlotLimit > 0 ? Run.State.relicSlotLimit : JumakShop.DefaultRelicSlotLimit;
            if (Run.State.relicIds.Count >= limit) return $"부적 슬롯이 가득 찼습니다 ({limit}/{limit}).";
            if (Run.State.nojatdon < offer.Definition.Price)
                return $"노잣돈이 부족합니다 ({offer.Definition.Price}닢 필요).";
            return null;
        }

        private string SalpuriBlockReason()
        {
            if (Run.State.deck.Count <= JumakShop.SalpuriMinimumDeckSize)
                return $"덱이 {JumakShop.SalpuriMinimumDeckSize}장이라 살풀이를 할 수 없습니다.";
            int cost = JumakShop.GetSalpuriCost(Run.State);
            if (Run.State.nojatdon < cost) return $"살풀이에는 {cost}닢이 필요합니다.";
            return null;
        }

        public void LeaveJumak()
        {
            if (_leaving || Run == null) return;
            _leaving = true;
            if (!Run.TodayNodeCleared && Run.CurrentNode.kind == NodeKind.Jumak)
                Run.MarkTodayNodeCleared();
            Flow.StartCoroutine(LeaveRoutine());
        }

        private IEnumerator LeaveRoutine()
        {
            yield return Flow.PopScreen();
            _onClosed?.Invoke();
        }

        private void RefreshHeader()
        {
            if (_nojatdonText != null)
                _nojatdonText.text = Run.State.nojatdon.ToString();
        }

        private static string TierLabel(EffectTier tier)
        {
            return tier == EffectTier.Rare ? "Rare" : "Common";
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }
    }
}
