using Hwatu.View.Flow;
using UnityEngine;

namespace Hwatu.View.Screens
{
    /// <summary>타이틀 (플레이스홀더). [이어하기]는 현재 버전 세이브가 있을 때만 표시한다.</summary>
    public sealed class TitleScreen : ScreenBase
    {
        protected override string ScreenName => "TitleScreen";

        protected override void Build(Transform canvasRoot)
        {
            var column = BuildCenterColumn(canvasRoot, "화투 로그라이크 (가제)");
            AddTitlePaintWash(canvasRoot);
            AddBody(column, "저승길 마흔아홉 날 — 걸어다니는 뼈대", 24);
            AddButton(column, "NewGameButton", "새 게임", () => Flow.StartNewGame());
            if (SaveSystem.HasUsableSave()) // 구버전 세이브는 이 검사에서 폐기되고 버튼이 숨는다
                AddButton(column, "ContinueButton", "이어하기", () => Flow.ContinueRun());
            AddButton(column, "QuitButton", "종료", () => Flow.QuitGame());
        }

        private static void AddTitlePaintWash(Transform canvasRoot)
        {
            var wash = UIBuilder.CreatePanel(canvasRoot, "TitlePaintWash",
                new Color(UIStyles.Vermilion.r, UIStyles.Vermilion.g, UIStyles.Vermilion.b, 0.28f));
            wash.raycastTarget = false;
            var rt = (RectTransform)wash.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(840f, 120f);
            rt.anchoredPosition = new Vector2(0f, 265f);
            wash.transform.SetSiblingIndex(1);
            wash.gameObject.AddComponent<PaintInEffect>().Play(0.55f, Ease.OutCubic, InkMaskKind.SweepDiag);
        }
    }
}
