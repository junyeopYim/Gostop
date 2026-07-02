using System;
using Hwatu.Core;

namespace Hwatu.Run
{
    /// <summary>
    /// 판 이벤트를 구독하고 규칙을 비트는 "효과"의 단일 소켓.
    ///
    /// [설계 노트] 부적, 캐릭터 패시브, 보스의 규칙 변형, 주간 지옥 컬러는 전부 같은
    /// 것이다 — 보스 기믹과 지옥 컬러는 "음(-)의 부적"으로서 이 IEffect로 구현될
    /// 예정이다. 소켓은 이것 하나만 둔다.
    ///
    /// 수명주기: 판 시작 시 EffectSystem이 OnAttach, 판 종료 시 전부 OnDetach.
    /// OnDetach 후에는 어떤 이벤트 구독·수정자 등록도 남아 있으면 안 된다.
    /// </summary>
    public interface IEffect
    {
        string Id { get; }
        void OnAttach(EffectContext ctx);
        void OnDetach();
    }

    /// <summary>효과가 판(엔진·이벤트)과 런 서비스에 접근하는 통로.</summary>
    public sealed class EffectContext
    {
        public RoundEngine Engine { get; }
        public RoundEvents Events => Engine.Events;
        public IRunServices Services { get; }

        public EffectContext(RoundEngine engine, IRunServices services)
        {
            Engine = engine ?? throw new ArgumentNullException(nameof(engine));
            Services = services ?? throw new ArgumentNullException(nameof(services));
        }
    }

    /// <summary>효과가 런 상태에 작용하는 서비스 창구 (이후 지시서에서 확장).</summary>
    public interface IRunServices
    {
        void AddNojatdon(int amount);
    }
}
