using System;
using System.Collections.Generic;

namespace Hwatu.Run
{
    /// <summary>
    /// 판 수명주기에 맞춰 효과를 부착/해제한다.
    /// AttachAll(판 시작) → 판 진행(효과가 이벤트 관찰·규칙 수정) → DetachAll(판 종료).
    /// DetachAll 후에는 어떤 구독도 남지 않아야 한다 (EffectSystemTests가 증명).
    /// </summary>
    public sealed class EffectSystem
    {
        private readonly List<IEffect> _attached = new List<IEffect>();

        public IReadOnlyList<IEffect> Attached => _attached;

        /// <summary>효과 id들을 EffectRegistry로 실체화해 부착한다 (판 시작 시 호출).</summary>
        public void AttachAll(IEnumerable<string> effectIds, EffectContext ctx)
        {
            if (effectIds == null) throw new ArgumentNullException(nameof(effectIds));
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));
            foreach (var id in effectIds)
            {
                var effect = EffectRegistry.Create(id);
                effect.OnAttach(ctx);
                _attached.Add(effect);
            }
        }

        /// <summary>부착된 모든 효과를 부착의 역순으로 해제한다 (판 종료 시 호출).</summary>
        public void DetachAll()
        {
            for (int i = _attached.Count - 1; i >= 0; i--)
                _attached[i].OnDetach();
            _attached.Clear();
        }
    }
}
