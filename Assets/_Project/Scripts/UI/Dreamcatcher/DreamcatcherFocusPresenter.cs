using System.Collections.Generic;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.UI
{
    // dreamcatcher-attach-lockon — 부착 조준 포커스 연출 facade. dim(A) + 유효
    // base-ring(B) + 락온 리티클(C) + 오프셋 콜아웃(D) + 확정 비트(E)를 한
    // 오버레이 프레젠터가 소유·구동한다(메커닉이 연출 소유). 애셋-프리 UI 쿼드,
    // 전 Graphic raycastTarget=false. 이징은 unscaledDeltaTime(전투 슬로모 무관).
    //
    // 요소는 프레젠터 GO 의 자식이 아니라 canvasRoot 직속으로 만들어 sibling
    // index 로 레이어를 강제한다(전장<dim<링<화살표<리티클<콜아웃). 애니메이션은
    // Update 에서 매프레임 갱신 — SetAim 은 상태만 저장하므로 손가락이 멈춰도
    // 움직이는 유닛을 리티클/콜아웃이 계속 추종한다.
    public class DreamcatcherFocusPresenter : MonoBehaviour
    {
        public enum AimKind { DimOnly, DefenderCast, AttachAim, EnemyMark, Selected }

        private DreamcatcherFocusConfig _cfg;
        private BattleBridge _bridge;
        private DreamcatcherHandController _controller;
        private Camera _cam;
        private TMP_FontAsset _font;

        // A · dim
        private Image _dim;
        private float _dimTarget;

        // C · reticle (root + 8 corner arms)
        private RectTransform _reticleRoot;
        private CanvasGroup _reticleGroup;
        private readonly RectTransform[] _arms = new RectTransform[8];
        private readonly Image[] _armImgs = new Image[8];
        private Rect _reticleCur;
        private bool _reticleInit;
        private float _reticleScale = 1f;

        // E · confirm pulse (independent timer — survives End())
        private RectTransform _pulse;
        private Image _pulseImg;
        private float _pulseT = -1f;
        private Vector2 _pulseCenter;

        // D · callout
        private RectTransform _callout;
        private CanvasGroup _calloutGroup;
        private Image _calloutIcon;
        private TextMeshProUGUI _calloutName;
        private TextMeshProUGUI _calloutCount;
        private float _calloutTarget;
        private float _calloutPunch;

        // B · base rings
        private RectTransform _ringsRoot;
        private readonly List<Image> _rings = new List<Image>();
        private Sprite _ringSprite;
        private Canvas _canvas; // H1 — scaleFactor 환산용

        // aim state (stored by SetAim, applied in Update)
        private bool _active;
        private AimKind _kind;
        private Sprite _cardIcon;   // EnemyMark — 적은 portrait 없어 카드 아트로 정체 표기
        private string _cardName;
        private bool _enemyMarkValid;   // EnemyMark 유효성(슬롯이 결정 — 적 미표식=유효 / 유닛=무효)
        private bool _enemyMarkOnUnit;  // 손가락이 유닛 위(적 표식 카드를 유닛에 = 무효) → "유닛 불가"
        private readonly HashSet<Entity> _attachable = new HashSet<Entity>();
        private Vector2 _pointer;
        private Entity _locked = Entity.Null;
        private Vector2Int _lockedCell;
        private bool _hasLockRect;
        private Rect _lockRect;
        private bool _lockValid;
        private Entity _lastLocked = Entity.Null;    // L6 — 정체 바뀌면 리티클 pop 새로고침
        private Entity _countEntity = Entity.Null;   // L4 — 콜아웃 카운트 문자열 캐시
        private string _countText = "";
        private readonly List<(Entity entity, Rect rect)> _rectBuf = new List<(Entity, Rect)>();
        // use-flow unit 2 — 드래그 시작에 붉게 칠한 불가 유닛(종료 시 원복 대상).
        private readonly List<Entity> _sweptUnits = new List<Entity>();
        private Entity _tintedUnit = Entity.Null;     // F — 현재 Spine 틴트 적용 유닛(clean 토글)

        // 화살표 끝점 당김용 — 락온 유닛 화면 중심(없으면 null).
        public Vector2? LockedCenter => _active && _hasLockRect ? (Vector2?)_lockRect.center : null;

        public static DreamcatcherFocusPresenter Create(Transform canvasRoot,
            DreamcatcherFocusConfig cfg, TMP_FontAsset font)
        {
            var go = new GameObject("FocusPresenter", typeof(RectTransform));
            go.transform.SetParent(canvasRoot, false);
            var p = go.AddComponent<DreamcatcherFocusPresenter>();
            p._cfg = cfg;
            p._font = font;
            p.Build(canvasRoot);
            return p;
        }

        public void Bind(BattleBridge bridge, DreamcatcherHandController controller, Camera cam)
        {
            _bridge = bridge;
            _controller = controller;
            _cam = cam;
        }

        // H1 — 이 오버레이는 CanvasScaler(ScaleWithScreenSize) 캔버스 직속이라 .position 은
        // device px, sizeDelta/anchoredPosition 은 canvas-local(×scaleFactor)이다. 스크린렉트
        // (device px)를 리티클/콜아웃 local 로 환산할 때 이 배율로 나눈다.
        private float ScaleFactor => _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;

        private void Build(Transform root)
        {
            _canvas = root.GetComponentInParent<Canvas>();
            // base-ring 두께를 SO(baseRingThickness)로 — 128px 원판에서 반경 대비 비율로 border 굽기.
            float ringBorder = _cfg != null && _cfg.baseRingRadius > 0f
                ? Mathf.Clamp(_cfg.baseRingThickness / (_cfg.baseRingRadius * 2f) * 128f, 2f, 60f) : 12f;
            _ringSprite = UiRoundedSprite.MakeCircle(128, Color.clear, ringBorder, Color.white);

            // A · dim — fullscreen, below the hand panel (parented to root, forced to first sibling).
            _dim = NewImage(root, "FocusDim", out var dimRt);
            dimRt.anchorMin = Vector2.zero; dimRt.anchorMax = Vector2.one;
            dimRt.offsetMin = Vector2.zero; dimRt.offsetMax = Vector2.zero;
            _dim.color = WithAlpha(_cfg != null ? _cfg.dimColor : Color.black, 0f);

            // B · base rings container
            _ringsRoot = NewNode(root, "FocusRings");

            // C · reticle
            _reticleRoot = NewNode(root, "FocusReticle");
            _reticleGroup = _reticleRoot.gameObject.AddComponent<CanvasGroup>();
            _reticleGroup.alpha = 0f;
            _reticleGroup.interactable = false; _reticleGroup.blocksRaycasts = false;
            for (int i = 0; i < 8; i++)
            {
                _armImgs[i] = NewImage(_reticleRoot, $"Arm_{i}", out var armRt);
                _arms[i] = armRt;
            }

            // D · callout
            _callout = NewNode(root, "FocusCallout");
            _callout.pivot = new Vector2(0.5f, 0f);
            var bg = _callout.gameObject.AddComponent<Image>();
            bg.raycastTarget = false;
            bg.type = Image.Type.Sliced;
            bg.sprite = UiRoundedSprite.Make(18f, 2f,
                _cfg != null ? _cfg.calloutBgColor : new Color(0.06f, 0.05f, 0.13f, 0.92f),
                _cfg != null ? _cfg.calloutBorderColor : Color.cyan);
            bg.color = Color.white;
            float ico = _cfg != null ? _cfg.calloutIconSize : 56f;
            _callout.sizeDelta = new Vector2(ico + 210f, ico + 22f);
            _calloutGroup = _callout.gameObject.AddComponent<CanvasGroup>();
            _calloutGroup.alpha = 0f;
            _calloutGroup.interactable = false; _calloutGroup.blocksRaycasts = false;

            _calloutIcon = NewImage(_callout, "Icon", out var iconRt);
            iconRt.anchorMin = new Vector2(0f, 0.5f); iconRt.anchorMax = new Vector2(0f, 0.5f);
            iconRt.pivot = new Vector2(0f, 0.5f);
            iconRt.anchoredPosition = new Vector2(12f, 0f);
            iconRt.sizeDelta = new Vector2(ico, ico);
            _calloutIcon.preserveAspect = true;

            _calloutName = NewText("Name", _callout, 30f, FontStyles.Bold, TextAlignmentOptions.BottomLeft);
            var nrt = _calloutName.rectTransform;
            nrt.anchorMin = new Vector2(0f, 0.5f); nrt.anchorMax = new Vector2(1f, 1f);
            nrt.offsetMin = new Vector2(ico + 22f, 0f); nrt.offsetMax = new Vector2(-12f, -6f);

            _calloutCount = NewText("Count", _callout, 30f, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            var crt = _calloutCount.rectTransform;
            crt.anchorMin = new Vector2(0f, 0f); crt.anchorMax = new Vector2(1f, 0.5f);
            crt.offsetMin = new Vector2(ico + 22f, 6f); crt.offsetMax = new Vector2(-12f, 0f);

            // E · confirm pulse ring
            _pulse = NewImage(root, "FocusPulse", out _).rectTransform;
            _pulseImg = _pulse.GetComponent<Image>();
            _pulseImg.sprite = _ringSprite;
            _pulseImg.type = Image.Type.Simple;
            _pulse.gameObject.SetActive(false);
        }

        // ── public drive API (called by DreamcatcherCardDragSlot) ────────────

        public void Begin(AimKind kind, HashSet<Entity> attachable)
        {
            _active = true;
            _kind = kind;
            _attachable.Clear();
            if (attachable != null) foreach (var e in attachable) _attachable.Add(e);
            _dimTarget = 1f;
            _locked = Entity.Null;
            _hasLockRect = false;
            _reticleInit = false;
            // L4 캐시 회귀 수정 — 부착수는 드래그 '중'엔 불변이나 드래그 '간'엔 바뀐다.
            // 매 조준 시작마다 카운트 캐시를 무효화해야 커밋 후 재조준 시 최신값을 낸다.
            _countEntity = Entity.Null;
            // use-flow unit 2 — 불가 유닛 일괄 붉은 틴트(AttachAim 전용). Begin 연타 방어로
            // 먼저 걷고 다시 깐다.
            ClearInvalidSweep();
            if (kind == AimKind.AttachAim) ApplyInvalidSweep();
        }

        // unit-dreamcatcher-inspect unit 6 — 선택 리티클. 조준(카드 스와이프 락온)과 같은
        // 시각 언어를 유닛 선택에도 쓴다: 리티클 + 콜아웃(portrait+이름)만. dim/링/틴트/
        // 카운트는 조준 전용 — 선택의 스포트라이트는 줌+슬로모(DcInspectController)가 이미
        // 담당하므로 dim 을 겹치지 않는다. 대상 전환은 Begin 재호출로 리티클이 새 유닛
        // 위에서 pop 한다(L6). 종료는 End() 공용.
        public void BeginSelection(Entity locked, Vector2Int lockedCell)
        {
            Begin(AimKind.Selected, null);
            _dimTarget = 0f;
            SetAim(default, locked, lockedCell);
        }

        // dreamcatcher-attach-lockon — 살찌운 제물(적 표식) 조준. 적은 portrait 가 없어
        // 콜아웃 정체를 카드 아트+이름으로 표기하고, 유효성은 '재표식 가능'(미표식)으로 본다.
        public void BeginEnemyMark(Sprite cardIcon, string cardName)
        {
            Begin(AimKind.EnemyMark, null);
            _cardIcon = cardIcon;
            _cardName = cardName;
        }

        public void SetAim(Vector2 pointer, Entity locked, Vector2Int lockedCell)
        {
            _pointer = pointer;
            _locked = locked;
            _lockedCell = lockedCell;
        }

        // EnemyMark 전용 — 유효성/유닛여부를 슬롯이 결정해 전달(적=미표식 유효, 유닛=무효).
        public void SetAimEnemyMark(Vector2 pointer, Entity locked, bool valid, bool onUnit)
        {
            _pointer = pointer;
            _locked = locked;
            _lockedCell = default;
            _enemyMarkValid = valid;
            _enemyMarkOnUnit = onUnit;
        }

        public void Confirm()
        {
            Vector2 center;
            if (_hasLockRect) center = _lockRect.center;
            else if (_locked != Entity.Null && _bridge != null &&
                     _bridge.TryGetUnitScreenRect(_locked, _cam, out var r)) center = r.center;
            else return;
            _pulseCenter = center;
            _pulseT = 0f;
            _pulse.gameObject.SetActive(true);
            _calloutPunch = 1f;
            if (_cfg != null && _cfg.enableHaptic && !Application.isEditor)
                Handheld.Vibrate();
        }

        public void End()
        {
            _active = false;
            _kind = AimKind.DimOnly;
            _dimTarget = 0f;
            _calloutTarget = 0f;
            _locked = Entity.Null;
            _hasLockRect = false;
            _attachable.Clear();
            ClearFocusTint(); // F — 락온 유닛 몸체 틴트 원복(누수 차단)
            ClearInvalidSweep(); // use-flow unit 2 — 불가 스윕 원복(어느 종료 경로든)
        }

        // 패널 비활성/도메인 리로드로 End 를 못 걷고 꺼질 때의 안전 원복.
        private void OnDisable()
        {
            ClearFocusTint();
            ClearInvalidSweep();
        }

        // use-flow unit 2 — 드래그 중 부착 불가 유닛 일괄 붉은 틴트. 화면 문법 대칭:
        // 시안 링 = 되는 곳(base-ring), 붉은 몸 = 안 되는 곳. 대상 = Begin 시점 전체 열거
        // − _attachable 스냅샷(드래그 중 재평가 없음 — 락온 valid 와 같은 정책). 색은 락온
        // invalid 와 동일한 focusTintInvalid — 같은 의미는 같은 색(새 노브 금지). 뷰별
        // 저장/복원(SetHoverHighlight)이라 다중 대상 안전, _dying 유닛은 뷰가 자체 거부.
        private void ApplyInvalidSweep()
        {
            if (_bridge == null || _cam == null || _cfg == null || !_cfg.enableFocusTint) return;
            _bridge.EnumerateDefenderScreenRects(_cam, _rectBuf);
            for (int i = 0; i < _rectBuf.Count; i++)
            {
                var e = _rectBuf[i].entity;
                if (_attachable.Contains(e)) continue;
                _bridge.SetDefenderHoverHighlight(e, true, _cfg.focusTintInvalid);
                _sweptUnits.Add(e);
            }
        }

        private void ClearInvalidSweep()
        {
            if (_bridge != null)
                for (int i = 0; i < _sweptUnits.Count; i++)
                    _bridge.SetDefenderHoverHighlight(_sweptUnits[i], false, default);
            _sweptUnits.Clear();
        }

        // ── per-frame ────────────────────────────────────────────────────────

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_cfg == null) return;

            // A · dim fade
            float dimA = _dim.color.a;
            float dimGoal = _dimTarget * _cfg.dimAlpha;
            float dimRate = _dimTarget > 0f
                ? (_cfg.dimFadeInSec > 0f ? _cfg.dimAlpha / _cfg.dimFadeInSec : 999f)
                : (_cfg.dimFadeOutSec > 0f ? _cfg.dimAlpha / _cfg.dimFadeOutSec : 999f);
            dimA = Mathf.MoveTowards(dimA, dimGoal, dimRate * dt);
            _dim.color = WithAlpha(_cfg.dimColor, dimA);

            // resolve locked rect (tracks a moving unit even if the finger is still)
            _hasLockRect = false;
            if (_active && _locked != Entity.Null && _bridge != null &&
                _bridge.TryGetUnitScreenRect(_locked, _cam, out var lr))
            {
                _hasLockRect = true;
                _lockRect = lr;
                if (_kind == AimKind.AttachAim) _lockValid = _attachable.Contains(_locked);
                else if (_kind == AimKind.EnemyMark) _lockValid = _enemyMarkValid; // 슬롯이 결정
                else _lockValid = true;
            }
            // L6 — 정체가 바뀌면 리티클을 새 대상 위에서 pop(화면 가로질러 날아오지 않게).
            if (_locked != _lastLocked) { _reticleInit = false; _lastLocked = _locked; }

            UpdateFocusTint();
            UpdateReticle(dt);
            UpdateCallout(dt);
            UpdateRings(dt);
            UpdatePulse(dt);
        }

        // F · 포커스 유닛 틴트 — 락온된 defender 몸체(Spine)에 상태색을 얹는다(리티클/
        // 화살표와 같은 언어를 유닛 자체에도). AttachAim/DefenderCast 만(EnemyMark 는
        // health-tint 합성 문제로, DimOnly 는 대상 없음으로 제외). 곱셈 틴트라 '밝힘'
        // 불가 — dim 위 색조 대비로 pop. 락온 유닛의 valid 는 드래그 중 불변(_attachable
        // 스냅샷 / DefenderCast=true)이라 색은 유닛 전환 때만 바뀐다 → 전환 시에만 on/off.
        private void UpdateFocusTint()
        {
            bool want = _active && _hasLockRect && _locked != Entity.Null && _bridge != null
                        && _cfg.enableFocusTint
                        && (_kind == AimKind.AttachAim || _kind == AimKind.DefenderCast)
                        // use-flow unit 2 — AttachAim 의 invalid 락온은 스윕이 이미 붉게
                        // 소유 중: 여기서 on/off 를 겹치면 off 복원이 스윕 틴트까지 걷는다.
                        && (_kind != AimKind.AttachAim || _lockValid);
            SetFocusTint(want ? _locked : Entity.Null);
        }

        private void ClearFocusTint() => SetFocusTint(Entity.Null);

        // 락온 유닛 몸체 틴트를 target 으로 전환(같으면 no-op). 이전 유닛 원복 후 새 대상
        // 틴트 — 유닛당 on 1회/off 1회로 SetHoverHighlight 저장·복원 균형. _bridge 없으면
        // 추적만 갱신(Bind 후 _bridge 는 불변이라 실경로엔 도달 안 함 — 방어적 대칭 가드).
        private void SetFocusTint(Entity target)
        {
            if (target == _tintedUnit) return;
            if (_bridge != null)
            {
                if (_tintedUnit != Entity.Null)
                    _bridge.SetDefenderHoverHighlight(_tintedUnit, false, default);
                if (target != Entity.Null)
                    _bridge.SetDefenderHoverHighlight(target, true,
                        _lockValid ? _cfg.focusTintValid : _cfg.focusTintInvalid);
            }
            _tintedUnit = target;
        }

        private void UpdateReticle(float dt)
        {
            bool show = _active && _hasLockRect;
            _reticleGroup.alpha = Mathf.MoveTowards(_reticleGroup.alpha, show ? 1f : 0f,
                dt / Mathf.Max(0.0001f, _cfg.calloutFadeSec));
            if (!show)
            {
                _reticleScale = Mathf.MoveTowards(_reticleScale, 1f, dt * 6f);
                return;
            }

            // target rect = lock rect + padding (+ invalid gap), min screen size guaranteed.
            float pad = _cfg.reticlePadding + (_lockValid ? 0f : _cfg.reticleInvalidGap);
            Rect tgt = Rect.MinMaxRect(_lockRect.xMin - pad, _lockRect.yMin - pad,
                                       _lockRect.xMax + pad, _lockRect.yMax + pad);
            EnforceMinSize(ref tgt, _cfg.reticleMinScreenSize);

            if (!_reticleInit)
            {
                _reticleCur = tgt;
                _reticleInit = true;
                _reticleScale = _cfg.reticlePopScale; // pop on first lock
            }
            float k = 1f - Mathf.Exp(-_cfg.reticleSnapSpring * dt);
            _reticleCur = LerpRect(_reticleCur, tgt, k);
            _reticleScale = Mathf.MoveTowards(_reticleScale, 1f, dt * 5f);

            _reticleRoot.position = _reticleCur.center; // device px (overlay position)
            _reticleRoot.localScale = Vector3.one * _reticleScale;
            // H1 — arm 은 canvas-local(anchoredPosition/sizeDelta) → device px 스팬을 scaleFactor 로 환산.
            float s = ScaleFactor;
            LayoutArms(_reticleCur.width / s, _reticleCur.height / s);

            var col = _lockValid ? _cfg.reticleColor : _cfg.reticleInvalidColor;
            for (int i = 0; i < 8; i++) _armImgs[i].color = col;
        }

        // 8 arms = 4 corners × (horizontal, vertical). Local space around center.
        private void LayoutArms(float w, float h)
        {
            float hw = w * 0.5f, hh = h * 0.5f;
            float len = _cfg.reticleCornerLen, t = _cfg.reticleThickness;
            // corner signs: TL(-,+) TR(+,+) BL(-,-) BR(+,-)
            int idx = 0;
            for (int cy = 1; cy >= -1; cy -= 2)
                for (int cx = -1; cx <= 1; cx += 2)
                {
                    // order becomes TL,TR,BL,BR via cy outer, cx inner
                    var hArm = _arms[idx];
                    hArm.sizeDelta = new Vector2(len, t);
                    hArm.anchoredPosition = new Vector2(cx * hw - cx * len * 0.5f, cy * hh);
                    var vArm = _arms[idx + 1];
                    vArm.sizeDelta = new Vector2(t, len);
                    vArm.anchoredPosition = new Vector2(cx * hw, cy * hh - cy * len * 0.5f);
                    idx += 2;
                }
        }

        private void UpdateCallout(float dt)
        {
            bool show = _active && _hasLockRect && _kind != AimKind.DimOnly;
            if (show)
            {
                // content — 방어수 부착/캐스트는 유닛 정체(portrait+name+X/3), 적 표식은
                // 카드 아트+이름+표식상태(적은 portrait 없음).
                Sprite icon = null; string nm = null;
                if (_kind == AimKind.EnemyMark) { icon = _cardIcon; nm = _cardName; }
                else if (_bridge != null && _bridge.TryGetDefenderData(_lockedCell, out var data) && data != null)
                {
                    icon = data.portrait;
                    nm = data.displayName;
                }
                _calloutIcon.enabled = icon != null;
                _calloutIcon.sprite = icon;
                _calloutName.text = string.IsNullOrEmpty(nm) ? "" : nm;

                if (_kind == AimKind.AttachAim && _controller != null)
                {
                    // L4 — 부착수는 드래그 중 불변(계약 #8) → 정체 바뀔 때만 재계산(dict 스캔+concat).
                    if (_locked != _countEntity)
                    {
                        _countEntity = _locked;
                        _countText = _controller.AttachCountOf(_locked) + "/" + _controller.MaxAttachPerUnit;
                    }
                    _calloutCount.text = _countText;
                }
                else if (_kind == AimKind.EnemyMark)
                    _calloutCount.text = _enemyMarkOnUnit ? "유닛 불가" : (_lockValid ? "표식 가능" : "이미 표식됨");
                else _calloutCount.text = "";

                var tcol = _lockValid ? _cfg.calloutValidTextColor : _cfg.calloutFullTextColor;
                _calloutCount.color = tcol;
                _calloutName.color = tcol;

                // unit-dreamcatcher-inspect unit 6 rev — 콜아웃 위치 규칙은 조준/선택 공통
                // 하나(사용자 결정 2026-07-29): 리티클 프레임 상단 + calloutFrameGap. 스무딩된
                // 프레임(_reticleCur) 기준이라 콜아웃이 프레임과 한 몸으로 움직인다. 손끝
                // 회피는 프레임 최소 크기(reticleMinScreenSize)가 담당. 첫 프레임(_reticleInit
                // 전)만 락온 렉트로 폴백. 화면 가장자리 클램프는 아래 공통.
                Vector2 p = _reticleInit
                    ? new Vector2(_reticleCur.center.x, _reticleCur.yMax + _cfg.calloutFrameGap)
                    : new Vector2(_lockRect.center.x, _lockRect.yMax + _cfg.calloutFrameGap);
                // H1 — sizeDelta 는 canvas-local, clamp 는 device px(Screen) → scaleFactor 로 환산.
                float s = ScaleFactor;
                float hw = _callout.sizeDelta.x * 0.5f * s, hgt = _callout.sizeDelta.y * s;
                float pad = _cfg.calloutEdgeClampPad;
                p.x = Mathf.Clamp(p.x, hw + pad, Screen.width - hw - pad);
                p.y = Mathf.Clamp(p.y, pad, Screen.height - hgt - pad);
                _callout.position = p;
                _calloutTarget = 1f;
            }
            else _calloutTarget = 0f;

            _calloutGroup.alpha = Mathf.MoveTowards(_calloutGroup.alpha, _calloutTarget,
                dt / Mathf.Max(0.0001f, _cfg.calloutFadeSec));
            _calloutPunch = Mathf.MoveTowards(_calloutPunch, 0f, dt * 4f);
            _callout.localScale = Vector3.one * (1f + 0.16f * _calloutPunch);
        }

        private void UpdateRings(float dt)
        {
            bool show = _active && _kind == AimKind.AttachAim && _bridge != null;
            if (!show)
            {
                for (int i = 0; i < _rings.Count; i++) if (_rings[i].enabled) _rings[i].enabled = false;
                return;
            }
            _bridge.EnumerateDefenderScreenRects(_cam, _rectBuf);
            float pulse = Mathf.Sin(Time.unscaledTime * (Mathf.PI * 2f / Mathf.Max(0.1f, _cfg.baseRingPulseSec)));
            float breathe = 0.85f + 0.15f * pulse;
            int used = 0;
            for (int i = 0; i < _rectBuf.Count; i++)
            {
                var (ent, rect) = _rectBuf[i];
                if (!_attachable.Contains(ent)) continue;
                var img = RingAt(used++);
                var center = new Vector2(rect.center.x, rect.center.y);
                img.rectTransform.position = center;
                img.rectTransform.sizeDelta = new Vector2(_cfg.baseRingRadius * 2f, _cfg.baseRingRadius * 2f);
                float a = _cfg.baseRingColor.a * breathe;
                // near-pointer reveal
                if (_cfg.baseRingRevealRadius > 1f)
                {
                    float d = Vector2.Distance(center, _pointer);
                    float near = Mathf.Clamp01(1f - d / _cfg.baseRingRevealRadius);
                    a *= Mathf.Lerp(_cfg.baseRingDistanceFade, 1f, near);
                }
                if (_locked != Entity.Null && ent == _locked) a *= _cfg.baseRingLockedFade;
                img.color = WithAlpha(_cfg.baseRingColor, a);
            }
            for (int i = used; i < _rings.Count; i++) if (_rings[i].enabled) _rings[i].enabled = false;
        }

        private void UpdatePulse(float dt)
        {
            if (_pulseT < 0f) return;
            _pulseT += dt;
            float dur = Mathf.Max(0.05f, _cfg.confirmPulseSec);
            float u = _pulseT / dur;
            if (u >= 1f)
            {
                _pulseT = -1f;
                _pulse.gameObject.SetActive(false);
                return;
            }
            float r = Mathf.Lerp(_cfg.confirmPulseMinRadius * 0.35f, _cfg.confirmPulseMinRadius * 1.25f, u);
            _pulse.position = _pulseCenter;
            _pulse.sizeDelta = new Vector2(r * 2f, r * 2f);
            _pulseImg.color = WithAlpha(_cfg.confirmPulseColor, _cfg.confirmPulseColor.a * (1f - u));
        }

        private Image RingAt(int i)
        {
            while (_rings.Count <= i)
            {
                var img = NewImage(_ringsRoot, $"Ring_{_rings.Count}", out _);
                img.sprite = _ringSprite;
                img.type = Image.Type.Simple;
                _rings.Add(img);
            }
            var r = _rings[i];
            if (!r.enabled) r.enabled = true;
            return r;
        }

        // dreamcatcher-attach-lockon 계약 #3 — draw-order 는 생성순이 아니라 sibling
        // index 로 강제. dim(맨 아래) < [손패] < 링 < 화살표 < 리티클 < 콜아웃 < 펄스.
        public void EnforceSiblingOrder(Transform arrow)
        {
            _dim.rectTransform.SetAsFirstSibling();
            _ringsRoot.SetAsLastSibling();
            if (arrow != null) arrow.SetAsLastSibling();
            _reticleRoot.SetAsLastSibling();
            _callout.SetAsLastSibling();
            _pulse.SetAsLastSibling();
        }

        // ── build helpers ────────────────────────────────────────────────────

        // 절대(screen) 배치가 명확하도록 center 점앵커로 통일 — sizeDelta == 실제 크기,
        // position == 절대 스크린 픽셀. dim(stretch)/콜아웃 자식은 생성 후 개별 override.
        private RectTransform NewNode(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            CenterAnchor(rt);
            return rt;
        }

        private Image NewImage(Transform parent, string name, out RectTransform rt)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            rt = (RectTransform)go.transform;
            CenterAnchor(rt);
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            return img;
        }

        private static void CenterAnchor(RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
        }

        private TextMeshProUGUI NewText(string name, Transform parent, float size,
            FontStyles style, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (_font != null) t.font = _font;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = align;
            t.raycastTarget = false;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Ellipsis;
            return t;
        }

        private static Color WithAlpha(Color c, float a) { c.a = a; return c; }

        private static void EnforceMinSize(ref Rect r, float min)
        {
            if (r.width < min) { float d = (min - r.width) * 0.5f; r.xMin -= d; r.xMax += d; }
            if (r.height < min) { float d = (min - r.height) * 0.5f; r.yMin -= d; r.yMax += d; }
        }

        private static Rect LerpRect(Rect a, Rect b, float t) => Rect.MinMaxRect(
            Mathf.Lerp(a.xMin, b.xMin, t), Mathf.Lerp(a.yMin, b.yMin, t),
            Mathf.Lerp(a.xMax, b.xMax, t), Mathf.Lerp(a.yMax, b.yMax, t));
    }
}
