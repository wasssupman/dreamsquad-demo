using Unity.Entities;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Core.TimeControl;
using Wassup.Data;

namespace Wassup.Presentation
{
    // sprite-unit-backend unit 2a/2b — 스프라이트 시트 유닛 뷰. UnitView 의 두 번째 구현체.
    //
    // SpineUnitView 와 **같은 계약, 다른 렌더러**다. 시뮬은 두 백엔드를 구분하지 못한다 — 풀(SpineUnitPool)이
    // 유닛 SO 의 UnitSpriteMotionSet 유무로 어느 컴포넌트를 붙일지 고르고, 그 뒤 20여 소비 seam 은
    // UnitView 멤버만 부른다. 프레임 진행·클럭은 SpriteFlipbookPlayer 가 소유하고 여기는 «어떤 시트를
    // 언제 트는가»(Spine 의 «어떤 트랙을 언제 트는가»에 대응)와 transform/색 반응만 소유한다.
    //
    // 상속으로 SpineUnitView 와 구현을 공유하지 않는다(README 계약). transform/색/카메라 수학은 그쪽에서
    // 그대로 복사했다 — 임시 기능이 Spine 경로에 회귀 위험을 만드는 쪽이 중복보다 비싸다.
    //
    // 재생기에 완료 콜백이 없으므로(그 spec 의 결정 — 첫 소비자가 요구할 때) 원샷 완주는 Update 가
    // 폴링한다(FlipbookCharacterView.PollPlayback 과 같은 형태). 전이가 최대 1프레임 늦는 것은 의도된 대가.
    [DisallowMultipleComponent]
    public class SpriteUnitView : UnitView
    {
        private SpriteRenderer _sr;
        private SpriteFlipbookPlayer _player;
        private ISpineUnitVisualData _visualData;
        private IDefenderSpineExtras _defenderExtras;
        private UnitSpriteMotionSet _set;
        private Entity _entity;
        private bool _dying;
        // 진행 중인 원샷(attack/deploy/death). null = 로코모션 루프 중.
        private SpriteFlipbookData _oneShot;
        private float _pendingFlipAccum;
        // sim 좌표 보존 — transform.position 은 view 좌표(ToView)라 sorting 셀 역산에 쓸 수 없다.
        private Vector3 _simWorld;
        private BlobShadow _blob;
        private WeaponTrailRig _weaponTrail;
        // 배틀 스케일은 재생기가 도메인 클럭으로 이미 따른다. 여기 값은 hop 진행·이동 측정용(Spine 과 동일 용도).
        private float _battleScale = 1f;
        private float _walkFactor = 1f;
        private float _smoothedSpeed;
        private bool _moving;
        private const float SimDtEpsilon = 1e-5f;
        private const float LocoMoveOnFrac = 0.15f;
        private const float LocoMoveOffFrac = 0.05f;

        public override Entity Entity => _entity;

        // 「지금 무엇을 재생 중인가」 — 플립북 에셋 이름. Spine 의 트랙0 애니 이름에 대응.
        public override string CurrentAnimationName => _player != null && _player.Current != null ? _player.Current.name : null;

        // 오른쪽(+x)을 보는가. 시트 규약(sheetFacesRight)으로 flipX 부호를 정규화한다 —
        // SkeletonFlipXModifier 가 리그 규약을 데이터에서 정규화하는 것의 스프라이트 대응.
        private bool FacingRight => _set.SheetFacesRight ? !_sr.flipX : _sr.flipX;

        public void Spawn(ISpineUnitVisualData visualData, IDefenderSpineExtras defenderExtras,
            UnitSpriteMotionSet set, Entity entity, Vector3 worldPos)
        {
            _visualData = visualData;
            _defenderExtras = defenderExtras;
            _set = set;
            _entity = entity;

            // SpineUnitView.Spawn 과 같은 순서 — _baseScale 을 위치 갱신보다 먼저(위치 갱신이 lift → 스케일까지 파생).
            float s = Mathf.Max(0.01f, visualData.SpineVisualScale * BattleBridge.CharacterVisualScale);
            _baseScale = new Vector3(s, s, s);
            ApplyRenderScale();
            ApplyRenderPosition(worldPos);

            _sr = gameObject.AddComponent<SpriteRenderer>();
            _sr.flipX = false;                       // 규약: 기본은 왼쪽을 본다. 시트가 오른쪽이면 FacingRight 가 그걸 흡수.
            _sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // 실그림자 N/A(README) — 블롭이 그림자다.
            _player = gameObject.AddComponent<SpriteFlipbookPlayer>();
            _player.TimeDomain = TimeDomain.Battle;  // 슬로우모 동반 — SpineUnitView 의 skeleton.timeScale 에 해당

            AttachWeaponTrail();
            PlayLocomotion(force: true);

            // tilted-billboard unit 0 — 두 백엔드가 같은 두 줄(QuadUnitView 도 같다).
            var billboard = gameObject.AddComponent<Billboard>();
            billboard.Setup(BillboardMode.Tilted, BattleBridge.CharacterBillboardTilt);

            if (BattleBridge.BlobShadowSprite != null)
                _blob = BlobShadow.Attach(transform, BattleBridge.BlobShadowSprite,
                    2f * _visualData.BodyRadiusTiles * BattleBridge.TileToWorld,
                    BattleBridge.BlobShadowColor,
                    BattleBridge.BlobShadowLift, BoardSortOrder.ShadowOrder, live: true);

            SetAnimationTimeScale(TimeManager.Instance.ScaleOf(TimeDomain.Battle));
        }

        // 배틀 스케일은 재생기가 TimeManager 도메인 클럭으로 따르므로 여기서 재생 속도를 건드리지 않는다.
        // 값은 hop 진행·이동 측정(Time.deltaTime × battleScale = sim dt)에만 쓴다 — Spine 과 같은 용도.
        public override void SetAnimationTimeScale(float scale) => _battleScale = scale;

        private void Update()
        {
            if (_player == null) return;
            if (_dying)
            {
                if (!_player.IsPlaying) Destroy(gameObject);
                return;
            }
            // 원샷 완주 → 로코모션 복귀. 재생기에 콜백이 없어 폴링한다.
            if (_oneShot != null && !_player.IsPlaying)
            {
                _oneShot = null;
                PlayLocomotion(force: true);
            }
        }

        // ---- 로코모션 ---------------------------------------------------------------------

        // 현재 이동 상태의 루프(walk/idle). 원샷 진행 중이면 건드리지 않고 완주 복귀에 맡긴다(Spine 과 같은 게이트).
        private void PlayLocomotion(bool force = false)
        {
            if (_dying || _set == null) return;
            if (!force && _oneShot != null) return;
            var desired = _set.ResolveLocomotion(_moving);
            if (desired == null) return;
            if (!force && _player.Current == desired && _player.IsPlaying) { ApplySpeed(); return; }
            _player.Play(desired);
            ApplySpeed();
        }

        // 최종 재생 배율 = 걷기 배율(이동 중 + 로코모션 루프일 때만). 원샷은 1(공격 압축은 PlayAttack 이 따로 세팅).
        // Spine ApplyTimeScale 과 같은 규칙 — 정지 유닛이 minTimeScale 로 느려지는 회귀 방지.
        private void ApplySpeed()
        {
            if (_player == null || _oneShot != null) return;
            _player.Speed = (_moving && _player.IsLooping) ? _walkFactor : 1f;
        }

        public override void UpdatePosition(Vector3 world)
        {
            FaceAlongMovement(world);
            UpdateWalkTimeScale(world);   // _simWorld 갱신 전에 측정(직전 프레임 sim 위치 대비)
            PlayLocomotion();             // _moving 이 바뀌면 walk↔idle 스위칭
            AdvanceHop();
            ApplyRenderPosition(world);
        }

        // enemy-walk-anim-speed unit 1/4 — SpineUnitView.UpdateWalkTimeScale 복사. 프레임당 view 변위로
        // 고유 속도를 추정해 걷기 배율과 이동/정지 히스테리시스를 갱신한다.
        private void UpdateWalkTimeScale(Vector3 world)
        {
            // Spine 과 같은 게이트 — 스타일 SO 미할당이면 측정도 이동 판정도 하지 않는다(현행 동작 그대로).
            if (!BattleBridge.WalkAnimSpeedEnabled || _dying) return;
            float simDt = Time.deltaTime * _battleScale;
            if (simDt <= SimDtEpsilon) return;
            float disp = Vector3.Distance(
                (Vector3)Wassup.Core.BoardSpace.ToView(world),
                (Vector3)Wassup.Core.BoardSpace.ToView(_simWorld));
            if (disp >= BattleBridge.WalkAnimTeleportGuard) return;
            float simSpeed = disp / simDt;
            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, simSpeed, BattleBridge.WalkAnimSmoothing);
            _walkFactor = Mathf.Clamp(_smoothedSpeed / BattleBridge.WalkAnimRefSpeed,
                BattleBridge.WalkAnimMinTimeScale, BattleBridge.WalkAnimMaxTimeScale);
            float refSpeed = Mathf.Max(0.01f, BattleBridge.WalkAnimRefSpeed);
            if (_moving && _smoothedSpeed < refSpeed * LocoMoveOffFrac) _moving = false;
            else if (!_moving && _smoothedSpeed > refSpeed * LocoMoveOnFrac) _moving = true;
            ApplySpeed();
        }

        // ---- 위치 · 스케일 (SpineUnitView 복사) ------------------------------------------------

        private void ApplyRenderPosition(Vector3 world)
        {
            _simWorld = world;
            Vector3 offset = _visualData != null ? (Vector3)_visualData.SpineVisualOffset : Vector3.zero;
            float lift = CurrentHopOffset() + _flightHeight;
            transform.position = (Vector3)Wassup.Core.BoardSpace.ToView(world) + offset
                                 + new Vector3(0f, lift, 0f);
            if (_blob != null) _blob.ClearGroundAnchor();
            ApplyLift(lift);
        }

        private void ApplyLift(float lift)
        {
            UnitLiftVisual.Resolve(lift, out float unitScale, out float shadowScale, out float shadowAlpha);
            _flightScale = unitScale;
            ApplyRenderScale();
            if (_blob != null) _blob.SetFlight(shadowScale, shadowAlpha);
        }

        // 스케일 쓰기의 단일 지점 — 피드(비행)·펀치·스쿼시가 슬롯을 각자 소유하고 여기서 곱한다.
        private Vector3 _baseScale = Vector3.one;
        private float _flightScale = 1f;
        private float _punchScale = 1f;
        private Vector3 _squash = Vector3.one;

        private void ApplyRenderScale()
            => transform.localScale = Vector3.Scale(_baseScale * (_flightScale * _punchScale), _squash);

        private float _flightHeight;
        public override void SetFlightHeight(float viewSpaceHeight) => _flightHeight = viewSpaceHeight;

        private float _hopElapsed = -1f;
        private float _hopDuration;
        private float _hopHeight;

        public override void PlayKnockupHop(float durationSec, float height)
        {
            if (durationSec <= 0f || height <= 0f) return;
            _hopElapsed = 0f;
            _hopDuration = durationSec;
            _hopHeight = height;
        }

        private void AdvanceHop()
        {
            if (_hopElapsed < 0f) return;
            _hopElapsed += Time.deltaTime * _battleScale;
            if (_hopElapsed >= _hopDuration) _hopElapsed = -1f;
        }

        private float CurrentHopOffset()
        {
            if (_hopElapsed < 0f) return 0f;
            float t = _hopElapsed / _hopDuration;
            return _hopHeight * 4f * t * (1f - t);
        }

        // 렌더러가 하나라 스윕이 없다 — Spine 이 GetComponentsInChildren 으로 돌며 궤적 리그·블롭을 제외하던
        // 가드가 여기선 필요 없다(블롭·리그는 자식이고 자기 대역을 소유한다).
        public override void UpdateSortingOrder(Unity.Mathematics.int2 gridSize, float tileSize)
        {
            if (_sr == null) return;
            _sr.sortingOrder = BoardSortOrder.ComputeFromWorld(gridSize, _simWorld, tileSize, BoardSortOrder.CharacterOffset);
        }

        public override bool TryGetScreenRect(Camera cam, out Rect rect)
        {
            rect = default;
            if (cam == null || _dying || _sr == null || _sr.sprite == null) return false;
            var b = _sr.bounds;
            Vector2 lo = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 hi = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? b.min.x : b.max.x,
                    (i & 2) == 0 ? b.min.y : b.max.y,
                    (i & 4) == 0 ? b.min.z : b.max.z);
                var sp = cam.WorldToScreenPoint(corner);
                if (sp.z <= 0f) return false;
                lo = Vector2.Min(lo, new Vector2(sp.x, sp.y));
                hi = Vector2.Max(hi, new Vector2(sp.x, sp.y));
            }
            rect = Rect.MinMaxRect(lo.x, lo.y, hi.x, hi.y);
            return true;
        }

        public override float ApproxWorldHeight =>
            _sr != null && _sr.sprite != null ? _sr.bounds.size.y : Mathf.Abs(transform.lossyScale.y);

        public override void SetFlightView(Vector3 viewPos, float lift = 0f, Vector3 groundAnchor = default)
        {
            transform.position = viewPos;
            if (_sr != null) _sr.sortingOrder = BoardSortOrder.DragPreviewOrder;
            if (_blob != null)
            {
                if (groundAnchor == default) _blob.ClearGroundAnchor();
                else _blob.SetGroundAnchor(groundAnchor);
            }
            ApplyLift(lift);
        }

        // ---- facing ---------------------------------------------------------------------------

        private void FaceAlongMovement(Vector3 world)
        {
            if (_dying || IsAttackPlaying()) return;
            float dx = ((Vector3)Wassup.Core.BoardSpace.ToView(world)).x
                       - ((Vector3)Wassup.Core.BoardSpace.ToView(_simWorld)).x;
            SetFacingByViewDelta(dx, immediate: false);
        }

        private bool IsAttackPlaying() =>
            _oneShot != null && _oneShot == _set.Attack && _player != null && _player.IsPlaying;

        // 판정은 UnitFacing(Spine 과 공유). 여기는 flipX 번역만 — 시트 규약은 FacingRight 가 흡수한다.
        private void SetFacingByViewDelta(float dx, bool immediate)
        {
            if (_dying || _sr == null) return;
            if (!UnitFacing.ShouldFlip(dx, FacingRight, immediate, ref _pendingFlipAccum)) return;
            bool wantRight = dx >= 0f;
            _sr.flipX = _set.SheetFacesRight ? !wantRight : wantRight;
        }

        public override void FaceToward(Vector3 worldPoint)
        {
            if (_dying || _sr == null) return;
            float dx = ((Vector3)Wassup.Core.BoardSpace.ToView(worldPoint)).x - transform.position.x;
            SetFacingByViewDelta(dx, immediate: true);
        }

        // ---- 무기 궤적 -------------------------------------------------------------------------

        // 본이 없으므로 Bind(null) = 구조물 경로(부모 transform 만 따른다). 리그가 자식이어야 틸트 평면을 상속한다.
        private void AttachWeaponTrail()
        {
            var prefab = _visualData?.SpineWeaponTrailPrefab;
            if (prefab == null) return;
            var rig = Instantiate(prefab, transform);
            _weaponTrail = rig.GetComponent<WeaponTrailRig>();
            if (_weaponTrail != null) _weaponTrail.Bind(null);
        }

        // ---- 틴트 4축 (SpineUnitView 복사 — Skeleton.GetColor/SetColor → _sr.color) -------------------

        private bool _hoverHighlightActive;
        private Color _savedTint = Color.white;

        private Color Rgb(Color c) => new Color(c.r, c.g, c.b);
        private void SetRgb(Color rgb) { var c = _sr.color; c.r = rgb.r; c.g = rgb.g; c.b = rgb.b; _sr.color = c; }

        public override void SetHoverHighlight(bool on, Color tint)
        {
            if (_dying || _sr == null) return;
            if (on)
            {
                if (!_hoverHighlightActive) { _savedTint = Rgb(_sr.color); _hoverHighlightActive = true; }
                SetRgb(tint);
            }
            else if (_hoverHighlightActive)
            {
                _hoverHighlightActive = false;
                SetRgb(_savedTint);
            }
        }

        public override void SetHealthTint(Color tint)
        {
            if (_dying || _sr == null) return;
            if (_hoverHighlightActive) { _savedTint = tint; return; }   // 호버 중엔 저장값으로 흡수
            SetRgb(tint);
        }

        private bool _flashActive;
        private Color _flashRestore;

        public override void FlashWhite(float dur = 0.14f)
        {
            if (_dying || !gameObject.activeInHierarchy || _sr == null) return;
            StartCoroutine(FlashRoutine(dur));
        }

        private System.Collections.IEnumerator FlashRoutine(float dur)
        {
            // 복귀 목표 = resting 색. hover 중이면 _savedTint, 연발 중이면 앞 flash 의 restore 승계(흰빛 오염 방지).
            Color restore = _hoverHighlightActive ? _savedTint : (_flashActive ? _flashRestore : Rgb(_sr.color));
            _flashRestore = restore;
            _flashActive = true;
            SetRgb(Color.white);
            float e = 0f;
            while (e < dur)
            {
                e += Time.unscaledDeltaTime;
                if (_dying || _sr == null) yield break;
                float k = Mathf.Clamp01(e / dur);
                SetRgb(Color.Lerp(Color.white, restore, k));
                yield return null;
            }
            if (!_dying && _sr != null) SetRgb(_hoverHighlightActive ? _savedTint : restore);
            _flashActive = false;
        }

        // 드래그 배치 중 반투명. 실그림자 스윕은 없다(README — 스프라이트 렌더러는 캐스트 off 고정).
        public override void SetDimmed(bool transparent, float alpha)
        {
            float a = Mathf.Clamp01(alpha);
            if (!_dying && _sr != null) { var c = _sr.color; c.a = a; _sr.color = c; }
            _blob?.SetDimAlpha(transparent ? a : 1f);
        }

        // ---- 스케일 반응 (SpineUnitView 복사) ---------------------------------------------------

        public override void PlayPunch(float overshoot = 0.28f, float dur = 0.16f)
        {
            if (_dying || !gameObject.activeInHierarchy) return;
            StartCoroutine(PunchRoutine(overshoot, dur));
        }

        private System.Collections.IEnumerator PunchRoutine(float overshoot, float dur)
        {
            float peak = 1f + Mathf.Max(0f, overshoot);
            float half = Mathf.Max(0.01f, dur * 0.35f);
            float e = 0f;
            while (e < half) { e += Time.unscaledDeltaTime; if (_dying) yield break;
                _punchScale = Mathf.Lerp(1f, peak, e / half); ApplyRenderScale(); yield return null; }
            float back = Mathf.Max(0.01f, dur - half);
            e = 0f;
            while (e < back) { e += Time.unscaledDeltaTime; if (_dying) yield break;
                _punchScale = Mathf.Lerp(peak, 1f, e / back); ApplyRenderScale(); yield return null; }
            if (!_dying) { _punchScale = 1f; ApplyRenderScale(); }
        }

        private Coroutine _squashRoutine;

        public override void PlayLandingSquash(float amount, float seconds)
        {
            if (amount <= 0f || seconds <= 0f || _dying || !gameObject.activeInHierarchy) return;
            if (_squashRoutine != null) StopCoroutine(_squashRoutine);
            _squashRoutine = StartCoroutine(SquashRoutine(amount, seconds));
        }

        private System.Collections.IEnumerator SquashRoutine(float amount, float seconds)
        {
            // k 를 증분 **전에** 적용 — 첫 프레임에 authored amount 에 닿아야 세기가 프레임레이트에 안 묶인다.
            float e = 0f;
            while (true)
            {
                float k = 1f - Mathf.Clamp01(e / seconds);
                _squash = new Vector3(1f + amount * k, 1f - amount * k, 1f + amount * k);
                ApplyRenderScale();
                if (e >= seconds || _dying) break;
                yield return null;
                e += Time.unscaledDeltaTime;
            }
            _squash = Vector3.one;
            if (!_dying) ApplyRenderScale();
            _squashRoutine = null;
        }

        // ---- 모션 사건 -------------------------------------------------------------------------

        private void PlayOneShot(SpriteFlipbookData data, float speed)
        {
            _oneShot = data;
            _player.Play(data);
            _player.Speed = speed;
        }

        // 공격 애니를 실제 발사 주기에 맞춰 압축 재생(compress-to-fit) — Spine 의 entry.TimeScale = max(1, Duration/period)
        // 와 같은 식. 하한 1(느린 공격을 늘리지 않음), period<=0 이면 1. 시트가 없으면 Spine 이 트랙 없을 때처럼 조용히 무시.
        public override void PlayAttack(float attackAnimPeriod = 0f)
        {
            if (_dying || _player == null || _set == null) return;
            var attack = _set.Attack;
            if (attack == null || attack.FrameCount == 0 || attack.Fps <= 0f) return;
            float duration = attack.FrameCount / attack.Fps;
            float speed = attackAnimPeriod > 0f && duration > 0f ? Mathf.Max(1f, duration / attackAnimPeriod) : 1f;
            PlayOneShot(attack, speed);
            // 궤적은 스윙 구간에만 — 압축 배율로 나눈다. 슬로우모는 재생기 도메인 클럭이 이미 반영하므로 여기선 안 나눈다
            // (Spine 은 skeleton.timeScale 로 따로 나눴다 — 그 항이 여기선 클럭 자체에 들어 있다).
            if (_weaponTrail != null && _visualData != null)
                _weaponTrail.Play(duration * Mathf.Clamp01(_visualData.SpineWeaponTrailEndNormalized) / speed);
        }

        public override bool PlayDeploy()
        {
            if (_dying || _player == null || _set == null) return false;
            if (_defenderExtras == null) return false;   // 적 가드(Spine 과 동일)
            var deploy = _set.ResolveDeploy();
            if (deploy == null) return false;
            if (deploy.Loop) { PlayLocomotion(force: true); return true; }   // idle 까지 폴백된 경우 — 원샷 폴링에 걸리면 영영 안 끝난다
            PlayOneShot(deploy, 1f);
            return true;
        }

        public override void Kill()
        {
            if (_dying) return;
            _dying = true;
            // 사망 프레임에 비행·펀치·스쿼시 배율 원복 — Kill 뒤엔 UpdatePosition 이 안 온다(Spine 과 같은 이유).
            _flightScale = 1f;
            _punchScale = 1f;
            _squash = Vector3.one;
            ApplyRenderScale();
            if (_blob != null) _blob.SetFlight(1f, 1f);
            var death = _set != null ? _set.Death : null;
            if (_player == null || death == null || death.FrameCount == 0 || death.Loop)
            {
                Destroy(gameObject);   // death 미저작 = 즉시 파괴. 루프 시트는 완주가 없어 같은 취급.
                return;
            }
            PlayOneShot(death, 1f);   // 완주는 Update 폴링 → Destroy
        }

        public override void Dispose()
        {
            _dying = true;
            if (this != null) Destroy(gameObject);
        }

        // ---- 앵커 -------------------------------------------------------------------------------

        // 본 추적 분기가 없다 — 저작 필드(SpineCastAnchorLocalOffset)만 재사용하고 Spine 의 정적 폴백 8줄을 복사했다.
        // 부호: Spine 은 ScaleX<0(=오른쪽 봄)일 때 x 반전 — 여기선 같은 조건이 FacingRight 다.
        public override Vector3 ResolveCastAnchor()
        {
            if (_defenderExtras == null) return transform.position;
            var off = _defenderExtras.SpineCastAnchorLocalOffset;
            if (FacingRight) off.x = -off.x;
            return transform.TransformPoint(off);
        }

        public override Vector3 ResolveProjectileLaunchAnchor()
        {
            if (_defenderExtras == null)
                return _sr != null && _sr.sprite != null ? _sr.bounds.center : transform.position;
            return ResolveCastAnchor();
        }
    }
}
