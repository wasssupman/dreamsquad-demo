using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Wassup.UI
{
    // defender-deploy-cutscene unit 2 — 드래그 배치 스와이프 시 화면 좌상단에 유닛 컷신을
    // 원샷 스프라이트 플립북으로 재생한다. 로비 캐릭터의 스프라이트 애니메이션 개념(프레임을
    // UI Image 에 순차 표시)을 참고하되, 원샷이라 Animator 없이 코루틴으로 프레임을 넘긴다.
    //
    // 연출: 화면 왼쪽 '바깥'에서 빠르게 슬라이드-인하며 동시에 플립북 재생 → holdSecondsAfter
    // 유지 → 왼쪽으로 슬라이드-아웃 후 숨김. 세로는 좌상단 고정.
    // 독립 재생: 재생은 드래그 세션 수명과 분리된다(드롭/취소가 중단하지 않음).
    // 시간은 Time.unscaledDeltaTime — 드래그 배틀 슬로우모/일시정지 영향 배제.
    public class DeployCutscenePlayer : MonoBehaviour
    {
        [SerializeField] private float holdSecondsAfter = 1f;   // 애니 종료 후 유지(초)
        [SerializeField] private float displayScale = 1f;       // 스프라이트 네이티브(640x360) 대비 배율
        [SerializeField] private Vector2 cornerMarginPx = new Vector2(-100f, 24f); // 좌상단 공유 baseline 여백(px). 유닛별 offset 이 더해짐. x=이미지 왼쪽끝 위치
        [SerializeField] private float slideInSeconds = 0.18f;  // 왼쪽 밖→목표 진입(빠르게)
        [SerializeField] private float slideOutSeconds = 0.18f; // 목표→왼쪽 밖 퇴장
        [SerializeField] private float offscreenMarginPx = 48f; // 화면 밖 여분(완전히 가려지도록)
        [SerializeField] private int sortingOrder = 20050;      // 드래그 프리뷰/거부 라벨 위

        private Canvas _canvas;
        private GameObject _canvasGO;
        private Image _image;
        private RectTransform _rt;
        private Coroutine _routine;

        public void Play(Sprite[] frames, float fps, float unitScale = 1f, Vector2 unitOffset = default)
        {
            if (frames == null || frames.Length == 0 || fps <= 0f) return;

            EnsureCanvas();
            if (_routine != null) StopCoroutine(_routine); // 재생 중 재트리거 = 처음부터 재시작
            _canvasGO.SetActive(true);
            _routine = StartCoroutine(PlayRoutine(frames, fps, unitScale, unitOffset));
        }

        private IEnumerator PlayRoutine(Sprite[] frames, float fps, float unitScale, Vector2 unitOffset)
        {
            int first = FirstNonNull(frames, 0);
            if (first < 0) { Hide(); yield break; }

            // 첫 프레임으로 크기 확정(슬라이드 오프셋 계산에 폭이 필요).
            _image.sprite = frames[first];
            _image.SetNativeSize();
            _rt.sizeDelta *= displayScale * (unitScale > 0f ? unitScale : 1f);

            // 공유 baseline + 유닛별 오프셋. y 는 아래 방향이 음수라 -cornerMarginPx.y 후 offset.y 더함.
            Vector2 target = new Vector2(cornerMarginPx.x + unitOffset.x, -cornerMarginPx.y + unitOffset.y);
            // 좌상단 앵커/피벗(0,1) 기준: x = -폭이면 오른쪽 끝이 화면 왼쪽 경계. 여분 더해 완전히 가림.
            Vector2 offscreen = new Vector2(-(_rt.sizeDelta.x + offscreenMarginPx), target.y);
            _rt.anchoredPosition = offscreen;

            float frameInterval = 1f / fps;
            float totalAnim = frames.Length * frameInterval;

            // Phase A: 플립북 재생 + 슬라이드-인 동시 진행.
            float t = 0f;
            int shown = -1;
            while (t < totalAnim)
            {
                t += Time.unscaledDeltaTime;
                float sIn = slideInSeconds > 0f ? Mathf.Clamp01(t / slideInSeconds) : 1f;
                _rt.anchoredPosition = Vector2.LerpUnclamped(offscreen, target, EaseOut(sIn));

                int idx = Mathf.Min(frames.Length - 1, (int)(t / frameInterval));
                if (idx != shown)
                {
                    shown = idx;
                    if (frames[idx] != null) _image.sprite = frames[idx];
                }
                yield return null;
            }
            _rt.anchoredPosition = target;

            // Phase B: 유지.
            float hold = 0f;
            while (hold < holdSecondsAfter) { hold += Time.unscaledDeltaTime; yield return null; }

            // Phase C: 왼쪽으로 슬라이드-아웃.
            float o = 0f;
            while (slideOutSeconds > 0f && o < slideOutSeconds)
            {
                o += Time.unscaledDeltaTime;
                float sOut = Mathf.Clamp01(o / slideOutSeconds);
                _rt.anchoredPosition = Vector2.LerpUnclamped(target, offscreen, EaseIn(sOut));
                yield return null;
            }

            _routine = null;
            Hide();
        }

        private static int FirstNonNull(Sprite[] frames, int from)
        {
            for (int i = from; i < frames.Length; i++)
                if (frames[i] != null) return i;
            return -1;
        }

        // 진입은 감속(EaseOut), 퇴장은 가속(EaseIn)으로 빠른 등장/퇴장 느낌.
        private static float EaseOut(float s) => 1f - (1f - s) * (1f - s);
        private static float EaseIn(float s) => s * s;

        private void EnsureCanvas()
        {
            if (_canvasGO != null) return;

            // 루트 오브젝트로 생성(부모 없음). nested Canvas 는 루트 Canvas 의 renderMode/
            // 좌표계를 상속하므로, DefenderSelector(자체 Canvas) 아래에 두면 화면이 아니라
            // 그 rect 기준으로 렌더된다 → 반드시 root ScreenSpaceOverlay 로 둔다.
            _canvasGO = new GameObject("DeployCutsceneCanvas", typeof(Canvas));
            _canvas = _canvasGO.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = sortingOrder;

            var imgGO = new GameObject("CutsceneImage", typeof(RectTransform));
            imgGO.transform.SetParent(_canvasGO.transform, false);
            _rt = (RectTransform)imgGO.transform;
            // 좌상단 앵커/피벗.
            _rt.anchorMin = _rt.anchorMax = new Vector2(0f, 1f);
            _rt.pivot = new Vector2(0f, 1f);

            _image = imgGO.AddComponent<Image>();
            _image.raycastTarget = false;      // 입력 통과(드래그 방해 금지)
            _image.preserveAspect = true;
        }

        private void Hide()
        {
            if (_canvasGO != null) _canvasGO.SetActive(false); // 재사용 위해 파괴하지 않음
        }

        private void StopAndHide()
        {
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            Hide();
        }

        private void OnDisable() => StopAndHide();

        private void OnDestroy()
        {
            if (_canvasGO != null) Destroy(_canvasGO);
        }
    }
}
