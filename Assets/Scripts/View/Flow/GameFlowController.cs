using System;
using System.Collections;
using Hwatu.Run;
using Hwatu.View;
using Hwatu.View.Screens;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Hwatu.View.Flow
{
    /// <summary>
    /// Main 씬의 유일한 컴포넌트 (AppRoot). 화면 스택을 소유하고 화면 간 흐름과
    /// 런 생명주기(생성/저장/삭제)를 중재한다. 화면들은 이 클래스의 의미 단위
    /// 메서드만 호출하고, 어떤 화면으로 갈지는 전부 여기서 결정한다.
    ///
    /// 흐름: Title → CharSelect → Story → Tutorial → Run → Ending → Title.
    /// 이어하기: Title → Run 직행 (세이브 로드).
    /// </summary>
    public sealed class GameFlowController : MonoBehaviour
    {
        /// <summary>기본 캐릭터 id (지금은 "노름꾼" 1종뿐).</summary>
        public const string DefaultCharacterId = "gambler";

        public ScreenStack Screens { get; private set; }
        public RunController CurrentRun { get; private set; }
        /// <summary>화면 전환 코루틴 실행 중 여부 (중복 내비게이션 차단).</summary>
        public bool IsTransitioning { get; private set; }
        /// <summary>[테스트 전용] 전환 주입 훅. 실제 플레이는 null → 먹 와이프(InkWipeTransition).</summary>
        public static Func<ITransition> DefaultTransitionFactory { get; set; }

        // [환경 안전] Enter Play Mode Options가 DisableDomainReload면 static이 세션 간 남는다.
        // 중간에 끊긴 PlayMode 테스트가 남긴 DefaultTransitionFactory(예: InstantTransition)가
        // 실제 플레이의 먹 와이프를 없애는 걸 막으려, 매 플레이 진입 시 초기화한다.
        // 테스트는 각자 SetUp에서 이 시점 이후 다시 지정하므로 영향받지 않는다.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetTestHooksOnPlay() => DefaultTransitionFactory = null;

        private ITransition _transition;
        private int _pendingRunSeed;

        private void Awake()
        {
            EnsureCamera();
            EnsureEventSystem();
            _transition = CreateDefaultTransition();
            Screens = new ScreenStack(this);
            Navigate(Screens.Replace(new TitleScreen(), _transition));
        }

        public void SetTransition(ITransition transition)
        {
            _transition = transition ?? new InstantTransition();
        }

        /// <summary>
        /// [A] 화면 내부 상태 전환용 공용 먹 와이프: Hide → midAction → Reveal.
        /// 스택 항해(Navigate)와 IsTransitioning 잠금을 공유해 중복 전환을 막고,
        /// 와이프 중 입력 차단은 전환 오버레이(레이캐스트)가 담당한다.
        /// hideMask는 이번 Hide 1회에만 적용된다 (스택 전환 기본값은 불변).
        /// 이터레이터가 아닌 즉시 평가 래퍼 — 호출 프레임에 잠금이 성립해
        /// 중첩 코루틴의 지연 시작 프레임 동안에도 중복 전환이 끼어들 수 없다.
        /// </summary>
        public IEnumerator PlayWipe(Action midAction, InkMaskKind hideMask = InkMaskKind.SweepDiag)
        {
            if (IsTransitioning)
            {
                Debug.LogWarning("화면 전환 중 중복 와이프 요청을 무시했습니다.");
                return EmptyRoutine();
            }
            IsTransitioning = true;
            return RunWipe(midAction, hideMask);
        }

        private IEnumerator RunWipe(Action midAction, InkMaskKind hideMask)
        {
            var ink = _transition as InkWipeTransition;
            if (ink != null) ink.SetNextHideMask(hideMask);
            yield return _transition.Hide();
            midAction?.Invoke();
            yield return _transition.Reveal();
            IsTransitioning = false;
        }

        private static IEnumerator EmptyRoutine()
        {
            yield break;
        }

        public IEnumerator PushScreen(IScreen screen)
        {
            if (IsTransitioning)
            {
                Debug.LogWarning("화면 전환 중 중복 내비게이션 요청을 무시했습니다.");
                yield break;
            }

            IsTransitioning = true;
            yield return Screens.Push(screen, _transition);
            IsTransitioning = false;
        }

        public IEnumerator PopScreen()
        {
            if (IsTransitioning)
            {
                Debug.LogWarning("화면 전환 중 중복 내비게이션 요청을 무시했습니다.");
                yield break;
            }

            IsTransitioning = true;
            yield return Screens.Pop(_transition);
            IsTransitioning = false;
        }

        // ── 타이틀 ──────────────────────────────────────────────

        /// <summary>새 게임 시작. seedOverride는 테스트/디버그용 (없으면 랜덤 런 시드).</summary>
        public void StartNewGame(int? seedOverride = null)
        {
            _pendingRunSeed = seedOverride ?? UnityEngine.Random.Range(0, int.MaxValue);
            Navigate(Screens.Replace(new CharacterSelectScreen(), _transition));
        }

        /// <summary>이어하기: 세이브 로드 후 런으로 직행.</summary>
        public void ContinueRun()
        {
            var state = SaveSystem.Load();
            if (state == null) return; // 세이브 소실 — 타이틀 유지
            AdoptRun(RunController.FromState(state));
            Navigate(Screens.Replace(new RunScreen(), _transition));
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ── 캐릭터 선택 → 스토리 → 튜토리얼 → 런 ─────────────────

        public void ConfirmCharacter(string characterId)
        {
            var run = RunController.StartNew(_pendingRunSeed, characterId);
            AdoptRun(run);
            Navigate(Screens.Replace(new StoryScreen(), _transition));
        }

        public void CompleteStory() => Navigate(Screens.Replace(new TutorialScreen(), _transition));

        public void CompleteTutorial() => Navigate(Screens.Replace(new RunScreen(), _transition));

        // ── 런 → 엔딩 → 타이틀 ──────────────────────────────────

        /// <summary>RunScreen이 RunEnded를 처리한 뒤 호출한다. 세이브는 RunEnded 시점에 이미 삭제됨.</summary>
        public void ShowEnding(RunEnding ending)
        {
            CurrentRun = null;
            Navigate(Screens.Replace(new EndingScreen(ending), _transition));
        }

        /// <summary>타이틀 복귀. 진행 중인 런이 있으면 자동 저장한다.</summary>
        public void ReturnToTitle()
        {
            if (CurrentRun != null && !CurrentRun.IsOver)
                SaveSystem.Save(CurrentRun.State);
            CurrentRun = null;
            Navigate(Screens.Replace(new TitleScreen(), _transition));
        }

        private void AdoptRun(RunController run)
        {
            CurrentRun = run;
            run.DayChanged += _ => SaveSystem.Save(run.State); // 자동 저장: 하루 전진 시
            run.RunEnded += _ => SaveSystem.Delete();          // 엔딩 도달: 세이브 삭제
        }

        // ── 내비게이션 직렬화 ────────────────────────────────────

        private void Navigate(IEnumerator navigation)
        {
            if (IsTransitioning)
            {
                Debug.LogWarning("화면 전환 중 중복 내비게이션 요청을 무시했습니다.");
                return;
            }
            IsTransitioning = true;
            StartCoroutine(RunNavigation(navigation));
        }

        private IEnumerator RunNavigation(IEnumerator navigation)
        {
            yield return navigation;
            IsTransitioning = false;
        }

        // ── 부트스트랩 보조 (HwatuPrototype 쪽 GameController와 동일 정책) ──

        private static ITransition CreateDefaultTransition()
        {
            return DefaultTransitionFactory != null
                ? DefaultTransitionFactory()
                : new InkWipeTransition();
        }

        private static void EnsureCamera()
        {
            if (Camera.main != null) return;
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = UIStyles.Ash;
            cam.orthographic = true;
            cam.cullingMask = 0;
        }

        private static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }
    }
}
