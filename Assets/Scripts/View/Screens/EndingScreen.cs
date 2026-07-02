using Hwatu.Run;
using UnityEngine;

namespace Hwatu.View.Screens
{
    /// <summary>엔딩 두 변형: 환생(49일 통과) / 소멸(혼불 소진). 임시 텍스트.</summary>
    public sealed class EndingScreen : ScreenBase
    {
        public RunEnding Ending { get; }

        public EndingScreen(RunEnding ending)
        {
            Ending = ending;
        }

        protected override string ScreenName => "EndingScreen";

        protected override void Build(Transform canvasRoot)
        {
            bool reincarnated = Ending == RunEnding.Reincarnated;
            var column = BuildCenterColumn(canvasRoot, reincarnated ? "환생" : "소멸");
            AddBody(column, reincarnated
                ? "마흔아홉 날을 건너, 망자는 이승의 빛 속으로 돌아간다.\n(임시 엔딩 텍스트)"
                : "혼불이 모두 꺼졌다. 망자는 저승길 위에서 흩어졌다.\n(임시 엔딩 텍스트)");
            AddButton(column, "ToTitleButton", "타이틀로", () => Flow.ReturnToTitle());
        }
    }
}
