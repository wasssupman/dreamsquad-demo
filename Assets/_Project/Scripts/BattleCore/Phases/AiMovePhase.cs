using Unity.Mathematics;
using Wassup.Battle.Units;
using Wassup.BattleCore.Combat;
using Wassup.BattleCore.Map;
using Wassup.BattleCore.Move;
using Wassup.UnitAi;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 2 — **적이 걷는다.**
    //
    // 하위 단계의 순서가 계약이다(옛 전투의 `[UpdateBefore]` 사슬을 호출 순서로 명시):
    //   ① 상태 판정(`EnemyAi.Evaluate`)  ② 어그로/도발 부여  ③ 거점 목적지
    //   ④ 감지  ⑤ 이동  ⑥ (별도 패스) 분리
    //
    // ⑤ 안의 순서는 census 「이동 결정 순서」 그대로다:
    //   외력 합성 단일 지점 → 상태 갈림 → 포털 → 칸·골 판정 → 당김 → 교전 정책 →
    //   스텝 소스(**어그로 &gt; 감지 &gt; 웨이포인트 &gt; 거점 &gt; 골**) → 평활화 → 충돌 trim
    //
    // ⚠ **상태 갈림은 거기서 끝난다** — 대치/추격은 포털·골·당김을 안 지난다.
    // ⚠ **스텝 소스 순서를 뒤집지 말 것.** 웨이포인트는 맵이 「이 길로 와라」고 정한 계약이고
    // 거점 선택은 그 안의 전술이다. 뒤집으면 저작이 조용히 무시된다.
    public sealed class AiMovePhase : ITickPhase
    {
        public string Name => "AiMove";

        // ── 감지의 네 박자 · 코드 상수(M9) ──────────────────────────────────────
        // 노브로 올리지 않는다 — 올리면 정의표 해시가 움직여 골든 빨강이 「조건 드리프트」로
        // 읽힌다. **표식 쿨 &gt; 억제** 관계 자체가 계약이다(뒤집히면 표식이 억제 창 안에서 두 번 난다).
        public const float GraceSeconds = 1f;
        public const float StuckReleaseSeconds = 2f;
        public const float SuppressSeconds = 5f;
        public const float MarkCooldownSeconds = 6f;

        /// <summary>경로 탐침 상한. 최근접이 못 가면 다음을 본다 — 「갈 수 있는 적이 있으면」이 규칙이다.</summary>
        public const int MaxPathProbes = 3;

        /// <summary>우회 상한(감지 반경 배수). 이보다 돌아가야 하면 「갈 수 없다」로 친다.</summary>
        public const float MaxDetourFactor = 2f;

        /// <summary>
        /// 유지 임계가 획득보다 넓다 — 방어유닛 둘 사이에서 대상이 튀는 것을 막는다.
        ///
        /// ⚠ **unit 3 에서 공격 락과 같은 자로 합쳤다.** unit 2 는 여기에 0.5 를 따로 들고
        /// 있었는데, 옛 전투의 감지도 `TargetPersistence.KeepsLock`(0.1)을 **재사용**했으므로
        /// 그쪽이 옳다. 같은 종류의 진동을 막는 데 두 개의 자를 두지 않는다 — 값의 근거
        /// (실측 지터 0.047·0.051의 약 2배)는 `TargetPersistence` 헤더에 있다.
        /// </summary>
        public const float HysteresisTiles = Combat.TargetPersistence.HysteresisTiles;

        private readonly MapRuntime _map;
        private readonly ChaseFieldPool _pool;

        // 재사용 버퍼 — 틱 중 할당 0.
        private readonly CellQueue _queue;
        private int2[] _chaseSources = new int2[64];
        private readonly float2[] _probeFlow;
        private readonly int[] _probeDist;

        private float2[] _push = new float2[64];
        private float2[] _forward = new float2[64];
        private Unit[] _movers = new Unit[64];

        private float2[] _structPos = new float2[16];
        private int[] _structFaction = new int[16];
        private int2[] _structCell = new int2[16];
        /// <summary>이번 틱 **살아 있는** 거점의 진영 비트. 죽은 자리는 0 이라 어떤 마스크도 안 문다.</summary>
        private int[] _structLiveFaction = new int[16];
        private int _structCount;

        private SimEntityId[] _rejected = new SimEntityId[MaxPathProbes];

        public AiMovePhase(MapRuntime map, ChaseFieldPool pool)
        {
            _map = map;
            _pool = pool;
            int n = math.max(1, map.Snapshot.CellCount);
            _queue = new CellQueue(n * 2);
            _probeFlow = new float2[n];
            _probeDist = new int[n];
            SortStructures(map.Snapshot);
        }

        // 거점 후보는 **칸 사전순**으로 한 번만 정렬해 둔다(M18). 동률 타이브레이크가 후보
        // 순서에 걸려 있는데, 순서가 흔들리면 「가이드 ≠ 실제 이동선」이 동률에서만 간헐적으로
        // 재현된다 — 가장 잡기 싫은 형태다.
        private void SortStructures(MapSnapshot map)
        {
            _structCount = map.Structures.Length;
            if (_structCount > _structPos.Length)
            {
                _structPos = new float2[_structCount];
                _structFaction = new int[_structCount];
                _structCell = new int2[_structCount];
                _structLiveFaction = new int[_structCount];
            }
            for (int i = 0; i < _structCount; i++)
            {
                _structCell[i] = map.Structures[i].Cell;
                _structFaction[i] = map.Structures[i].Faction;
            }
            for (int i = 1; i < _structCount; i++)
            for (int j = i; j > 0 && StructureChoice.IsBefore(_structCell[j], _structCell[j - 1]); j--)
            {
                (_structCell[j], _structCell[j - 1]) = (_structCell[j - 1], _structCell[j]);
                (_structFaction[j], _structFaction[j - 1]) = (_structFaction[j - 1], _structFaction[j]);
            }
            for (int i = 0; i < _structCount; i++)
            {
                float3 c = _map.CenterOf(_structCell[i]);
                _structPos[i] = new float2(c.x, c.z);
            }
        }

        // ── 거점 선택의 창구(M18) ─────────────────────────────────────────────
        //
        // **예고선이 「어디로 갈까」를 묻는 자리다.** 후보 배열도 정렬도 생존·방패 반영도
        // 전부 이 클래스의 것이고, 밖으로 내보내는 것은 **답 하나**다.
        //
        // 배열을 그대로 빌려주지 않는 이유: 빌려주면 부르는 쪽이 자기 필터를 한 줄 얹게 되고,
        // 그 한 줄이 곧 두 번째 자다. 옛 전투가 정확히 그렇게 갈렸다 — 브리지가 후보를 다시
        // 모으고 `StructureChoice` 만 공유했는데, 방패 배제가 한쪽에만 들어가 **예고선은
        // 마음으로 가는 길을 그리는데 적은 본능으로 갔다.**

        /// <summary>거점 후보 수(저작 자리). 판 중에 늘거나 줄지 않는다.</summary>
        public int StructureCount => _structCount;

        /// <summary>
        /// 그 자리에서 이 마스크로 갈 거점. 없으면 false — 그때는 골이 목적지다.
        ///
        /// ⚠ **이동이 매 틱 쓰는 그 배열 그대로** 고른다(`_structLiveFaction` — 죽은 자리와
        /// 방패에 가린 마음은 0 이라 어떤 마스크도 안 문다). 이 함수가 도는 시점이 틱 밖이면
        /// 값은 「마지막 틱의 생존」이고, 그것이 예고선이 원하는 답이다.
        /// </summary>
        public bool TryPickStructure(float2 from, int targetMask, out int2 cell)
        {
            cell = default;
            if (_structCount == 0) return false;
            int pick = StructureChoice.NearestIndex(
                from, _structPos, _structLiveFaction, _structCount, targetMask);
            if (pick < 0) return false;
            cell = _structCell[pick];
            return true;
        }

        public void Run(TickContext ctx)
        {
            if (_map == null || _map.Snapshot.CellCount == 0) return;

            StepAiState(ctx);
            GrantAggro(ctx);
            StepStructureDestination(ctx);
            StepDetection(ctx);
            StepMovement(ctx);
            StepSeparation(ctx);
        }

        // ── ① 상태 판정 ───────────────────────────────────────────────────────
        private void StepAiState(TickContext ctx)
        {
            var units = ctx.World.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Move == null || u.Dead) continue;
                if (u.Kind != UnitKind.Enemy) { u.Ai.Enemy = AiState.Marching; continue; }

                var def = EnemyDefOf(ctx, u);
                bool aggroed = u.Aggro != null && !u.Aggro.Target.IsNone;
                bool guardianInRange = aggroed && ReachProbe.GuardianInRange(ctx.World, u, def, _map.TileSize);
                bool hasFireTarget = !aggroed && ReachProbe.HasFireTarget(ctx.World, u, def, _map.TileSize);
                // **결정은 `Wassup.UnitAi`, 저장은 `Unit.Ai`**(unit 3 구현 12) — 공격 루프가
                // 읽는 자리와 같아야 「락은 있는데 Marching」 데드락이 안 난다.
                u.Ai.Enemy = EnemyAi.Evaluate(aggroed, guardianInRange, hasFireTarget);
            }
        }

        // ── ② 어그로·도발 부여 ────────────────────────────────────────────────
        //
        // 히트 요청은 unit 3 이, 도발 요청은 unit 7 이 넣는다. **게이트는 여기 하나**다.
        private void GrantAggro(TickContext ctx)
        {
            var reqs = ctx.World.AggroRequests;
            if (reqs.Count == 0) return;

            for (int r = 0; r < reqs.Count; r++)
            {
                var req = reqs[r];
                var enemy = ctx.World.Find(req.Enemy);
                var guardian = ctx.World.Find(req.Guardian);
                if (enemy == null || enemy.Dead || enemy.Move == null) continue;
                if (guardian == null || guardian.Dead || guardian.Aggro == null) continue;

                bool already = enemy.Aggro != null && !enemy.Aggro.Target.IsNone;
                // 히트는 선점(먼저 온 쪽이 이긴다) — 도발은 그것을 우회한다(나중에 부른 쪽이 이긴다).
                if (!req.Taunt && already) continue;

                var def = EnemyDefOf(ctx, enemy);

                // **유닛을 노리지 않는 적은 유인으로 막을 수 없다**(히트·도발 둘 다) — 거점 전담
                // 적(마음사냥꾼)은 죽여야만 막힌다. 옛 `AggroStateSystem` 의 도발 범위 게이트이고,
                // 저작 **의도**(해석된 대상 진영)를 읽는다 — 0(미저작)은 기본 마스크로 풀려
                // 유닛 비트를 갖는다(2026-09-24 드리프트 감사 H5).
                if ((TargetDefaults.ResolveEnemy(def.TargetFactions) & Factions.AnyUnit) == 0) continue;

                // 공격 수단이 없으면 가디언을 때릴 수 없으므로 거부한다 — 안 그러면
                // 「못 때리는데 끌려가서 영원히 서 있는」 적이 생긴다.
                int tileRange = AggroChaseMath.ResolveTileRange(def.AttackRange > 0f, def.AttackRange, false, 0f);
                if (tileRange == AggroChaseMath.NoAttack) continue;

                // 도발은 **수용량과 선점 둘만** 우회한다. 도달 불가는 그대로 막는다 —
                // 풀면 「못 가는 곳을 향해 영원히 밀리는 적」이 부활한다.
                if (!req.Taunt)
                {
                    int cap = guardian.Aggro.Capacity;
                    if (cap <= 0 || guardian.Aggro.Held >= cap) continue;
                }

                var cache = enemy.Aggro?.Chase ?? _pool.Rent();
                if (!BuildChase(enemy, _map.CellOf(guardian.Position), tileRange, cache, enemy.Position))
                {
                    // ⚠ **이미 어그로된 적을 도발이 가져올 때 새 추격판을 못 구우면 거절한다.**
                    // 대상만 바꾸면 적은 **옛 가디언 기준으로 구운 필드**를 그대로 들고 그쪽으로
                    // 걸어간다 — 엉뚱한 곳으로 끌려가는 것보다 거절이 낫다.
                    if (enemy.Aggro?.Chase == null) _pool.Return(cache);
                    continue;
                }

                enemy.Aggro = enemy.Aggro ?? ctx.World.Parts.RentAggro();   // F4 — 틱 중 할당 0
                enemy.Aggro.Target = req.Guardian;
                enemy.Aggro.Remaining = req.Seconds;
                enemy.Aggro.Taunted = req.Taunt;
                enemy.Aggro.Chase = cache;
                if (!already) guardian.Aggro.Held++;

                ctx.Bus.Publish(CoreEvent.AggroAcquired(ctx.Tick, enemy, req.Guardian, req.Taunt));
            }
            reqs.Clear();
        }

        // ── ③ 거점 목적지 ─────────────────────────────────────────────────────
        //
        // 「내가 팰 수 있는 거점 중 가장 가까운 것」. 웨이포인트 **뒤**에 오는 스텝 소스다.
        private void StepStructureDestination(TickContext ctx)
        {
            if (_structCount == 0) return;
            RefreshStructureLiveness(ctx);
            var units = ctx.World.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Move == null || u.Dead || u.Kind != UnitKind.Enemy) continue;

                var def = EnemyDefOf(ctx, u);
                int mask = TargetDefaults.ResolveEnemy(def.TargetFactions);
                int pick = StructureChoice.NearestIndex(
                    new float2(u.Position.x, u.Position.z), _structPos, _structLiveFaction, _structCount, mask);

                u.Move.HasStructureDest = false;
                if (pick < 0) continue;

                // 그 통행 층으로 못 가는 거점이면 골로 되돌아간다.
                byte layers = LayersOf(u);
                if (!_map.Flow.HasSlot(_structCell[pick], layers)) continue;
                var slot = _map.Flow.Slot(_structCell[pick], layers);
                if (!slot.Reaches(_map.CellOf(u.Position))) continue;

                u.Move.HasStructureDest = true;
                u.Move.StructureDest = _structCell[pick];
            }
        }

        /// <summary>
        /// **무너진 거점은 목적지가 아니다.** 저작 자리는 판 내내 스냅샷에 남지만 그 위의
        /// 개체는 죽는다 — 자리만 보면 적이 **잔해를 향해 계속 걸어간다.**
        ///
        /// 죽은 자리의 진영을 0 으로 두는 것으로 고르기에서 빠진다(`(faction & mask) != 0` 이
        /// 자격 술어다). `StructureChoice` 의 서명을 안 바꾸는 이유는 그 함수를 예고선이 함께
        /// 쓰기 때문이다(M18) — 자를 하나 더 만들면 「가이드 ≠ 실제 이동선」이 돌아온다.
        ///
        /// 개체와 자리는 **칸으로** 맞춘다. 거점은 자기 칸 중앙에 서고 움직이지 않으므로
        /// 그 대응이 판 내내 유지된다(움직이는 거점이 생기면 이 가정부터 깨진다).
        ///
        /// **방패 걸린 마음(`Untargetable`)도 목적지가 아니다.** 「표적에서 뺀다」는 조준과
        /// 경로 둘 다다 — 조준만 빼면 적이 방패 걸린 마음 앞에 도착해 **때리지도 못하고
        /// 서 있는다.** 옛 `StructureDestinationSystem` 의 `.WithNone&lt;CoreShielded&gt;()` 이 이것이다.
        /// 단, 마음 타워는 골 자리(`MapSnapshot.Goals`)에서 세워지고 이 후보 목록은 저작 거점
        /// (`MapSnapshot.Structures`)에서 오므로, 저작이 골 칸에 마음 자리를 따로 두지 않는 한
        /// 마음은 애초에 여기 없다 — 「가장 가까운 마음」은 골 흐름장이 안다(`pick &lt; 0` 폴백).
        /// </summary>
        private void RefreshStructureLiveness(TickContext ctx)
        {
            for (int i = 0; i < _structCount; i++) _structLiveFaction[i] = 0;

            var units = ctx.World.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Kind != UnitKind.Structure || u.Dead || u.Untargetable) continue;
                var cell = _map.CellOf(u.Position);
                for (int k = 0; k < _structCount; k++)
                {
                    if (!_structCell[k].Equals(cell)) continue;
                    _structLiveFaction[k] = _structFaction[k];
                    break;
                }
            }
        }

        // ── ④ 감지 ────────────────────────────────────────────────────────────
        private void StepDetection(TickContext ctx)
        {
            var units = ctx.World.Units;
            float dt = ctx.Dt;

            for (int i = 0; i < units.Count; i++)
            {
                var self = units[i];
                var d = self.Detection;
                if (d == null || self.Dead || self.Move == null) continue;
                // ⚠ 골에 닿은 적은 감지를 돌리지 않는다 — 이동 루프에서 빠져 `holdingGround` 가
                // 1에 얼어붙고 막힘 타이머가 영원히 쌓인다.
                if (self.Move.PastGoal) continue;

                bool prevHunting = d.Hunting;

                // 억제·표식 쿨은 상태와 무관하게 흐른다.
                if (d.Suppress > 0f) d.Suppress = math.max(0f, d.Suppress - dt);
                if (d.MarkCooldown > 0f) d.MarkCooldown = math.max(0f, d.MarkCooldown - dt);

                var def = EnemyDefOf(ctx, self);
                bool hasAtk = def.AttackRange > 0f;
                bool aggroed = self.Aggro != null && !self.Aggro.Target.IsNone;

                // 무기 없는 적은 감지하지 않는다(fail-closed) · 어그로가 감지를 이긴다.
                if (!hasAtk || aggroed || d.Suppress > 0f)
                {
                    ClearDetection(d);
                    continue;
                }

                float rangeTiles = d.Range;
                bool unlimited = d.Unlimited;
                byte layers = LayersOf(self);
                int myTileRange = GridMath.RangeToTiles(def.AttackRange);
                bool canRoute = !unlimited && _map.InBounds(_map.CellOf(self.Position));
                int maxDetourCost = (int)math.round(rangeTiles * MaxDetourFactor * 10f);

                // ── 이미 문 대상을 유지할 수 있나(히스테리시스) ──
                // 매 틱 최근접을 다시 고르지 않는다 — 그러면 방어유닛 둘 사이에서 대상이 튄다.
                var cur = SimEntityId.None;
                var curUnit = ctx.World.Find(d.Target);
                bool keep = false;
                if (curUnit != null && curUnit.IsTargetable() && curUnit.Health > 0f)
                {
                    keep = unlimited || AttackReach.InReach(
                        self.Position, curUnit.Position, rangeTiles + HysteresisTiles, _map.TileSize,
                        self.HitRadius, curUnit.HitRadius);
                    if (keep) cur = d.Target;
                }

                if (!keep)
                {
                    cur = ScanForTarget(ctx, self, d, def, rangeTiles, unlimited, canRoute,
                                        myTileRange, maxDetourCost);
                }
                else if (canRoute && (d.Chase == null || !d.Chase.Matches(cur, _map.Obstacles.Signature)))
                {
                    // 대상은 유지인데 추격판이 없거나 낡았다(첫 획득 · 장애물 변경).
                    // 다시 굽고, 그래도 못 가면 대상을 놓는다.
                    d.Chase = d.Chase ?? _pool.Rent();
                    if (!BuildChase(self, _map.CellOf(curUnit.Position), myTileRange, d.Chase, self.Position)
                        || d.Chase.Dist[Index(self.Position)] > maxDetourCost)
                        cur = SimEntityId.None;
                }

                if (!cur.IsNone)
                {
                    d.Target = cur;
                    d.Hunting = true;
                    d.Grace = 0f;
                }
                else if (d.Hunting)
                {
                    // 대상을 잃었다 → 관성. 사망·소멸·반경 이탈이 **같은 경로**를 지난다
                    // (「죽었을 때만 관성」 비대칭 방지).
                    d.Target = SimEntityId.None;
                    if (d.Grace <= 0f) d.Grace = GraceSeconds;
                    d.Grace -= dt;
                    if (d.Grace <= 0f) { d.Hunting = false; d.Grace = 0f; }
                }
                else
                {
                    d.Target = SimEntityId.None;
                    d.Grace = 0f;
                }

                // ── 막힘 해제 ──
                // ⚠ **무제한 사냥은 면제한다.** 「방어유닛을 전멸시켜야 골에 간다」는 저작된
                // 성질이고, 타이머가 그것을 취소할 권한을 갖는 순간 감지가 패배 통로의 조절기가 된다.
                // ⚠ **CC·도약 중은 «막힘»이 아니다.** `HoldingGround` 는 CC 잠금도 함께 접으므로
                // 그것만 보면 자장가 한 번에 감지가 풀리고 억제까지 걸린다 —
                // **플레이어가 CC 를 쓸수록 적이 사냥을 그만두는** 정반대 방향이다.
                if (d.Hunting && !unlimited)
                {
                    bool blocked = !self.MovementLocked
                                   && self.Ai.Enemy == AiState.Marching
                                   && self.Move.HoldingGround;
                    d.Stuck = blocked ? d.Stuck + dt : 0f;
                    if (d.Stuck >= StuckReleaseSeconds)
                    {
                        d.Hunting = false;
                        d.Target = SimEntityId.None;
                        d.Grace = 0f;
                        d.Stuck = 0f;
                        d.Suppress = SuppressSeconds;
                    }
                }
                else d.Stuck = 0f;

                // 사냥이 끝났으면 추격판을 돌려준다.
                // ⚠ **관성 중에는 떼지 않는다** — 대상이 죽어 비어도 `Hunting` 은 유지되므로
                // 여기 안 걸린다. 그 1초 동안 적은 마지막 대상 자리로 계속 간다(관성의 실체).
                if (!d.Hunting && d.Chase != null) { _pool.Return(d.Chase); d.Chase = null; }

                // ── 발견 사건(전이 1회) ──
                // 매 틱 쏘면 초당 60건이라 표식이 화면을 덮고 트레이스가 무의미해진다.
                // 관성을 거쳐 다시 잡은 것은 **새 발견이 아니다**(그 사이 `Hunting` 이 유지된다).
                if (!prevHunting && d.Hunting && d.MarkCooldown <= 0f)
                {
                    d.MarkCooldown = MarkCooldownSeconds;
                    ctx.Bus.Publish(CoreEvent.Detected(ctx.Tick, self, d.Target));
                }
            }
        }

        // 후보 스캔 — legal 필터 + 반경 → 최근접(동거리는 낮은 id).
        //
        // ⚠ **최근접 하나로 끝나지 않는다.** 고른 대상까지 갈 수 없으면 그 후보를 빼고 다음을
        // 본다. 규칙은 「감지 반경 안에 **갈 수 있는** 적이 있으면」이지 「최근접이 갈 수 있으면」이
        // 아니다 — 앞의 것으로 끝내면 벽 너머 가까운 유닛 하나가 뒤 레인 전체를 가려 버린다.
        //
        // ⚠ **첫 도달 가능을 그냥 채택하지 않는다** — 랭킹이 직선이라 그러면 우회가 짧은 직통을
        // 이긴다. 탐침한 것 중 **경로가 가장 짧은** 것을 고른다.
        private SimEntityId ScanForTarget(TickContext ctx, Unit self, Detection d, in EnemyDef def,
                                          float rangeTiles, bool unlimited, bool canRoute,
                                          int myTileRange, int maxDetourCost)
        {
            var units = ctx.World.Units;
            int rejectCount = 0;
            var bestReach = SimEntityId.None;
            int2 bestReachCell = default;
            int bestCost = int.MaxValue;
            var lastBuilt = SimEntityId.None;

            for (int probe = 0; probe < MaxPathProbes; probe++)
            {
                var pick = SimEntityId.None;
                float3 pickPos = default;
                float bestSq = float.MaxValue;
                int bestId = int.MaxValue;

                for (int i = 0; i < units.Count; i++)
                {
                    var c = units[i];
                    if (!ReachProbe.IsLegalDetectionTarget(self, c, def)) continue;
                    bool skipped = false;
                    for (int r = 0; r < rejectCount; r++)
                        if (_rejected[r] == c.Id) { skipped = true; break; }
                    if (skipped) continue;

                    // 감지 판정은 사거리와 **같은 자·같은 몸**이다. 무제한은 반경만 건너뛴다.
                    if (!unlimited && !AttackReach.InReach(self.Position, c.Position, rangeTiles,
                                                           _map.TileSize, self.HitRadius, c.HitRadius))
                        continue;

                    float dx = c.Position.x - self.Position.x, dz = c.Position.z - self.Position.z;
                    float sq = dx * dx + dz * dz;
                    if (!ReachProbe.RanksBefore(sq, c.Id.Value, bestSq, bestId)) continue;
                    bestSq = sq; bestId = c.Id.Value; pick = c.Id; pickPos = c.Position;
                }

                if (pick.IsNone) break;                 // 후보 소진 — 원래 가던 길
                if (!canRoute) return pick;             // 기하 생략(무제한)

                _rejected[rejectCount++] = pick;        // 탐침한 것은 무조건 소진 처리

                d.Chase = d.Chase ?? _pool.Rent();
                bool ok = BuildChase(self, _map.CellOf(pickPos), myTileRange, d.Chase, self.Position);
                lastBuilt = pick;
                int cost = ok ? d.Chase.Dist[Index(self.Position)] : -1;
                if (ok && cost <= maxDetourCost && cost < bestCost)
                { bestCost = cost; bestReach = pick; bestReachCell = _map.CellOf(pickPos); }
            }

            if (bestReach.IsNone) return SimEntityId.None;
            // 이긴 후보가 마지막으로 구운 것이 아니면 다시 굽는다(뒤 탐침이 덮어썼다).
            if (lastBuilt != bestReach)
                BuildChase(self, bestReachCell, myTileRange, d.Chase, self.Position);
            return bestReach;
        }

        // ── ⑤ 이동 ────────────────────────────────────────────────────────────
        private void StepMovement(TickContext ctx)
        {
            var units = ctx.World.Units;
            float dt = ctx.Dt;

            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                var mv = u.Move;
                if (mv == null || u.Dead) continue;
                if (mv.PastGoal) continue;   // 골 도달은 1회 고정 — 이동 루프에서 빠진다

                byte layers = LayersOf(u);
                var nav = _map.Nav.For(layers, _map.Obstacles);

                // 기본값은 「정지」. 자기주도 변위를 **실제로 적용하는 지점에서만** 내린다.
                // 케이스를 열거하지 않으므로 새 이탈 경로가 생겨도 자동으로 정지에 편입된다.
                mv.HoldingGround = true;

                // 순간이동 — **위치는 이동이 소유한다.** 요청자(전투·스킬)는 좌표를 대입하지 않는다.
                if (mv.HasBlink)
                {
                    float3 from = u.Position;
                    u.Position = mv.BlinkTo;
                    mv.HasBlink = false;
                    ctx.Bus.Publish(CoreEvent.Blinked(ctx.Tick, u, from));
                }

                float3 current = u.Position;

                // **외력 합성 단일 지점.** 예전엔 이 3줄이 7곳에 복붙돼 있었고 각 복사본이 서로
                // 다른 힘 부분집합만 알았다 — 넉백이 나중에 추가되면서 교전·도발·순찰·고립
                // 상태의 적이 통째로 넉백 면역이 됐다.
                // unit 6a — 넉백은 **군중 제어 슬롯이 소유**하고 이동은 소비만 한다.
                // 슬롯은 초당 속도를 들고 있고 지속 동안 매 틱 이만큼을 민다.
                // `PendingImpulse` 는 슬롯을 안 쓰는 한 방짜리 외력의 자리로 남는다.
                float3 impulse = mv.PendingImpulse + u.Cc.ImpulseStep(dt);
                mv.PendingImpulse = float3.zero;
                bool hasImpulse = math.lengthsq(impulse) > 1e-8f;

                // ── 상태 갈림 — 여기서 끝나는 상태는 포털·골·당김을 안 지난다 ──
                if (u.Ai.Enemy == AiState.Standoff)
                {
                    if (hasImpulse) u.Position = Compose(current, float3.zero, impulse, mv.Radius, in nav);
                    continue;
                }

                if (u.Ai.Enemy == AiState.Chasing)
                {
                    StepChasing(ctx, u, mv, in nav, impulse, hasImpulse, dt);
                    continue;
                }

                // ── 포털 ── 입구 반경에 들어오면 출구로. 다음 틱 흐름장이 방향을 준다.
                var fields = ctx.World.Fields;
                for (int p = 0; p < fields.Count; p++)
                {
                    var f = fields[p];
                    if (f.Kind != FieldKind.Portal) continue;
                    float pdx = current.x - f.Center.x, pdz = current.z - f.Center.z;
                    if (pdx * pdx + pdz * pdz > f.Range * f.Range) continue;
                    u.Position = new float3(f.Exit.x, current.y, f.Exit.z);
                    current = u.Position;
                    break;
                }

                // ── 칸·골 판정 ──
                int2 cell = _map.CellOf(current);
                int idx = GridMath.CellIndex(cell, _map.GridSize);

                bool detectedHunting = u.Detection != null && u.Detection.Hunting;
                bool unlimited = u.Detection != null && u.Detection.Unlimited;
                bool huntShared = detectedHunting && unlimited && _map.Hunt.HasSources && _map.Hunt.Reaches(cell);
                bool huntTargeted = detectedHunting && !unlimited && u.Detection.Chase != null
                                    && u.Detection.Chase.Dist[idx] != int.MaxValue;
                bool hunting = huntShared || huntTargeted;

                // ⚠⚠ **유출 면제는 «무제한 감지 전용»이다.** `Hunting` 에 묶지 않는다 —
                // 그 값은 감지 타이머에 따라 매 틱 꺼질 수 있는데, 그러면 무제한 사냥꾼이 그
                // 틈에 골을 유출한다. 유한 감지에 상속시키면 감지가 **이 게임의 유일한 패배
                // 통로**의 조절기가 된다.
                bool leakProof = unlimited && _map.Hunt.HasSources && _map.Hunt.Reaches(cell);

                // ⚠ 순찰 소환물은 골 판정을 갈아탄다 — 박스 안에 골 칸이 들어와도 붙지 않는다.
                if (!leakProof && u.Patrol == null && _map.Snapshot.IsGoalCell(cell))
                {
                    mv.PastGoal = true;
                    var def0 = EnemyDefOf(ctx, u);
                    bool canSiege = (TargetDefaults.ResolveEnemy(def0.TargetFactions)
                                     & (int)Faction.DefenderCore) != 0;
                    ctx.Bus.Publish(CoreEvent.GoalReached(ctx.Tick, u, canSiege));
                    continue;
                }

                // ── 당김 ── 이동을 **대체하지 않는** 가산 변위. 벽·장애물에 막힌다.
                float3 pull = float3.zero;
                for (int p = 0; p < fields.Count; p++)
                {
                    var f = fields[p];
                    if (f.Kind != FieldKind.Pull) continue;
                    float inv = _map.TileSize > 1e-6f ? 1f / _map.TileSize : 1f;
                    // 판정은 **원 + 피해자 몸**(제약 13 「자리에 떨어지는 것」 — 회오리는 좌표에 선다).
                    if (!Wassup.Skills.SkillMath.ReachFromCell(
                            (current.x - f.Center.x) * inv, (current.z - f.Center.z) * inv,
                            f.Range, u.HitRadius)) continue;
                    float3 toCenter = f.Center - current;
                    toCenter.y = 0f;
                    float centerDist = math.length(toCenter);
                    float pullStep = f.Speed * dt;
                    pull = (centerDist <= pullStep || centerDist < 1e-4f)
                        ? toCenter
                        : math.normalize(toCenter) * pullStep;
                    break;
                }
                bool hasPull = math.lengthsq(pull) > 1e-8f;

                // ── 교전 정책 ── 멈춰 있어도 외력은 받는다.
                if (u.Ai.Enemy == AiState.Engaging)
                {
                    bool advance = mv.Engage == EngageMovement.Advance
                                   || (mv.Engage == EngageMovement.Pulse && !u.MovementLocked);
                    if (u.MovementLocked || !advance)
                    {
                        if (hasPull || hasImpulse)
                            u.Position = Compose(current, float3.zero, pull + impulse, mv.Radius, in nav);
                        continue;
                    }
                }

                // ── 스텝 소스 ── 어그로 > 감지 > 웨이포인트 > 거점 > 골
                float2 dir;
                float2[] routeFlow;
                int[] routeDist;

                if (u.Patrol != null)
                {
                    dir = mv.PatrolStep;
                    if (math.lengthsq(dir) < 1e-6f)
                    {
                        if (hasPull || hasImpulse)
                            u.Position = Compose(current, float3.zero, pull + impulse, mv.Radius, in nav);
                        continue;
                    }
                    routeFlow = null;
                    routeDist = null;
                }
                else
                {
                    var goalSlot = _map.Flow.GoalSlot(layers);
                    routeFlow = goalSlot.Flow;
                    routeDist = goalSlot.Dist;

                    if (huntShared) { routeFlow = _map.Hunt.Flow; routeDist = _map.Hunt.Dist; }
                    else if (huntTargeted) { routeFlow = u.Detection.Chase.Flow; routeDist = u.Detection.Chase.Dist; }
                    else if (mv.PathIndex >= 0)
                    {
                        int count = _map.Snapshot.WaypointCountAt(mv.PathIndex);
                        if (mv.WaypointIndex < count)
                        {
                            int2 wp = _map.Snapshot.WaypointAt(mv.PathIndex, mv.WaypointIndex);
                            bool reachable = _map.Flow.Slot(wp, layers).Reaches(cell);
                            WaypointProgress.Step(cell, wp, reachable, mv.WaypointIndex, count,
                                                  out int nextIndex, out bool advanced, out bool done);
                            if (advanced) mv.WaypointIndex = nextIndex;
                            if (!done)
                            {
                                var next = _map.Flow.Slot(_map.Snapshot.WaypointAt(mv.PathIndex, nextIndex), layers);
                                routeFlow = next.Flow;
                                routeDist = next.Dist;
                            }
                        }
                    }
                    else if (mv.HasStructureDest)
                    {
                        var slot = _map.Flow.Slot(mv.StructureDest, layers);
                        if (slot.Dist[idx] != int.MaxValue) { routeFlow = slot.Flow; routeDist = slot.Dist; }
                    }

                    dir = routeFlow[idx];
                    if (math.lengthsq(dir) < 1e-6f)
                    {
                        // 방향 없는 칸 — 외력에 밀려 도달 불가 칸에 떨어졌을 수 있다.
                        float2 recov = FlowRecovery.RecoveryDir(cell, routeDist, _map.GridSize);
                        if (math.lengthsq(recov) < 1e-6f)
                        {
                            if (hunting && TryHuntCloseIn(ctx, u, mv, in nav, routeDist, idx,
                                                          pull + impulse, dt))
                                continue;
                            if (hasPull || hasImpulse)
                                u.Position = Compose(current, float3.zero, pull + impulse, mv.Radius, in nav);
                            continue;
                        }
                        dir = recov;
                    }
                    else
                    {
                        // ── 평활화 ── 필드를 **대체하지 않는다**. 후보를 필드가 만들므로
                        // 오목 지형에서도 갇히지 않는다.
                        if (PathSmoothing.TryStepTarget(current, in nav, routeFlow, mv.Radius,
                                                        PathSmoothing.DefaultLookahead, out float3 aim))
                        {
                            float2 toAim = new float2(aim.x - current.x, aim.z - current.z);
                            if (math.lengthsq(toAim) > 1e-6f) dir = toAim;
                        }
                    }
                }

                float2 stepDir = math.normalizesafe(dir);
                float3 self = u.MovementLocked
                    ? float3.zero
                    : new float3(stepDir.x, 0f, stepDir.y) * SpeedOf(u, mv) * dt;

                if (math.lengthsq(self) > 1e-12f)
                {
                    mv.HoldingGround = false;
                    mv.LastMoveDir = math.normalize(self.xz);
                }

                u.Position = Compose(current, self, impulse + pull, mv.Radius, in nav);
            }
        }

        // 추격 레인 — 추격판 하강 + 「도착했는데 못 쏜다」 보정(M4·M5).
        private void StepChasing(TickContext ctx, Unit u, MoveState mv, in NavGrid nav,
                                 float3 impulse, bool hasImpulse, float dt)
        {
            float3 current = u.Position;
            if (u.MovementLocked)
            {
                // 잠/스턴: 자기주도 이동만 정지. 외력은 그대로 받는다.
                if (hasImpulse) u.Position = Compose(current, float3.zero, impulse, mv.Radius, in nav);
                return;
            }

            var chase = u.Aggro?.Chase;
            if (chase == null)
            {
                if (hasImpulse) u.Position = Compose(current, float3.zero, impulse, mv.Radius, in nav);
                return;
            }

            int2 cell = _map.CellOf(current);
            int idx = GridMath.CellIndex(cell, _map.GridSize);
            float2 dir = FlowRecovery.RecoveryDir(cell, chase.Dist, _map.GridSize);

            if (math.lengthsq(dir) > 1e-6f)
            {
                float3 step = new float3(dir.x, 0f, dir.y) * SpeedOf(u, mv) * dt;
                u.Position = Compose(current, step, impulse, mv.Radius, in nav);
                mv.HoldingGround = false;
                mv.LastMoveDir = math.normalize(dir);
                return;
            }

            // dir zero 는 둘이다: **사격 칸 도착**(dist 0)과 **고립**(더 나은 이웃 없음).
            // 보정 대상은 앞의 것 하나뿐 — 고립은 원래 멈췄다.
            bool arrived = chase.Dist[idx] == 0;
            var guardian = ctx.World.Find(u.Aggro.Target);
            if (arrived && guardian != null)
            {
                float2 taken = TryCloseIn(current, guardian.Position, SpeedOf(u, mv) * dt,
                                          chase.Dist, mv.Radius, in nav, impulse, out float3 next);
                if (math.lengthsq(taken) > 1e-6f)
                {
                    u.Position = next;
                    mv.HoldingGround = false;
                    mv.LastMoveDir = math.normalize(taken);
                    return;
                }
            }

            if (hasImpulse) u.Position = Compose(current, float3.zero, impulse, mv.Radius, in nav);
        }

        // 사냥 레인의 같은 동결. 어그로 레인과 결함도 처방도 같다.
        //
        // ⚠⚠ **`!Locked` 가 여기 있어야 한다.** 이 분기는 역사적으로 자기주도 이동이 0 이라
        // 잠금 게이트보다 앞에 있어도 안전했다. 보정이 **자기 이동을 넣었으므로** 게이트도 같이
        // 와야 한다 — 없으면 자장가·동상에 걸린 헌터가 계속 걷는다.
        // ⚠ 대상 지향 사냥이면 **그 대상**으로 붙는다. 공용 사냥판(무제한)은 도착지가 특정되지
        // 않으니 「소스 칸을 만든 것이 그중 하나」로 **추정**할 수밖에 없다(M20 의 비대칭).
        private bool TryHuntCloseIn(TickContext ctx, Unit u, MoveState mv, in NavGrid nav,
                                    int[] routeDist, int idx, float3 external, float dt)
        {
            if (u.MovementLocked || u.Ai.Enemy != AiState.Marching) return false;
            if (routeDist[idx] != 0) return false;

            float3 targetPos;
            bool hasTarget = false;

            if (u.Detection != null && !u.Detection.Unlimited && !u.Detection.Target.IsNone)
            {
                var t = ctx.World.Find(u.Detection.Target);
                if (t != null) { targetPos = t.Position; hasTarget = true; }
                else targetPos = default;
            }
            else
            {
                // 최근접 방어유닛 — 소스 칸을 만든 것이 그중 하나다(추정).
                float bestSq = float.MaxValue;
                float3 best = default;
                var units = ctx.World.Units;
                for (int i = 0; i < units.Count; i++)
                {
                    var c = units[i];
                    if (c.Dead || c.Deploying) continue;
                    if (((int)c.Faction & (int)Faction.DefenderUnit) == 0) continue;
                    float dx = c.Position.x - u.Position.x, dz = c.Position.z - u.Position.z;
                    float sq = dx * dx + dz * dz;
                    if (sq < bestSq) { bestSq = sq; best = c.Position; hasTarget = true; }
                }
                targetPos = best;
            }

            // ⚠ 관성 중에는 대상이 비어 있다 — 그때 목록도 비면 붙을 자리가 없다.
            // 원점(0,0,0)으로 기어가지 않도록 자리를 요구한다.
            if (!hasTarget) return false;

            float2 taken = TryCloseIn(u.Position, targetPos, SpeedOf(u, mv) * dt, routeDist,
                                      mv.Radius, in nav, external, out float3 next);
            if (math.lengthsq(taken) <= 1e-6f) return false;

            u.Position = next;
            mv.HoldingGround = false;
            mv.LastMoveDir = math.normalize(taken);
            return true;
        }

        // **어그로 레인과 사냥 레인이 이 하나를 공유한다** — 두 벌이면 조용히 갈리고, 그게 이
        // 결함의 원인(한 루프에 자가 셋)과 같은 클래스다.
        //
        // ⚠ **잠금 판정은 호출부 소유다.** 이 함수는 기계장치만 공유하고 게이트는 공유하지
        // 않는다 — 두 호출부가 **각자** `!Locked` 를 건다(M5). 한쪽만 걸었다가 잠긴 헌터가
        // 걷는 결함이 실제로 났다.
        //
        // **fail-closed**: 거리장이 없으면 스텝을 아예 안 취한다. **소스 영역(dist 0) 이탈
        // 스텝도 안 취한다** — 벗어나면 다음 틱 하강이 되돌려 왕복이 된다.
        // 단 **외력은 이 불변식 밖**이다(넉백은 원래 소스 안팎을 안 가린다).
        private float2 TryCloseIn(float3 current, float3 targetPos, float closeDist, int[] firingDist,
                                  float radius, in NavGrid nav, float3 external, out float3 next)
        {
            next = current;
            if (firingDist == null) return float2.zero;   // fail-closed
            float dx = targetPos.x - current.x;
            float dz = targetPos.z - current.z;
            if (dx * dx + dz * dz <= 1e-6f) return float2.zero;

            AggroChaseMath.CloseInCardinals(dx, dz, out var primary, out var secondary);
            for (int a = 0; a < 2; a++)
            {
                float2 axis = a == 0 ? primary : secondary;
                float3 step = new float3(axis.x, 0f, axis.y) * closeDist;
                // 막힘 판정은 **자기주도 변위만으로** 한다 — 외력을 섞으면 벽에 막힌 축도
                // 「움직였다」로 읽혀 폴백이 죽고, 가지 않은 방향이 진행 방향에 박힌다.
                float3 probe = Compose(current, step, float3.zero, radius, in nav);
                if (math.lengthsq(probe - current) < 1e-8f) continue;          // 막혔다
                int2 pCell = _map.CellOf(probe);
                if (firingDist[GridMath.CellIndex(pCell, _map.GridSize)] != 0) continue;   // 소스 이탈
                next = Compose(current, step, external, radius, in nav);
                return axis;
            }
            return float2.zero;
        }

        // ── ⑥ 분리 ────────────────────────────────────────────────────────────
        //
        // 이동 **뒤** 별도 패스다. 누적 먼저, 적용 나중(야코비 1회) — 먼저 적용하면 뒤 유닛이
        // 갱신된 위치를 보게 되어 순회 순서에 따라 결과가 갈린다.
        //
        // ⚠ 누적은 **`SimEntityId` 오름차순**이다(M27). 목록이 이미 오름차순이라 그냥 순회하면
        // 되지만, 그 사실에 기대는 것이 곧 계약이므로 여기 적어 둔다 — 목록 정렬이 깨지면
        // 결정론이 **조용히** 낮아진다.
        private void StepSeparation(TickContext ctx)
        {
            var units = ctx.World.Units;

            int n = 0;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Move == null || u.Dead || u.Move.PastGoal) continue;
                if (n >= _movers.Length)
                {
                    GrowUnits(ref _movers);
                    GrowVec(ref _push);
                    GrowVec(ref _forward);
                }
                _movers[n] = u;
                _push[n] = float2.zero;
                _forward[n] = u.Move.LastMoveDir;
                n++;
            }
            if (n < 2) return;

            for (int a = 0; a < n; a++)
            for (int b = 0; b < n; b++)
            {
                if (a == b) continue;
                float sum = _movers[a].Move.Radius + _movers[b].Move.Radius;
                _push[a] += Separation.PairPush(_movers[a].Position, _movers[b].Position,
                                                sum, Separation.DefaultStrength);
            }

            for (int a = 0; a < n; a++)
            {
                var u = _movers[a];
                float2 acc = _push[a];
                // 정지한 유닛은 밀어냄의 **전진 성분을 거부**한다 — 뒤 무리가 경로를 따라
                // 교전 중인 유닛을 4~9칸 밀어 나르는 것을 막는다.
                if (u.Move.HoldingGround) acc = Separation.RejectForwardPush(acc, _forward[a]);
                if (math.lengthsq(acc) < 1e-8f) continue;

                float3 moved = Separation.ApplyAccumulated(u.Position, acc, u.Move.Radius);
                var nav = _map.Nav.For(LayersOf(u), _map.Obstacles);
                u.Position = AgentCollision.Resolve(u.Position, moved, u.Move.Radius, in nav);
            }
        }

        // ── 공용 ──────────────────────────────────────────────────────────────

        // 변위 합성의 **단일 지점**. self = 자기주도 이동, external = 외력(넉백 + 당김).
        // clamp 는 터널링 차단, Resolve 는 벽 밀어넣기 차단이다.
        private float3 Compose(float3 current, float3 self, float3 external, float radius, in NavGrid nav)
        {
            float3 desired = current + self + external;
            desired = MovementCellTrim.ClampDisplacement(current, desired, _map.TileSize);
            return AgentCollision.Resolve(current, desired, radius, in nav);
        }

        // 대상 칸 주변 사격 칸을 소스로 추격판을 굽는다. 반환 = 그 자리에서 도달 가능한가.
        private bool BuildChase(Unit self, int2 targetCell, int tileRange, ChaseFieldCache cache, float3 selfPos)
        {
            byte layers = LayersOf(self);
            var nav = _map.Nav.For(layers, _map.Obstacles);
            int need = FlowFieldBuilder.DiscArea(math.max(1, tileRange));
            if (_chaseSources.Length < need) _chaseSources = new int2[need];

            int sources = AggroChaseMath.BuildChaseField(nav.StaticWalk, _map.GridSize, targetCell,
                                                          tileRange, _chaseSources, cache.Flow,
                                                          cache.Dist, _queue);
            cache.MarkBuilt(self.Id, _map.Obstacles.Signature);
            if (sources == 0) return false;
            return cache.Dist[Index(selfPos)] != int.MaxValue;
        }

        private int Index(float3 pos) => GridMath.CellIndex(_map.CellOf(pos), _map.GridSize);

        private static byte LayersOf(Unit u)
        {
            byte l = u.Move != null ? u.Move.TraversalLayers : (byte)0;
            return l == 0 ? TraversalSlots.DefaultMask : l;
        }

        private static EnemyDef EnemyDefOf(TickContext ctx, Unit u)
            => u.DefIndex >= 0 && u.DefIndex < ctx.Def.Enemies.Length
                ? ctx.Def.Enemies[u.DefIndex]
                : default;

        private void ClearDetection(Detection d)
        {
            d.Hunting = false;
            d.Target = SimEntityId.None;
            d.Grace = 0f;
            d.Stuck = 0f;   // 억제는 유지한다 — 상태와 무관하게 흐른다
            if (d.Chase != null) { _pool.Return(d.Chase); d.Chase = null; }
        }

        private static void GrowUnits(ref Unit[] buffer)
        {
            var next = new Unit[buffer.Length * 2];
            System.Array.Copy(buffer, next, buffer.Length);
            buffer = next;
        }

        private static void GrowVec(ref float2[] buffer)
        {
            var next = new float2[buffer.Length * 2];
            System.Array.Copy(buffer, next, buffer.Length);
            buffer = next;
        }

        /// <summary>
        /// 이 유닛의 **실효 이동 속도.** 저작 속도 × 이동 배율이고, 배율의 바닥(0.15)은
        /// `ModifierMath` 가 잡는다 — 그래서 감속으로 «완전 정지» 를 만들 수 없다
        /// (전면 정지가 필요해지면 배율이 아니라 전용 잠금이다).
        /// </summary>
        private static float SpeedOf(Unit u, MoveState mv)
            => mv.Speed * u.Modifiers.Effective.MoveSpeedMul;

    }
}
