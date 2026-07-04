using System;
using UnityEngine;

namespace Hwatu.View.Stage
{
    /// <summary>
    /// 이름 붙은 카메라 포즈: 로컬 위치 + 오일러 회전 + FOV. 포즈를 "데이터"로 두어
    /// 이후 단계(WalkView 등)가 새 시선을 코드 수정 없이 등록할 수 있게 한다.
    /// 값은 리그 루트(무대 원점) 로컬 기준이다.
    /// </summary>
    [Serializable]
    public struct CameraPose
    {
        public string Id;
        public Vector3 Position;
        public Vector3 Euler;
        public float Fov;
        /// <summary>[B] 이 포즈에서 상시 호흡 노이즈를 허용할지. 판(TableView)은 죽은 듯 고정(false),
        /// 차사 정면(FrontView)·비판 상황은 미세한 숨을 남긴다(true).</summary>
        public bool AllowBreathing;

        public CameraPose(string id, Vector3 position, Vector3 euler, float fov, bool allowBreathing = true)
        {
            Id = id;
            Position = position;
            Euler = euler;
            Fov = fov;
            AllowBreathing = allowBreathing;
        }

        public Quaternion Rotation => Quaternion.Euler(Euler);
    }
}
