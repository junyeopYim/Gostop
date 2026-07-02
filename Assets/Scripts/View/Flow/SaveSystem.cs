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

        /// <summary>세이브가 없거나 읽을 수 없으면 null.</summary>
        public static RunState Load()
        {
            if (!Exists()) return null;
            try
            {
                var state = JsonUtility.FromJson<RunState>(File.ReadAllText(SavePath));
                // 최소한의 온전성 검사: 덱이 비어 있으면 쓸 수 없는 세이브다
                return state != null && state.deck != null && state.deck.Count > 0 ? state : null;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"세이브 로드 실패: {e.Message}");
                return null;
            }
        }

        public static void Delete()
        {
            if (Exists()) File.Delete(SavePath);
        }
    }
}
