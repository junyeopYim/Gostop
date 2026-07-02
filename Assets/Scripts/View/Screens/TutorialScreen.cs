using UnityEngine;

namespace Hwatu.View.Screens
{
    /// <summary>튜토리얼 자리 (준비 중 — 실제 콘텐츠는 이후 지시서).</summary>
    public sealed class TutorialScreen : ScreenBase
    {
        protected override string ScreenName => "TutorialScreen";

        protected override void Build(Transform canvasRoot)
        {
            var column = BuildCenterColumn(canvasRoot, "튜토리얼 (준비 중)");
            AddButton(column, "SkipButton", "건너뛰기", () => Flow.CompleteTutorial());
        }
    }
}
