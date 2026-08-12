using System.Collections;
using System.Collections.Generic;
using Spine;
using Spine.Unity;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Wassup.Data;
using Wassup.Presentation;

namespace Wassup.Core
{
    // scene-transition unit 0/2 — persistent, self-bootstrapping scene-transition
    // controller. Single public entry point: static Go(sceneName). Runs on a
    // DontDestroyOnLoad canvas so the cover survives the very scene swap it hides.
    //
    // unit 2 flow: front (current time-of-day bg, dissolve material) snaps opaque —
    // imperceptible because it matches the lobby backdrop — then dissolves away with
    // the lobby's radial-golden wavefront (Wassup/UI/BackgroundDissolve) from the click
    // point, REVEALING the spine loading screen behind it: a Casual Character
    // SkeletonGraphic running over a solid-dark backdrop (under). BattleScene streams
    // during the reveal; the cover then fades out to the loaded scene. Global gold tint
    // is off (goldenTintStrength 0) so only the wavefront glow spreads from the button.
    // Values/assets are authored on the Resources/SceneTransition prefab (제약 #6).
    public class SceneTransition : MonoBehaviour
    {
        private const string ResourcePath = "SceneTransition";

        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        private static readonly int ModeId = Shader.PropertyToID("_Mode");
        private static readonly int InvertId = Shader.PropertyToID("_Invert");
        private static readonly int CenterId = Shader.PropertyToID("_Center");
        private static readonly int MaxRadiusId = Shader.PropertyToID("_MaxRadius");
        private static readonly int AspectId = Shader.PropertyToID("_Aspect");
        private static readonly int TintStrengthId = Shader.PropertyToID("_TintStrength");

        [Header("Timing (unscaled)")]
        [Tooltip("front 이 걷히며 스파인 로딩 화면을 드러내는 골든 라디얼 디졸브 시간.")]
        [SerializeField] private float swapDuration = 1.2f;
        [Tooltip("스파인 로딩 화면 노출 시간(로드 시작 기준, 로딩이 즉시 끝나도 이만큼 고정 노출).")]
        [SerializeField] private float minLoadingSeconds = 2f;
        [Tooltip("로딩 화면에서 다음 씬으로 걷어내는 페이드.")]
        [SerializeField] private float coverFadeOut = 0.3f;
        [Tooltip("배경 디졸브가 없는 방향(예: 배틀→로비)에서 로딩 화면을 띄우는 페이드인.")]
        [SerializeField] private float loadingFadeIn = 0.3f;

        [Header("Dissolve cover")]
        [SerializeField] private CanvasGroup coverGroup;
        [Tooltip("현재 시간대 배경(dissolve 머티리얼). 걷히며 로딩 화면을 드러냄.")]
        [SerializeField] private Image frontImage;
        [Tooltip("드러나는 로딩 배경(단색 다크).")]
        [SerializeField] private Image underImage;
        [Tooltip("Wassup/UI/BackgroundDissolve 머티리얼(로비와 동일). 런타임 인스턴스로 사용.")]
        [SerializeField] private Material dissolveMaterial;
        [SerializeField] private Sprite daySprite;
        [SerializeField] private Sprite nightSprite;
        [SerializeField] private bool startNight = true;
        [Range(0f, 1f)]
        [Tooltip("전역 골든 틴트(front 전체를 물들임). 0=파면 글로우만 버튼에서 퍼짐.")]
        [SerializeField] private float goldenTintStrength = 0f;
        [Tooltip("포인터를 못 읽을 때 라디얼 확산 중심 (UV).")]
        [SerializeField] private Vector2 fallbackCenter = new Vector2(0.5f, 0.35f);

        [Header("Spine loading screen")]
        [Tooltip("함께 뛰어가는 로딩 러너들. 확정 스쿼드에서 뽑은 유닛 외형으로 스킨을 갈아끼운다. 비어 있으면 로딩 화면 생략.")]
        [SerializeField] private SkeletonGraphic[] loadingRunners;
        [Tooltip("러너 전체 페이드를 제어하는 그룹(LoadingRunners 부모의 CanvasGroup).")]
        [SerializeField] private CanvasGroup loadingSpineGroup;
        [SerializeField] private string loadingAnimation = "Run";

        [Header("Loading squad source")]
        [Tooltip("로비/배틀이 공유하는 in-memory 프로파일. CommittedSquad() 로 러너 외형을 뽑는다.")]
        [SerializeField] private PlayerProfileSO profileSO;
        [Tooltip("스쿼드 unitId → DefenderUnitData 해석용 카탈로그.")]
        [SerializeField] private DefenderCatalog catalog;

        public static SceneTransition Instance { get; private set; }

        private Material _runtimeMat;
        private bool _night;
        private bool _transitioning;
        // Authored skin per runner, captured before any squad injection so the
        // no-squad fallback can restore the generic look (data-driven, no literal).
        private string[] _runnerDefaultSkins;
        // 저작된 리그. unit 8 이후 러너의 skeletonDataAsset 이 유닛마다 교체되므로,
        // 스킨만 되돌리면 «마지막 유닛 리그 + 저작 스킨» 이라는 없는 조합이 된다
        // (고유 리그엔 그 스킨이 없어 FindSkin 이 null → 복원이 조용히 no-op).
        // SceneTransition 은 DontDestroyOnLoad 라 그 상태가 앱 수명 동안 남는다.
        private SkeletonDataAsset[] _runnerDefaultDataAssets;
        private Vector3[] _runnerBaseScales;   // 저작 스케일(임시 리그 보정의 기준)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var prefab = Resources.Load<GameObject>(ResourcePath);
            if (prefab == null)
            {
                Debug.LogError($"[SceneTransition] Resources/{ResourcePath}.prefab missing — transitions hard-cut.");
                return;
            }
            Instantiate(prefab);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);   // 계약 #3 — single persistent instance
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _night = startNight;
            if (frontImage != null && dissolveMaterial != null)
            {
                _runtimeMat = Instantiate(dissolveMaterial);
                _runtimeMat.SetFloat(ModeId, 1f);               // radial
                _runtimeMat.SetFloat(InvertId, 0f);             // wavefront 중심→밖 (버튼에서 퍼짐)
                _runtimeMat.SetFloat(TintStrengthId, goldenTintStrength);
                _runtimeMat.SetFloat(DissolveId, 0f);
                frontImage.material = _runtimeMat;
            }
            if (loadingSpineGroup != null) loadingSpineGroup.alpha = 0f;
            if (coverGroup != null)
            {
                coverGroup.alpha = 0f;                           // rest = fully hidden
                coverGroup.blocksRaycasts = false;
            }
            CaptureRunnerDefaults();
        }

        private void CaptureRunnerDefaults()
        {
            if (loadingRunners == null) return;
            _runnerDefaultSkins = new string[loadingRunners.Length];
            _runnerDefaultDataAssets = new SkeletonDataAsset[loadingRunners.Length];
            _runnerBaseScales = new Vector3[loadingRunners.Length];
            for (int i = 0; i < loadingRunners.Length; i++)
            {
                _runnerDefaultSkins[i] = loadingRunners[i] != null ? loadingRunners[i].InitialSkinName : null;
                _runnerDefaultDataAssets[i] = loadingRunners[i] != null ? loadingRunners[i].skeletonDataAsset : null;
                // 저작된 스케일을 1회 캡처한다 — 보정을 매번 곱하면 로드마다 누적된다.
                _runnerBaseScales[i] = loadingRunners[i] != null
                    ? loadingRunners[i].transform.localScale : Vector3.one;
            }
        }

        // 임시 리그 크기 보정(DefenderUnitData.outgameScaleMul 주석 참조). 리그가 정규화되면
        // 이 메서드와 필드를 함께 지운다. unit 이 null 이면 저작값 그대로.
        private void ApplyRunnerScale(int index, DefenderUnitData unit)
        {
            if (_runnerBaseScales == null || index >= _runnerBaseScales.Length) return;
            var runner = loadingRunners[index];
            if (runner == null) return;
            float mul = unit != null ? Mathf.Max(0.01f, unit.outgameScaleMul) : 1f;
            runner.transform.localScale = _runnerBaseScales[index] * mul;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_runtimeMat != null) Destroy(_runtimeMat);
        }

        // Single public entry point. A null instance (missing bootstrap) degrades to
        // a hard cut so the player is never stranded on the current scene.
        public static void Go(string sceneName)
        {
            if (Instance == null)
            {
                Debug.LogWarning("[SceneTransition] No instance — hard-cut load.");
                SceneManager.LoadScene(sceneName);
                return;
            }
            Instance.BeginGo(sceneName);
        }

        private void BeginGo(string sceneName)
        {
            if (_transitioning) return;   // 계약 #2 — re-entrancy guard, no double load
            if (_runtimeMat == null || frontImage == null)
            {
                SceneManager.LoadScene(sceneName);   // degrade to instant load
                return;
            }
            _transitioning = true;
            StartCoroutine(Run(sceneName, ReadPointerCenterUv()));
        }

        private IEnumerator Run(string sceneName, Vector2 centerUv)
        {
            if (coverGroup != null) coverGroup.blocksRaycasts = true;

            // The bg dissolve only makes sense when the current scene owns a matching
            // day/night backdrop (the lobby). Leaving battle, there is nothing to
            // dissolve → just fade the loading screen in.
            var lobbyBg = Object.FindFirstObjectByType<Wassup.UI.LobbyBackgroundDissolve>();
            bool dissolveFromBg = lobbyBg != null;

            // Spine loading screen — resolve the confirmed squad into the runners and
            // start them running before they are shown, so they are already in motion
            // when revealed.
            ConfigureRunners();
            if (loadingSpineGroup != null) loadingSpineGroup.alpha = 1f;

            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            op.allowSceneActivation = false;
            float loadStart = Time.unscaledTime;

            if (dissolveFromBg)
            {
                // front = on-screen time-of-day (opaque snap is imperceptible over the
                // matching lobby backdrop); it dissolves away from the click point,
                // revealing the loading screen behind it.
                _night = lobbyBg.IsNight;
                frontImage.sprite = _night ? nightSprite : daySprite;
                _runtimeMat.SetFloat(DissolveId, 0f);
                ApplyRadialUniforms(centerUv);
                if (coverGroup != null) coverGroup.alpha = 1f;   // opaque snap, no fade-in
                yield return Dissolve(0f, 1f, swapDuration);
            }
            else
            {
                // Battle → lobby: no bg dissolve. front fully dissolved (transparent) so
                // only the dark backdrop + spine show; fade the loading screen in.
                _runtimeMat.SetFloat(DissolveId, 1f);
                if (coverGroup != null) coverGroup.alpha = 0f;
                yield return FadeGroup(coverGroup, 0f, 1f, loadingFadeIn);
            }

            while (op.progress < 0.9f) yield return null;
            while (Time.unscaledTime - loadStart < minLoadingSeconds) yield return null;   // 계약 #5

            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;

            // Reveal the loaded scene from behind the cover (backdrop + spine fade out).
            yield return FadeGroup(coverGroup, 1f, 0f, coverFadeOut);

            if (loadingSpineGroup != null) loadingSpineGroup.alpha = 0f;
            _night = !_night;   // flag toggles each transition (fallback)
            if (coverGroup != null) coverGroup.blocksRaycasts = false;
            _transitioning = false;
        }

        // Resolve the confirmed squad into the runner slots. Each runner shares the
        // one Casual Character rig, so this is a pure skin swap (SpineCombinedSkinCache).
        // A different random trio is chosen every load (user decision). No squad /
        // no skeleton-equipped units → runners keep their authored default look so the
        // loading screen is never empty.
        private void ConfigureRunners()
        {
            if (loadingRunners == null || loadingRunners.Length == 0) return;

            List<DefenderUnitData> units = BuildRunnerUnits(loadingRunners.Length);
            bool fallback = units.Count == 0;

            for (int i = 0; i < loadingRunners.Length; i++)
            {
                var runner = loadingRunners[i];
                if (runner == null) continue;

                if (i < units.Count)
                {
                    ApplyRunnerSkin(runner, units[i]);
                    ApplyRunnerScale(i, units[i]);
                    runner.gameObject.SetActive(true);
                }
                else if (fallback)
                {
                    RestoreRunnerDefault(runner, i);
                    ApplyRunnerScale(i, null);   // 저작값 복귀
                    runner.gameObject.SetActive(true);
                }
                else
                {
                    runner.gameObject.SetActive(false);   // squad smaller than the runner count
                }
            }
        }

        // Selected squad → shuffled, skeleton-equipped defenders, capped at the runner
        // count. Empty when no squad is selected or the catalog is missing.
        private List<DefenderUnitData> BuildRunnerUnits(int max)
        {
            var chosen = new List<DefenderUnitData>();
            var squad = profileSO != null && profileSO.profile != null ? profileSO.profile.CommittedSquad() : null;
            if (squad == null || catalog == null) return chosen;

            var ids = SquadDraw.Resolve(squad.unitIds);
            var pool = new List<DefenderUnitData>();
            for (int i = 0; i < ids.Count; i++)
            {
                var u = catalog.ById(ids[i]);
                if (u != null && u.SpineSkeletonDataAsset != null) pool.Add(u);
            }

            // Fisher–Yates — a different trio each load.
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }

            int n = Mathf.Min(max, pool.Count);
            for (int i = 0; i < n; i++) chosen.Add(pool[i]);
            return chosen;
        }

        private void ApplyRunnerSkin(SkeletonGraphic runner, DefenderUnitData unit)
        {
            var dataAsset = unit.SpineSkeletonDataAsset;
            if (dataAsset != null && runner.skeletonDataAsset != dataAsset)
            {
                // ⚠ 스켈레톤을 바꾸기 **전에** 초기 스킨을 그 유닛 것으로 바꿔야 한다.
                // Initialize 안의 AssignInitialSkin 이 씬에 저작된 이름(러너는 'full_skins')을
                // 새 스켈레톤에 적용하는데, 고유 리그(CH1 등)엔 그 스킨이 없어 SetSkin 이
                // ArgumentException 을 던지고 씬 전환 코루틴이 통째로 죽는다.
                // SpineUnitView.Spawn 과 **같은 관용구** — 'default' 는 AssignInitialSkin 이
                // 건너뛰므로 스킨이 하나뿐인 리그에서도 안전하다.
                runner.InitialSkinName =
                    string.IsNullOrEmpty(unit.SpineSkinName) ? "default" : unit.SpineSkinName;
                runner.skeletonDataAsset = dataAsset;
                runner.Initialize(true);
            }
            else if (runner.Skeleton == null)
            {
                runner.Initialize(false);
            }
            if (runner.Skeleton != null)
                SpineCombinedSkinCache.Apply(runner.Skeleton, unit);
            PlayRun(runner, unit);
        }

        private void RestoreRunnerDefault(SkeletonGraphic runner, int index)
        {
            // 리그부터 되돌린다 — 직전 전환이 고유 리그를 꽂아 뒀을 수 있다.
            var authoredData = _runnerDefaultDataAssets != null && index < _runnerDefaultDataAssets.Length
                ? _runnerDefaultDataAssets[index] : null;
            if (authoredData != null && runner.skeletonDataAsset != authoredData)
            {
                runner.InitialSkinName = _runnerDefaultSkins != null && index < _runnerDefaultSkins.Length
                    ? _runnerDefaultSkins[index] : null;
                runner.skeletonDataAsset = authoredData;
                runner.Initialize(true);
            }
            if (runner.Skeleton == null) runner.Initialize(false);
            string defaultSkin = _runnerDefaultSkins != null && index < _runnerDefaultSkins.Length
                ? _runnerDefaultSkins[index] : null;
            if (runner.Skeleton != null && runner.Skeleton.Data != null && !string.IsNullOrEmpty(defaultSkin))
            {
                var skin = runner.Skeleton.Data.FindSkin(defaultSkin);
                if (skin != null)
                {
                    runner.Skeleton.SetSkin(skin);
                    runner.Skeleton.SetupPoseSlots();
                }
            }
            PlayRun(runner);
        }

        // Loop the run animation, already in motion before the runner is revealed.
        // unit 은 null 일 수 있다(fallback 경로) — 그 땐 저작된 이름만 본다.
        private void PlayRun(SkeletonGraphic runner, DefenderUnitData unit = null)
        {
            var animation = runner.Animation as SkeletonAnimation;
            if (animation == null || animation.AnimationState == null || runner.Skeleton == null) return;
            // ⚠ AnimationState.SetAnimation(string) 은 애니가 없으면 **예외를 던진다**.
            // 러너는 이제 유닛마다 다른 리그를 쓸 수 있어(고유 스파인) 'Run' 이 없을 수 있다.
            string resolved = ResolveRunnerAnimation(runner.Skeleton.Data, loadingAnimation, unit);
            if (!string.IsNullOrEmpty(resolved))
                animation.AnimationState.SetAnimation(0, resolved, true);
        }

        // 러너가 실제로 재생할 수 있는 애니 이름. 저작된 달리기 → 유닛의 걷기 → 유닛의 대기 →
        // 관례 이름 순. 하나도 없으면 null 이고 호출측은 애니를 걸지 않는다(setup pose 유지).
        // 달릴 줄 모르는 리그(예: 소환사 CH1)는 서 있는 채로 나온다 — 로딩 화면이 비거나
        // 씬 전환이 죽는 것보다 낫다.
        internal static string ResolveRunnerAnimation(SkeletonData data, string authored, ISpineUnitVisualData unit)
        {
            if (data == null) return null;
            if (!string.IsNullOrEmpty(authored) && data.FindAnimation(authored) != null) return authored;
            if (unit != null)
            {
                if (!string.IsNullOrEmpty(unit.SpineWalkAnimation) && data.FindAnimation(unit.SpineWalkAnimation) != null)
                    return unit.SpineWalkAnimation;
                if (!string.IsNullOrEmpty(unit.SpineIdleAnimation) && data.FindAnimation(unit.SpineIdleAnimation) != null)
                    return unit.SpineIdleAnimation;
            }
            if (data.FindAnimation("idle") != null) return "idle";
            if (data.FindAnimation("Idle") != null) return "Idle";
            return null;
        }

        // 계약 #7 — all cover motion is unscaled, independent of TimeManager / pause.
        private IEnumerator Dissolve(float from, float to, float duration)
        {
            if (duration <= 0f) { _runtimeMat.SetFloat(DissolveId, to); yield break; }
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                _runtimeMat.SetFloat(DissolveId, Mathf.Lerp(from, to, Mathf.Clamp01(t / duration)));
                yield return null;
            }
            _runtimeMat.SetFloat(DissolveId, to);
        }

        private static IEnumerator FadeGroup(CanvasGroup group, float from, float to, float duration)
        {
            if (group == null) yield break;
            if (duration <= 0f) { group.alpha = to; yield break; }
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
                yield return null;
            }
            group.alpha = to;
        }

        private Vector2 ReadPointerCenterUv()
        {
            var pointer = Pointer.current;
            if (pointer == null) return fallbackCenter;
            Vector2 screen = pointer.position.ReadValue();
            if (Screen.width <= 0 || Screen.height <= 0) return fallbackCenter;
            return new Vector2(
                Mathf.Clamp01(screen.x / Screen.width),
                Mathf.Clamp01(screen.y / Screen.height));
        }

        private void ApplyRadialUniforms(Vector2 centerUv)
        {
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 1f;
            _runtimeMat.SetFloat(AspectId, aspect);
            _runtimeMat.SetVector(CenterId, centerUv);

            // Farthest corner from the center (aspect-corrected) = radius that fully covers.
            float maxR = 0f;
            for (int cx = 0; cx <= 1; cx++)
            for (int cy = 0; cy <= 1; cy++)
            {
                var d = new Vector2((cx - centerUv.x) * aspect, cy - centerUv.y);
                maxR = Mathf.Max(maxR, d.magnitude);
            }
            _runtimeMat.SetFloat(MaxRadiusId, maxR);
        }
    }
}
