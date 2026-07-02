using Hwatu.View.Flow;
using UnityEngine;

namespace Hwatu.View.Screens
{
    /// <summary>캐릭터 선택 (플레이스홀더). 지금은 캐릭터 카드 1장뿐이다.</summary>
    public sealed class CharacterSelectScreen : ScreenBase
    {
        protected override string ScreenName => "CharacterSelectScreen";

        protected override void Build(Transform canvasRoot)
        {
            var column = BuildCenterColumn(canvasRoot, "캐릭터 선택");
            AddBody(column, "노름꾼 (기본)\n노름빚을 지고 끌려온 망자", 30);
            AddButton(column, "SelectButton", "선택",
                () => Flow.ConfirmCharacter(GameFlowController.DefaultCharacterId));
        }
    }
}
