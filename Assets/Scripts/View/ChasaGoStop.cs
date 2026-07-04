using System;

namespace Hwatu.View
{
    /// <summary>
    /// 임베드 판이 고/스톱 결정 시점에 프레젠터에게 넘기는 문맥. 엔진은 건드리지 않고
    /// 표시에 필요한 값(다음 배수·스톱 차단 사유)과 결정 콜백(Go/Stop)·연출 훅만 담는다.
    /// </summary>
    public sealed class GoStopContext
    {
        /// <summary>다음 고의 배수 (쪽지 라벨에 표기 — 현재 끗수·배수·손패는 스크린 HUD가 이미 보임).</summary>
        public int NextMultiplier;
        /// <summary>스톱 차단 사유 (null=스톱 가능). 심판 기믹(염라 업경대)이 채운다.</summary>
        public string StopBlockReason;
        /// <summary>고 선언 (엔진 경로는 GameController가 가드한다 — 이미 결정 국면 아니면 무시).</summary>
        public Action Go;
        /// <summary>스톱 선언 (동일 가드).</summary>
        public Action Stop;
        /// <summary>[C] 먹 순간 강조(한 호흡 조여듦) 훅.</summary>
        public Action EmphasizeTension;
    }

    /// <summary>
    /// [3단계·C] 고/스톱 — 박스 모달의 대체. 시선은 판 유지 → TensionVignette 순간 강조 →
    /// DialogueBox 한 줄 → ChasaOffer 쪽지 2장([고 — 다음 배수], [스톱 — 여기서 끝]) →
    /// 선택 즉시 엔진 경로(Go/Stop). 스톱 봉인 시 스톱 쪽지는 먹으로 지워진 비활성 +
    /// 대사 한 줄 교체([E]). 대사는 [E] 고정 — 창작·수정 금지.
    /// </summary>
    public static class ChasaGoStop
    {
        // ── [E] 고정 대사 (창작·수정 금지) ──
        public const string LineMain = "더 가시려는가.";
        public const string LineTutorialFirst = "더 가면 배가 불고, 빈손이면 재가 되오. 그대의 패를 믿으시오?";
        public const string LineStopSealed = "…대왕 앞에서는 멈출 수 없소.";

        /// <summary>
        /// 고/스톱 질문을 제시한다. customLine이 있으면 그 대사를 앞세운다(튜토리얼 첫 제안).
        /// 스톱이 봉인되면 대사는 [E]의 봉인 대사로 교체되고 스톱 쪽지는 비활성(먹 지움)이 된다.
        /// </summary>
        public static void Present(GoStopContext ctx, string customLine = null)
        {
            if (ctx == null) return;
            ctx.EmphasizeTension?.Invoke();

            bool sealedStop = !string.IsNullOrEmpty(ctx.StopBlockReason);
            string line = sealedStop ? LineStopSealed : (customLine ?? LineMain);

            Dialogue.Play(new[] { line }, () => ShowOffer(ctx, sealedStop), lockInput: true);
        }

        private static void ShowOffer(GoStopContext ctx, bool sealedStop)
        {
            var options = new[]
            {
                new ChasaOfferOption($"고 — 배수 x{ctx.NextMultiplier}"),
                new ChasaOfferOption("스톱 — 여기서 끝", !sealedStop,
                    sealedStop ? ctx.StopBlockReason : null),
            };
            ChasaOffer.Show(options, index =>
            {
                if (index == 0) ctx.Go?.Invoke();
                else ctx.Stop?.Invoke();
            });
        }
    }
}
