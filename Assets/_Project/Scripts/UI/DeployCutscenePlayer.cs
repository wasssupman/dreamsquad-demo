using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Wassup.DepthParallax;

namespace Wassup.UI
{
    // defender-deploy-cutscene unit 2 — 드래그 배치 스와이프 시 화면 좌하단에 유닛 컷신을
    // 원샷 스프라이트 플립북으로 재생한다. 로비 캐릭터의 스프라이트 애니메이션 개념(프레임을
    // UI Image 에 순차 표시)을 참고하되, 원샷이라 Animator 없이 코루틴으로 프레임을 넘긴다.
    //
    // 연출: 화면 왼쪽 '바깥'에서 빠르게 슬라이드-인하며 동시에 플립북 재생 → 스와이프(드래그)가
    // 끝날 때까지 계속 유지 → 왼쪽으로 슬라이드-아웃 후 숨김. 세로는 좌하단 고정.
    // 수명: 컷신은 드래그 세션에 묶인다 — CleanupSession(드롭/취소)이 EndCutscene() 로 슬라이드-아웃 트리거.
    //       (rev — 기존 "1초 hold 후 독립 소멸"에서 "스와이프 종료 시 소멸"로 변경.)
    // 시간은 Time.unscaledDeltaTime — 드래그 배틀 슬로우모/일시정지 영향 배제.
    public class DeployCutscenePlayer : MonoBehaviour
    {
        [SerializeField] private float holdSecondsAfter = 100000000f; // 사실상 무한 유지 — 스와이프 종료 시 EndCutscene 로 탈출
        [SerializeField] private float displayScale = 1.2f;     // 스프라이트 네이티브 대비 공유 배율
        [SerializeField] private Vector2 cornerMarginPx = new Vector2(-100f, 24f); // 좌하단 공유 baseline 여백(px). 유닛별 offset 이 더해짐. x=이미지 왼쪽끝 위치, y=하단에서 위로
        [SerializeField] private float slideInSeconds = 0.18f;  // 왼쪽 밖→목표 진입(빠르게)
        [SerializeField] private float slideOutSeconds = 0.18f; // 목표→왼쪽 밖 퇴장
        [SerializeField] private float offscreenMarginPx = 48f; // 화면 밖 여분(완전히 가려지도록)
        [SerializeField] private int sortingOrder = 20050;      // 드래그 프리뷰/거부 라벨 위

        // depth-parallax unit 6 — 틸트 스프링/진폭/persp 등 시각 튜너블(모듈 SO). 미할당이면 기본 인스턴스 폴백.
        // 유닛별 틸트 게인은 컨트롤러(unit 7)가 SetTilt 전에 곱하므로 플레이어는 게인을 모른다.
        [SerializeField] private DepthParallaxSettings depthParallaxSettings;

        private Canvas _canvas;
        private GameObject _canvasGO;
        private Image _image;
        private RectTransform _rt;
        private Coroutine _routine;
        private bool _endRequested;   // 스와이프(드래그) 종료 시 컨트롤러가 세팅 → hold 탈출 → 슬라이드-아웃

        // depth-parallax unit 6 — per-instance 패럴랙스 머티리얼 + 틸트 스프링 상태(플레이어 소유).
        private Material _parallaxMat;
        private Vector2 _tiltTarget, _tilt, _tiltVel;
        private float _lastFeedUnscaled;
        private DepthParallaxSettings _cfgFallback;
        private DepthParallaxSettings Cfg => depthParallaxSettings != null
            ? depthParallaxSettings
            : (_cfgFallback != null ? _cfgFallback : (_cfgFallback = ScriptableObject.CreateInstance<DepthParallaxSettings>()));

        // 기존 4-arg 호출부 하위호환 — 뎁스 없이 색만 재생(그레이스풀).
        public void Play(Sprite[] frames, float fps, float unitScale = 1f, Vector2 unitOffset = default)
            => Play(frames, null, fps, unitScale, unitOffset);

        // depth-parallax unit 6 — 색 프레임과 뎁스 프레임을 lockstep 스왑. depth==null → 기존 동작.
        // depth 길이 1 = 전 프레임 공유(정적 단일 뎁스, 기본), 길이 N = 프레임별. 틸트 게인은
        // 컨트롤러가 SetTilt 전에 곱하므로 플레이어는 게인을 모른다(unit 7).
        public void Play(Sprite[] color, Texture2D[] depth, float fps, float unitScale, Vector2 unitOffset)
        {
            if (color == null || color.Length == 0 || fps <= 0f) return;

            EnsureCanvas();
            if (_routine != null) StopCoroutine(_routine); // 재생 중 재트리거 = 처음부터 재시작
            _canvasGO.SetActive(true);
            _endRequested = false; // 새 드래그 시작 — 스와이프 끝날 때까지 유지
            _routine = StartCoroutine(PlayRoutine(color, depth, fps, unitScale, unitOffset));
        }

        // depth-parallax unit 6 — 컨트롤러(unit 7)가 매 프레임 스와이프 방향을 피드. target 갱신 +
        // 타임스탬프로 staleness watchdog(무피드 → 0 복귀) 구동. 게인은 컨트롤러가 이미 곱해서 넘긴다.
        public void SetTilt(Vector2 t)
        {
            _tiltTarget = t;
            _lastFeedUnscaled = Time.unscaledTime;
        }

        // depth-parallax — DefenderSelector 가 runtime 생성 후 튜닝 SO 주입(선택). 미주입이면 fallback 기본값.
        // null 이면 무시(Cfg 폴백 유지). 라이브 튜닝하려면 DefenderSelector 에 .asset 할당.
        public void SetSettings(DepthParallaxSettings s) { if (s != null) depthParallaxSettings = s; }

        // 스와이프(드래그) 종료 시 컨트롤러의 CleanupSession 이 호출 → hold 종료하고 슬라이드-아웃.
        // 재생/슬라이드-인 도중 호출돼도 안전(다음 yield 에서 현재 위치부터 퇴장).
        public void EndCutscene() { _endRequested = true; }

        private IEnumerator PlayRoutine(Sprite[] frames, Texture2D[] depth, float fps, float unitScale, Vector2 unitOffset)
        {
            int first = FirstNonNull(frames, 0);
            if (first < 0) { Hide(); yield break; }

            // 첫 프레임으로 크기 확정(슬라이드 오프셋 계산에 폭이 필요).
            _image.sprite = frames[first];
            if (depth != null && depth.Length > 0 && _parallaxMat != null)
            {
                int di = Mathf.Min(first, depth.Length - 1);
                if (depth[di] != null) _parallaxMat.SetTexture("_DepthTex", depth[di]);
            }
            _image.SetNativeSize();
            _rt.sizeDelta *= displayScale * (unitScale > 0f ? unitScale : 1f);

            // 공유 baseline + 유닛별 오프셋. 좌하단 앵커라 y 양수 = 하단에서 위로.
            Vector2 target = new Vector2(cornerMarginPx.x + unitOffset.x, cornerMarginPx.y + unitOffset.y);
            // 좌하단 앵커/피벗(0,0) 기준: x = -폭이면 오른쪽 끝이 화면 왼쪽 경계. 여분 더해 완전히 가림.
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
                    // 색과 lockstep 으로 뎁스 스왑(desync 방지). 길이 클램프 → 길이 1 이면 고정.
                    if (depth != null && depth.Length > 0 && _parallaxMat != null)
                    {
                        int di = Mathf.Min(idx, depth.Length - 1);
                        if (depth[di] != null) _parallaxMat.SetTexture("_DepthTex", depth[di]);
                    }
                }
                yield return null;
            }
            _rt.anchoredPosition = target;

            // Phase B: 유지 — holdSecondsAfter(사실상 무한)까지, 단 스와이프 종료(EndCutscene)면 즉시 탈출.
            // 유지 중에도 SetTilt/틸트 스프링은 계속 동작(스와이프 방향 틸트).
            float hold = 0f;
            while (hold < holdSecondsAfter && !_endRequested) { hold += Time.unscaledDeltaTime; yield return null; }

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
            // 좌하단 앵커/피벗.
            _rt.anchorMin = _rt.anchorMax = new Vector2(0f, 0f);
            _rt.pivot = new Vector2(0f, 0f);

            _image = imgGO.AddComponent<Image>();
            _image.raycastTarget = false;      // 입력 통과(드래그 방해 금지)
            _image.preserveAspect = true;

            // depth-parallax unit 6 — per-instance 패럴랙스 머티리얼 주입(선례 = UiCardFaceMesh).
            // 셰이더 없으면 기본 머티리얼 유지 = 색만(그레이스풀). 정적 파라미터는
            // 생성 시 1회 push, 이후 틸트/뎁스만 SetVector/SetTexture(런타임 머티리얼 스왑 금지).
            //
            // ⚠️ Shader.Find 는 **빌드에 포함된 셰이더만** 찾는다. 이 셰이더는 씬의 머티리얼이 참조하지
            // 않으므로(런타임 생성) GraphicsSettings 의 Always Included Shaders 에 등록돼 있어야 한다.
            // 빠지면 기기에서만 null → 아래 폴백이 조용히 삼켜 "에디터는 되는데 빌드만 효과 없음"이 된다.
            // 실제로 그렇게 한 번 출시됐다(2026-07-15). 그래서 폴백은 반드시 경고를 남긴다.
            var sh = Shader.Find("Wassup/UI/DepthParallax");
            if (sh == null)
            {
                Debug.LogWarning("DeployCutscenePlayer: 'Wassup/UI/DepthParallax' 셰이더를 못 찾음 — 패럴랙스 없이 색만 재생. " +
                                 "빌드라면 GraphicsSettings > Always Included Shaders 에 등록됐는지 확인할 것(스트리핑).", this);
            }
            if (sh != null)
            {
                _parallaxMat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
                var cfg = Cfg;
                _parallaxMat.SetFloat("_Amplitude", cfg.amplitude);
                _parallaxMat.SetFloat("_DepthCenter", cfg.depthCenter);
                _parallaxMat.SetFloat("_DepthSign", cfg.depthSign);
                _parallaxMat.SetFloat("_Persp", cfg.perspective);
                _parallaxMat.SetFloat("_HiStrength", cfg.highlightStrength);
                _parallaxMat.SetFloat("_HiWidth", cfg.highlightWidth);
                _parallaxMat.SetVector("_Tilt", Vector2.zero);
                _image.material = _parallaxMat;
            }
        }

        // depth-parallax unit 6 — 틸트 스프링을 플레이어가 소유(독립 수명). 머티리얼은 첫 Play 의
        // EnsureCanvas 에서 lazy 생성되므로 그 전(씬 로드~첫 드래그)엔 null → 맨 앞에서 가드해
        // 매 프레임 NRE + 무의미한 idle 적분을 막는다. 시간은 unscaled(드래그 슬로우모/일시정지 무관).
        private void Update()
        {
            if (_parallaxMat == null) return;

            // staleness watchdog: 무피드 > tiltStalenessSeconds → target 0(릴리즈). 드래그가 컷신
            // 중간에 끝나도 플레이어는 드래그 수명을 몰라도 자동으로 0 으로 복귀.
            var cfg = Cfg;
            if (Time.unscaledTime - _lastFeedUnscaled > cfg.tiltStalenessSeconds) _tiltTarget = Vector2.zero;

            DepthParallaxMath.SpringStep(ref _tilt, ref _tiltVel, _tiltTarget,
                cfg.tiltSpring, cfg.tiltDamping, cfg.tiltMaxSpeed, Time.unscaledDeltaTime);
            _parallaxMat.SetVector("_Tilt", _tilt);
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
            if (_parallaxMat != null) Destroy(_parallaxMat); // per-instance 머티리얼 Dispose(leak 방지)
        }
    }
}
