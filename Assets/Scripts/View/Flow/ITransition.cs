using System.Collections;

namespace Hwatu.View.Flow
{
    /// <summary>
    /// 화면 전환 연출의 이음매. 화면 교체는 항상
    /// Hide() 완료 → 화면 교체 → Reveal() 완료 순서를 거친다 (코루틴 기반).
    /// 지금은 InstantTransition 하나뿐이며, 추후 먹붓 와이프가 이 인터페이스로 꽂힌다.
    /// </summary>
    public interface ITransition
    {
        /// <summary>화면을 가린다 (교체 직전).</summary>
        IEnumerator Hide();

        /// <summary>가림을 걷어 새 화면을 드러낸다 (교체 직후).</summary>
        IEnumerator Reveal();
    }

    /// <summary>연출 없음 — 즉시 전환.</summary>
    public sealed class InstantTransition : ITransition
    {
        public IEnumerator Hide() { yield break; }
        public IEnumerator Reveal() { yield break; }
    }
}
