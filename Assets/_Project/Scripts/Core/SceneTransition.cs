using System.Collections;
using PrimeTween;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Wassup.Core
{
    // scene-transition unit 0 — persistent, self-bootstrapping scene-transition
    // controller. Single public entry point: static Go(sceneName). Runs
    // cover-in → async load (activation gated) → cover-out on a DontDestroyOnLoad
    // canvas, so the cover overlay survives the very scene swap it is hiding.
    //
    // Values are authored on the Resources/SceneTransition prefab (no separate
    // settings SO — the prefab IS the authoring source, CLAUDE.md 제약 #6). Unit 2
    // fills the no-op PlayCutIn hook with a Spine SkeletonGraphic cut-in.
    public class SceneTransition : MonoBehaviour
    {
        private const string ResourcePath = "SceneTransition";

        [Header("Cover fade (unscaled time)")]
        [SerializeField] private float fadeInDuration = 0.35f;
        [SerializeField] private float fadeOutDuration = 0.35f;
        [Tooltip("화면이 완전히 가려진 상태를 최소 이만큼 유지 — 로딩이 즉시 끝나도 깜빡임 방지.")]
        [SerializeField] private float minCoverSeconds = 0.4f;
        [SerializeField] private Color coverColor = Color.black;

        [Header("Wired on the prefab")]
        [SerializeField] private CanvasGroup coverGroup;
        [SerializeField] private Image coverImage;

        public static SceneTransition Instance { get; private set; }

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

            if (coverImage != null) coverImage.color = coverColor;
            if (coverGroup != null)
            {
                coverGroup.alpha = 0f;
                coverGroup.blocksRaycasts = false;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
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
            _transitioning = true;
            StartCoroutine(Run(sceneName));
        }

        private IEnumerator Run(string sceneName)
        {
            var direction = ResolveDirection(sceneName);

            if (coverGroup != null) coverGroup.blocksRaycasts = true;
            yield return FadeCover(1f, fadeInDuration);

            // Cover is fully opaque here; the load's blank frame can never show
            // (계약 #4). Cut-in hook — unit 2 fills this, no-op today.
            yield return PlayCutIn(direction);

            var op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            op.allowSceneActivation = false;
            float coverStart = Time.unscaledTime;
            while (op.progress < 0.9f) yield return null;

            // Hold the cover at least minCoverSeconds so an instant load doesn't flash
            // (계약 #5).
            while (Time.unscaledTime - coverStart < minCoverSeconds) yield return null;

            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;

            yield return FadeCover(0f, fadeOutDuration);
            if (coverGroup != null) coverGroup.blocksRaycasts = false;
            _transitioning = false;
        }

        private IEnumerator FadeCover(float target, float duration)
        {
            if (coverGroup == null) yield break;
            // 계약 #7 — unscaled time, independent of TimeManager / battle pause.
            yield return Tween.Alpha(coverGroup, target, duration, Ease.Linear, useUnscaledTime: true)
                .ToYieldInstruction();
        }

        // Unit 2 replaces this no-op with SkeletonGraphic cut-in playback. Direction
        // is resolved and passed now so the hook shape is stable (공용 1벌부터).
        private IEnumerator PlayCutIn(TransitionDirection direction)
        {
            yield break;
        }

        private static TransitionDirection ResolveDirection(string sceneName)
        {
            if (sceneName == SceneNames.Battle) return TransitionDirection.IntoBattle;
            if (sceneName == SceneNames.Outgame) return TransitionDirection.ToLobby;
            return TransitionDirection.Other;
        }

        private enum TransitionDirection { IntoBattle, ToLobby, Other }
    }
}
