using System.Collections;
using Hwatu.Run;
using Hwatu.View.Flow;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hwatu.View.Screens
{
    /// <summary>
    /// 갈림길 이벤트 화면 (주막과 동일 패턴): Event 노드 도착 시 자동 Push, 종료 시 Pop →
    /// 그날 완료 → 다음 날 갈림길. 제목(붓글씨) / 일러스트 슬롯(비워 둠) / 도입 텍스트(한지) /
    /// 선택지 버튼 2~3(비활성 시 사유) / 선택 후 결과 텍스트로 교체 + [계속].
    /// 부적 획득은 칩으로, 도박 승리는 붉은 인장(SealStampEffect)으로 표시한다.
    /// </summary>
    public sealed class EventScreen : ScreenBase
    {
        protected override string ScreenName => "EventScreen";

        private readonly System.Action _onClosed;
        private EventDefinition _definition;
        private RectTransform _content;
        private bool _resultShown;
        private bool _leaving;

        private RunController Run => Flow.CurrentRun;

        /// <summary>[테스트용] 이벤트가 제시한 선택지 수.</summary>
        public int ChoiceCount => _definition != null ? _definition.Choices.Count : 0;
        /// <summary>[테스트용] 선택 후 결과 패널로 전환됐는지.</summary>
        public bool IsResultShown => _resultShown;

        public EventScreen(System.Action onClosed)
        {
            _onClosed = onClosed;
        }

        protected override void Build(Transform canvasRoot)
        {
            _definition = EventRegistry.Resolve(Run.State.runSeed, Run.State.currentDay, Run.State.seenEventIds);

            var column = BuildCenterColumn(canvasRoot, _definition.Title);
            var panelImage = UIStyles.CreatePanel(column, "EventPanel", new Vector2(980f, 780f));
            panelImage.gameObject.AddComponent<RectMask2D>();

            var layout = panelImage.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(34, 34, 26, 26);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            // 일러스트 슬롯 — 아트 없음(비워 둠). 이후 지시서의 이음매 자리.
            var art = UIStyles.CreatePanel(panelImage.transform, "IllustrationSlot", new Vector2(910f, 150f));
            art.color = new Color(UIStyles.Ink.r, UIStyles.Ink.g, UIStyles.Ink.b, 0.06f);

            var intro = UIStyles.CreateText(panelImage.transform, "Intro", UITextPreset.Body,
                ComposeIntro(), 23, UIStyles.Ink, TextAnchor.UpperCenter);
            UIBuilder.SetPreferred(intro.gameObject, 910f, 150f);

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(panelImage.transform, false);
            _content = (RectTransform)contentGo.transform;
            UIBuilder.SetPreferred(contentGo, 910f, 360f);
            var contentLayout = contentGo.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 12f;
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = false;
            contentLayout.childForceExpandHeight = false;

            BuildChoices();
        }

        private string ComposeIntro()
        {
            string intro = _definition.Intro;
            if (_definition.ShowStatusSummary)
            {
                var s = Run.State;
                intro += $"\n\n{s.currentDay}일차 · 혼불 {s.honbul}/{s.honbulMax} · 부적 {s.relicIds.Count}개";
            }
            return intro;
        }

        private void BuildChoices()
        {
            ClearChildren(_content);
            _resultShown = false;

            for (int i = 0; i < _definition.Choices.Count; i++)
            {
                var choice = _definition.Choices[i];
                var state = EventResolver.GetChoiceState(choice, Run.State);
                int index = i;

                var button = UIStyles.CreateButton(_content, $"Choice_{i}", choice.Label,
                    new Vector2(780f, 62f), 24, () => SelectChoice(index));
                button.interactable = state.Active;

                if (!state.Active)
                {
                    var reason = UIStyles.CreateText(_content, $"Reason_{i}", UITextPreset.Body,
                        state.Reason, 16, UIStyles.Ash, TextAnchor.MiddleCenter);
                    reason.enableWordWrapping = false;
                    UIBuilder.SetPreferred(reason.gameObject, 780f, 22f);
                }
            }
        }

        /// <summary>선택지 선택 → 결과 적용 후 결과 패널로 교체. (비활성/이미 결과면 무시)</summary>
        public void SelectChoice(int index)
        {
            if (_resultShown || _leaving || Run == null) return;
            if (index < 0 || index >= _definition.Choices.Count) return;
            if (!EventResolver.GetChoiceState(_definition.Choices[index], Run.State).Active) return;

            var outcome = EventResolver.Resolve(_definition, index, Run, Run.State.currentDay);
            ShowResult(outcome);
        }

        private void ShowResult(EventOutcome outcome)
        {
            ClearChildren(_content);
            _resultShown = true;

            var resultText = UIStyles.CreateText(_content, "Result", UITextPreset.Body,
                outcome.ResultText ?? "", 24, UIStyles.Ink, TextAnchor.MiddleCenter);
            UIBuilder.SetPreferred(resultText.gameObject, 880f, 120f);

            if (!string.IsNullOrEmpty(outcome.GainedRelicId))
                BuildRelicChip(outcome.GainedRelicId);

            if (!string.IsNullOrEmpty(outcome.LostRelicId))
            {
                var lost = UIStyles.CreateText(_content, "LostRelic", UITextPreset.Body,
                    $"{EffectRegistry.GetDisplayName(outcome.LostRelicId)}을(를) 거울에 놓아주었다.",
                    18, UIStyles.Ash, TextAnchor.MiddleCenter);
                UIBuilder.SetPreferred(lost.gameObject, 880f, 28f);
            }

            UIStyles.CreateButton(_content, "ContinueButton", "계속", new Vector2(240f, 60f), 24, Continue);

            if (outcome.ShowVictorySeal)
                SealStampEffect.PlayInsideParentTopRight((RectTransform)resultText.transform, SealStampKind.Red);
        }

        private void BuildRelicChip(string relicId)
        {
            string name = EffectRegistry.GetDisplayName(relicId);
            var chipImage = UIStyles.CreatePanel(_content, "RelicChip", new Vector2(300f, 38f));
            chipImage.raycastTarget = false;
            var row = chipImage.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(12, 14, 4, 4);
            row.spacing = 6f;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlWidth = false;
            row.childControlHeight = false;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = false;
            UIStyles.CreateIcon(chipImage.transform, "bujeok", new Vector2(52f, 26f));
            var label = UIStyles.CreateText(chipImage.transform, "Label", UITextPreset.Hwaje,
                name, 20, UIStyles.Ink, TextAnchor.MiddleLeft, FontStyle.Bold);
            label.enableWordWrapping = false;
            UIBuilder.SetPreferred(label.gameObject, 210f, 30f);
        }

        /// <summary>[계속]: 이벤트 노드 완료 처리 후 Pop → 그날 갈림길로.</summary>
        public void Continue()
        {
            if (_leaving || Run == null) return;
            _leaving = true;
            if (!Run.TodayNodeCleared && Run.CurrentNode.kind == NodeKind.Event)
                Run.MarkTodayNodeCleared();
            Flow.StartCoroutine(LeaveRoutine());
        }

        private IEnumerator LeaveRoutine()
        {
            yield return Flow.PopScreen();
            _onClosed?.Invoke();
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }
    }
}
