using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hwatu.View
{
    /// <summary>
    /// [3단계·A] 언더테일식 대화 상자 — 하단 중앙 한지 패널에 글자를 타자기로 찍고
    /// 글자마다 저음 블립(<see cref="BlipVoice"/>)을 울린다. 클릭 2단 동작:
    ///   타자 중 클릭 = 현재 문장 즉시 완성 / 완성 상태 클릭 = 다음 문장 /
    ///   마지막 문장에서 완성 후 클릭 = 종료.
    /// 정적 파사드 <see cref="Dialogue"/>로 호출한다. 공유 오버레이(ChasaVoiceOverlay)에 산다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DialogueBox : MonoBehaviour
    {
        private RectTransform _panel;
        private TextMeshProUGUI _text;
        private Image _blocker;          // 전면 클릭 캐처(판 입력 잠금 겸 어디든 클릭으로 진행)
        private Button _blockerButton;
        private Button _panelButton;
        private CanvasGroup _group;

        private bool _clickPending;
        private bool _playing;
        private Coroutine _co;
        private Action _onComplete;

        public bool IsPlaying => _playing;

        public void Play(string[] lines, Action onComplete, float charDelay, bool lockInput)
        {
            EnsureBuilt();
            // 진행 중이던 대화가 있으면 콜백 없이 즉시 정리하고 새 대화를 얹는다.
            if (_co != null) StopCoroutine(_co);
            _onComplete = onComplete;
            _playing = true;
            _clickPending = false;

            gameObject.SetActive(true);
            _group.alpha = 1f;
            _blocker.gameObject.SetActive(lockInput);
            _blocker.raycastTarget = lockInput;

            float delay = charDelay > 0f ? charDelay : ViewTuning.DialogueCharDelay;
            _co = StartCoroutine(Run(lines ?? Array.Empty<string>(), delay));
        }

        /// <summary>진행 중 대화를 콜백 없이 즉시 걷어낸다 (외부 상태 변화로 무효가 될 때).</summary>
        public void Dismiss()
        {
            if (_co != null) { StopCoroutine(_co); _co = null; }
            _playing = false;
            _onComplete = null;
            _clickPending = false;
            if (gameObject != null) gameObject.SetActive(false);
        }

        private IEnumerator Run(string[] lines, float charDelay)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                yield return TypeLine(lines[i] ?? string.Empty, charDelay);
                yield return WaitClick();
            }
            _co = null;
            var cb = _onComplete;
            _onComplete = null;
            _playing = false;
            gameObject.SetActive(false);
            cb?.Invoke();
        }

        private IEnumerator TypeLine(string line, float charDelay)
        {
            _text.text = line;
            _text.maxVisibleCharacters = 0;
            _text.ForceMeshUpdate();
            for (int i = 0; i < line.Length; i++)
            {
                if (ConsumeClick()) { _text.maxVisibleCharacters = line.Length; yield break; } // 즉시 완성
                _text.maxVisibleCharacters = i + 1;
                if (BlipVoice.IsVoicedChar(line[i])) BlipVoice.PlayBlip();

                float e = 0f;
                while (e < charDelay)
                {
                    if (ConsumeClick()) { _text.maxVisibleCharacters = line.Length; yield break; }
                    e += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
            _text.maxVisibleCharacters = line.Length;
        }

        private IEnumerator WaitClick()
        {
            while (!ConsumeClick()) yield return null;
        }

        private bool ConsumeClick()
        {
            if (!_clickPending) return false;
            _clickPending = false;
            return true;
        }

        private void RequestAdvance() => _clickPending = true;

        private void EnsureBuilt()
        {
            if (_panel != null) return;
            var root = (RectTransform)transform;
            UIBuilder.Stretch(root, 0f, 0f);
            _group = gameObject.GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();

            // 전면 클릭 캐처 (판 입력 잠금 + 어디든 클릭 진행)
            _blockerButton = UIStyles.CreateInvisibleButton(transform, "DialogueClickCatcher", RequestAdvance);
            _blocker = _blockerButton.GetComponent<Image>();
            UIBuilder.Stretch((RectTransform)_blocker.transform, 0f, 0f);

            // 하단 중앙 한지 패널
            var panel = UIStyles.CreatePanel(transform, "DialoguePanel", new Vector2(1280f, 210f));
            _panel = (RectTransform)panel.transform;
            _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0f);
            _panel.pivot = new Vector2(0.5f, 0f);
            _panel.anchoredPosition = new Vector2(0f, 46f);
            // 패널 자체도 클릭 진행 (잠금 해제 대화에서 패널 클릭으로 진행)
            _panelButton = panel.gameObject.AddComponent<Button>();
            _panelButton.transition = Selectable.Transition.None;
            panel.raycastTarget = true;
            _panelButton.targetGraphic = panel;
            _panelButton.onClick.AddListener(RequestAdvance);

            // 세로 중앙 정렬(짧은 대사가 박스 중앙에 앉음) + 넉넉한 크기·줄간격·자간으로 가독성 확보.
            _text = UIStyles.CreateText(panel.transform, "DialogueText", UITextPreset.Body, "",
                ViewTuning.DialogueFontSize, UIStyles.Ink, TextAnchor.MiddleLeft);
            _text.enableWordWrapping = true;
            _text.lineSpacing = ViewTuning.DialogueLineSpacing;
            _text.characterSpacing = ViewTuning.DialogueCharSpacing;
            var tr = (RectTransform)_text.transform;
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(54f, 26f);
            tr.offsetMax = new Vector2(-54f, -26f);

            gameObject.SetActive(false);
        }

        private void OnDisable()
        {
            // 비활성화되며 남은 진행 상태 정리 (파괴/전환 안전).
            _clickPending = false;
        }
    }

    /// <summary>[3단계·A] 대화 시스템 정적 파사드. 공유 오버레이의 단일 DialogueBox를 구동한다.</summary>
    public static class Dialogue
    {
        private static DialogueBox _box;

        public static bool IsPlaying => _box != null && _box.IsPlaying;

        /// <summary>대사 줄들을 타자기로 재생한다. onComplete는 마지막 문장 종료 후 호출된다.</summary>
        public static void Play(string[] lines, Action onComplete = null,
            float charDelay = 0f, bool lockInput = true)
        {
            EnsureBox().Play(lines, onComplete, charDelay, lockInput);
        }

        /// <summary>진행 중 대화를 콜백 없이 즉시 걷어낸다.</summary>
        public static void Dismiss()
        {
            if (_box != null) _box.Dismiss();
        }

        private static DialogueBox EnsureBox()
        {
            if (_box != null) return _box;
            var go = new GameObject("DialogueBox", typeof(RectTransform));
            go.transform.SetParent(ChasaVoiceOverlay.Root, false);
            _box = go.AddComponent<DialogueBox>();
            return _box;
        }
    }
}
