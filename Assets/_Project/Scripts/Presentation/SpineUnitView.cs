using Spine;
using Spine.Unity;
using Unity.Entities;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Core.TimeControl;
using Wassup.Data;

namespace Wassup.Presentation
{
    [DisallowMultipleComponent]
    public class SpineUnitView : MonoBehaviour
    {
        private SkeletonAnimation _skeleton;
        private ISpineUnitVisualData _visualData;
        private IDefenderSpineExtras _defenderExtras;
        private Entity _entity;
        private bool _dying;
        private string _attackAnimationName;
        private const float FacingMoveEpsilon = 0.001f;
        // tilemap-view-backend unit 3 — sim 좌표 보존. transform.position 은 view 좌표(ToView)라
        // sorting 셀 역산에 쓸 수 없다(z 소실). sorting 은 이 sim 좌표로 계산한다.
        private Vector3 _simWorld;
        // placement-enemy-see-through unit 2 — dim 페이드용 blob 참조.
        private BlobShadow _blob;
        // dreamcatcher-awakening-hand rev 4 — 스크린 픽킹용 렌더러 캐시(Spawn 시 1회).
        private MeshRenderer _meshRenderer;
        private bool _shadowTransparent; // 실그림자 토글 상태 캐시(매 프레임 alloc 방지).
        // enemy-walk-anim-speed unit 1 — 걷기 애니 속도 변조 상태.
        // _battleScale: 슬로우모/정지 스케일(SpineUnitPool 이 ScaleChanged 로 fan-out).
        // _walkFactor: 이동속도 기반 배율(SO 미할당 시 1 = 현행 동작). _smoothedSpeed: 변위 EMA.
        private float _battleScale = 1f;
        private float _walkFactor = 1f;
        private float _smoothedSpeed;
        private const float SimDtEpsilon = 1e-5f;
        // enemy-walk-anim-speed unit 4 — 이동/정지 상태(히스테리시스). 두 곳이 소비한다:
        //  (1) ApplyTimeScale — 걷기 배율은 **이동 중일 때만** 적용, 정지 유닛은 factor 1(자연속도).
        //      → 정지 유닛(디펜더/멈춘 적/보스)이 minTimeScale(0.15)로 슬로모 재생되던 버그 해소.
        //  (2) UpdateLocomotionAnimation — walkAnimation 설정 시 이동=walk / 정지=idle 스위칭.
        // 임계는 "정지" 판정이 목적이라 낮게(ref 대비 분율): 느린 이동은 여전히 배율 동기(발 접지 유지).
        private bool _moving;
        private const float LocoMoveOnFrac = 0.15f;   // _smoothedSpeed > ref×이 값 → 이동
        private const float LocoMoveOffFrac = 0.05f;  // _smoothedSpeed < ref×이 값 → 정지
        private const float LocoMixDuration = 0.15f;  // walk↔idle 크로스페이드 초
        // spine-weapon-trail unit 3 — 무기 궤적 리그(프리팹 할당분만 비null). 타이머와
        // 부착 로직은 리그가 소유한다 — 뷰는 붙이고 재생 신호만 준다.
        private WeaponTrailRig _weaponTrail;

        public Entity Entity => _entity;

        public void Spawn(ISpineUnitVisualData visualData, IDefenderSpineExtras defenderExtras, Entity entity, Vector3 worldPos)
        {
            _visualData = visualData;
            _defenderExtras = defenderExtras;
            _entity = entity;
            // flight-lift-feel unit 1 — _baseScale 을 ApplyRenderPosition 보다 **먼저** 잡는다.
            // 위치 갱신이 이제 lift → 스케일 파생까지 하므로, 기준이 없으면 스폰 프레임에 Vector3.one
            // 로 한 번 써버린다.
            float s = Mathf.Max(0.01f, visualData.SpineVisualScale * BattleBridge.CharacterVisualScale);
            _baseScale = new Vector3(s, s, s); // card-fly unit 1 — 펀치 펄스 복귀 기준
            transform.localScale = _baseScale;
            ApplyRenderPosition(worldPos);

            _skeleton = gameObject.AddComponent<SkeletonAnimation>();
            _skeleton.skeletonDataAsset = visualData.SpineSkeletonDataAsset;
            _skeleton.initialSkinName = string.IsNullOrEmpty(visualData.SpineSkinName) ? "default" : visualData.SpineSkinName;
            _skeleton.Initialize(true);
            _meshRenderer = GetComponent<MeshRenderer>();

            // unit-parts-appearance 1 — 단일/조합 스킨 + 슬롯 틴트는 공용 헬퍼가 소유
            // (드래그 프리뷰와 동일 경로).
            if (_skeleton.Skeleton != null)
                SpineCombinedSkinCache.Apply(_skeleton.Skeleton, visualData);

            AttachWeaponTrail();

            PlayIdleLooping();

            // tilted-billboard unit 0 — 틸트는 Billboard 컴포넌트가 소유. 스폰 시 1회 주입
            // (tilemapBillboardTilt, BattleBridge 가 세팅).
            // 카메라 yaw 고정(0) 전제라 월드 X 틸트로 충분. ScaleX(좌우반전)는 skeleton 채널이라 독립.
            var billboard = gameObject.AddComponent<Billboard>();
            billboard.Setup(BillboardMode.Tilted, BattleBridge.CharacterBillboardTilt);

            ApplyTilemapShadow();

            // time-manager Unit 4 — 스폰 순간의 Battle 스케일을 pull 해 초기화(슬로우모/정지 중
            // 스폰된 유닛도 즉시 동기화; ScaleChanged 이벤트는 스폰 이후 변화만 전달하므로 레이스 방지).
            SetAnimationTimeScale(TimeManager.Instance.ScaleOf(TimeDomain.Battle));
        }

        // time-manager Unit 4 — 전투 표현 재생 속도를 Battle 도메인 스케일에 맞춘다.
        // spine-unity 는 Time.deltaTime * timeScale 로 진행하는데 전역 timeScale 은 1 고정이라,
        // 이 값을 직접 세팅하지 않으면 슬로우모에서 애니만 풀스피드로 튀어 시뮬과 desync 된다.
        // enemy-walk-anim-speed unit 1 — Battle 스케일과 걷기 배율(_walkFactor)을 곱해 합성한다.
        // 이 진입점은 Battle 스케일만 갱신하고 실제 세팅은 ApplyTimeScale 이 담당(정지 프리즈 유지).
        public void SetAnimationTimeScale(float scale)
        {
            _battleScale = scale;
            ApplyTimeScale();
        }

        // enemy-walk-anim-speed unit 1 — 최종 재생속도 = Battle 도메인 스케일 × 걷기 배율.
        // battleScale=0(정지)이면 곱해서 0 → 프리즈.
        // 단 timeScale 은 트랙 전역 배율이라, 걷기 배율은 **로코모션 루프(걷기/idle)가 재생 중일 때만**
        // 적용한다. 공격/사망/배치 같은 원샷(loop=false)에는 배율 1 — 정지 유닛의 walkFactor(→minTimeScale)가
        // 공격 애니까지 느리게 만드는 회귀 방지.
        private void ApplyTimeScale()
        {
            if (_skeleton == null) return;
            // 걷기 배율은 **이동 중 + 로코모션 루프**일 때만. 정지 유닛(디펜더/멈춘 적/보스)과
            // 원샷(공격/사망/배치)은 factor 1 = 자연속도(battleScale 만). 이게 "정지 유닛 슬로모"
            // 회귀 방지의 본질 — minTimeScale 은 느린 '이동' 하한이지 정지 유닛에 쓰라는 게 아니다.
            float factor = (_moving && IsLocomotionLoopPlaying()) ? _walkFactor : 1f;
            _skeleton.timeScale = _battleScale * factor;
        }

        // 걷기 배율 적용 대상 판정: track0 의 현재 애니가 루프면 로코모션(걷기/idle)으로 본다.
        // 공격/사망/배치는 loop=false 로 세팅되므로 자연 배제된다.
        private bool IsLocomotionLoopPlaying()
        {
            if (_dying || _skeleton == null) return false;
            var current = _skeleton.AnimationState?.GetCurrent(0);
            return current != null && current.Loop;
        }

        // tilemap-real-shadows — Tilemap 모드 그림자: 진짜(빌보드 cast) vs 블롭(상호배타).
        // 진짜 = renderer 가 실루엣 그림자 cast(평면이라 TwoSided). 블롭 = 발밑 타원 + cast OFF.
        private void ApplyTilemapShadow()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            if (BattleBridge.UseRealShadows)
            {
                for (int i = 0; i < renderers.Length; i++)
                    renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            }
            else
            {
                for (int i = 0; i < renderers.Length; i++)
                    renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (BattleBridge.BlobShadowSprite != null)
                    _blob = BlobShadow.Attach(transform, BattleBridge.BlobShadowSprite, BattleBridge.BlobShadowSize,
                        BattleBridge.BlobShadowColor,
                        BattleBridge.BlobShadowGroundY, BoardSortOrder.ShadowOrder, live: true); // 유닛은 이동 — 매 프레임 따라감
            }
        }

        public void UpdatePosition(Vector3 world)
        {
            FaceAlongMovement(world);
            // enemy-walk-anim-speed unit 1 — 걷기 배율은 ApplyRenderPosition 이 _simWorld 를
            // 갱신하기 전에 측정한다(_simWorld = 직전 프레임 sim 위치).
            UpdateWalkTimeScale(world);
            // enemy-walk-anim-speed unit 4 — 갱신된 _smoothedSpeed 로 walk↔idle 전환.
            UpdateLocomotionAnimation();
            AdvanceHop(); // knockup unit 3 — 호핑 시간 진행은 프레임당 여기서만
            ApplyRenderPosition(world);
        }

        // enemy-walk-anim-speed unit 1 — 프레임당 실제 view 변위로 고유 이동속도를 추정해
        // 걷기 애니 배율(_walkFactor)을 변조한다. sim-time 정규화(disp / (realDt × battleScale))로
        // 슬로우모 이중감산을 피하고, 포탈 텔레포트는 변위 임계값으로 무시한다.
        // SO 미할당(WalkAnimSpeedEnabled=false)이면 _walkFactor=1 유지 → 현행 동작.
        private void UpdateWalkTimeScale(Vector3 world)
        {
            if (!BattleBridge.WalkAnimSpeedEnabled || _dying) return;
            float simDt = Time.deltaTime * _battleScale;
            if (simDt <= SimDtEpsilon) return; // 정지/도메인리로드 프레임 — 직전 배율 유지(ApplyTimeScale 은 battleScale 로 프리즈)
            float disp = Vector3.Distance(
                (Vector3)Wassup.Core.BoardSpace.ToView(world),
                (Vector3)Wassup.Core.BoardSpace.ToView(_simWorld));
            if (disp >= BattleBridge.WalkAnimTeleportGuard) return; // 포탈 점프 — 측정 스킵
            float simSpeed = disp / simDt;
            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, simSpeed, BattleBridge.WalkAnimSmoothing);
            _walkFactor = Mathf.Clamp(_smoothedSpeed / BattleBridge.WalkAnimRefSpeed,
                BattleBridge.WalkAnimMinTimeScale, BattleBridge.WalkAnimMaxTimeScale);
            // unit 4 — 이동/정지 히스테리시스(ApplyTimeScale + 로코 스위칭 공용). 모든 유닛에
            // 적용 — 정지 판정이라 임계 낮음(느린 이동은 여전히 _moving=true 로 배율 동기 유지).
            float refSpeed = Mathf.Max(0.01f, BattleBridge.WalkAnimRefSpeed);
            if (_moving && _smoothedSpeed < refSpeed * LocoMoveOffFrac) _moving = false;
            else if (!_moving && _smoothedSpeed > refSpeed * LocoMoveOnFrac) _moving = true;
            ApplyTimeScale();
        }

        // enemy-spawn-positioning 0 — 렌더 위치의 단일 지점: sim 좌표(ToView) + 유닛 타입별 피봇 오프셋.
        // _simWorld 은 순수 sim 좌표 유지(정렬/셀 역산용). visualOffset 은 view-space 시각 보정만(sim 무영향).
        private void ApplyRenderPosition(Vector3 world)
        {
            _simWorld = world;
            Vector3 offset = _visualData != null ? (Vector3)_visualData.SpineVisualOffset : Vector3.zero;
            // flight-lift-feel unit 1 — 이 합이 곧 lift(지면에서 뜬 view 공간 높이)다. 위치·크기·
            // 그림자의 **공통 입력**이라 한 지점에서 구해 함께 흘린다.
            float lift = CurrentHopOffset() + _flightHeight;
            transform.position = (Vector3)Wassup.Core.BoardSpace.ToView(world) + offset
                                 + new Vector3(0f, lift, 0f);
            ApplyLift(lift);
        }

        // flight-lift-feel unit 1 — lift → 유닛 확대 + 그림자 축소·페이드.
        // 매 프레임 피드가 값을 다시 쓰므로(비행 아니면 lift 0 → 항등) 별도 clear 경로가 필요 없다
        // — _flightHeight 가 쓰던 규약 그대로다.
        private void ApplyLift(float lift)
        {
            UnitLiftVisual.Resolve(lift, out float unitScale, out float shadowScale, out float shadowAlpha);
            _flightScale = unitScale;
            ApplyRenderScale();
            if (_blob != null) _blob.SetFlight(shadowScale, shadowAlpha);
        }

        // flight-lift-feel unit 1 — **스케일 쓰기의 단일 지점.** transform.localScale 직접 대입 금지:
        // 매 프레임 피드(비행)와 코루틴(펀치·착지 스쿼시)이 같은 필드를 다투면 한쪽이 조용히 진다
        // (피드가 펀치를 덮거나, 펀치 종료의 복귀 대입이 비행 배율을 지운다).
        // ApplyRenderPosition 이 hop + flightHeight 를 한 곳에서 합치는 것과 같은 모양.
        private float _flightScale = 1f;   // 매 프레임 피드 소유
        private float _punchScale = 1f;    // PunchRoutine 소유
        private Vector3 _squash = Vector3.one; // 착지 스쿼시 소유(unit 3)

        private void ApplyRenderScale()
            => transform.localScale = Vector3.Scale(_baseScale * (_flightScale * _punchScale), _squash);

        // boss-jjangssen unit 7 — 뷰 비행(보스 도약) 아치 높이. 넉업 hop 과 **같은 이유로**
        // ToView 뒤에 더한다: BoardSpace.ToView 는 sim-Y 를 버리므로 sim 좌표에 높이를 넣으면
        // 화면에서 평면화되고, camUp 의 보드 평면 성분만 살아 "뜨는" 대신 옆으로 미끄러진다.
        // hop 과 독립 슬롯인 이유 = 도약 중 넉업을 맞을 수 있고 둘은 합산돼야 한다.
        // 매 프레임 피드가 값을 다시 쓰므로(비행 아니면 0) 별도 clear 가 필요 없다.
        private float _flightHeight;

        public void SetFlightHeight(float viewSpaceHeight) => _flightHeight = viewSpaceHeight;

        // knockup-fighter-defender unit 3 — 넉업 띄우기. sim 은 이 유닛이 떠 있다는 사실을
        // 모른다(심의 실체는 짧은 Stun) — 여기서만 해석하는 순수 뷰 오프셋이다.
        // ⚠ sim-Y 에 넣으면 안 된다: 평면 tilemap 보드라 BoardSpace.ToView 가 sim-Y 를 버려
        // 화면에 아무 변화가 없다. 그래서 ToView **뒤에** view 공간 Y 로 더한다.
        private float _hopElapsed = -1f;   // <0 = 비활성
        private float _hopDuration;
        private float _hopHeight;

        public void PlayKnockupHop(float durationSec, float height)
        {
            if (durationSec <= 0f || height <= 0f) return;
            // 재신호는 재시작 — 연속 히트로 계속 떠 있는 것이 의도(스턴도 remainingTime=max 로 갱신).
            _hopElapsed = 0f;
            _hopDuration = durationSec;
            _hopHeight = height;
        }

        // 시간 진행은 프레임 진입점(UpdatePosition)에서 **한 번만** 한다. ApplyRenderPosition 은
        // Spawn 에서도 불리므로 거기서 진행시키면 스폰 프레임에 한 칸 건너뛴다.
        private void AdvanceHop()
        {
            if (_hopElapsed < 0f) return;
            // 배틀 스케일을 따른다 — 슬로모 중엔 천천히 뜨고 천천히 떨어져야 스턴 지속(sim 시간)과
            // 착지 시점이 어긋나지 않는다.
            _hopElapsed += Time.deltaTime * _battleScale;
            if (_hopElapsed >= _hopDuration) _hopElapsed = -1f;
        }

        private float CurrentHopOffset()
        {
            if (_hopElapsed < 0f) return 0f;
            float t = _hopElapsed / _hopDuration;      // 0..1
            return _hopHeight * 4f * t * (1f - t);     // 포물선: 양끝 0, 중앙 최고
        }

        public void UpdateSortingOrder(Unity.Mathematics.int2 gridSize, float tileSize)
        {
            // sim 좌표로 셀 역산 — view 좌표(transform.position)는 z 가 소실돼 행 정렬이 붕괴한다.
            int order = BoardSortOrder.ComputeFromWorld(
                gridSize,
                _simWorld,
                tileSize,
                BoardSortOrder.CharacterOffset);
            var renderers = GetComponentsInChildren<Renderer>(true);
            // spine-weapon-trail — 궤적 리그 하위는 **건너뛴다**. 리본 메시는 씬 루트라
            // 이 스윕에 안 걸리지만 프리셋의 pointA 파티클은 리그의 자식이라 걸린다.
            // 그대로 두면 파티클만 유닛 대역(수백)으로 끌려가 리본(15500)과 갈라지고
            // 앞 유닛에 가린다(실측: 파티클 111 vs 리본 15500). 리그가 자기 대역을 소유한다.
            Transform rigRoot = _weaponTrail != null ? _weaponTrail.transform : null;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (rigRoot != null && renderers[i].transform.IsChildOf(rigRoot)) continue;
                renderers[i].sortingOrder = order;
            }
        }

        // dreamcatcher-awakening-hand rev 4 — 스크린 스페이스 픽킹: 스프라이트
        // 렌더러 월드 AABB 를 화면에 투영한 사각형. 포인터가 유닛 "몸체" 위인지의
        // 판정 근거다 — 보드 평면 레이캐스트(발밑 셀)는 틸트 빌보드가 화면상
        // 위로 솟아 있어 몸체 포인팅을 놓친다(근본 원인). 카메라 뒤쪽이면 false.
        public bool TryGetScreenRect(Camera cam, out Rect rect)
        {
            rect = default;
            if (cam == null || _dying || _meshRenderer == null) return false;
            var b = _meshRenderer.bounds;
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

        // defender-relocation unit 6 — 재배치 비행 키링을 머리 위에 얹기 위한 대략 높이(월드).
        // 메시 AABB 세로 크기(빌보드 실루엣의 화면 세로 높이). 미준비 시 스케일 폴백.
        public float ApproxWorldHeight =>
            _meshRenderer != null ? _meshRenderer.bounds.size.y : Mathf.Abs(transform.lossyScale.y);

        // defender-relocation unit 6 — 비행 중 VIEW 좌표 직접 배치(BoardSpace.ToView 우회 — 평면 정면뷰가
        // sim 높이를 버려 아치가 평면이 되던 문제 교정: 아치가 이미 view 공간에 있으므로 그대로 쓴다) +
        // 전경 소팅(보드 타일 위로 hop). 비행은 PendingDeployment(비전투)라 facing/walk/게이지 갱신 불요.
        // flight-lift-feel unit 2 — lift 는 좌표에서 역산할 수 없어 호출측이 같이 준다(절대 view 좌표라
        // 기저선을 뷰가 모른다). 이 경로는 ApplyRenderPosition 을 타지 않으므로 반응 적용도 여기서 한다.
        public void SetFlightView(Vector3 viewPos, float lift = 0f)
        {
            transform.position = viewPos;
            if (_meshRenderer != null) _meshRenderer.sortingOrder = BoardSortOrder.DragPreviewOrder;
            ApplyLift(lift);
        }

        // dreamcatcher-awakening-hand rev 4 — 카드 드래그 타겟팅 호버 강조.
        // on: 현재 RGB 를 저장하고 tint 로 교체 / off: 저장값 복원. 호버 중 들어오는
        // SetHealthTint 는 저장값에 흡수해(스켈레톤 직접 쓰기 대신) 해제 시 최신
        // 체력색으로 복원된다. 현재 defender 는 health tint 미적용(적 전용 루프)이라
        // 실충돌은 없지만 순서 안전하게 방어. 알파 불변(RGB 만).
        private bool _hoverHighlightActive;
        private Color _savedTint = Color.white;
        private Vector3 _baseScale = Vector3.one; // card-fly unit 1 — 펀치 펄스 복귀 기준(스폰 시 캡처)

        public void SetHoverHighlight(bool on, Color tint)
        {
            if (_dying || _skeleton == null || _skeleton.Skeleton == null) return;
            var skel = _skeleton.Skeleton;
            if (on)
            {
                if (!_hoverHighlightActive)
                {
                    _savedTint = new Color(skel.R, skel.G, skel.B);
                    _hoverHighlightActive = true;
                }
                skel.R = tint.r;
                skel.G = tint.g;
                skel.B = tint.b;
            }
            else if (_hoverHighlightActive)
            {
                _hoverHighlightActive = false;
                skel.R = _savedTint.r;
                skel.G = _savedTint.g;
                skel.B = _savedTint.b;
            }
        }

        // unit-health-display unit 1 — 적 저체력 틴트. BattleBridge 가 HealthDisplayStyle 로
        // ratio→Color 를 평가해 주입한다(뷰는 SO 를 모른다). _dying 중엔 마지막 틴트를 유지해
        // 죽음 연출 색을 덮지 않는다. 알파는 건드리지 않음(RGB 만).
        public void SetHealthTint(Color tint)
        {
            if (_dying || _skeleton == null || _skeleton.Skeleton == null) return;
            // 호버 강조 중엔 저장값으로 흡수 — 해제 시 이 색으로 복원된다.
            if (_hoverHighlightActive) { _savedTint = tint; return; }
            var skel = _skeleton.Skeleton;
            skel.R = tint.r;
            skel.G = tint.g;
            skel.B = tint.b;
        }

        // card-fly-to-target-absorb unit 1 — 카드 흡수 묵직 임팩트(타겟 월드 반응).
        // 둘 다 self-contained: base 스케일/현재 틴트를 캡처해 복귀하므로 health/hover 틴트
        // 로직과 충돌 없음. unscaled(슬로모 중에도 스냅) — 카드 비행과 톤 일치.
        public void PlayPunch(float overshoot = 0.28f, float dur = 0.16f)
        {
            if (_dying || !gameObject.activeInHierarchy) return;
            StartCoroutine(PunchRoutine(overshoot, dur));
        }

        private System.Collections.IEnumerator PunchRoutine(float overshoot, float dur)
        {
            // base 대비 크게 튄 뒤 base 로 복귀(살짝 세로 눌림 없이 균일 펀치 — 유닛은 3D 반응 주역).
            // flight-lift-feel unit 1 — localScale 직접 대입 → _punchScale 슬롯으로. 비행 배율과
            // 곱해질 뿐 서로를 지우지 않는다. 시계는 unscaled 그대로(카드 비행과 톤 일치).
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

        // flight-lift-feel unit 3 — 착지 눌림(squash & stretch). PunchRoutine 이 균등 펀치인 것은
        // "유닛은 3D 반응 주역" 이라는 판단이었지만, 착지는 다른 맥락이라 비균등을 쓴다 —
        // 2D 스켈레톤이라 가로 확장·세로 압축이 오히려 어울린다. amount 0 이면 꺼진다.
        // 시계는 unscaled: 착지는 순간 반응이라 슬로모에 늘어지면 임팩트가 죽는다.
        private Coroutine _squashRoutine;

        public void PlayLandingSquash(float amount, float seconds)
        {
            if (amount <= 0f || seconds <= 0f || _dying || !gameObject.activeInHierarchy) return;
            if (_squashRoutine != null) StopCoroutine(_squashRoutine);
            _squashRoutine = StartCoroutine(SquashRoutine(amount, seconds));
        }

        private System.Collections.IEnumerator SquashRoutine(float amount, float seconds)
        {
            float e = 0f;
            while (e < seconds)
            {
                e += Time.unscaledDeltaTime;
                if (_dying) break;
                float k = 1f - Mathf.Clamp01(e / seconds);   // 눌림 최대 → 0 으로 복귀
                _squash = new Vector3(1f + amount * k, 1f - amount * k, 1f + amount * k);
                ApplyRenderScale();
                yield return null;
            }
            _squash = Vector3.one;
            if (!_dying) ApplyRenderScale();
            _squashRoutine = null;
        }

        public void FlashWhite(float dur = 0.14f)
        {
            if (_dying || !gameObject.activeInHierarchy || _skeleton == null || _skeleton.Skeleton == null) return;
            StartCoroutine(FlashRoutine(dur));
        }

        private bool _flashActive;      // use-flow unit 3 rev 2 — 연발 flash 가드
        private Color _flashRestore;    // 진행 중 flash 의 복귀 목표(연발 시 승계)

        private System.Collections.IEnumerator FlashRoutine(float dur)
        {
            var skel = _skeleton.Skeleton;
            // flash 복귀 목표 = resting 색. hover 틴트 활성 중이면 _savedTint(=진짜 base)를
            // 잡는다 — skel 현재값은 우리 틴트라 그걸 restore 로 캡처하면 hover 가 flash 도중
            // 해제될 때 flash 종료가 틴트색으로 굳는다(stray tint). 저장값 기준으로 닫는다.
            // 연발 가드(rev 2) — 앞 flash 가 skel 을 흰빛으로 밀어둔 채 새 flash 가 "현재 색"을
            // 캡처하면 복귀 목표가 중간 흰빛으로 오염돼 유닛이 밝게 굳는다. 진행 중이면
            // 기존 restore 를 승계한다(발동 임팩트가 연발 경로를 만들며 노출된 잠재 버그).
            Color restore = _hoverHighlightActive ? _savedTint
                : (_flashActive ? _flashRestore : new Color(skel.R, skel.G, skel.B));
            _flashRestore = restore;
            _flashActive = true;
            skel.R = 1f; skel.G = 1f; skel.B = 1f;
            float e = 0f;
            while (e < dur)
            {
                e += Time.unscaledDeltaTime;
                if (_dying || _skeleton == null || _skeleton.Skeleton == null) yield break;
                float k = Mathf.Clamp01(e / dur);
                skel = _skeleton.Skeleton;
                skel.R = Mathf.Lerp(1f, restore.r, k);
                skel.G = Mathf.Lerp(1f, restore.g, k);
                skel.B = Mathf.Lerp(1f, restore.b, k);
                yield return null;
            }
            if (!_dying && _skeleton != null && _skeleton.Skeleton != null)
            {
                var s = _skeleton.Skeleton;
                // 복귀 목표를 다시 저장값 기준으로 — hover 중이면 _savedTint 가 최신 resting.
                Color target = _hoverHighlightActive ? _savedTint : restore;
                s.R = target.r; s.G = target.g; s.B = target.b;
            }
            _flashActive = false; // 연발 시 뒤 코루틴이 마지막으로 닫으며 해제
        }

        // placement-enemy-see-through unit 2 — 드래그 배치 중 반투명 전환.
        // 적 Spine 머티리얼은 PMA transparent 라 블렌드 전환 없이 skeleton.A 로 페이드한다.
        // R/G/B(health tint)와 독립. _dying 중엔 사망 연출 색/알파를 덮지 않는다.
        public void SetDimmed(bool transparent, float alpha)
        {
            float a = Mathf.Clamp01(alpha);
            if (!_dying && _skeleton != null && _skeleton.Skeleton != null)
                _skeleton.Skeleton.A = a;
            _blob?.SetDimAlpha(transparent ? a : 1f);
            // 그림자 캐스팅 토글은 상태 변화 시에만 — QuadUnitView 와 일관, 매 프레임 GetComponentsInChildren alloc 방지.
            if (BattleBridge.UseRealShadows && transparent != _shadowTransparent)
            {
                var mode = transparent
                    ? UnityEngine.Rendering.ShadowCastingMode.Off
                    : UnityEngine.Rendering.ShadowCastingMode.TwoSided;
                var renderers = GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                    renderers[i].shadowCastingMode = mode;
                _shadowTransparent = transparent;
            }
        }

        public void PlayAttack(float attackAnimPeriod = 0f)
        {
            if (_dying || _skeleton == null) return;
            string attack = ResolveAnimation(_visualData.SpineAttackAnimation);
            if (string.IsNullOrEmpty(attack)) return;
            _attackAnimationName = attack;
            var state = _skeleton.AnimationState;
            var entry = state.SetAnimation(0, attack, false);
            // attack-anim-speed-match — 공격 애니를 실제 발사 주기(sim 값, max(간격, hitDelay))에 맞춰
            // 압축 재생(compress-to-fit). TrackEntry.TimeScale 은 이 공격 애니만 스케일 →
            // skeleton.timeScale(걷기/battleScale)과 독립 곱. 별도 튜닝 데이터 없이 공격속도 필드에서 직접 도출.
            // 하한 1.0 은 구조 상수(느린 공격을 저작속도보다 느리게 늘리지 않음 = 자연+대기). 상한 없음.
            // 배율은 animDuration/period 라 authoring 규율(과도히 작은 cooldownDuration 지양)이 유한성의 실질
            // 근거다 — attackSpeedMul 클램프(0.2~5)는 그 위 배수만 제한. period<=0 면 폴백(TimeScale=1, 현행).
            if (attackAnimPeriod > 0f && entry != null && entry.Animation != null && entry.Animation.Duration > 0f)
                entry.TimeScale = Mathf.Max(1f, entry.Animation.Duration / attackAnimPeriod);
            // spine-weapon-trail unit 1 — 궤적은 공격 사건에 물린다. TimeScale 확정 이후에 호출할 것.
            PlayWeaponTrail(entry);
            // enemy-walk-anim-speed unit 4 — 공격 후 복귀 = 현재 이동상태 로코모션(walk/idle).
            string loco = ResolveLocomotionAnimation();
            if (!string.IsNullOrEmpty(loco))
                state.AddAnimation(0, loco, true, 0f);
            // 공격(원샷) 즉시 배율 1 반영 — 다음 UpdatePosition 을 기다리지 않고 이 프레임부터 정상속도.
            ApplyTimeScale();
        }

        // spine-weapon-trail unit 3 — 궤적 리그 부착. 대상은 **유닛 타입을 가리지 않는다**
        // (디펜더·적·보스). 프리팹 미할당이면 무동작 = 유일한 게이트.
        // 리그는 반드시 **이 transform 의 자식**이어야 한다 — Billboard(Tilted) 로 기울어진
        // 평면을 상속해야 리본이 스프라이트와 같은 평면에 생긴다.
        private void AttachWeaponTrail()
        {
            if (_visualData == null || _skeleton == null) return;
            var prefab = _visualData.SpineWeaponTrailPrefab;
            if (prefab == null) return;

            var rig = Instantiate(prefab, transform);
            _weaponTrail = rig.GetComponent<WeaponTrailRig>();
            if (_weaponTrail != null) _weaponTrail.Bind(_skeleton);
        }

        // spine-weapon-trail unit 1 — 방출은 **스윙 구간에만** 건다. 종료 시각은
        // 애니 길이 × 비율 ÷ TimeScale — PlayAttack 이 공격 주기에 맞춰 애니를 압축 재생하므로
        // (TimeScale ≥ 1) 이 나눗셈을 빼면 공속이 빠른 유닛에서 방출이 스윙보다 오래 남는다.
        private void PlayWeaponTrail(TrackEntry entry)
        {
            if (_weaponTrail == null || _visualData == null) return;
            if (entry == null || entry.Animation == null) return;
            float duration = entry.Animation.Duration;
            if (duration <= 0f) return;

            float scale = entry.TimeScale > 0f ? entry.TimeScale : 1f;
            // 배틀 도메인 슬로우모는 _skeleton.timeScale 로 반영된다(ApplyTimeScale). 이 항을
            // 빼면 슬로우모에서 방출이 스윙 도중에 끊긴다 — 0.25x 실측으로 창 0.269s 대
            // 실제 스윙 1.075s (4배 짧음). 스윙이 느려진 만큼 창도 늘어나야 한다.
            // 정지(0)면 스윙이 진행되지 않으므로 방출 자체를 걸지 않는다.
            float animScale = _skeleton.timeScale;
            if (animScale <= 0f) return;
            _weaponTrail.Play(duration * Mathf.Clamp01(_visualData.SpineWeaponTrailEndNormalized) / (scale * animScale));
        }

        public bool PlayDeploy()
        {
            if (_dying || _skeleton == null) return false;
            // Defender-only feedback. Enemies spawn without IDefenderSpineExtras
            // and never invoke this path; guarding here prevents accidental
            // animation triggers if an enemy entity ever reaches it.
            if (_defenderExtras == null) return false;
            string animation = ResolveAnimation(
                _defenderExtras.SpineDeployAnimation,
                _defenderExtras.SpineDragAnimation,
                _visualData.SpineAttackAnimation,
                _visualData.SpineIdleAnimation,
                "idle",
                "walk");
            if (string.IsNullOrEmpty(animation)) return false;
            var state = _skeleton.AnimationState;
            state.SetAnimation(0, animation, false);
            // enemy-walk-anim-speed unit 4 — 배치 후 복귀도 로코모션 리졸브 경유.
            string loco = ResolveLocomotionAnimation();
            if (!string.IsNullOrEmpty(loco))
                state.AddAnimation(0, loco, true, 0f);
            ApplyTimeScale(); // 배치(원샷) 즉시 배율 1 반영.
            return true;
        }

        public void Kill()
        {
            if (_dying) return;
            _dying = true;
            string death = ResolveAnimation(_visualData.SpineDeathAnimation);
            if (_skeleton == null || string.IsNullOrEmpty(death))
            {
                Destroy(gameObject);
                return;
            }
            var track = _skeleton.AnimationState.SetAnimation(0, death, false);
            // 사망(원샷) 즉시 배율 1 반영 — Kill 이후엔 UpdatePosition 이 안 불려 마지막 walkFactor 로
            // 굳는다(정지 유닛이면 minTimeScale 로 사망이 느려짐). _dying=true 라 factor=1 로 세팅됨.
            ApplyTimeScale();
            track.Complete += _ => { if (this != null) Destroy(gameObject); };
        }

        public void Dispose()
        {
            _dying = true;
            if (this != null) Destroy(gameObject);
        }

        public void FaceToward(Vector3 worldPoint)
        {
            if (_dying || _skeleton == null || _skeleton.Skeleton == null) return;
            // worldPoint 는 sim 좌표(NotifyAttack 경유) — view 좌표로 변환해 view transform 과 같은 공간에서 비교.
            float dx = ((Vector3)Wassup.Core.BoardSpace.ToView(worldPoint)).x - transform.position.x;
            SetFacingByViewDelta(dx);
        }

        private void FaceAlongMovement(Vector3 world)
        {
            if (_dying || IsAttackAnimationPlaying()) return;
            float dx = ((Vector3)Wassup.Core.BoardSpace.ToView(world)).x
                       - ((Vector3)Wassup.Core.BoardSpace.ToView(_simWorld)).x;
            SetFacingByViewDelta(dx);
        }

        private bool IsAttackAnimationPlaying()
        {
            if (_skeleton == null || string.IsNullOrEmpty(_attackAnimationName)) return false;
            var current = _skeleton.AnimationState?.GetCurrent(0);
            return current != null
                   && !current.Loop
                   && current.Animation != null
                   && current.Animation.Name == _attackAnimationName;
        }

        private void SetFacingByViewDelta(float dx)
        {
            if (_dying || _skeleton == null || _skeleton.Skeleton == null) return;
            if (Mathf.Abs(dx) <= FacingMoveEpsilon) return;
            float currentAbs = Mathf.Abs(_skeleton.Skeleton.ScaleX);
            if (currentAbs < 0.001f) currentAbs = 1f;
            // 적/디펜더 모두 Casual Character 단일 리그를 공유한다(unit-parts-appearance 6).
            // 리그 컨벤션: ScaleX=+1 이 -x(왼쪽)를 본다 → dx>0(오른쪽 이동/타겟)이면 -1 로 뒤집어 +x 를 향한다.
            // 과거엔 적이 반대 방향의 별도 리그라 부호를 enemy/defender 로 분기했으나, 단일 리그가 된
            // 지금은 규칙도 하나다. 방향이 반대인 미래 리그는 코드 분기 대신 SkeletonFlipX modifier 로
            // 데이터에서 정규화한다(net facing = Skeleton.ScaleX * rootScaleX).
            float desiredSign = dx >= 0f ? -1f : 1f;
            _skeleton.Skeleton.ScaleX = currentAbs * desiredSign;
        }

        public Vector3 ResolveCastAnchor()
        {
            // 반환값은 view 공간(transform 기반, transform.position 은 이미 ToView). 호출측은 view 끼리 비교/빼기.
            // Cast anchor is only meaningful for defenders firing projectiles.
            // Without IDefenderSpineExtras (enemies), preserve the existing transform
            // origin fallback used by beam/cast callers.
            if (_defenderExtras == null) return transform.position;

            if (_skeleton != null && _skeleton.Skeleton != null && !string.IsNullOrEmpty(_defenderExtras.SpineCastAnchorBone))
            {
                var bone = _skeleton.Skeleton.FindBone(_defenderExtras.SpineCastAnchorBone);
                if (bone != null)
                    return transform.TransformPoint(new Vector3(bone.WorldX, bone.WorldY, 0f));
            }
            var off = _defenderExtras.SpineCastAnchorLocalOffset;
            if (_skeleton != null && _skeleton.Skeleton != null && _skeleton.Skeleton.ScaleX < 0f)
                off.x = -off.x;
            return transform.TransformPoint(off);
        }

        public Vector3 ResolveProjectileLaunchAnchor()
        {
            // defender는 저작된 weapon bone/cast offset을 그대로 공유한다. 적은
            // defender extras가 없으므로 renderer의 실제 world body center를 쓴다.
            // transform origin(발밑)이나 고정 sim 높이는 원근 카메라 위치에 따라
            // 화면상 발사점이 달라 보이므로 projectile 전용 seam에서만 보정한다.
            if (_defenderExtras == null)
                return _meshRenderer != null ? _meshRenderer.bounds.center : transform.position;
            return ResolveCastAnchor();
        }

        private void PlayIdleLooping()
        {
            if (_skeleton == null) return;
            string animation = ResolveLocomotionAnimation();
            if (!string.IsNullOrEmpty(animation))
                _skeleton.AnimationState.SetAnimation(0, animation, true);
        }

        // enemy-walk-anim-speed unit 4 — 현재 이동상태 기준 로코모션 루프 애니 이름.
        // walk 애니(SpineWalkAnimation) 설정 + 이동 중이면 walk, 아니면 idle(폴백 체인 유지).
        // _locoMoving 히스테리시스는 UpdateLocomotionAnimation 이 갱신 — 여기선 읽기만.
        private string ResolveLocomotionAnimation()
        {
            string walk = ResolveAnimation(_visualData.SpineWalkAnimation);
            if (!string.IsNullOrEmpty(walk) && _moving) return walk;
            return ResolveAnimation(_visualData.SpineIdleAnimation, "idle", "Idle", "walk", "Walk");
        }

        // enemy-walk-anim-speed unit 4 — 이동/정지에 따라 로코모션 루프를 walk↔idle 전환.
        // walk 애니 미설정이면 즉시 반환(단일 idle 루프 = 현행 동작, 회귀 없음). 원샷(공격/
        // 사망/배치) 진행 중이면 건드리지 않고 큐 복귀에 맡긴다. 전환은 크로스페이드.
        private void UpdateLocomotionAnimation()
        {
            if (_dying || _skeleton == null) return;
            if (string.IsNullOrEmpty(ResolveAnimation(_visualData.SpineWalkAnimation))) return;
            var current = _skeleton.AnimationState?.GetCurrent(0);
            if (current == null || !current.Loop) return; // 원샷 진행 중 — 유지

            // _moving 은 UpdateWalkTimeScale 이 이미 갱신(같은 프레임, UpdatePosition 순서).
            string desired = ResolveLocomotionAnimation();
            if (string.IsNullOrEmpty(desired)) return;
            if (current.Animation == null || current.Animation.Name != desired)
            {
                var e = _skeleton.AnimationState.SetAnimation(0, desired, true);
                if (e != null) e.MixDuration = LocoMixDuration;
                ApplyTimeScale();
            }
        }

        private string ResolveAnimation(params string[] candidates)
        {
            if (_skeleton == null || _skeleton.Skeleton == null || _skeleton.Skeleton.Data == null) return null;
            for (int i = 0; i < candidates.Length; i++)
            {
                string candidate = candidates[i];
                if (string.IsNullOrEmpty(candidate)) continue;
                if (_skeleton.Skeleton.Data.FindAnimation(candidate) != null)
                    return candidate;
            }
            return null;
        }
    }
}
