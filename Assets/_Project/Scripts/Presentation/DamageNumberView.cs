using System;
using TMPro;
using UnityEngine;
using Wassup.Core.TimeControl;

namespace Wassup.Presentation
{
    // World-space floating damage number. Self-animates (punch scale-in, upward
    // drift, fade-out), billboards to the battle camera, then reports completion so
    // the pool can recycle it. Magnitude drives font size + face color + punch via
    // the spawner-provided DamageNumberStyle.
    [RequireComponent(typeof(TextMeshPro))]
    public class DamageNumberView : MonoBehaviour
    {
        private TextMeshPro _tmp;
        private MeshRenderer _renderer;
        private Camera _camera;
        private DamageNumberStyle _style;
        private Action<DamageNumberView> _onComplete;

        private Vector3 _startPos;
        private float _elapsed;
        private float _lifetime;
        private float _punchT;
        private Color _faceColor;
        private bool _playing;
        private int _index; // spawner-owned monotonic spawn index (deterministic motion — unit 1)
        private Color _gradTopRGB; // 정점 그라데이션 상단 RGB (알파는 매 프레임 페이드로 주입)
        private float _tiltDeg;    // index 결정론 미세 roll

        private void Awake()
        {
            _tmp = GetComponent<TextMeshPro>();
            _renderer = GetComponent<MeshRenderer>();
            // Render above unit sprites/meshes.
            if (_renderer != null) _renderer.sortingOrder = 32000;
        }

        // viewPos 는 이미 view 공간(스포너가 ToView + 머리 앵커 + 격자 스냅 적용). 여기서 재변환 금지 — 이중 ToView 방지.
        public void Play(int amount, Vector3 viewPos, Camera cam, DamageNumberStyle style, Action<DamageNumberView> onComplete, int index)
        {
            if (_tmp == null) _tmp = GetComponent<TextMeshPro>();
            if (_renderer == null) _renderer = GetComponent<MeshRenderer>();
            _camera = cam;
            _style = style;
            _onComplete = onComplete;
            _index = index;
            _startPos = viewPos;
            _lifetime = Mathf.Max(0.05f, style.lifetime);

            float t = style.Normalize(amount);
            _punchT = t;
            _tmp.fontSize = Mathf.Lerp(style.minFontSize, style.maxFontSize, t);
            _tmp.text = amount.ToString();
            _faceColor = style.EvaluateColor(t);
            // 정점 그라데이션 상단 밝기(RGB 캐시). 알파는 ApplyFrame 이 4-corner 로 주입(페이드와 분리).
            _gradTopRGB = new Color(
                Mathf.Clamp01(_faceColor.r * style.topBoost),
                Mathf.Clamp01(_faceColor.g * style.topBoost),
                Mathf.Clamp01(_faceColor.b * style.topBoost), 1f);
            _tmp.enableVertexGradient = true;
            // index 결정론 미세 회전(±maxTiltDeg): frac(index·φ) 저불일치 시퀀스, RNG/시간 미사용.
            float h = _index * 0.61803398875f; h -= Mathf.Floor(h);
            _tiltDeg = (h * 2f - 1f) * style.maxTiltDeg;

            transform.position = _startPos; // 스포너가 넘긴 view 공간 위치
            transform.localScale = Vector3.one;
            gameObject.SetActive(true);
            _elapsed = 0f;
            _playing = true;
            ApplyFrame(0f);
        }

        private void Update()
        {
            if (!_playing) return;
            // 글로벌 Time.timeScale 은 1 고정 → Time.deltaTime 은 TimeManager 정지에 안 멈춘다.
            // Battle 도메인 델타 경유(정지/슬로우 반영). TimeManager 부재 시에만 폴백.
            var tm = TimeManager.Instance;
            _elapsed += tm != null ? tm.DeltaTime(TimeDomain.Battle) : Time.deltaTime;
            float n = _elapsed / _lifetime;
            if (n >= 1f) { Finish(); return; }
            ApplyFrame(n);
        }

        private void ApplyFrame(float n)
        {
            // driftUp: view 공간 world-up 상승 (앵커와 동일 축).
            Vector3 pos = _startPos + Vector3.up * (_style.driftUp * n);
            // 대형 히트 셰이크: 방향 시드는 index(구조적), 진동은 수명 클럭 n, 초반 감쇠. 화면 평면에서 흔든다.
            if (_style.shakeAmp > 0f && _punchT > 0f && _camera != null)
            {
                float decay = 1f - n; decay *= decay;
                float mag = _style.shakeAmp * _punchT * decay;
                float phase = _index * 2.3999632f;
                Vector3 dir = _camera.transform.right * Mathf.Cos(phase + n * 46f)
                            + _camera.transform.up * Mathf.Sin(phase * 1.31f + n * 39f);
                pos += dir * mag;
            }
            transform.position = pos;

            float curve = _style.scaleCurve != null && _style.scaleCurve.length > 0
                ? _style.scaleCurve.Evaluate(n)
                : 1f;
            // Amplify the overshoot/deviation-from-1 for big hits.
            float punchMul = Mathf.Lerp(1f, Mathf.Max(1f, _style.bigHitPunchMul), _punchT);
            float scale = 1f + (curve - 1f) * punchMul;
            transform.localScale = Vector3.one * Mathf.Max(0.001f, scale);

            float a = _style.alphaCurve != null && _style.alphaCurve.length > 0
                ? _style.alphaCurve.Evaluate(n)
                : 1f - n;
            // 정점 그라데이션(상단 밝게) 유지 + 페이드는 4-corner 알파로만 — 단색 _tmp.color 덮어쓰기 금지.
            Color bot = _faceColor; bot.a = a;
            Color top = _gradTopRGB; top.a = a;
            _tmp.colorGradient = new VertexGradient(top, top, bot, bot);
        }

        private void LateUpdate()
        {
            if (!_playing || _camera == null) return;
            // 빌보드(카메라 정렬) + index 결정론 미세 roll — 격자의 딱딱함 완화.
            transform.rotation = _camera.transform.rotation * Quaternion.Euler(0f, 0f, _tiltDeg);
        }

        private void Finish()
        {
            // _playing/_onComplete 를 먼저 정리한 뒤 비활성화 → OnDisable 이 중복 콜백을 내지 않는다.
            _playing = false;
            var cb = _onComplete;
            _onComplete = null;
            gameObject.SetActive(false);
            cb?.Invoke(this);
        }

        // 비정상 반납(씬 언로드/StopBattle 자식 비활성/도메인 리로드) 시에도 점유 셀을 풀도록 완료 콜백을 멱등 호출.
        private void OnDisable()
        {
            if (!_playing) return; // 자연 종료(Finish)는 이미 콜백을 냈다.
            _playing = false;
            var cb = _onComplete;
            _onComplete = null;
            cb?.Invoke(this);
        }
    }
}
