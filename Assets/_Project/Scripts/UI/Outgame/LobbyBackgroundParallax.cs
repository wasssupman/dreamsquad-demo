using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Wassup.DepthParallax;

namespace Wassup.UI
{
    // lobby-background-parallax unit 3 — 로비 배경 뎁스 패럴랙스 드라이버(모듈 소비처).
    //
    // 틸트 = 앰비언트 자동 드리프트(다주기 sin) + **포인터 위치**(터치/마우스). 유닛 드래그 속도가
    // 아니라 화면상 포인터가 어디 있느냐로 구동한다 — 화면 중심=0, 가장자리=±1. 캐릭터를 끌 때뿐
    // 아니라 아무 곳이나 터치해도 반응하고, "손가락 쪽을 바라보는" 모델이라 배경에 자연스럽다.
    // 둘을 합산해 스프링으로 감쇠한 뒤
    // 배경 앞/뒤 두 Image 에 **같은 _Tilt/_DepthTex** 를 민다(어긋나면 낮/밤 전환 중 두 레이어가 갈라짐).
    //   앞 = LobbyBackgroundDissolve 의 런타임 머티리얼(디졸브 셰이더가 모듈 .cginc 의 Cue A 를 include)
    //   뒤 = underImage 에 붙이는 모듈 머티리얼(DepthParallax_UI)
    //
    // Cue B(사다리꼴)/C(하이라이트)는 강제로 끈다: 전체화면 배경은 여백이 없어 사다리꼴이 가장자리를
    // 안쪽으로 당기면 캔버스가 드러난다(README 계약). 배경은 Cue A(뎁스 UV)만.
    //
    // 뎁스는 평탄화된 저주파 1장 — 난간·가로등이 흡수돼 있어 늘어질 절벽이 없다. 낮/밤 지오메트리가
    // 같아(상관 0.998) 시간대 스왑 없이 1장을 공유한다.
    [DisallowMultipleComponent]
    public class LobbyBackgroundParallax : MonoBehaviour
    {
        [Header("대상 (앞/뒤 둘 다 같은 틸트를 받아야 함)")]
        [Tooltip("앞 레이어 — 디졸브가 런타임 머티리얼을 소유. 값만 주입한다.")]
        [SerializeField] private LobbyBackgroundDissolve dissolve;
        [Tooltip("뒤 레이어 — 여기에 모듈 머티리얼(DepthParallax_UI)을 런타임 부착.")]
        [SerializeField] private Image underImage;

        [Header("뎁스 / 튜닝")]
        [Tooltip("평탄화된 로비 뎁스맵(낮/밤 공유 1장).")]
        [SerializeField] private Texture2D depthMap;
        [Tooltip("모듈 튜너블 SO. 미할당이면 기본 인스턴스 폴백.")]
        [SerializeField] private DepthParallaxSettings settings;

        [Header("앰비언트 (상시 미세 드리프트)")]
        [Tooltip("앰비언트 틸트 크기(0~1). 입력 없어도 배경이 살아있게.")]
        [SerializeField] private float ambientAmplitude = 0.25f;
        [Tooltip("x 축 sin 속도(rad/s). y 와 서로소에 가깝게 둬야 반복 티가 안 난다.")]
        [SerializeField] private float ambientSpeedX = 0.19f;
        [SerializeField] private float ambientSpeedY = 0.13f;

        [Header("포인터 (터치/마우스 위치)")]
        [Tooltip("포인터 틸트 배율. 화면 중심=0, 가장자리=±1 에 이 값을 곱함. 음수면 방향 반전(튜닝 노브).")]
        [SerializeField] private float pointerGain = 1f;
        [Tooltip("에디터/데스크톱 마우스는 누르지 않아도(hover) 추종. 모바일은 hover 가 없어 터치 중에만 동작.")]
        [SerializeField] private bool mouseHoverFollows = true;

        private Material _underMat;
        private DepthParallaxSettings _cfgFallback;
        private DepthParallaxSettings Cfg => settings != null
            ? settings
            : (_cfgFallback != null ? _cfgFallback : (_cfgFallback = ScriptableObject.CreateInstance<DepthParallaxSettings>()));

        private Vector2 _tilt, _tiltVel;

        private void Start()
        {
            EnsureUnderMaterial();
            PushStaticParams();
        }

        // 뒤 레이어에 모듈 머티리얼 부착(per-instance — 선례 UiCardFaceMesh). 셰이더 없으면 그레이스풀 skip.
        private void EnsureUnderMaterial()
        {
            if (underImage == null) return;
            var sh = Shader.Find("Wassup/UI/DepthParallax");
            if (sh == null)
            {
                Debug.LogWarning($"{nameof(LobbyBackgroundParallax)}: 'Wassup/UI/DepthParallax' 미발견 — 뒤 레이어 패럴랙스 비활성.", this);
                return;
            }
            _underMat = new Material(sh) { name = "LobbyBgParallaxInst", hideFlags = HideFlags.HideAndDontSave };
            underImage.material = _underMat;
        }

        // 정적 파라미터는 1회. 앞/뒤 동일값이어야 전환 중 어긋나지 않는다.
        private void PushStaticParams()
        {
            var s = Cfg;
            if (dissolve != null) dissolve.SetParallaxParams(depthMap, s.amplitude, s.depthCenter, s.depthSign);
            if (_underMat != null)
            {
                if (depthMap != null) _underMat.SetTexture("_DepthTex", depthMap);
                _underMat.SetFloat("_Amplitude", s.amplitude);
                _underMat.SetFloat("_DepthCenter", s.depthCenter);
                _underMat.SetFloat("_DepthSign", s.depthSign);
                // 전체화면 배경 — Cue B/C 강제 off(가장자리 노출 방지). SO 값과 무관하게 0.
                _underMat.SetFloat("_Persp", 0f);
                _underMat.SetFloat("_HiStrength", 0f);
            }
        }

        // 포인터 위치 → 화면 중심 기준 [-1,1]. 프로젝트는 activeInputHandler=Input System 전용이라
        // 레거시 UnityEngine.Input 은 죽어있다 — Pointer.current(마우스+터치 통합)가 관례(cf. PlacementInput).
        // 모바일은 hover 가 없고 놓아도 마지막 터치 위치가 남으므로(stale) 눌린 동안만 유효로 본다.
        private bool TryGetPointerTilt(out Vector2 tilt)
        {
            tilt = Vector2.zero;
            var p = Pointer.current;
            if (p == null || Screen.width <= 0 || Screen.height <= 0) return false;
            bool active = p.press.isPressed || (mouseHoverFollows && p is Mouse);
            if (!active) return false;

            Vector2 pos = p.position.ReadValue();
            tilt = new Vector2(pos.x / Screen.width, pos.y / Screen.height) * 2f - Vector2.one; // 중심=0, 가장자리=±1
            tilt = Vector2.ClampMagnitude(tilt, 1f); // 화면 밖(음수/초과) 좌표 방어
            return true;
        }

        private void Update()
        {
            var s = Cfg;

            // 앰비언트: 서로 다른 주기의 sin 두 개 → 반복 티가 안 나는 상시 드리프트.
            float t = Time.unscaledTime;
            Vector2 ambient = new Vector2(Mathf.Sin(t * ambientSpeedX), Mathf.Sin(t * ambientSpeedY + 1.7f)) * ambientAmplitude;

            // 포인터가 없거나(모바일 무터치) 비활성이면 0 → 스프링이 앰비언트만 남게 부드럽게 되돌린다.
            // 폴링이라 push 기반 staleness watchdog 불필요(활성 여부를 직접 안다).
            Vector2 pointerTilt = TryGetPointerTilt(out var pt) ? pt * pointerGain : Vector2.zero;
            Vector2 target = Vector2.ClampMagnitude(ambient + pointerTilt, 1f);

            DepthParallaxMath.SpringStep(ref _tilt, ref _tiltVel, target,
                s.tiltSpring, s.tiltDamping, s.tiltMaxSpeed, Time.unscaledDeltaTime);

            if (dissolve != null) dissolve.SetParallaxTilt(_tilt);
            if (_underMat != null) _underMat.SetVector("_Tilt", _tilt);
        }

        private void OnDestroy()
        {
            if (_underMat != null) { Destroy(_underMat); _underMat = null; }
            if (_cfgFallback != null) { Destroy(_cfgFallback); _cfgFallback = null; }
        }
    }
}
