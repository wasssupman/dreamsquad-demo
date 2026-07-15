using UnityEngine;
using UnityEngine.UI;

namespace Wassup.DepthParallax
{
    // depth-parallax unit 5 — 정적 컨텐츠(카드·로비 캐릭터)용 제네릭 소비 컴포넌트.
    // 단일 스프라이트+뎁스에 틸트 패럴랙스를 준다. 플립북 컷신은 자체 프레임 루프라 이 컴포넌트를
    // 호스트하지 않는다(README "Two-surface"). 콘텐츠 타입 무지 — 입력은 plain 값(SetTilt/depthMap/tint)뿐.
    // 붙이고 매 프레임 SetTilt 만 먹이면 되고, staleness watchdog 이 무피드 시 자동 0 복귀.
    [DisallowMultipleComponent]
    public class DepthParallaxView : MonoBehaviour
    {
        [Tooltip("셰이더 파라미터/틸트 스프링 튜너블(모듈 SO). 미지정 시 런타임 기본값 인스턴스로 폴백.")]
        [SerializeField] private DepthParallaxSettings settings;
        [Tooltip("R8 뎁스맵. null 이면 패럴랙스 skip(색만).")]
        [SerializeField] private Texture depthMap;
        [Tooltip("셰이더 _Color 로 곱해지는 틴트.")]
        [SerializeField] private Color tint = Color.white;
        [Tooltip("대상 UGUI Graphic(Image/RawImage). 미지정 시 같은 GO 에서 GetComponent.")]
        [SerializeField] private Graphic graphic;

        private Material _mat;         // per-instance 머티리얼(런타임 스왑 금지, Set* 로만 구동)
        private bool _ownsSettings;    // 폴백 SO 를 우리가 만들었으면 OnDestroy 에서 정리
        private Vector2 _tiltTarget;
        private Vector2 _tilt;
        private Vector2 _tiltVel;
        private float _lastFeedUnscaled;

        // 소비처(컨트롤러)가 매 프레임 피드. staleness watchdog 기준 시각도 함께 갱신.
        public void SetTilt(Vector2 tilt)
        {
            _tiltTarget = tilt;
            _lastFeedUnscaled = Time.unscaledTime;
        }

        private void Awake()
        {
            // 절대 NRE 안 나게 SO 폴백(제약 10 shape — 값은 아키텍처를 모른 채 흐른다).
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<DepthParallaxSettings>();
                _ownsSettings = true;
            }
            if (graphic == null) graphic = GetComponent<Graphic>();

            EnsureMaterial();
        }

        private void EnsureMaterial()
        {
            if (graphic == null)
            {
                Debug.LogWarning($"{nameof(DepthParallaxView)}: 대상 Graphic 이 없어 패럴랙스 비활성.", this);
                return;
            }
            var sh = Shader.Find("Wassup/UI/DepthParallax");
            if (sh == null)
            {
                // 폴백: 셰이더 미발견 시 Graphic 기본 머티리얼 유지(graceful degrade).
                Debug.LogWarning($"{nameof(DepthParallaxView)}: 'Wassup/UI/DepthParallax' 셰이더 미발견 — 패럴랙스 비활성.", this);
                return;
            }
            _mat = new Material(sh) { name = "DepthParallaxInst", hideFlags = HideFlags.HideAndDontSave };
            graphic.material = _mat;
            PushStaticParams();
        }

        // 정적 파라미터는 1회만 push. 프레임마다 바뀌는 건 _Tilt 뿐(Update).
        private void PushStaticParams()
        {
            _mat.SetColor("_Color", tint);
            _mat.SetFloat("_Amplitude", settings.amplitude);
            _mat.SetFloat("_DepthCenter", settings.depthCenter);
            _mat.SetFloat("_DepthSign", settings.depthSign);
            _mat.SetFloat("_Persp", settings.perspective);
            _mat.SetFloat("_HiStrength", settings.highlightStrength);
            _mat.SetFloat("_HiWidth", settings.highlightWidth);
            if (depthMap != null) _mat.SetTexture("_DepthTex", depthMap); // 없으면 셰이더 기본 "gray" 유지 → 패럴랙스 skip
        }

        private void Update()
        {
            if (_mat == null) return;

            // staleness watchdog — 무피드 > tiltStalenessSeconds 면 릴리즈(target 0 복귀).
            if (Time.unscaledTime - _lastFeedUnscaled > settings.tiltStalenessSeconds)
                _tiltTarget = Vector2.zero;

            DepthParallaxMath.SpringStep(ref _tilt, ref _tiltVel, _tiltTarget,
                settings.tiltSpring, settings.tiltDamping, settings.tiltMaxSpeed, Time.unscaledDeltaTime);

            _mat.SetVector("_Tilt", _tilt);
        }

        private void OnDestroy()
        {
            if (_mat != null)
            {
                if (Application.isPlaying) Destroy(_mat); else DestroyImmediate(_mat);
                _mat = null;
            }
            if (_ownsSettings && settings != null)
            {
                if (Application.isPlaying) Destroy(settings); else DestroyImmediate(settings);
                settings = null;
                _ownsSettings = false;
            }
        }
    }
}
