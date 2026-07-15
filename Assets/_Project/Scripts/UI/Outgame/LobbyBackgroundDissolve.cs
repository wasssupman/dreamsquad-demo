using UnityEngine;
using UnityEngine.UI;

namespace Wassup.UI
{
    // 로비 배경 낮/밤 전환. 앞 레이어(이 GO의 Image)가 선택된 스타일로 사라지면
    // 뒤 레이어(underImage)의 반대 시간대 배경이 드러나고, 완료 시 앞 레이어를 스왑 후 리셋.
    // 트리거: LobbyReactionLock.ReactionStarted — 어느 캐릭터든 터치 리액션이 시작되면
    // 전환이 일어나고, 원형 확산 중심은 트리거한 캐릭터 위치를 따른다. 전환 중 재트리거는 무시.
    // 스타일 비교는 인스펙터의 style 필드로. 공용 머티리얼 에셋 대신 런타임 인스턴스 사용.
    [RequireComponent(typeof(Image))]
    public class LobbyBackgroundDissolve : MonoBehaviour
    {
        // 셰이더 _Mode 매핑. RadialWithGoldenTint 는 Radial 모드 + 전체 틴트.
        public enum TransitionStyle
        {
            NoiseDissolve = 0,
            RadialFromCharacter = 1,
            HorizontalSweep = 2,
            GoldenCrossfade = 3,
            RadialWithGoldenTint = 4,
        }

        private static readonly int DissolveId = Shader.PropertyToID("_Dissolve");
        private static readonly int ModeId = Shader.PropertyToID("_Mode");
        private static readonly int CenterId = Shader.PropertyToID("_Center");
        private static readonly int MaxRadiusId = Shader.PropertyToID("_MaxRadius");
        private static readonly int AspectId = Shader.PropertyToID("_Aspect");
        private static readonly int TintStrengthId = Shader.PropertyToID("_TintStrength");
        private static readonly int TintColorId = Shader.PropertyToID("_TintColor");
        private static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
        // lobby-background-parallax unit 3 — 패럴랙스 유니폼(셰이더가 모듈 .cginc 의 Cue A 만 include).
        private static readonly int TiltId = Shader.PropertyToID("_Tilt");
        private static readonly int DepthTexId = Shader.PropertyToID("_DepthTex");
        private static readonly int AmplitudeId = Shader.PropertyToID("_Amplitude");
        private static readonly int DepthCenterId = Shader.PropertyToID("_DepthCenter");
        private static readonly int DepthSignId = Shader.PropertyToID("_DepthSign");

        [SerializeField] private Material dissolveMaterial;
        [SerializeField] private Image underImage;
        [SerializeField] private Sprite daySprite;
        [SerializeField] private Sprite nightSprite;
        [SerializeField] private bool startNight = true;
        [SerializeField] private float transitionDuration = 2f;
        [SerializeField] private TransitionStyle style = TransitionStyle.RadialWithGoldenTint;
        [SerializeField] private float goldenTintStrength = 0.4f;    // 크로스페이드/원형+틴트에서 사용
        [SerializeField] private Vector2 fallbackCenter = new Vector2(0.5f, 0.35f);

        // 방향별 색: night→day 는 밝은 새벽 금빛, day→night 는 초저녁의 옅은 어둠(남보라).
        // 밴드 색의 알파는 파면 글로우 혼합 강도로 쓰인다.
        [SerializeField] private Color dawnTintColor = new Color(1f, 0.84f, 0.52f);
        [SerializeField] private Color dawnBandColor = new Color(1f, 0.88f, 0.55f, 0.9f);
        [SerializeField] private Color duskTintColor = new Color(0.42f, 0.4f, 0.58f);
        [SerializeField] private Color duskBandColor = new Color(0.52f, 0.5f, 0.78f, 0.7f);

        private Image _image;
        private RectTransform _rt;
        private Material _runtimeMat;
        private bool _isNight;
        private float _progress = -1f; // < 0 이면 전환 중 아님
        private Transform _centerSource; // 이번 전환을 트리거한 캐릭터 (원형 확산 중심)

        public bool IsTransitioning => _progress >= 0f;

        // 현재 표시 중인 시간대. SceneTransition 이 씬 전환 커버를 실제 로비 배경과
        // 일치(현재→반대 스왑)시키기 위해 읽는다.
        public bool IsNight => _isNight;

        public TransitionStyle Style
        {
            get => style;
            set => style = value;
        }

        private void Awake()
        {
            _image = GetComponent<Image>();
            _rt = (RectTransform)transform;
            _runtimeMat = Instantiate(dissolveMaterial);
            _image.material = _runtimeMat;
            _isNight = startNight;
            _image.sprite = _isNight ? nightSprite : daySprite;
            underImage.sprite = _isNight ? daySprite : nightSprite;
            SetDissolve(0f);
            LobbyReactionLock.ReactionStarted += OnReactionStarted;
        }

        private void OnDestroy()
        {
            LobbyReactionLock.ReactionStarted -= OnReactionStarted;
            if (_runtimeMat != null) Destroy(_runtimeMat);
        }

        private void OnReactionStarted(Component owner)
        {
            _centerSource = owner != null ? owner.transform : null;
            StartTransition();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        // 전환 시작. 진행 중이면 무시. (잠금 이벤트 경유 + 검증 툴 직접 호출용 public)
        public void StartTransition()
        {
            if (IsTransitioning) return;
            underImage.sprite = _isNight ? daySprite : nightSprite;
            ApplyStyleUniforms();
            _progress = 0f;
        }

        // Update 에서 분리된 이유: 비포커스 에디터에선 프레임이 안 흘러, 에디터 검증 툴이
        // dt 를 직접 주입해 진행도를 전진시킬 수 있어야 한다.
        public void Tick(float dt)
        {
            if (!IsTransitioning) return;
            _progress += dt / transitionDuration;
            if (_progress >= 1f)
            {
                _isNight = !_isNight;
                _image.sprite = _isNight ? nightSprite : daySprite;
                SetDissolve(0f);
                _runtimeMat.SetFloat(TintStrengthId, 0f);
                _progress = -1f;
                return;
            }
            SetDissolve(_progress);
        }

        public void SetDissolve(float value)
        {
            _runtimeMat.SetFloat(DissolveId, Mathf.Clamp01(value));
        }

        // lobby-background-parallax unit 3 — 배경 패럴랙스 주입. 런타임 머티리얼 소유는 계속 디졸브가 갖고,
        // 외부(LobbyBackgroundParallax)는 값만 밀어넣는다. 앞(이 Image)과 뒤(underImage)가 같은
        // _Tilt/_DepthTex 를 받아야 낮/밤 전환 중에도 두 레이어가 어긋나지 않는다.
        // 정적 파라미터는 1회, 틸트는 매 프레임.
        public void SetParallaxParams(Texture depth, float amplitude, float depthCenter, float depthSign)
        {
            if (_runtimeMat == null) return;
            if (depth != null) _runtimeMat.SetTexture(DepthTexId, depth);
            _runtimeMat.SetFloat(AmplitudeId, amplitude);
            _runtimeMat.SetFloat(DepthCenterId, depthCenter);
            _runtimeMat.SetFloat(DepthSignId, depthSign);
        }

        public void SetParallaxTilt(Vector2 tilt)
        {
            if (_runtimeMat == null) return;
            _runtimeMat.SetVector(TiltId, tilt);
        }

        private void ApplyStyleUniforms()
        {
            float mode = style switch
            {
                TransitionStyle.NoiseDissolve => 0f,
                TransitionStyle.RadialFromCharacter => 1f,
                TransitionStyle.RadialWithGoldenTint => 1f,
                TransitionStyle.HorizontalSweep => 2f,
                TransitionStyle.GoldenCrossfade => 3f,
                _ => 1f,
            };
            float tint = style switch
            {
                TransitionStyle.GoldenCrossfade => goldenTintStrength,
                TransitionStyle.RadialWithGoldenTint => goldenTintStrength,
                _ => 0f,
            };
            _runtimeMat.SetFloat(ModeId, mode);
            _runtimeMat.SetFloat(TintStrengthId, tint);

            // 전환 방향별 색 (StartTransition 시점: _isNight == 아직 스왑 전 → true 면 day 로 가는 중)
            bool toDay = _isNight;
            _runtimeMat.SetColor(TintColorId, toDay ? dawnTintColor : duskTintColor);
            _runtimeMat.SetColor(EdgeColorId, toDay ? dawnBandColor : duskBandColor);

            Rect rect = _rt.rect;
            _runtimeMat.SetFloat(AspectId, rect.height > 0f ? rect.width / rect.height : 1f);

            Vector2 centerUv = fallbackCenter;
            if (_centerSource != null)
            {
                Vector2 local = _rt.InverseTransformPoint(_centerSource.position);
                centerUv = Rect.PointToNormalized(rect, local);
            }
            _runtimeMat.SetVector(CenterId, centerUv);

            // 중심에서 가장 먼 코너까지의 거리(aspect 보정) = 확산이 화면을 완전히 덮는 반경
            float aspect = rect.height > 0f ? rect.width / rect.height : 1f;
            float maxR = 0f;
            for (int cx = 0; cx <= 1; cx++)
            for (int cy = 0; cy <= 1; cy++)
            {
                Vector2 d = new Vector2((cx - centerUv.x) * aspect, cy - centerUv.y);
                maxR = Mathf.Max(maxR, d.magnitude);
            }
            _runtimeMat.SetFloat(MaxRadiusId, maxR);
        }
    }
}
