using UnityEngine;

namespace Hwatu.View.Flow
{
    /// <summary>
    /// 화면 1장. Enter에서 자체 Canvas 루트를 코드로 생성하고 Exit에서 스스로 파괴한다.
    /// 스택에 덮여 가려질 때는 ScreenStack이 Root의 SetActive로 숨김/복귀를 제어한다.
    /// </summary>
    public interface IScreen
    {
        /// <summary>Enter에서 생성되는 화면 루트 (Canvas GameObject). Exit 후 null.</summary>
        GameObject Root { get; }

        void Enter(GameFlowController flow);
        void Exit();
    }
}
