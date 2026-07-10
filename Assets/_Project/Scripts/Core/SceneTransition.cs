using System.Collections;
using Spine.Unity;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
        [Tooltip("스파인 로딩 화면에서 배틀로 걷어내는 페이드.")]
        [SerializeField] private float coverFadeOut = 0.3f;

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
        [Tooltip("디졸브가 드러내는 로딩 캐릭터. Null 이면 로딩 화면 생략.")]
        [SerializeField] private SkeletonGraphic loadingSpine;
        [SerializeField] private CanvasGroup loadingSpineGroup;
        [SerializeField] private string loadingAnimation = "Run";

        public static SceneTransition Instance { get; private set; }

        private Material _runtimeMat;
        private bool _night;
        private bool _transitioning;

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

            // front = the on-screen time-of-day, so its opaque snap over the lobby is
            // imperceptible. Synced from LobbyBackgroundDissolve (toggled by character
            // touches); own toggled flag as fallback in scenes without it.
            var lobbyBg = Object.FindFirstObjectByType<Wassup.UI.LobbyBackgroundDissolve>();
            if (lobbyBg != null) _night = lobbyBg.IsNight;
            frontImage.sprite = _night ? nightSprite : daySprite;

            _runtimeMat.SetFloat(DissolveId, 0f);
            if (coverGroup != null) coverGroup.alpha = 1f;   // opaque snap, no fade-in
            ApplyRadialUniforms(centerUv);

            // The loading screen (spine over the dark backdrop) sits behind the front
            // cover, already running — the dissolve REVEALS it from the click point.
            if (loadingSpineGroup != null) loadingSpineGroup.alpha = 1f;
            if (loadingSpine != null && loadingSpine.AnimationState != null && !string.IsNullOrEmpty(loadingAnimation))
                loadingSpine.AnimationState.SetAnimation(0, loadingAnimation, true);

            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            op.allowSceneActivation = false;
            float loadStart = Time.unscaledTime;

            // Front dissolves away from the click point, revealing the spine loading screen.
            yield return Dissolve(0f, 1f, swapDuration);

            while (op.progress < 0.9f) yield return null;
            while (Time.unscaledTime - loadStart < minLoadingSeconds) yield return null;   // 계약 #5

            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;

            // Reveal the loaded scene from behind the cover (dark backdrop + spine fade out).
            yield return FadeGroup(coverGroup, 1f, 0f, coverFadeOut);

            if (loadingSpineGroup != null) loadingSpineGroup.alpha = 0f;
            _night = !_night;   // flag toggles each transition (fallback)
            if (coverGroup != null) coverGroup.blocksRaycasts = false;
            _transitioning = false;
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
