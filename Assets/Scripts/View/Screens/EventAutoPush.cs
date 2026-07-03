using UnityEngine;

namespace Hwatu.View.Screens
{
    /// <summary>주막 자동 진입과 동일 패턴: Event 노드에 있으면 매 프레임 진입을 시도한다.</summary>
    public sealed class EventAutoPush : MonoBehaviour
    {
        private RunScreen _screen;

        public void Bind(RunScreen screen)
        {
            _screen = screen;
        }

        private void Update()
        {
            _screen?.TryOpenEventIfNeeded();
        }
    }
}
