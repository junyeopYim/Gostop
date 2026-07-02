using Hwatu.View.Flow;
using UnityEngine;

namespace Hwatu.View.Screens
{
    /// <summary>타이틀 (플레이스홀더). [이어하기]는 세이브가 있을 때만 표시한다.</summary>
    public sealed class TitleScreen : ScreenBase
    {
        protected override string ScreenName => "TitleScreen";

        protected override void Build(Transform canvasRoot)
        {
            var column = BuildCenterColumn(canvasRoot, "화투 로그라이크 (가제)");
            AddBody(column, "저승길 마흔아홉 날 — 걸어다니는 뼈대", 24);
            AddButton(column, "NewGameButton", "새 게임", () => Flow.StartNewGame());
            if (SaveSystem.Exists())
                AddButton(column, "ContinueButton", "이어하기", () => Flow.ContinueRun());
            AddButton(column, "QuitButton", "종료", () => Flow.QuitGame());
        }
    }
}
