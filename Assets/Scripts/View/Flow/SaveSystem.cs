using System.IO;
using Hwatu.Run;
using UnityEngine;

namespace Hwatu.View.Flow
{
    /// <summary>
    /// RunState를 persistentDataPath/run.json 에 JsonUtility로 저장/복원한다.
    /// 자동 저장 시점: 하루 전진 시, 타이틀 복귀 시. 엔딩 도달 시 삭제.
    /// (트리거는 GameFlowController가 담당하고, 여기는 파일 입출력만 안다)
    /// </summary>
    public static class SaveSystem
    {
        public static string SavePath => Path.Combine(Application.persistentDataPath, "run.json");

        public static bool Exists() => File.Exists(SavePath);

        public static void Save(RunState state)
        {
            File.WriteAllText(SavePath, JsonUtility.ToJson(state, prettyPrint: true));
        }

        /// <summary>
        /// 세이브가 없거나 읽을 수 없으면 null. 구버전/깨진 세이브는 마이그레이션하지
        /// 않고 그 자리에서 파일까지 안전 폐기한다 (프로토타입 단계의 의도적 단순화 —
        /// 생성 규칙이 바뀔 때마다 세이브 버전을 올리고 구버전은 버린다).
        /// </summary>
        public static RunState Load()
        {
            if (!Exists()) return null;
            try
            {
                var state = JsonUtility.FromJson<RunState>(File.ReadAllText(SavePath));
                if (RunStateMigration.EnsureCurrent(state)) return state;
                Delete(); // 구버전 폐기
                return null;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"세이브 로드 실패: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 이어하기 가능한(현재 버전) 세이브가 있는가. 타이틀이 [이어하기] 표시 여부에
        /// 쓴다 — 구버전 세이브는 이 검사에서 이미 폐기되므로 버튼이 숨는다.
        /// </summary>
        public static bool HasUsableSave() => Load() != null;

        public static void Delete()
        {
            if (Exists()) File.Delete(SavePath);
        }
    }
}
