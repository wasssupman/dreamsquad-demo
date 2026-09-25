using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Wassup.BattleCore;
using Wassup.BattleCore.Combat.Projectile;
using Wassup.Data;
using Wassup.Presentation;
using Wassup.Rendering;

namespace Wassup.BattleCoreUnity.View
{
    // battle-core-rebuild unit 6c — **일반 VFX**. 옛 `Presentation.VfxSpawner`(476줄) + 브리지의
    // 호출부들(공격 시각 드레인 · 회복/실드/감지 드레인 · `PlayDeploymentPresentation` ·
    // `FireOnPlaceCameraShake` · `TrySpawnCastVfx` · 착탄 드레인의 광역 분기)의 후계다.
    //
    // 옛 것은 **슬롯과 스폰**을 갖고 호출은 브리지가 했다. 새 것은 **자기 사건을 구독한다**(계약 12) —
    // 그 사이에 값을 옮겨 적는 중개자가 없다. 사건 → 그림:
    //   · `AttackResolved`   — 방어유닛 타격 이펙트(참격 자국 포함) · 적의 유닛별 공격 광역(회오리)
    //   · `ProjectileSpawned`— 총구 캐스트 VFX(탄 저작 `castPrefab`)
    //   · `ProjectileHit`    — 광역 착탄 버스트(프리팹 없는 `TileAoe`) · 광역 착탄 줌 펄스
    //   · `HealApplied` · `ShieldGranted` · `Detected` — 원샷
    //   · `Placed` → 착지   — 배치 링 펄스 · 유닛 저작 배치 VFX
    //   · `DefenderActivated`— 배치 카메라 흔들기(세기·길이 = 유닛 저작, 흔드는 주인 = `CameraDirector.Shake`)
    //
    // ⚠ **판정 0.** 반경은 사건이 나른 값의 **짝**으로만 그린다(제약 13, 구현 6): 범위 항은
    // `AreaTiles`, 원점 항은 `SiteFired.OriginBody` — 0 이면 칸 반폭(자리형), &gt; 0 이면 그 몸(몸형).
    // 뷰가 반경을 다시 계산하면 화면이 규칙을 틀리게 가르친다.
    //
    // ⚠ **옛 「타이밍 큐」(`_pendingHitVfx`)는 접혔다.** 옛 시각 사건은 공격 **START** 에 나와서
    // `hitDelaySec` 만큼 미뤄 RESOLVE 에 맞췄는데, 새 사건 `AttackResolved` 는 **그 자체가 RESOLVE** 다.
    // 미룰 것이 없다 — 미루면 타격보다 늦게 터진다(6c 이식 제외).
    [DisallowMultipleComponent]
    public sealed class CoreVfxSpawner : MonoBehaviour
    {
        [SerializeField] private BattleDriver _driver;
        [SerializeField] private CoreUnitViewPool _units;
        [Tooltip("타격·총구 VFX 를 재생하는 풀(탄 VFX 와 같은 풀링을 쓴다).")]
        [SerializeField] private CoreProjectileViewPool _projectiles;
        [Tooltip("배치 비행 — 「착지했다」를 여기서 읽는다. 비어 있으면 배치 사건 즉시 링이 난다.")]
        [SerializeField] private CoreDeployFlightPresenter _deployFlight;

        // ── 프리팹 슬롯(옛 씬 `VfxSpawner` 블록의 값 그대로 — 씬 배선 참조) ─────────────
        [Header("원샷 슬롯 (옛 VfxSpawner)")]
        [SerializeField] private GameObject _placementRingPrefab;
        [SerializeField] private GameObject _meteorBurstPrefab;
        [SerializeField] private GameObject _healAppliedPrefab;
        [SerializeField] private GameObject _shieldGrantedPrefab;
        [Tooltip("실드 부여 이펙트 스케일(타일 1 유닛 기준)")]
        [SerializeField] private float _shieldGrantedScale = 0.7f;
        [SerializeField] private GameObject _detectionMarkPrefab;
        [Tooltip("마음 붕괴 원샷(옛 `VfxSpawner.goalCollapsePrefab` — unit 8a2 행 3). 비면 배치 링 펄스로 폴백(옛 폴백 그대로).")]
        [SerializeField] private GameObject _goalCollapsePrefab;
        [Tooltip("마음 붕괴 이펙트 스케일(옛 `goalCollapseScale` · 옛 씬 `BattleScene.unity:4455` = 1.2)")]
        [SerializeField] private float _goalCollapseScale = 1.2f;
        [Tooltip("⚠ 짝 = `DetectionMark_SKELETON/BodyFlash` 의 localPosition.y — 하나를 바꾸면 다른 하나를 같이 본다.")]
        [SerializeField] private float _detectionMarkLift = 0.9f;
        [Tooltip("유닛별 공격 광역(회오리)의 지속 배수. 수명 = 공격 주기 × 이 값(동시 인스턴스 수로 읽는다).")]
        [SerializeField] private float _unitAttackAoeSustainMul = 2f;

        [Header("카드 (unit 7c — 옛 VfxSpawner 카드 흡수 슬롯 · 브리지 DC 발동 드레인)")]
        [Tooltip("카드 흡수 전용 임팩트(옛 `cardAbsorbPrefab`). 비면 배치 링 + 착탄 버스트로 폴백(옛 폴백 그대로).")]
        [SerializeField] private GameObject _cardAbsorbPrefab;
        [Tooltip("흡수 이펙트 스케일(타일 1 유닛 기준 축소)")]
        [SerializeField] private float _cardAbsorbScale = 0.6f;
        [Tooltip("드림캐쳐 발동 임팩트 코얼레스 간격의 주인(5a 가 소비처 0 으로 세워 둔 자산).")]
        [SerializeField] private Wassup.Data.BattleView.DcVisualConfig _dcVisual;

        [Header("브레스 · 착탄 예고 (unit 7d — 옛 VfxSpawner 브레스 슬롯 · 브리지 PinSkillTelegraph)")]
        [SerializeField] private GameObject _areaBreathPrefab;
        [SerializeField] private float _areaBreathScalePerTile = 0.55f;
        [SerializeField] private float _areaBreathScaleMax = 2.4f;
        [SerializeField] private float _areaBreathForwardFactor = 0.45f;
        [SerializeField] private float _areaBreathAngleOffset = 90f;
        [SerializeField] private CoreMapOverlay _overlay;

        [Header("배치 링 펄스 (옛 브리지 코루틴)")]
        [SerializeField] private Color _deployRingColor = new Color(0.2f, 0.95f, 1f, 0.7f);
        [SerializeField] private float _deployRingStartScale = 0.2f;
        [SerializeField] private float _deployRingEndScale = 1.35f;
        [SerializeField] private float _deployRingThickness = 0.025f;
        [Tooltip("링 펄스 최소 길이(초). 배치 모션이 이보다 짧은 유닛도 링은 이만큼 퍼진다.")]
        [SerializeField] private float _deployRingMinSeconds = 0.35f;

        // 착지를 기다리는 배치. 사건(`Placed`)은 **드롭 순간**에 오고 비행은 그 뒤다 — 옛 연출은 착지
        // 순간에 났으므로 비행이 끝날 때까지 적어 둔다. 비행을 모르는 경로(헤드리스·테스트·비행 없는
        // 설정)는 다음 프레임에 바로 난다.
        private readonly List<SimEntityId> _awaitingLanding = new List<SimEntityId>(4);
        private readonly List<SimEntityId> _scratch = new List<SimEntityId>(4);
        private CameraDirector _cameraDirector;
        private bool _cameraDirectorMissWarned;
        private readonly HashSet<string> _missingSlotLogged = new HashSet<string>();
        // 숙주별 마지막 발동 임팩트 시각(unscaled) — 주기 발동이 촘촘한 유닛(머신거너)의 도배 방지(옛 `_dcProcLastImpact`).
        private readonly Dictionary<int, float> _procLastImpact = new Dictionary<int, float>();
        private int _procTick = -1;
        private readonly HashSet<int> _procThisTick = new HashSet<int>();

        /// <summary>이번 판의 카드 발동 임팩트 수(테스트).</summary>
        public int ProcImpactCount { get; private set; }

        /// <summary>이번 판에 이 풀이 낸 원샷 수(진단·테스트). 「사건 1 → 그림 1」의 오른쪽 항이다.</summary>
        public int SpawnedCount { get; private set; }

        private void OnEnable()
        {
            if (_driver != null) _driver.Subscribe(ViewOrder.Effect, OnCoreEvent);
        }

        private void OnDisable()
        {
            if (_driver != null) _driver.Unsubscribe(OnCoreEvent);
            _awaitingLanding.Clear();
        }

        private void OnCoreEvent(CoreEvent e)
        {
            switch (e.Kind)
            {
                case CoreEventKind.MatchStarted:
                    _awaitingLanding.Clear();
                    _procLastImpact.Clear();
                    SpawnedCount = 0;
                    CollapsedMarkerCount = 0;
                    ProcImpactCount = 0;
                    _telegraphProjectile = SimEntityId.None;
                    break;

                case CoreEventKind.TriggerFired: OnTriggerFired(e); break;
                case CoreEventKind.SkillVisual: OnSkillVisual(e); break;

                case CoreEventKind.AttackResolved: OnAttackResolved(e); break;
                case CoreEventKind.ProjectileSpawned: OnProjectileSpawned(e); break;
                case CoreEventKind.ProjectileHit: ReleaseTelegraph(e.A); OnProjectileHit(e); break;
                case CoreEventKind.ProjectileDespawned: ReleaseTelegraph(e.A); break;

                case CoreEventKind.HealApplied:
                    OneShot(_healAppliedPrefab, nameof(_healAppliedPrefab), e.SiteFired.Pos, 0.08f, 1f, 1.1f, oneShot: false);
                    break;
                case CoreEventKind.ShieldGranted:
                    // 실드 부여의 자리 = 받은 쪽의 몸(`SiteTarget`). 부여자가 아니다.
                    OneShot(_shieldGrantedPrefab, nameof(_shieldGrantedPrefab), e.SiteTarget.Pos, 0.08f,
                            Mathf.Max(0.1f, _shieldGrantedScale), 0f, oneShot: true);
                    break;
                case CoreEventKind.Detected:
                    // ⚠ **발견한 적에게만** 붙는다. 대상(`B`)은 트레이스 전용 — 화면이 가리키면 안 된다(M9).
                    OneShot(_detectionMarkPrefab, nameof(_detectionMarkPrefab), e.SiteFired.Pos,
                            _detectionMarkLift, 1f, 0f, oneShot: true);
                    break;

                case CoreEventKind.HeartCollapsed: OnHeartCollapsed(); break;

                case CoreEventKind.Placed:
                    _awaitingLanding.Add(e.A);
                    break;
                case CoreEventKind.DefenderActivated:
                    ShakeFor(DefenderData(e.Arg));
                    break;
                case CoreEventKind.UnitDestroyed:
                    _awaitingLanding.Remove(e.A);
                    break;
            }
        }

        // ── 공격 ─────────────────────────────────────────────────────────────
        private void OnAttackResolved(CoreEvent e)
        {
            bool defender = ((int)e.Faction & Wassup.Battle.Units.Factions.AnyDefender) != 0;
            if (!defender)
            {
                // 적의 «유닛별 공격 광역»(회오리). 「회오리를 갖는가」는 **프리팹 유무**가 정한다.
                var enemy = EnemyData(e.DefIndex);
                if (enemy == null || enemy.attackVfxPrefab == null || _units == null) return;
                if (!_units.TryResolveViewPosition(e.A, useAnchor: false, out var origin)) return;
                float period = e.Amount > 0f ? e.Amount : Mathf.Max(0.1f, enemy.attackCooldown);
                SpawnUnitAttackAoe(enemy.attackVfxPrefab, origin, enemy.attackRange,
                                   enemy.attackVfxScalePerTile, period);
                return;
            }

            var data = DefenderData(e.DefIndex);
            if (data == null || data.attackVfxPrefab == null || _projectiles == null) return;

            // 그림이 찍히는 자리 = 대상 자리, 또는 **공격자 발밑**(도형 유닛의 참격 — `attackVfxAtAttacker`).
            bool atAttacker = data.attackVfxAtAttacker;
            float3 simPos = atAttacker ? e.SiteFired.Pos : e.SiteTarget.Pos;

            Vector3 facing = default;
            if (data.attackVfxFacesTarget)
            {
                // 방향의 원점 = 그림의 원점. 발밑 참격은 **사건이 나른 공격 축**(`AttackDir` — 부가 타격을
                // 고른 그 방향)을 뷰 공간으로 옮긴다. 타격점 VFX 는 캐스트 앵커에서 대상 자리로 잰다.
                if (atAttacker && math.lengthsq(e.AttackDir) > 0f)
                {
                    var axis = new float3(e.AttackDir.x, 0f, e.AttackDir.y);
                    facing = (Vector3)Wassup.Core.BoardSpace.ToView(e.SiteFired.Pos + axis)
                             - (Vector3)Wassup.Core.BoardSpace.ToView(e.SiteFired.Pos);
                }
                else if (!atAttacker && _units != null && _units.TryResolveViewPosition(e.A, true, out var originView))
                    facing = (Vector3)Wassup.Core.BoardSpace.ToView(e.SiteTarget.Pos) - originView;
            }

            // 참격 자국 = **판정 도형에서 실시간 생성한 메시**. 도형·사거리·몸이 전부 사건의 스냅샷이다 —
            // 길이 = RESOLVE 시점 사거리(`AttackRange`) + 내 몸(`SiteFired.OriginBody`). 공격자를 되묻지 않는다
            // (계약 4·7 — 옛 브리지 `BattleBridge.cs:4869-4884` 는 `AttackState` 를 드레인 시점에 읽었다).
            // 메시가 이미 월드 단위라 저작 배율은 1.
            Mesh mark = null;
            float scale = data.attackVfxScale;
            if (atAttacker && !e.AttackShape.IsOmni && _driver != null)
            {
                mark = _projectiles.GetShapeMarkMesh(ShapeMarkOf(e.AttackShape,
                    e.AttackRange + e.SiteFired.OriginBody, _driver.TileSize));
                scale = 1f;
            }
            _projectiles.PlayHit(data.attackVfxPrefab, simPos, scale: scale, facingViewDir: facing,
                                 eulerOffset: data.attackVfxEulerOffset, meshOverride: mark);
            SpawnedCount++;
        }

        // 코어 bake → 뷰 메시 키. 옛 `ShapeMarkSpec.FromBaked` 와 **같은 역산**이다(옛 타입은
        // `Wassup.Data.AttackShapeBaked` 라 코어 타입을 못 받는다 — 필드가 같아 옮겨 담는다).
        // 배치 도형 가이드(`CoreMapOverlay`)도 이 함수를 지난다 — 옛 가이드와 참격이 `ShapeMarkSpec.AngleDegOf`
        // 하나를 읽던 「같은 역산 = 같은 윤곽」(directional-attack-shape 리뷰 L-5)을 새 층에서도 구조로 둔다.
        internal static ShapeMarkSpec ShapeMarkOf(in Wassup.BattleCore.Combat.AttackShapeBaked s,
                                                 float lengthTiles, float cellSize)
        {
            var legacy = new Wassup.Data.AttackShapeBaked
            {
                kind = s.kind, sinHalf = s.sinHalf, cosHalf = s.cosHalf, halfWidth = s.halfWidth,
            };
            return ShapeMarkSpec.FromBaked(in legacy, lengthTiles, cellSize);
        }

        // 옛 `VfxSpawner.SpawnUnitAttackAoe` 그대로. ⚠ 이 슬롯의 프리팹은 **루프여야** 한다(저작 계약을
        // 여기서 강제 — 단발로 저작하면 주기보다 먼저 말라 깜빡인다). 호출 유닛이 공격 중 정지한다는
        // 전제(인스턴스는 스폰 위치에 고정)도 옛 그대로다.
        private void SpawnUnitAttackAoe(GameObject prefab, Vector3 originView, float radiusTiles,
                                        float scalePerTile, float attackPeriodSeconds)
        {
            var go = Instantiate(prefab, originView, Quaternion.identity, transform);
            float s = Mathf.Max(0.05f, radiusTiles * Mathf.Max(0.01f, scalePerTile));
            go.transform.localScale = Vector3.one * s;
            var renderers = go.GetComponentsInChildren<ParticleSystemRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].sortingOrder += BoardSortOrder.UnitAttackAoeOrder;
            var systems = go.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            for (int i = 0; i < systems.Length; i++)
            {
                var main = systems[i].main;
                main.loop = true;
                main.playOnAwake = true;
                systems[i].Clear(true);
                systems[i].Play(true);
            }
            float sustain = attackPeriodSeconds * Mathf.Max(1f, _unitAttackAoeSustainMul);
            Destroy(go, Mathf.Max(0.1f, sustain));
            SpawnedCount++;
        }

        // ── 탄 ───────────────────────────────────────────────────────────────
        // 총구 캐스트. **탄마다** 난다 — 옛 것은 공격 시작 사건에 한 번이었고, 연발(머신건 10연발)은
        // 첫 발에만 섬광이 났다. 탄 사건이 곧 발사이므로 발마다 나는 것이 그림의 뜻과 맞는다(고친 것).
        // 하늘에서 떨어지는 탄은 쏜 유닛의 손에서 나오지 않으므로 캐스트가 없다(옛 앵커 규칙과 같은 판단).
        private void OnProjectileSpawned(CoreEvent e)
        {
            if (e.AreaTiles > 0) PinTelegraph(e);
            if (_projectiles == null || _units == null || e.B.IsNone) return;
            var movement = (MovementKind)e.Arg;
            if (movement == MovementKind.SkyFall || movement == MovementKind.SkyFallOnEntity) return;

            var proj = _driver != null ? _driver.Match?.World.FindProjectile(e.A) : null;
            var data = proj != null && _driver != null ? _driver.ViewAssets.Projectile(proj.DefIndex) : null;
            if (data == null || data.castPrefab == null) return;

            if (!_units.TryResolveViewPosition(e.B, useAnchor: true, out var anchor)) return;
            var dir = (Vector3)Wassup.Core.BoardSpace.ToView(e.SiteTarget.Pos) - anchor;
            dir.z = 0f;   // 화면 평면(XY 보드)에 평탄화 — 옛 규칙
            _projectiles.PlayCast(data.castPrefab, anchor, dir, data.castVfxLifetime);
            SpawnedCount++;
        }

        // 광역 착탄. **프리팹이 있으면 탄 풀이 그린다**(저작 `hitPrefab` 이 이긴다 — 옛 라우팅) —
        // 여기는 프리팹 없는 `TileAoe` 의 버스트와, 라우팅과 무관한 줌 펄스다.
        private void OnProjectileHit(CoreEvent e)
        {
            if (e.Payload != PayloadKind.TileAoe || e.AreaTiles <= 0) return;

            // 줌 펄스 — 헤비(광역) 착탄의 구두점. 시각 라우팅과 무관하다(옛 규칙).
            ResolveCameraDirector()?.ZoomPulse();

            var data = _driver != null ? _driver.ViewAssets.Projectile(e.DefIndex) : null;
            if (data != null && data.hitPrefab != null) return;
            if (_meteorBurstPrefab == null) { MissingSlot(nameof(_meteorBurstPrefab)); return; }

            // 반경 = 범위 항 + 원점 항(제약 13). 뷰는 **짝을 합칠 뿐** 다시 재지 않는다.
            float radiusWorld = CoreDrawRadius.AreaTiles(e.AreaTiles, e.SiteFired.OriginBody)
                                * (_driver != null ? _driver.TileSize : 1f);
            var view = (Vector3)Wassup.Core.BoardSpace.ToView(new float3(e.SiteFired.Pos.x, 0f, e.SiteFired.Pos.z));
            var go = Instantiate(_meteorBurstPrefab, view + Vector3.up * 0.05f, Quaternion.identity, transform);
            go.transform.localScale = Vector3.one * Mathf.Max(0.1f, radiusWorld);
            Destroy(go, 1.2f);
            SpawnedCount++;
        }

        // ── 착탄 예고 · 브레스 (unit 7d) ─────────────────────────────────────
        //
        // 착탄 예고(옛 `BattleBridge.cs:5456` `PinSkillTelegraph`): 예고 반경은 **스킬 intent 값**이고(7a — 탄 정의표 값이
        // 아니다) `ProjectileSpawned.AreaTiles` 가 싣는다(0 = 예고 없음). 중심 = 착탄점의 칸 중심 · 반경 = 칸 수 + 칸 반폭
        // (자리형 — `CoreDrawRadius`). 칸 하나에 예고 하나(옛 단일 슬롯 — 마지막 탄이 이긴다). 반납은 **그 탄의** 착탄·소멸.
        private SimEntityId _telegraphProjectile = SimEntityId.None;

        private void PinTelegraph(CoreEvent e)
        {
            var map = _driver != null ? _driver.Match?.Map : null;
            if (map == null) return;
            if (_overlay == null) _overlay = FindAnyObjectByType<CoreMapOverlay>();
            if (_overlay == null) { MissingSlot(nameof(_overlay)); return; }
            var center = map.CenterOf(map.CellOf(e.SiteTarget.Pos));
            _overlay.ShowTelegraph(e.A, center, CoreDrawRadius.AreaTiles(e.AreaTiles, 0f));
            _telegraphProjectile = e.A;
        }

        private void ReleaseTelegraph(SimEntityId projectile)
        {
            if (_telegraphProjectile.IsNone || _telegraphProjectile != projectile) return;
            if (_overlay != null) _overlay.HideTelegraph(projectile);
            _telegraphProjectile = SimEntityId.None;
        }

        // 브레스(옛 `VfxSpawner.SpawnAreaBreath` 그대로): 그림은 공격 도형이 아니라 **스킬의 콘**이다 — `TriggerFired` 가 축
        // (`AttackDir` = 시전자→대상)·부채꼴(`AttackShape`)·사거리(칸, `AttackRange`)를 싣는다(7a). 크기는 **저작값**
        // (콘 기하를 그대로 쓰면 화면을 덮는다 — 옛 주석). 슬롯이 비면 옛 폴백(발동 지점 링 펄스)도 없이 에러 한 번.
        private void SpawnAreaBreath(CoreEvent e)
        {
            if (_areaBreathPrefab == null) { MissingSlot(nameof(_areaBreathPrefab)); return; }
            if (_units == null || !_units.TryResolveViewPosition(e.A, useAnchor: true, out var origin)) return;
            float rangeWorld = e.AttackRange * (_driver != null ? _driver.TileSize : 1f);
            Vector3 ahead = (Vector3)Wassup.Core.BoardSpace.ToViewVector(new Vector3(e.AttackDir.x, 0f, e.AttackDir.y));
            if (ahead.sqrMagnitude < 1e-6f) ahead = Vector3.right;
            ahead.Normalize();
            float angle = Mathf.Atan2(ahead.y, ahead.x) * Mathf.Rad2Deg;
            var pos = origin + ahead * (rangeWorld * _areaBreathForwardFactor);
            var go = Instantiate(_areaBreathPrefab, pos, Quaternion.Euler(0f, 0f, angle + _areaBreathAngleOffset), transform);
            go.transform.localScale = Vector3.one * Mathf.Clamp(rangeWorld * _areaBreathScalePerTile, 0.1f,
                                                                Mathf.Max(0.1f, _areaBreathScaleMax));
            var renderers = go.GetComponentsInChildren<ParticleSystemRenderer>(true);
            for (int i = 0; i < renderers.Length; i++) renderers[i].sortingOrder += BoardSortOrder.AreaBreathOrder;
            Destroy(go, ConfigureOneShot(go));
            SpawnedCount++;
        }

        // ── 마음 붕괴 (unit 8a2 행 3) ─────────────────────────────────────────
        //
        // 옛 `BattleBridge.PlayCoreBurst`(`:7308-7319`) + `DrainGoalCollapsedEvents`(`:9596-9609`)의 후계. **규칙은 하나도 없다** —
        // 판을 끝낸 것은 `HeartMeter`(첫 붕괴 = 판의 끝)이고, 붕괴 박자의 슬로모(1.25초 · 0.3)는 `CoreMatchOutcomePresenter` 가
        // 도메인 리스로 이미 건다(5c — `HeartHudConfig.CoreBurst*`). 여기는 그 한 박자에 보이는 것 둘뿐:
        //   · 마음 칸마다 붕괴 원샷(옛 `SpawnGoalCollapse` — 칸 중심 · 0.08 띄움 · 저작 스케일 · 루프 프리팹 단발화)
        //   · 스테이지의 골 마커를 「무너졌다」로(옛 `GoalMarker.MarkCollapsed` — 어두운 틴트 + 주저앉음)
        // 마음 칸 = 정의표의 골(`Map.Goals` — 마음 타워는 골당 하나이고 체력 저수지를 공유한다 X29 — 그래서 무너질 때 전부 무너진다).
        // 사건은 자리를 안 나르지만 골은 **정의표 값**(판 중 불변)이라 되묻기가 아니다.
        private void OnHeartCollapsed()
        {
            var def = _driver != null ? _driver.Definition : null;
            var map = _driver != null ? _driver.Match?.Map : null;
            if (def == null || map == null) return;
            var goals = def.Map.Goals;
            for (int i = 0; i < goals.Length; i++)
            {
                var center = map.CenterOf(goals[i]);
                if (_goalCollapsePrefab != null)
                    OneShot(_goalCollapsePrefab, nameof(_goalCollapsePrefab), center, 0.08f,
                            Mathf.Max(0.1f, _goalCollapseScale), 0f, oneShot: true);
                else
                {
                    // 옛 폴백 그대로 — 최소한 붕괴 지점 링 펄스(`VfxSpawner.cs:242-246`). 한 번은 알린다.
                    if (_missingSlotLogged.Add(nameof(_goalCollapsePrefab)))
                        Debug.LogWarning("[CoreVfxSpawner] _goalCollapsePrefab 미할당 — 배치 링 펄스로 폴백.", this);
                    OneShot(_placementRingPrefab, nameof(_placementRingPrefab), center, 0.02f, 1f, 0.6f, oneShot: false);
                }
            }
            MarkGoalMarkersCollapsed(goals);
        }

        /// <summary>이번 판에 무너뜨린 골 마커 수(테스트).</summary>
        public int CollapsedMarkerCount { get; private set; }

        // 골 마커 ↔ 칸: 스테이지 로컬 → 칸(옛 `BattleBridge.cs:1164-1169` 의 `_goalMarkersByCell` 과 같은 사상).
        private void MarkGoalMarkersCollapsed(Unity.Mathematics.int2[] goals)
        {
            var stage = _driver.StageRoot;
            if (stage == null) return;
            foreach (var marker in stage.GetComponentsInChildren<Wassup.Core.GoalMarker>(false))
            {
                var local = stage.transform.InverseTransformPoint(marker.transform.position);
                var cell = Wassup.Data.MapStageMath.LocalToCell(local, stage.gridOriginLocal, _driver.TileSize);
                for (int i = 0; i < goals.Length; i++)
                {
                    if (goals[i].x != cell.x || goals[i].y != cell.y) continue;
                    marker.MarkCollapsed();
                    CollapsedMarkerCount++;
                    break;
                }
            }
        }

        // ── 카드 (unit 7c) ───────────────────────────────────────────────────

        /// <summary>
        /// 카드 흡수 임팩트(옛 `VfxSpawner.SpawnCardAbsorb`). 손패 흡수 비행이 닿는 순간 · 카드 규칙이 발동하는 순간 둘이 **같은 그림**이다
        /// (use-flow 3 rev 2 — 「부착 순간 박히던 그 임팩트가 발동 순간 다시 친다」 · 인과 언어 일치). 좌표는 view 그대로.
        /// </summary>
        public void SpawnCardAbsorb(Vector3 viewPos)
        {
            if (_cardAbsorbPrefab != null)
            {
                var go = Instantiate(_cardAbsorbPrefab, new Vector3(viewPos.x, viewPos.y + 0.05f, viewPos.z), Quaternion.identity, transform);
                go.transform.localScale = Vector3.one * Mathf.Max(0.05f, _cardAbsorbScale);
                Destroy(go, 1.6f);
                SpawnedCount++;
                return;
            }
            // 폴백(프리팹 미할당) — 옛 그대로 링 + 버스트 재사용.
            if (_placementRingPrefab != null)
            {
                var ring = Instantiate(_placementRingPrefab, new Vector3(viewPos.x, viewPos.y + 0.02f, viewPos.z), Quaternion.identity, transform);
                Destroy(ring, 0.6f);
            }
            if (_meteorBurstPrefab != null)
            {
                var burst = Instantiate(_meteorBurstPrefab, new Vector3(viewPos.x, viewPos.y + 0.05f, viewPos.z), Quaternion.identity, transform);
                burst.transform.localScale = Vector3.one * 0.6f;
                Destroy(burst, 1.0f);
            }
            SpawnedCount++;
        }

        // 카드 규칙 발동 → 숙주 몸 펀치 + 흰 플래시 + 흡수 임팩트(옛 `DrainDcTriggerFiredEvents`). 같은 틱 같은 숙주 다발은 1회,
        // 숙주당 최소 간격(`DcVisualConfig`) 안의 연타는 월드 임팩트를 건너뛴다. 카메라 킥·흡수음은 **뺀다**(주기 발동 연타에 멀미·소음 —
        // 옛 결정). 유닛 저작 스킬(배치 스킬 등)은 카드가 아니다 — 규칙 줄의 출처로 가른다.
        private void OnTriggerFired(CoreEvent e)
        {
            if ((Wassup.BattleCore.Trigger.TriggerPayload)(int)e.Amount == Wassup.BattleCore.Trigger.TriggerPayload.AreaBreath
                && e.AttackShape.kind == Wassup.BattleCore.Combat.AttackShapeBaked.SectorKind)
                SpawnAreaBreath(e);
            var def = _driver != null ? _driver.Definition : null;
            int row = e.DefIndex;
            if (def == null || row < 0 || row >= def.Bindings.Length
                || def.Bindings[row].Origin != Wassup.BattleCore.Trigger.BindingOrigin.Card) return;
            if (!e.A.IsEntity || _units == null) return;
            if (_procTick != e.Tick) { _procTick = e.Tick; _procThisTick.Clear(); }
            if (!_procThisTick.Add(e.A.Value)) return;
            float gap = _dcVisual != null ? _dcVisual.ProcImpactMinIntervalSec : 0f;
            if (_procLastImpact.TryGetValue(e.A.Value, out float last) && Time.unscaledTime - last < gap) return;
            if (!_units.TryGet(e.A, out var view) || view == null) return;
            view.PlayPunch();
            view.FlashWhite();
            SpawnCardAbsorb(view.transform.position);
            _procLastImpact[e.A.Value] = Time.unscaledTime;
            ProcImpactCount++;
        }

        // 스킬이 요청한 연출 중 **적중 펄스**(옛 `ProjectileHitEvents` 로 host 위치 1회 — 탄 저작 `hitPrefab`). 빔은 빔 프리젠터의 것이다.
        private void OnSkillVisual(CoreEvent e)
        {
            if ((Wassup.Skills.SkillVisualKind)e.Arg != Wassup.Skills.SkillVisualKind.HitPulse) return;
            var data = _driver != null ? _driver.ViewAssets.Projectile(e.DefIndex) : null;
            if (data == null || data.hitPrefab == null || _projectiles == null) return;
            // 탄 착탄과 **같은 호출**(수명 · 높이 · 스케일 = 탄 저작)이다 — 착탄 VFX 경로를 빌려 쓰던 옛 라우팅 그대로.
            _projectiles.PlayHit(data.hitPrefab, e.SiteTarget.Pos, data.hitVfxLifetime, data.visualHeightOffset, data.hitVfxScale);
            SpawnedCount++;
        }

        // ── 배치 ─────────────────────────────────────────────────────────────
        private void LateUpdate()
        {
            if (_awaitingLanding.Count == 0 || _driver == null || !_driver.Running) return;
            _scratch.Clear();
            for (int i = 0; i < _awaitingLanding.Count; i++)
            {
                var id = _awaitingLanding[i];
                // 비행 중이면 기다린다. **착지 = 비행 키가 사라진 프레임**이다(비행 프리젠터의 규약).
                if (_deployFlight != null && _deployFlight.IsFlying(id)) continue;
                _scratch.Add(id);
            }
            for (int i = 0; i < _scratch.Count; i++)
            {
                _awaitingLanding.Remove(_scratch[i]);
                PlayDeploymentLanding(_scratch[i]);
            }
            _scratch.Clear();
        }

        /// <summary>
        /// unit 8a — 퇴근 비행이 **뽑히는 순간** 떠난 칸에 치는 링. 옛 `VfxSpawner.SpawnPlacementRing`
        /// (`Presentation/VfxSpawner.cs:71~83`) 그대로 — 배치 때 나는 그 링(같은 프리팹 · 0.02 띄움 · 0.6초)이다.
        /// 옛 것은 sim 을 받아 진입부에서 `ToView` 했다. 여기는 **view 를 받는다**(호출자가 이미 view 공간에 있다).
        /// </summary>
        public void SpawnPlacementRing(Vector3 viewPos)
        {
            if (_placementRingPrefab == null) { MissingSlot(nameof(_placementRingPrefab)); return; }
            var go = Instantiate(_placementRingPrefab, viewPos + Vector3.up * 0.02f, Quaternion.identity, transform);
            Destroy(go, 0.6f);
        }

        private void PlayDeploymentLanding(SimEntityId id)
        {
            var u = _driver.Find(id);
            if (u == null) return;
            var data = DefenderData(u.DefIndex);
            // 연출은 유닛이 **실제로 서는 자리**(발밑)에서 난다 — 다칸 유닛도 앵커 칸이 아니라 발밑.
            var view = (Vector3)Wassup.Core.BoardSpace.ToView(u.Position);
            float motion = data != null ? Mathf.Max(0f, data.DeployMotionSeconds) : 0f;

            if (data != null && data.placementVfxPrefab != null)
            {
                var go = Instantiate(data.placementVfxPrefab, view, Quaternion.identity, transform);
                Destroy(go, Mathf.Max(motion, 1f) + 0.25f);
            }
            else if (_placementRingPrefab != null)
            {
                var go = Instantiate(_placementRingPrefab, view + Vector3.up * 0.02f, Quaternion.identity, transform);
                Destroy(go, 0.6f);
            }
            else MissingSlot(nameof(_placementRingPrefab));

            StartCoroutine(DeploymentRingPulse(view, Mathf.Max(motion, _deployRingMinSeconds)));
            SpawnedCount++;
        }

        // 옛 `PlayDeploymentRingPulse` 그대로 — 머티리얼은 **`RuntimeMaterialFactory`** 경유다.
        private IEnumerator DeploymentRingPulse(Vector3 world, float duration)
        {
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "CoreDeploymentRingPulse";
            ring.transform.SetParent(transform, true);
            ring.transform.position = world + Vector3.up * 0.08f;
            var collider = ring.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            var renderer = ring.GetComponent<Renderer>();
            var material = RuntimeMaterialFactory.CreateTransparent(_deployRingColor);
            if (renderer != null) renderer.sharedMaterial = material;

            float elapsed = 0f;
            float d = Mathf.Max(0.1f, duration);
            while (elapsed < d && ring != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / d);
                float scale = Mathf.Lerp(_deployRingStartScale, _deployRingEndScale, t);
                ring.transform.localScale = new Vector3(scale, _deployRingThickness, scale);
                if (material != null)
                {
                    var color = material.color;
                    color.a = Mathf.Lerp(_deployRingColor.a, 0f, t);
                    RuntimeMaterialFactory.ApplyColor(material, color);
                }
                yield return null;
            }
            if (ring != null) Destroy(ring);
            if (material != null) Destroy(material);
        }

        // 세기·길이는 **유닛이 저작한다**(제약 6). 흔드는 물리의 주인은 카메라다 — 호출부만 여기.
        private void ShakeFor(DefenderUnitData data)
        {
            if (data == null || data.onPlaceShakeStrength <= 0f) return;
            ResolveCameraDirector()?.Shake(data.onPlaceShakeStrength, data.onPlaceShakeDuration);
        }

        private CameraDirector ResolveCameraDirector()
        {
            if (_cameraDirector != null) return _cameraDirector;
            if (_cameraDirectorMissWarned) return null;
            var cam = Camera.main;
            if (cam == null) return null;
            _cameraDirector = cam.GetComponent<CameraDirector>();
            if (_cameraDirector == null)
            {
                Debug.LogWarning("[CoreVfxSpawner] CameraDirector 미배선 — 흔들기·줌 펄스 생략.", this);
                _cameraDirectorMissWarned = true;
            }
            return _cameraDirector;
        }

        // ── 공용 ─────────────────────────────────────────────────────────────
        private void OneShot(GameObject prefab, string slot, float3 simPos, float lift, float scale,
                             float lifetime, bool oneShot)
        {
            if (prefab == null) { MissingSlot(slot); return; }
            var view = (Vector3)Wassup.Core.BoardSpace.ToView(simPos);
            var go = Instantiate(prefab, view + Vector3.up * lift, Quaternion.identity, transform);
            go.transform.localScale = Vector3.one * scale;
            float life = oneShot ? ConfigureOneShot(go) : lifetime;
            Destroy(go, Mathf.Max(0.1f, life));
            SpawnedCount++;
        }

        // 조용한 리턴 금지 — 슬롯이 비면 「사건은 나는데 화면만 조용한」 상태가 되고 그것은 「기능이
        // 죽었다」와 구분이 안 된다(옛 규약: 슬롯 null 이면 에러 · 폴백 없음). 슬롯당 1회.
        private void MissingSlot(string slot)
        {
            if (_missingSlotLogged.Add(slot))
                Debug.LogError($"[CoreVfxSpawner] {slot} 미할당 — 인스펙터에서 프리팹을 연결할 것.", this);
        }

        // 루프형 벤더 프리팹을 **인스턴스 단위로** 단발화한다(공유 에셋 무접촉). 옛 함수 그대로.
        private static float ConfigureOneShot(GameObject root)
        {
            var systems = root.GetComponentsInChildren<ParticleSystem>(includeInactive: true);
            float maxLife = 0f;
            for (int i = 0; i < systems.Length; i++)
            {
                var ps = systems[i];
                var main = ps.main;
                main.loop = false;
                main.playOnAwake = true;
                float burst = Mathf.Max(1f, main.duration) * Mathf.Max(1f, ps.emission.rateOverTime.constant);
                var emission = ps.emission;
                emission.rateOverTime = 0f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Clamp(Mathf.RoundToInt(burst), 4, 24)) });
                float life = main.duration + main.startLifetime.constantMax;
                if (life > maxLife) maxLife = life;
                ps.Clear(true);
                ps.Play(true);
            }
            return maxLife > 0f ? maxLife + 0.2f : 1.5f;
        }

        private DefenderUnitData DefenderData(int defIndex)
        {
            if (_driver == null || defIndex < 0) return null;
            var list = _driver.DefenderAssets;
            return defIndex < list.Count ? list[defIndex] : null;
        }

        private AttackUnitData EnemyData(int defIndex)
        {
            if (_driver == null || defIndex < 0) return null;
            var list = _driver.EnemyAssets;
            return defIndex < list.Count ? list[defIndex] : null;
        }
    }

    /// <summary>
    /// unit 6c — **제약 13 을 그림에 적용하는 한 줄.** 뷰는 반경을 재지 않는다 — 사건이 나른
    /// 「범위 항」과 「원점 항」을 **합칠 뿐**이다. 원점 항은 문면 그대로 「몸 반경 0 = 그 자리는
    /// 칸이다」: 0 이면 칸 반폭(자리에 떨어지는 것), &gt; 0 이면 그 몸(몸에서 나오는 것).
    /// 대상의 몸은 여기 없다 — 그것은 그 유닛의 그림자가 말한다(옛 D6).
    /// 호출처가 둘(광역 착탄 버스트 · 존 장판)이라 한 곳에 둔다(제약 10 (b)).
    /// </summary>
    public static class CoreDrawRadius
    {
        public static float OriginTermTiles(float originBody)
            => originBody > 0f ? originBody : Wassup.Skills.SkillMath.CellShapePaddingTiles;

        public static float AreaTiles(float rangeTiles, float originBody)
            => rangeTiles + OriginTermTiles(originBody);

        /// <summary>
        /// unit 8a2 — 배치 사거리 **칸 채움**이 가정하는 대상 몸(표준 잡몹 — rule-holders T3). 칸은 크기를 표현 못 해
        /// 표준을 가정하는 것을 감수한다. 오버레이가 판정 본체(`SkillMath`)를 직접 부르지 않도록 여기서 한 번 이름을 붙인다
        /// (`CoreViewYardstickTests` — 오버레이의 자는 `AttackReach.InReach` 하나).
        /// </summary>
        public const float StandardTargetBodyTiles = Wassup.Skills.SkillMath.StandardBodyRadiusTiles;
    }
}
