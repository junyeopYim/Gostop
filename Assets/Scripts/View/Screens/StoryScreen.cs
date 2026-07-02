using UnityEngine;

namespace Hwatu.View.Screens
{
    /// <summary>도입 스토리 (임시 스텁 — 실제 콘텐츠는 이후 지시서).</summary>
    public sealed class StoryScreen : ScreenBase
    {
        protected override string ScreenName => "StoryScreen";

        protected override void Build(Transform canvasRoot)
        {
            var column = BuildCenterColumn(canvasRoot, "스토리");
            AddBody(column,
                "노름으로 삶을 탕진하고 빚 대신 목숨을 저당 잡힌 밤, 차사가 문턱에 섰다.\n" +
                "\"갚을 것이 남았으니 따라오너라.\" 그렇게 저승길 마흔아홉 날이 시작되었다.\n\n" +
                "(임시 스토리 스텁)");
            AddButton(column, "ContinueButton", "계속", () => Flow.CompleteStory());
        }
    }
}
