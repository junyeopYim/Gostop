using UnityEngine;

namespace Hwatu.View.Screens
{
    public sealed class JumakAutoPush : MonoBehaviour
    {
        private RunScreen _screen;

        public void Bind(RunScreen screen)
        {
            _screen = screen;
        }

        private void Update()
        {
            _screen?.TryOpenJumakIfNeeded();
        }
    }
}
