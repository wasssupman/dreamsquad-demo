using System;
using System.Collections.Generic;
using UnityEngine;
using Somnia.Battle.BattleCore;
using Somnia.Battle.Core.TimeControl;
using Somnia.Battle.Data;
using Somnia.Battle.Data.BattleView;
using Somnia.Battle.Presentation;

namespace Somnia.Battle.BattleCoreUnity.View
{
    // battle-core-rebuild unit 5a — 유닛 뷰 풀. 옛 `SpineUnitPool` + `QuadUnitViewPool` 의 후계다.
    //
    // 통합 뷰는 없다(계약 12) — 이 풀은 **자기 사건만** 구독하고, 다른 풀이 무엇을 하는지 모른다.
    // 순서가 필요한 곳은 `ViewOrder` 가 말한다.
    //
    // 옛 풀과 달라진 것 둘:
    //   ① 키가 `Entity` 가 아니라 `SimEntityId` 다.
    //   ② **`EntityManager.Exists` 매 프레임 폴링이 없다.** 소멸은 소멸 사건으로 온다(계약 7).
    //      `IsAlive` 는 **초당 한 번**의 자가 치유로만 쓰고, 유령이 잡히면 **경고를 남긴다** —
    //      그것은 정상 경로가 아니라 「어떤 소멸 경로가 사건을 안 냈다」는 신호이기 때문이다.
    //      경고 없이 조용히 치유하면 그 구멍은 영원히 안 고쳐진다.
    //
    // 백엔드 선택은 **`TrySpawn` 한 곳**이다(옛 계약 그대로). 스프라이트 세트가 있으면 스프라이트,
    // 없고 스켈레톤이 있으면 Spine, 둘 다 없으면 개발용 쿼드 폴백이다.
    // ⚠ 쿼드는 `CoreUnitView` 로 **편입하지 않는다** — 개발용 폴백을 백엔드로 승격시키는 것은
    // 별개 결정이고, 편입하려면 없는 멤버 열두 개를 지어내야 한다. 그래서 사전이 둘이다.
    [DisallowMultipleComponent]
    public sealed class CoreUnitViewPool : MonoBehaviour
    {
        [SerializeField] private BattleDriver _driver;

        [Header("뷰 설정")]
        [SerializeField] private CharacterViewConfig _characterView;
        [SerializeField] private BlobShadowConfig _blobShadow;
        [SerializeField] private UnitLiftKnobs _liftKnobs;

        [Header("도약 연출")]
        [Tooltip("비행 중 위치를 덮어쓰는 프리젠터. 비어 있으면 도약은 순간이동으로 보인다.")]
        [SerializeField] private CoreLeapPresenter _leap;

        [Tooltip("배치 비행 프리젠터. 비어 있으면 놓은 유닛이 「툭」 생긴다.")]
        [SerializeField] private CoreDeployFlightPresenter _deployFlight;

        [Header("개발용 폴백(쿼드)")]
        [Tooltip("스프라이트도 스켈레톤도 없는 저작이 나왔을 때 그리는 메시. 비우면 Unity 기본 Quad.")]
        [SerializeField] private Mesh _fallbackMesh;
        [SerializeField] private Material _fallbackMaterial;

        private readonly Dictionary<int, CoreUnitView> _byId = new Dictionary<int, CoreUnitView>();
        private readonly Dictionary<int, CoreQuadUnitView> _quadById = new Dictionary<int, CoreQuadUnitView>();
        private readonly List<int> _scratch = new List<int>();

        private float _nextSweep;

        // ── unit 8a2 행 4 — 배치 드래그 중 적 반투명(옛 `BattleBridge.SetEnemiesDimmed` `:79` · 페이드 `:3108-3110`) ──
        // 입력(`DragPlacementInput`)이 켜고 끈다. 알파는 **unscaled** 로 페이드한다 — 드래그 슬로모와 무관(옛 그대로).
        // 값(목표 알파 · 속도)은 `CharacterViewConfig.EnemyDragDim*` 가 정본이다. 적만 흐린다(방어유닛 루프 미적용 — 옛 그대로).
        private bool _enemyDimActive;
        private float _enemyDimAlpha = 1f;
        private bool _healthTintWarned;

        // ── unit 8a2 행 6 — 소환사 유지 루프(옛 `SyncSummonerAnimationState` `:4108-4122`) — 유닛 줄별 (루프, 상실 원샷) 캐시.
        private readonly Dictionary<int, SummonPatrolAbility> _summonAbility = new Dictionary<int, SummonPatrolAbility>();

        /// <summary>배치 드래그 중 적을 흐린다/되돌린다(옛 `SetEnemiesDimmed`). 페이드는 풀의 매 프레임이 진다.</summary>
        public void SetEnemiesDimmed(bool active) => _enemyDimActive = active;

        /// <summary>테스트 창구 — 지금 적에게 미는 알파(1 = 안 흐림).</summary>
        public float EnemyDimAlpha => _enemyDimAlpha;
        public bool EnemiesDimmed => _enemyDimActive;

        // 증언 창(테스트) — 풀이 **마지막으로 민 값**. 「사건/입력 → 뷰 호출」을 백엔드 내부를 열지 않고 잰다.
        private readonly Dictionary<int, Color> _pushedTint = new Dictionary<int, Color>();
        private readonly Dictionary<int, Somnia.Battle.UnitAi.DefenderAiState> _pushedAi = new Dictionary<int, Somnia.Battle.UnitAi.DefenderAiState>();

        /// <summary>테스트 창구 — 그 적에게 마지막으로 민 체력 틴트(행 5).</summary>
        public bool TryGetPushedEnemyTint(SimEntityId id, out Color tint) => _pushedTint.TryGetValue(id.Value, out tint);

        /// <summary>테스트 창구 — 그 소환사에게 마지막으로 민 AI 상태(행 6).</summary>
        public bool TryGetPushedAiState(SimEntityId id, out Somnia.Battle.UnitAi.DefenderAiState state)
            => _pushedAi.TryGetValue(id.Value, out state);

        /// <summary>살아 있는 유닛 뷰 수. 「뷰 수 = 코어 유닛 수」 검사의 오른쪽 항이다.</summary>
        public int ViewCount => _byId.Count + _quadById.Count;

        private CoreViewKnobs Knobs => new CoreViewKnobs(
            _characterView, _blobShadow, _liftKnobs,
            _driver != null ? _driver.TileSize : 1f);

        private void OnEnable()
        {
            if (_driver != null) _driver.Subscribe(ViewOrder.Unit, OnCoreEvent);
            // Battle 도메인 배율이 바뀌면 살아 있는 뷰의 애니 속도를 같이 민다.
            // (스폰 순간의 초기화는 뷰가 pull 로 한다 — 스폰 레이스를 막는 옛 계약 그대로.)
            TimeManager.Instance.ScaleChanged += OnBattleScaleChanged;
        }

        private void OnDisable()
        {
            if (_driver != null) _driver.Unsubscribe(OnCoreEvent);
            TimeManager.Instance.ScaleChanged -= OnBattleScaleChanged;
        }

        private void OnDestroy() => DisposeAll();

        private void OnBattleScaleChanged(TimeDomain domain, float scale)
        {
            if (domain != TimeDomain.Battle) return;
            foreach (var kv in _byId)
                if (kv.Value != null) kv.Value.SetAnimationTimeScale(scale);
        }

        // ── 사건 ─────────────────────────────────────────────────────────────
        private void OnCoreEvent(CoreEvent e)
        {
            switch (e.Kind)
            {
                case CoreEventKind.MatchStarted:
                    _summonAbility.Clear();   // 유닛 표는 판마다 새로 짓는다(8b 로비 편성)
                    _pushedTint.Clear();
                    _pushedAi.Clear();
                    break;

                case CoreEventKind.UnitSpawned:
                    TrySpawn(e);
                    break;

                case CoreEventKind.UnitDestroyed:
                    // 소멸은 **제거**다. 사망 연출은 `UnitSlain` 이 이미 걸었고, 이 시점의 뷰는
                    // 그 연출을 재생 중이거나(Spine·스프라이트: 자멸) 그냥 사라져도 되는 것이다.
                    Despawn(e.A);
                    break;

                case CoreEventKind.UnitSlain:
                    // **피해로** 죽었다 — 사망 모션을 틀고 뷰는 스스로 정리한다.
                    if (TryGet(e.B, out var slain)) slain.Kill();
                    _byId.Remove(e.B.Value);
                    break;

                case CoreEventKind.AttackResolved:
                    // `Amount` = 실주기. 애니가 실발사보다 빨리 끝나지 않게 뷰가 압축한다.
                    if (TryGet(e.A, out var attacker))
                    {
                        attacker.FaceToward((Vector3)Somnia.Battle.Core.BoardSpace.ToView(e.SiteTarget.Pos));
                        attacker.PlayAttack(e.Amount);
                    }
                    break;

                case CoreEventKind.ProjectileHit:
                    // E27 — 피격 팝. **생산자는 착탄 하나뿐**이라 근접 공격에는 안 뜬다(옛 성질 유지).
                    // 0.15초·0.2배는 이제 코드 상수가 아니라 `CharacterViewConfig` 의 노브다.
                    if (_characterView != null && _characterView.HitPopSeconds > 0f
                        && TryGet(e.B, out var victim))
                        victim.PlayPunch(_characterView.HitPopOvershoot, _characterView.HitPopSeconds);
                    break;

                case CoreEventKind.Knockup:
                    // 띄우기는 **띄운 쪽이 대상을 직접 신호한다** — 심에서 넉업의 실체는 짧은
                    // 기절이라 뷰가 군중 제어 종류로는 일반 기절과 구분할 수 없다.
                    if (TryGet(e.A, out var tossed))
                        tossed.PlayKnockupHop(e.Amount, e.Arg / 1000f);
                    break;

                case CoreEventKind.DefenderActivated:
                    if (TryGet(e.A, out var deployed)) deployed.PlayDeploy();
                    break;
            }
        }

        // ── 생성 ─────────────────────────────────────────────────────────────
        //
        // **백엔드 선택은 여기 한 곳이다.** 호출처가 늘어도 선택 규칙이 흩어지지 않는다.
        private void TrySpawn(CoreEvent e)
        {
            var kind = (UnitKind)e.Arg;
            // 거점은 이 풀의 것이 아니다 — 프랍은 **맵 수명**이라 `CoreStructurePropLayer` 가 든다.
            if (kind == UnitKind.Structure) return;
            // unit 6c — 길막 설치물은 **바닥에 놓인 물건**이라 `CoreHazardViewPool` 이 든다.
            // 여기서 받으면 그 줄 번호(`BlockingHazards`)를 유닛 표로 읽어 엉뚱한 스켈레톤이 선다.
            if (kind == UnitKind.BlockingHazard) return;
            if (_byId.ContainsKey(e.A.Value) || _quadById.ContainsKey(e.A.Value)) return;

            var visual = ResolveVisual(kind, e.Faction, e.DefIndex);
            var knobs = Knobs;
            Vector3 worldPos = (Vector3)e.SiteFired.Pos;
            string prefix = kind == UnitKind.Enemy ? "Enemy" : "Defender";

            var set = visual != null ? visual.SpriteMotions : null;
            if (set != null && !set.HasIdle)
            {
                // 세트가 있는데 idle 이 비면 **조용히 안 보이는 유닛**이 나온다 — 경고하고 무시한다.
                Debug.LogWarning($"[CoreUnitViewPool] '{visual.SpineDisplayName}' 의 스프라이트 세트 "
                    + $"'{set.name}' 에 idle 이 없다 — 세트를 무시한다.", set);
                set = null;
            }

            if (visual != null && (set != null || visual.SpineSkeletonDataAsset != null))
            {
                string safeName = string.IsNullOrEmpty(visual.SpineDisplayName) ? "Unit" : visual.SpineDisplayName;
                var go = new GameObject($"{prefix}_{safeName}_{e.A.Value}");
                go.transform.SetParent(transform, worldPositionStays: false);
                var extras = visual as IDefenderSpineExtras;

                CoreUnitView view;
                if (set != null)
                {
                    var sprite = go.AddComponent<CoreSpriteUnitView>();
                    sprite.Spawn(visual, extras, set, e.A, worldPos, knobs);
                    view = sprite;
                }
                else
                {
                    var spine = go.AddComponent<CoreSpineUnitView>();
                    spine.Spawn(visual, extras, e.A, worldPos, knobs);
                    view = spine;
                }
                _byId[e.A.Value] = view;
                return;
            }

            // 개발용 폴백. 「안 보이는 유닛」이 판에 도는 것보다 네모라도 보이는 편이 낫다.
            var quadGo = new GameObject($"QuadUnit_{prefix}_{e.A.Value}");
            quadGo.transform.SetParent(transform, worldPositionStays: false);
            quadGo.transform.position = (Vector3)Somnia.Battle.Core.BoardSpace.ToView(e.SiteFired.Pos);
            var quad = quadGo.AddComponent<CoreQuadUnitView>();
            // 몸 반경은 **사건이 값으로 나른다**(`SiteFired.OriginBody`) — 기본값을 두지 않는다.
            // 호출처마다 정답이 다르고, 기본값이 있으면 새 호출처가 조용히 표준값을 받는다.
            quad.Configure(e.A, _fallbackMesh, _fallbackMaterial,
                           knobs.CharacterVisualScale, e.SiteFired.OriginBody, knobs);
            _quadById[e.A.Value] = quad;
        }

        private ISpineUnitVisualData ResolveVisual(UnitKind kind, Somnia.Battle.Skills.Faction faction, int defIndex)
        {
            if (_driver == null || defIndex < 0) return null;
            // ⚠ 종류에 따라 **가리키는 표가 다르다**. 적이면 적 표, 그 외(방어유닛·순찰)는 유닛 표다.
            if (kind == UnitKind.Enemy)
            {
                var enemies = _driver.EnemyAssets;
                return defIndex < enemies.Count ? enemies[defIndex] : null;
            }
            var units = _driver.DefenderAssets;
            return defIndex < units.Count ? units[defIndex] : null;
        }

        // ── 조회 ─────────────────────────────────────────────────────────────
        public bool TryGet(SimEntityId id, out CoreUnitView view)
            => _byId.TryGetValue(id.Value, out view) && view != null;

        /// <summary>
        /// unit 8a — 뷰를 풀에서 **떼어 넘긴다**(옛 `SpineUnitPool.Detach` 의 계약). 이 순간부터 뷰의 수명은
        /// 받은 쪽(퇴근 비행)의 것이다 — 풀은 뒤따르는 소멸 사건에서 그 뷰를 모른다. 없으면 false.
        /// </summary>
        public bool TryDetach(SimEntityId id, out CoreUnitView view)
        {
            if (!TryGet(id, out view)) return false;
            _byId.Remove(id.Value);
            return true;
        }

        public bool TryGetQuad(SimEntityId id, out CoreQuadUnitView view)
            => _quadById.TryGetValue(id.Value, out view) && view != null;

        public bool TryResolveProjectileLaunchAnchor(SimEntityId id, out Vector3 worldPos)
        {
            if (TryGet(id, out var view))
            {
                worldPos = view.ResolveProjectileLaunchAnchor();
                return true;
            }
            worldPos = default;
            return false;
        }

        /// <summary>
        /// unit 6c — 그 유닛의 **지금 화면 자리**(view 공간). 빔·총구·표식이 같은 질문을 하므로
        /// 한 곳에서 답한다. `useAnchor` = 캐스트 앵커(손·총구) 우선. 스파인·스프라이트 → 쿼드 폴백
        /// → 읽기 모델 순이다 — 뷰 풀에 없는 유닛이 끝점이면 빔이 통째로 죽는다(옛 브리지의 함정).
        /// </summary>
        public bool TryResolveViewPosition(SimEntityId id, bool useAnchor, out Vector3 pos)
        {
            if (TryGet(id, out var view))
            {
                pos = useAnchor ? view.ResolveCastAnchor() : view.transform.position;
                return true;
            }
            if (TryGetQuad(id, out var quad))
            {
                pos = quad.transform.position;
                return true;
            }
            if (_driver != null && _driver.TryGetRenderPosition(id, out var sim))
            {
                pos = (Vector3)Somnia.Battle.Core.BoardSpace.ToView(sim);
                return true;
            }
            pos = default;
            return false;
        }

        public void Despawn(SimEntityId id)
        {
            _pushedTint.Remove(id.Value);
            _pushedAi.Remove(id.Value);
            if (_byId.TryGetValue(id.Value, out var view))
            {
                _byId.Remove(id.Value);
                if (view != null) view.Dispose();
            }
            if (_quadById.TryGetValue(id.Value, out var quad))
            {
                _quadById.Remove(id.Value);
                if (quad != null) Destroy(quad.gameObject);
            }
        }

        public void DisposeAll()
        {
            foreach (var kv in _byId) if (kv.Value != null) kv.Value.Dispose();
            _byId.Clear();
            foreach (var kv in _quadById) if (kv.Value != null) Destroy(kv.Value.gameObject);
            _quadById.Clear();
        }

        // ── 매 프레임 ────────────────────────────────────────────────────────
        private void LateUpdate()
        {
            // 페이드는 판 상태와 무관하게 돈다(옛 `_running` 앞 — 드래그가 끝나면 어느 페이즈든 원복된다).
            StepEnemyDim();
            if (_driver == null || !_driver.Running) return;
            SyncViews();
            SelfHealOnce();
        }

        private void StepEnemyDim()
        {
            float target = _enemyDimActive && _characterView != null ? Mathf.Clamp01(_characterView.EnemyDragDimAlpha) : 1f;
            float speed = _characterView != null ? _characterView.EnemyDragDimFadeSpeed : float.PositiveInfinity;
            _enemyDimAlpha = Mathf.MoveTowards(_enemyDimAlpha, target, speed * Time.unscaledDeltaTime);
        }

        // 적 틴트 두 축(옛 `:3863-3882`): **흐림을 틴트 앞에** — 쿼드는 `SetHealthTint` 가 알파를 반영한다.
        // 0.999 = 「다 돌아왔다」 판별(옛 `:3866` `_enemyDimAlpha < 0.999f`) — 페이드 꼬리에서 불투명 전환을 한 번에 한다.
        private Color EnemyTintOf(Unit u)
        {
            var style = _characterView != null ? _characterView.HealthDisplayStyle : null;
            var mode = _characterView != null ? _characterView.HealthPresentationMode : UnitHealthPresentationMode.Legacy;
            if (style == null && mode != UnitHealthPresentationMode.UnifiedOverhead && !_healthTintWarned)
            {
                _healthTintWarned = true;
                Debug.LogWarning("[CoreUnitViewPool] healthDisplayStyle 미할당 — 적 체력 틴트 스킵.", this);
            }
            return CoreEnemyHealthTint.Resolve(mode, style, u.Health, u.MaxHealth);
        }

        // 소환사 — 「그 줄에 순찰 소환 능력 + 유지 루프 이름이 있나」(옛 `FindSummonPatrolAbility` · `activeAnimation` 게이트).
        private SummonPatrolAbility SummonAbilityOf(int defIndex)
        {
            if (_summonAbility.TryGetValue(defIndex, out var cached)) return cached;
            SummonPatrolAbility found = null;
            var units = _driver.DefenderAssets;
            var data = defIndex >= 0 && defIndex < units.Count ? units[defIndex] : null;
            if (data != null && data.abilities != null)
                for (int i = 0; i < data.abilities.Count && found == null; i++)
                    found = data.abilities[i] as SummonPatrolAbility;
            if (found != null && string.IsNullOrEmpty(found.activeAnimation)) found = null;
            _summonAbility[defIndex] = found;
            return found;
        }

        private void SyncViews()
        {
            var gridSize = _driver.GridSize;
            float tileSize = _driver.TileSize;

            var units = _driver.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Kind == UnitKind.Structure || u.Kind == UnitKind.BlockingHazard) continue;

                // 뷰 위치를 덮어쓰는 축은 **닫혀 있다 — 둘뿐**이다(도약 · 배치 비행). 둘은
                // 좌표계가 달라 한 질문으로 접히지 않는다: 도약은 sim 좌표 + view 높이라
                // 정상 피드를 그대로 타고, 배치 비행은 아치가 camUp 이라 **view 절대 좌표**다
                // (sim 으로 접으면 `ToView` 가 높이를 버려 화면에서 옆으로 미끄러진다).
                // 그래서 인터페이스 하나로 묶지 않고 각자에게 묻는다.
                if (_deployFlight != null
                    && _deployFlight.TryGetFlightView(u.Id, out var deployPos, out float deployLift, out var deployGround))
                {
                    if (_byId.TryGetValue(u.Id.Value, out var flying) && flying != null)
                        flying.SetFlightView(deployPos, deployLift, deployGround);
                    continue;
                }

                Unity.Mathematics.float3 pos;
                float flightHeight = 0f;
                // 도약 중이면 **그 프리젠터의 값이 이긴다**(`ViewOrder.Leap` 이 먼저 돌았다).
                // 이 순서가 X3 의 이행이다 — 뒤집히면 착지점에서 출발점으로 튀는 팝이 보인다.
                if (_leap != null && _leap.TryGetFlightOverride(u.Id, out var flightPos, out flightHeight))
                    pos = flightPos;
                else if (!_driver.TryGetRenderPosition(u.Id, out pos))
                    continue;

                bool enemy = u.Kind == UnitKind.Enemy;
                bool dimmed = _enemyDimAlpha < 0.999f;
                if (_byId.TryGetValue(u.Id.Value, out var view) && view != null)
                {
                    view.SetFlightHeight(flightHeight);
                    view.UpdatePosition((Vector3)pos);
                    view.UpdateSortingOrder(gridSize, tileSize);
                    if (enemy)
                    {
                        view.SetDimmed(dimmed, _enemyDimAlpha);   // 행 4
                        var tint = EnemyTintOf(u);                 // 행 5
                        view.SetHealthTint(tint);
                        _pushedTint[u.Id.Value] = tint;
                    }
                    else if (u.Kind == UnitKind.Defender && u.Attack != null
                             && u.Attack.Policy == AttackPolicy.Summon && view is CoreSpineUnitView)
                    {
                        // 행 6 — **매 프레임** 민다(원샷 도중 요청은 그 프레임에 못 들어가 재시도가 필요하다 — 뷰의 계약).
                        // 뷰는 AI 상태 하나로 루프를 고르고, 엣지(걸려 있었나)는 뷰가 판정한다. Spine 만(옛 `is SpineUnitView`).
                        var ability = SummonAbilityOf(u.DefIndex);
                        if (ability != null)
                        {
                            view.SetAiState(u.Ai.Defender, ability.activeAnimation, ability.lostAnimation);
                            _pushedAi[u.Id.Value] = u.Ai.Defender;
                        }
                    }
                }
                else if (_quadById.TryGetValue(u.Id.Value, out var quad) && quad != null)
                {
                    quad.SetFlightHeight(flightHeight);
                    quad.UpdatePosition((Vector3)pos);
                    quad.UpdateSortingOrder(gridSize, tileSize);
                    if (enemy)
                    {
                        quad.SetDimmed(dimmed, _enemyDimAlpha);
                        var tint = EnemyTintOf(u);
                        quad.SetHealthTint(tint);
                        _pushedTint[u.Id.Value] = tint;
                    }
                }
            }
        }

        // 초당 한 번. **정상 경로가 아니다** — 여기서 유령이 잡히면 어떤 소멸 경로가 소멸
        // 사건을 안 냈다는 뜻이고(계약 7 위반), 그래서 조용히 치우지 않고 경고를 남긴다.
        private void SelfHealOnce()
        {
            if (Time.unscaledTime < _nextSweep) return;
            _nextSweep = Time.unscaledTime + 1f;

            _scratch.Clear();
            foreach (var kv in _byId)
                if (kv.Value == null || !_driver.IsAlive(new SimEntityId(kv.Key))) _scratch.Add(kv.Key);
            foreach (var kv in _quadById)
                if (kv.Value == null || !_driver.IsAlive(new SimEntityId(kv.Key))) _scratch.Add(kv.Key);

            for (int i = 0; i < _scratch.Count; i++)
            {
                Debug.LogWarning($"[CoreUnitViewPool] 유령 뷰 회수 — id {_scratch[i]} 는 판에 없는데 뷰가 남아 있었다. "
                    + "소멸 사건을 안 낸 경로가 있다(계약 7).", this);
                Despawn(new SimEntityId(_scratch[i]));
            }
            _scratch.Clear();
        }
    }
}
