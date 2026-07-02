using System.Collections;
using System.Collections.Generic;

namespace Hwatu.View.Flow
{
    /// <summary>
    /// 화면 컨트롤러 스택. 유니티 씬은 하나(Main)뿐이고 모든 화면 전환은 이 스택이
    /// 담당한다. 모든 조작은 transition.Hide() → 교체 → transition.Reveal() 순서를
    /// 지키는 코루틴이다. 동시에 하나의 전환만 실행하는 직렬화는 GameFlowController가
    /// 책임진다.
    /// </summary>
    public sealed class ScreenStack
    {
        private readonly List<IScreen> _screens = new List<IScreen>();
        private readonly GameFlowController _flow;

        public ScreenStack(GameFlowController flow)
        {
            _flow = flow;
        }

        public IScreen Current => _screens.Count > 0 ? _screens[_screens.Count - 1] : null;
        public int Count => _screens.Count;

        /// <summary>현재 화면을 가려 둔 채(비활성) 새 화면을 위에 얹는다.</summary>
        public IEnumerator Push(IScreen screen, ITransition transition)
        {
            yield return transition.Hide();
            if (Current != null && Current.Root != null) Current.Root.SetActive(false);
            _screens.Add(screen);
            screen.Enter(_flow);
            yield return transition.Reveal();
        }

        /// <summary>맨 위 화면을 내리고(Exit) 새 화면으로 바꾼다.</summary>
        public IEnumerator Replace(IScreen screen, ITransition transition)
        {
            yield return transition.Hide();
            if (_screens.Count > 0)
            {
                var top = _screens[_screens.Count - 1];
                _screens.RemoveAt(_screens.Count - 1);
                top.Exit();
            }
            _screens.Add(screen);
            screen.Enter(_flow);
            yield return transition.Reveal();
        }

        /// <summary>맨 위 화면을 걷어내고(Exit) 아래 화면을 복귀시킨다.</summary>
        public IEnumerator Pop(ITransition transition)
        {
            yield return transition.Hide();
            if (_screens.Count > 0)
            {
                var top = _screens[_screens.Count - 1];
                _screens.RemoveAt(_screens.Count - 1);
                top.Exit();
            }
            if (Current != null && Current.Root != null) Current.Root.SetActive(true);
            yield return transition.Reveal();
        }
    }
}
