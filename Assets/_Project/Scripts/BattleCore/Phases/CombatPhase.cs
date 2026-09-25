using System.Collections.Generic;
using Unity.Mathematics;
using Wassup.Skills;
using Wassup.BattleCore.Combat;
using Wassup.BattleCore.Combat.Emission;
using Wassup.BattleCore.Combat.Projectile;
using Wassup.BattleCore.Effects;
using Wassup.BattleCore.Map;
using Wassup.UnitAi;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 3 — **유닛이 때리고, 맞고, 죽는다.**
    //
    // 하위 단계의 순서가 계약이다(UML §4 · spec 변경 대상 표):
    //   ① 후보 스냅샷 → ② 공격 루프 → **[Attack seam]** → ③ 피해 → **[Death seam]**
    //   → ④ 후처리(군중 제어 부여·기상) → ⑤ 소멸 → **[Lifecycle seam]** → ⑥ 경계
    //   → **[Threshold seam]** → ⑦ 도약·순간이동 → ⑧ 실드 부여 스테이징
    //
    // ⚠ **③ 뒤에 ④ 가 오는 것이 C9 의 절반이다.** 「내가 때린 피해가 내가 건 잠을 깨우지
    // 않는다」는 옛 전투에서 **시스템 순서의 우연**이었다(피해 프레임 N, 수면 적용 N+1).
    // 한 틱 안에서 도는 새 코어에는 그 우연이 없으므로 가드를 **명시로** 세운다:
    //   ⒜ 군중 제어 부여는 피해 판정 **뒤** 단계다(이 순서).
    //   ⒝ 기상 요청은 **그 틱에 수면이 걸린 대상을 뺀다**(`FlushCc` 의 필터).
    // 둘 중 하나만으로는 부족하다.
    //
    // ⚠ **소멸은 「표시한 틱」에 하지 않는다**(구현 9). 사망 표시 틱 ≠ 소멸 틱이라야
    // 시체 폭발·사직서 드랍·순찰 연쇄가 자기 자리를 읽을 창이 생긴다(C18 이 나르던 순서).
    // 그래서 ⑤ 는 **이전 틱에 표시된** 것만 지운다.
    public sealed class CombatPhase : ITickPhase, ISeamHost
    {
        public string Name => "Combat";

        /// <summary>unit 7a — 이 단계가 여는 seam, **실행 순서대로**(`Run` 의 호출부와 같은 순서여야 한다).</summary>
        public void AppendSeams(List<Seam> into)
        {
            into.Add(Seam.Attack);
            into.Add(Seam.Death);
            into.Add(Seam.Lifecycle);
            into.Add(Seam.Threshold);
        }

        /// <summary>
        /// 이번 틱에 공격이 볼 수 있는 후보 하나. **전 진영 통합 풀**이고 진영 판정은
        /// 공격자마다 따로 한다 — 「미리 걸러져 있다」고 읽으면 아군 오사가 난다.
        /// </summary>
        private struct Candidate
        {
            public Unit U;
            public int SimId;
            public int Faction;
            public byte Layers;
            public int Class;
            public float3 Pos;
            public float Body;
            public int2 Cell;
        }

        private readonly MapRuntime _map;

        // 재사용 버퍼 — 틱 중 할당 0.
        private Candidate[] _cands = new Candidate[128];
        private int _candCount;

        private NearestTargeting.Candidate[] _near = new NearestTargeting.Candidate[128];
        private AggroCandidate[] _aggro = new AggroCandidate[128];
        private int[] _aggroIdx = new int[16];
        private int[] _aggroBack = new int[128];
        private Unit[] _hit = new Unit[16];
        private bool[] _picked = new bool[128];

        private readonly List<CcRequest> _pendingCc = new List<CcRequest>(16);
        private readonly List<WakeRequest> _pendingWake = new List<WakeRequest>(16);
        private readonly List<SimEntityId> _toDestroy = new List<SimEntityId>(16);

        // 발사 명세 스크래치.
        private float2[] _patternXZ = new float2[128];
        private int[] _patternIds = new int[128];
        private float[] _patternBody = new float[128];
        private int[] _patternScope = new int[128];

        // unit 7a — 이번 선정이 **진짜 최전방**이었나(폴백 최근접이 아니라). 최전방 배율의 자격이다.
        private bool _pickWasFrontmost;

        public CombatPhase(MapRuntime map) => _map = map;

        public void Run(TickContext ctx)
        {
            BuildCandidates(ctx);

            StepAttack(ctx);
            // 진행 중인 발사 명세는 공격 루프와 **별도로** 매 틱 전진한다 — 버스트가 틱을
            // 넘기 때문이다(한 트리거가 여러 틱에 걸쳐 탄을 뱉는다).
            StepEmitters(ctx, _map != null ? _map.TileSize : 1f);
            ctx.Seams.Run(Seam.Attack, ctx);

            StepDamage(ctx);
            ctx.Seams.Run(Seam.Death, ctx);

            FlushCc(ctx);

            StepDestroy(ctx);
            ctx.Seams.Run(Seam.Lifecycle, ctx);

            StepThreshold(ctx);
            ctx.Seams.Run(Seam.Threshold, ctx);

            StepLeap(ctx);

            StageShields(ctx);
        }

        // ── ① 후보 스냅샷 ────────────────────────────────────────────────────
        //
        // 틱 시작에 한 번 모은다. 제외 3종(`IsTargetable`)만 걸러지고 **진영은 안 거른다.**
        private void BuildCandidates(TickContext ctx)
        {
            var units = ctx.World.Units;
            _candCount = 0;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (!u.IsTargetable()) continue;
                if (_candCount >= _cands.Length) GrowCandidates();
                _cands[_candCount++] = new Candidate
                {
                    U = u,
                    SimId = u.Id.Value,
                    Faction = (int)u.Faction,
                    Layers = u.Move != null ? u.Move.TraversalLayers : (byte)0,
                    Class = ClassOf(ctx, u),
                    Pos = u.Position,
                    Body = u.HitRadius,
                    Cell = _map != null ? _map.CellOf(u.Position) : int2.zero,
                };
            }
        }

        private static int ClassOf(TickContext ctx, Unit u)
        {
            if (u.Kind != UnitKind.Defender) return -1;
            if (u.DefIndex < 0 || u.DefIndex >= ctx.Def.Units.Length) return -1;
            return ctx.Def.Units[u.DefIndex].Role;
        }

        // ── ② 공격 루프 ──────────────────────────────────────────────────────
        //
        // **방어유닛·적·도발받은 적·순찰병이 한 루프**다(census 「통합 공격자 루프」).
        // 진영별 분기는 마스크가, 아키타입 분기는 **정책 값**이 한다 — 타입을 묻지 않는다.
        private void StepAttack(TickContext ctx)
        {
            float tileSize = _map != null ? _map.TileSize : 1f;
            var units = ctx.World.Units;

            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                var atk = u.Attack;
                if (atk == null || atk.Unarmed) continue;
                // 배치 중·사망 대기는 공격자가 아니다(옛 쿼리 랭크 `WithNone` 의 후계).
                if (u.Deploying || u.Dead) continue;

                // ⒜ 쿨다운은 **CC 중에도 돈다** — 풀리는 즉시 때리는 근거다.
                //    이 줄을 잠금 판정 뒤로 옮기면 CC 와 규약이 갈린다(묶인 유닛의 쿨이 언다).
                if (atk.CooldownRemaining > 0f)
                    atk.CooldownRemaining = math.max(0f, atk.CooldownRemaining - ctx.Dt);

                // ⒝ 행동 잠금은 **START 만 막는다.** 이미 시작한 스윙은 완료된다.
                bool actionLocked = u.ActionLocked;
                var phase = UnitActionPhase.Resolve(actionLocked, atk.Swinging);
                bool cooldownReady = atk.CooldownRemaining <= 0f;

                // ⒞ 행동 상태 — **결정은 `Wassup.UnitAi`, 저장은 `Unit.Ai`**(구현 12).
                //    공격 루프와 이동이 **같은 술어**를 봐야 데드락이 안 난다.
                bool isDefenderAi = u.Kind == UnitKind.Defender;
                bool summonAlive = atk.Policy == AttackPolicy.Summon && HasLiveSummon(ctx, u.Id);
                if (isDefenderAi)
                {
                    var aiBefore = u.Ai.Defender;
                    u.Ai.Defender = DefenderAi.Resolve(new DefenderAiInput
                    {
                        deploying = u.Deploying,
                        actionLocked = actionLocked,
                        swinging = atk.Swinging,
                        policy = ToUnitAiPolicy(atk.Policy),
                        summonAlive = summonAlive,
                    });
                    // unit 8a2 — 전이 관측(옛 `TraceDefenderAiTransition` — 「변할 때만 한 줄」). 판정이 아니다.
                    if (u.Ai.Defender != aiBefore)
                        ctx.Bus.Publish(CoreEvent.DefenderAiChanged(ctx.Tick, u, aiBefore, u.Ai.Defender));
                }

                bool canStart = isDefenderAi
                    ? DefenderAi.CanStartAttack(u.Ai.Defender, cooldownReady)
                    : UnitActionPhase.CanStartAction(phase) && cooldownReady;

                // ⒟ 정책 — **대상을 고르기 전에** 처리하고 루프를 빠져나간다(C3).
                //    뒤에 두면 소환사의 근접 사거리 안에 적이 들어와야 소환돼 순찰병이
                //    마중 나갈 시간이 없어진다.
                if (atk.Policy == AttackPolicy.Bomb) { StepBomb(ctx, u, atk, canStart, tileSize); continue; }
                if (atk.Policy == AttackPolicy.Summon) { StepSummon(ctx, u, atk, canStart, summonAlive); continue; }

                // ⒠ 대상 선정 → START / RESOLVE
                StepTargetPolicy(ctx, u, atk, actionLocked, canStart, tileSize);
            }
        }

        private static DefenderAttackPolicy ToUnitAiPolicy(AttackPolicy p)
        {
            // 명시 switch — 번호가 같다는 사실에 기대지 않는다(둘은 다른 어셈블리의 어휘다).
            switch (p)
            {
                case AttackPolicy.Bomb: return DefenderAttackPolicy.Bomb;
                case AttackPolicy.Summon: return DefenderAttackPolicy.Summon;
                default: return DefenderAttackPolicy.Target;
            }
        }

        // ── 폭탄맨 ───────────────────────────────────────────────────────────
        //
        // 사거리 안 **최근접 적이 선 칸**에 던진다. 착지 칸은 발사 시점 스냅샷이다 —
        // 적이 걸어 나가면 빗나간다(유도가 아니다).
        //
        // ⚠ **사각 자를 그대로 둔다**(C20 — 보류). 이름은 폴백인데 이 아키타입의 유일
        // 경로이고, 양쪽 몸이 0 이다. 원 자로 바꾸면 착지 칸이 조용히 달라진다 = 밸런스 변경.
        private void StepBomb(TickContext ctx, Unit u, AttackState atk, bool canStart, float tileSize)
        {
            if (!canStart) return;
            if (atk.Bomb.ProjectileDefIndex < 0)
            {
                // C4 — 규칙이 발동했는데 실행할 팔이 없다. 조용한 무동작 금지가 계약이다.
                ctx.Warn("[Combat] 폭탄 정책인데 탄 정의가 없다 — 쿨은 소비되지 않고 대기한다.");
                return;
            }

            int pick = PickChebyshevNearest(u, atk);
            // **던진 프레임에만 쿨을 리셋한다.** 사거리에 적이 없으면 쿨을 만료 상태로
            // 대기시켜(C1) 적이 들어온 프레임에 즉시 던진다 — 여기서 리셋하면 최대 한 쿨 늦다.
            if (pick < 0) return;

            var target = _cands[pick];
            int2 landCell = target.Cell;
            float3 landWorld = _map != null ? _map.CenterOf(landCell) : target.Pos;

            var req = ProjectileRequest.Empty;
            req.DefIndex = atk.Bomb.ProjectileDefIndex;
            req.Movement = MovementKind.GrenadeToCell;
            req.Payload = PayloadKind.TileAoe;
            req.Owner = u.Id;
            req.OwnerFaction = u.Faction;
            req.TargetMask = atk.TargetMask;
            req.TargetLayers = atk.TargetLayers;
            req.Origin = u.Position;
            req.Impact = landWorld;
            req.Damage = atk.Bomb.Damage;
            // **자리에 떨어지는 것** — 폭탄은 그 유닛에서 나오는 것이 아니라 그 유닛이
            // 지정한 좌표에 떨어진다. 0 을 남기는 것이 그 형의 표현이다(제약 13).
            req.OriginBodyRadius = 0f;
            req.ImpactTileRange = atk.Bomb.AoeTileRange;
            req.AoeTargetCap = atk.Bomb.AoeTargetCap;
            req.FlightTime = atk.Bomb.TravelSeconds;
            req.FuseSeconds = atk.Bomb.FuseSeconds;
            ctx.World.ProjectileRequests.Add(req);

            ctx.Bus.Publish(CoreEvent.AttackResolved(ctx.Tick, u, SimEntityId.None,
                                                     landWorld, 0f, 1, atk.Period(IntervalMul(u))));
            atk.FireCount++;
            atk.CooldownRemaining = atk.Interval;
            // unit 7a — 폭탄이 **실제로 손을 떠난** 틱만 이 유닛의 공격 사건이다(off-grid 로 쿨만 도는 틱은 0).
            ctx.Triggers?.RaiseAttack(u, target.U, landWorld);
        }

        private int PickChebyshevNearest(Unit u, AttackState atk)
        {
            int tileRange = Wassup.Skills.SkillMath.RangeToTiles(atk.Range);
            int2 selfCell = _map != null ? _map.CellOf(u.Position) : int2.zero;
            int n = 0;
            for (int i = 0; i < _candCount; i++)
            {
                var c = _cands[i];
                bool eligible = c.U != u && Legal(in c, atk);
                if (n >= _near.Length) System.Array.Resize(ref _near, _near.Length * 2);
                _near[n++] = new NearestTargeting.Candidate
                {
                    Eligible = eligible,
                    TileDist = TileAoe.TileDistance(c.Cell, selfCell),
                    SqDist = SqXZ(u.Position, c.Pos),
                    SimId = c.SimId,
                };
            }
            return NearestTargeting.SelectNearest(_near, n, tileRange);
        }

        // ── 소환사 ───────────────────────────────────────────────────────────
        //
        // **소환물이 살아 있어도 쿨은 돈다**(C2) — 스폰만 건너뛴다. 안 돌리면 소환물이
        // 죽는 즉시 재소환이 되어 동작이 바뀐다. 그래서 유지 상태도 공격 START 를 «시도»한다.
        //
        // 초회 게이트: 첫 순찰병은 **담당 구역 안에 적이 있을 때만** 낸다. 게이트가 닫혀
        // 있으면 쿨을 리셋하지 않고 만료 상태로 대기한다(폭탄맨과 같은 규율).
        private void StepSummon(TickContext ctx, Unit u, AttackState atk, bool canStart, bool summonAlive)
        {
            if (!canStart) return;

            int patrolDef = atk.Summon.PatrolDefIndex;
            if (patrolDef < 0 || patrolDef >= ctx.Def.Units.Length)
            {
                ctx.Warn("[Combat] 소환 정책인데 순찰 정의가 없다 — 쿨은 소비되지 않고 대기한다.");
                return;
            }

            int coverTiles = math.max(1, Wassup.Skills.SkillMath.RangeToTiles(atk.Range));
            int2 anchor = _map != null ? _map.CellOf(u.Position) : int2.zero;

            bool gateOpen = atk.HasSummonedOnce;
            if (!gateOpen && !summonAlive)
            {
                for (int i = 0; i < _candCount && !gateOpen; i++)
                {
                    var c = _cands[i];
                    if ((c.Faction & (int)Faction.EnemyUnit) == 0) continue;
                    if (!LayerBits.CanTarget(atk.TargetLayers, c.Layers)) continue;
                    // 골에 붙어 타워를 때리는 적도 순찰을 부를 이유다(C14 와 같은 근거).
                    if (Wassup.BattleCore.Move.PatrolAreaMath.IsInArea(c.Cell, anchor, coverTiles)) gateOpen = true;
                }
            }

            if (gateOpen && !summonAlive)
            {
                var patrol = SpawnPatrol(ctx, patrolDef, anchor, coverTiles, u.Id,
                                         _map != null ? _map.CenterOf(anchor) : u.Position);
                atk.HasSummonedOnce = true;

                ctx.Bus.Publish(CoreEvent.AttackResolved(ctx.Tick, u, patrol.Id,
                                                         patrol.Position, patrol.HitRadius, 1,
                                                         atk.Period(IntervalMul(u))));
            }

            if (gateOpen) atk.CooldownRemaining = atk.Interval;
        }

        /// <summary>
        /// 순찰 소환물 하나를 세우는 **단 하나의 조립 자리**. 소환사(공격 루프)와 디버그 커맨드(tools 10 — `DebugSummonPatrol`)가
        /// 같은 문을 지난다 — 두 벌이면 「어떤 경로로 태어났나」가 구역·이동·공격 규칙을 바꾼다.
        /// `owner` = 소환사(없으면 None — 소환사 연쇄 소멸이 없다). 이동·앵커 수학은 unit 2 의 `PatrolAreaMath` 가 돈다.
        /// </summary>
        internal static Unit SpawnPatrol(TickContext ctx, int patrolDef, int2 anchor, int radius,
                                         SimEntityId owner, float3 position)
        {
            ref var pd = ref ctx.Def.Units[patrolDef];
            var patrol = ctx.World.Spawn(UnitKind.Patrol, Faction.DefenderUnit, patrolDef, position,
                                         pd.BodyRadiusTiles, pd.Health, deploying: false, tick: ctx.Tick);
            var parts = ctx.World.Parts;
            var box = parts.RentPatrol();
            box.Anchor = anchor;
            box.Home = anchor;
            box.Radius = math.max(1, radius);
            box.SummonedBy = owner;
            patrol.Patrol = box;

            var move = parts.RentMove();
            move.Speed = pd.MoveSpeed;
            move.TraversalLayers = (byte)pd.TraversalLayers;
            patrol.Move = move;

            patrol.Attack = BuildAttackState(in pd, ctx.Def, parts);
            return patrol;
        }

        private static bool HasLiveSummon(TickContext ctx, SimEntityId owner)
        {
            var units = ctx.World.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Patrol != null && !u.Dead && u.Patrol.SummonedBy == owner) return true;
            }
            return false;
        }

        // ── 타겟 정책(일반 공격) ──────────────────────────────────────────────
        private void StepTargetPolicy(TickContext ctx, Unit u, AttackState atk,
                                      bool actionLocked, bool canStart, float tileSize)
        {
            SimEntityId best = SimEntityId.None;
            float3 bestPos = default;
            float bestBody = 0f;

            PickTarget(ctx, u, atk, actionLocked, tileSize, ref best, ref bestPos, ref bestBody);

            // START / RESOLVE — 선딜이 있으면 두 박자, 없으면 같은 틱에 둘 다.
            bool doResolve = false;
            if (atk.HitDelayRemaining > 0f)
            {
                float rem = atk.HitDelayRemaining - ctx.Dt;
                atk.HitDelayRemaining = math.max(0f, rem);
                if (rem <= 0f) doResolve = true;   // 선딜 만료 → 이번 틱 타격
                // 선딜 중에는 새 START 가 없다
            }
            else if (canStart && !best.IsNone && StateAllowsFire(u))
            {
                // ── START ── 커밋 + 쿨다운 + 선딜 시작. 타격은 RESOLVE 다.
                atk.CommittedTarget = best;
                if (IsDirectional(ctx, atk))
                {
                    float2 toTarget = (bestPos - u.Position).xz;
                    atk.CommittedDirection = math.lengthsq(toTarget) > 1e-6f
                        ? math.normalize(toTarget) : new float2(0f, 1f);
                    atk.HasCommittedDirection = true;
                }

                // 공속 배율은 **간격을 나눈다**(×2 = 절반 간격). 실주기는 `max(간격, 선딜)` 라
                // 선딜이 긴 유닛은 공속을 올려도 그 바닥에서 멈춘다(C19 — 현행 보류).
                atk.CooldownRemaining = atk.Interval * IntervalMul(u);
                // 충전 — 다음 공격 한 번을 즉시 더 쏜다. **각 발이 온전한 공격**이라
                // 군중 제어·로그가 발마다 한 번씩 난다(RESOLVE 안에서 복제하지 않는 이유).
                // unit 7a — 소비는 공격 수식자의 일(⑤)이다. 부여는 스킬(`GrantSelfCharge`)이 한다.
                if (AttackMod.ConsumeCharge(u)) atk.CooldownRemaining = 0f;

                // unit 7a — 최전방 배율 **스냅샷**(옛 `FrontmostAttackLock`) — 스윙 중 카드가 바뀌어도 이 공격은
                // START 의 값으로 끝난다. 폴백 최근접(최전방 후보 없음)은 배율을 안 받는다.
                atk.FrontmostTarget = _pickWasFrontmost ? best : SimEntityId.None;
                atk.FrontmostMulSnapshot = _pickWasFrontmost ? AttackMod.FrontmostMul(atk.Mods) : 1f;

                if (atk.HitDelay <= 0f) doResolve = true;
                else atk.HitDelayRemaining = atk.HitDelay;
            }

            if (!doResolve) return;

            // RESOLVE — 커밋한 대상을 다시 확인한다. **strict lapse**: 사라졌으면 빗나간다.
            // 유일한 예외가 방향탄이다 — 겨눌 임자가 없는 궤적이라 커밋 축으로 나간다.
            var committed = ctx.World.Find(atk.CommittedTarget);
            bool committedValid = committed != null && committed.IsTargetable()
                && TargetPersistence.KeepsLock(true, u.Position, committed.Position,
                                               atk.Range, tileSize, u.HitRadius, committed.HitRadius);
            bool directionalLapse = !committedValid && IsDirectional(ctx, atk) && atk.HasCommittedDirection;

            if (committedValid)
            {
                best = committed.Id;
                bestPos = committed.Position;
                bestBody = committed.HitRadius;
            }
            else if (directionalLapse)
            {
                best = SimEntityId.None;
                bestPos = u.Position + new float3(atk.CommittedDirection.x, 0f, atk.CommittedDirection.y)
                          * (atk.Range * tileSize);
                bestBody = 0f;
            }
            else
            {
                atk.ClearCommit();
                return;   // 빗나감 — START 모션만 나가고 아무 일도 안 일어난다
            }

            Resolve(ctx, u, atk, best, bestPos, bestBody, tileSize);
            atk.ClearCommit();
        }

        // 적은 `Engaging`·`Standoff` 에서만 쏜다. 방어유닛·순찰병은 상태 머신 대상이 아니다.
        private static bool StateAllowsFire(Unit u)
        {
            if (u.Kind != UnitKind.Enemy) return true;
            return u.Ai.Enemy == AiState.Engaging || u.Ai.Enemy == AiState.Standoff;
        }

        private static bool IsDirectional(TickContext ctx, AttackState atk)
        {
            if (atk.ProjectileDefIndex < 0 || atk.ProjectileDefIndex >= ctx.Def.Projectiles.Length) return false;
            var kind = (MovementKind)ctx.Def.Projectiles[atk.ProjectileDefIndex].Movement;
            return MovementBinding.Of(kind) == BindingClass.Direction;
        }

        // ── 타겟 선정 ────────────────────────────────────────────────────────
        //
        // 한 번의 순회로 최근접·우선 클래스·최전방·최저 체력을 전부 재고, 그 뒤에 덮어쓰기
        // 사슬을 탄다. 순서가 곧 우선순위다:
        //   최근접 → 힐러(최저 체력) → 우선 클래스 → 지속 락 → 어그로 → 최전방
        private void PickTarget(TickContext ctx, Unit u, AttackState atk, bool actionLocked,
                                float tileSize, ref SimEntityId best, ref float3 bestPos, ref float bestBody)
        {
            _pickWasFrontmost = false;
            float bestSq = float.MaxValue;
            int bestSimId = int.MaxValue;
            float prioSq = float.MaxValue;
            int prioSimId = int.MaxValue;
            SimEntityId prio = SimEntityId.None;
            float3 prioPos = default;
            float prioBody = 0f;

            // 힐러 = 아군을 겨누는 방어유닛. 최근접이 아니라 **가장 다친 아군**을 고른다.
            bool rankByHealth = u.Kind == UnitKind.Defender
                                && atk.TargetMask == (int)Faction.DefenderUnit;
            bool wantFrontmost = atk.WantsFrontmost;

            Unit healBest = null; LowestHealthTargeting.Candidate healBestC = default;
            Unit fmBest = null; FrontmostTargeting.Candidate fmBestC = default;
            bool lockStillCandidate = false;

            for (int i = 0; i < _candCount; i++)
            {
                var c = _cands[i];
                if ((c.Faction & atk.TargetMask) == 0) continue;
                if (!LayerBits.CanTarget(atk.TargetLayers, c.Layers)) continue;
                // 락이 **아직 합법 후보인가** — 여기서 표시하면 조회도 스캔도 늘지 않는다.
                if (!atk.Lock.IsNone && c.SimId == atk.Lock.Value) lockStillCandidate = true;
                if (c.U == u) continue;
                if (!ClassAllowed(atk, c.Class)) continue;
                if (!AttackReach.InReach(u.Position, c.Pos, atk.Range, tileSize, u.HitRadius, c.Body)) continue;

                float d2 = SqXZ(u.Position, c.Pos);

                // **거점 특별 취급 없음**(C15) — 마스크를 통과한 후보는 종류를 묻지 않고
                // 거리로만 경쟁한다. 2026-08-09 사용자 확정으로 예외가 제거됐다.
                if (d2 < bestSq || (d2 == bestSq && c.SimId < bestSimId))
                {
                    bestSq = d2; bestSimId = c.SimId;
                    best = c.U.Id; bestPos = c.Pos; bestBody = c.Body;
                }

                if (rankByHealth)
                {
                    var hc = new LowestHealthTargeting.Candidate
                    {
                        HpRatio = HealthMath.ComputeRatio(c.U.Health, c.U.MaxHealth),
                        SqDist = d2,
                        SimId = c.SimId,
                    };
                    if (healBest == null || LowestHealthTargeting.RanksBefore(in hc, in healBestC))
                    { healBest = c.U; healBestC = hc; }
                }

                if (atk.PriorityClass > 0 && c.Class == atk.PriorityClass
                    && (d2 < prioSq || (d2 == prioSq && c.SimId < prioSimId)))
                {
                    prioSq = d2; prioSimId = c.SimId;
                    prio = c.U.Id; prioPos = c.Pos; prioBody = c.Body;
                }

                if (wantFrontmost)
                {
                    int fdist = FlowDistanceOf(in c);
                    if (fdist == FrontmostTargeting.UnreachableDist) continue;
                    var fc = new FrontmostTargeting.Candidate
                    {
                        FlowDist = fdist, SqDist = d2, SimId = c.SimId,
                    };
                    if (fmBest == null || FrontmostTargeting.RanksBefore(in fc, in fmBestC))
                    { fmBest = c.U; fmBestC = fc; }
                }
            }

            // 힐러 — 같은 후보 집합에서 **순위만** 바꾼다. 최근접이 못 고르는 상황에서는
            // 이쪽도 못 고른다(없던 대상을 만들지 않는다).
            if (rankByHealth && healBest != null)
            {
                best = healBest.Id; bestPos = healBest.Position; bestBody = healBest.HitRadius;
            }

            // 우선 클래스 — 사거리 안에 있으면 최근접을 덮는다.
            if (!prio.IsNone) { best = prio; bestPos = prioPos; bestBody = prioBody; }

            // ── 지속 락 ──
            //
            // **제외 4종은 누락이 아니라 계약이다**(C13 의 짝):
            //   facing    — 은퇴했다(레인 witness 자체가 사라졌다). 살아 있는 제외는 아래 셋.
            //   frontmost — 「매 공격마다 지금의 최전방」이 그 카드의 계약이다.
            //   힐러      — 최저 체력 재랭킹이 정체성이다.
            //   가디언    — 어그로 자석이 「아직 안 물린 적 우선」으로 신규 팩을 흡수한다.
            //               주 대상을 고정하면 자석이 죽는다.
            bool wantsLock = atk.Mode != TargetMode.None
                             && !wantFrontmost && !rankByHealth && atk.AggroCapacity <= 0;
            if (wantsLock)
            {
                // C13 — **행동 불능 중에는 락을 비우고 재잠금도 건너뛴다.**
                // ⚠ `else` 로 감싸는 것이 핵심이다. 비우기만 하고 아래로 흘리면 해제 분기가
                // 그 틱의 최근접으로 **즉시 다시 잠근다**(초판이 그랬고 테스트가 잡았다).
                if (actionLocked)
                {
                    atk.Lock = SimEntityId.None;
                }
                else
                {
                    var cur = ctx.World.Find(atk.Lock);
                    bool keep = cur != null && lockStillCandidate && cur.IsTargetable()
                        && TargetPersistence.KeepsLock(true, u.Position, cur.Position,
                                                       atk.Range, tileSize, u.HitRadius, cur.HitRadius);
                    if (keep)
                    {
                        best = cur.Id; bestPos = cur.Position; bestBody = cur.HitRadius;
                    }
                    else
                    {
                        // 사망 ∨ 사거리 이탈 → 해제하고 이미 계산된 픽을 채택한다.
                        atk.Lock = best;
                    }
                }
            }

            // ── 어그로 sticky ──
            //
            // **배타적이다**(C11). 도발당한 적은 가디언만 본다 — 사거리 밖이면 쏘지 않고
            // 걸어간다. 「가디언 없으면 최근접」으로 풀면 가는 길에 만난 유닛과 싸우느라
            // 가디언에 영영 도착하지 않는다.
            if (u.Aggro != null && !u.Aggro.Target.IsNone && u.Kind == UnitKind.Enemy)
            {
                best = SimEntityId.None;
                var g = ctx.World.Find(u.Aggro.Target);
                if (g != null && g.IsTargetable()
                    && AttackReach.InReach(u.Position, g.Position, atk.Range, tileSize,
                                           u.HitRadius, g.HitRadius))
                {
                    best = g.Id; bestPos = g.Position; bestBody = g.HitRadius;
                }
            }

            // ── 최전방 ──
            //
            // 스윙 중 유지는 **커밋**이 한다(strict lapse 가 그쪽 계약과 같다) — 옛 전투가
            // 별도 락을 둔 이유는 카드 배율 스냅샷 때문이었고, 그 배율은 unit 7 의 것이다.
            if (wantFrontmost && fmBest != null)
            {
                best = fmBest.Id; bestPos = fmBest.Position; bestBody = fmBest.HitRadius;
                _pickWasFrontmost = true;
            }
        }

        private int FlowDistanceOf(in Candidate c)
        {
            if (_map == null || _map.Snapshot.CellCount == 0) return FrontmostTargeting.UnreachableDist;
            var slot = _map.Flow.GoalSlot(TraversalSlots.DefaultMask);
            if (!slot.Exists) return FrontmostTargeting.UnreachableDist;
            return slot.DistAt(c.Cell);
        }

        // ── RESOLVE ──────────────────────────────────────────────────────────
        private void Resolve(TickContext ctx, Unit u, AttackState atk,
                             SimEntityId primary, float3 primaryPos, float primaryBody, float tileSize)
        {
            atk.FireCount++;

            // unit 7a — 강공(①)은 **이 공격**을 센다 — 대표 대상이 있을 때만(빗나간 방향탄은 0). 카운터의 주인은
            // `AttackMod.HeavyStrike` 하나다.
            var primaryUnitAtFire = ctx.World.Find(primary);
            float heavyMul = AttackMod.HeavyStrike(atk.Mods, primaryUnitAtFire);

            // 탄이 있으면 요청을 내고 끝난다 — 피해는 착탄이 정한다.
            if (atk.ProjectileDefIndex >= 0)
            {
                // 발사 명세 유닛은 **패턴이 단발을 대체한다**(옛 `AttackSystem` 의 `pushedPattern`
                // 게이트). 둘 다 쏘면 머신거너 한 공격이 단발 1 + 연발 10 = 11발이 된다
                // (2026-09-24 드리프트 감사 H2).
                if (atk.PatternSlots.Count == 0)
                    EmitProjectile(ctx, u, atk, primary, primaryPos, tileSize, heavyMul);
                var aim = AimDirection(u, atk, primaryPos);
                ctx.Bus.Publish(CoreEvent.AttackResolved(ctx.Tick, u, primary, primaryPos,
                                                         primaryBody, 1, atk.Period(IntervalMul(u)),
                                                         aim, atk.Shape, atk.Range));
                FirePatterns(ctx, u, atk, ShotDamage(ctx, u, atk, primary) * heavyMul, aim);
                // unit 7a — 공격 사건(`AttackN`). 대표 대상은 **지금 손에 든 값**이다(S18).
                if (primaryUnitAtFire != null) ctx.Triggers?.RaiseAttack(u, primaryUnitAtFire, primaryPos);
                atk.FrontmostTarget = SimEntityId.None;
                return;
            }

            // 근접 — 즉시 해결. 주 대상 + 부가 타격(도형 AND).
            // 도형의 축 = 부가 타격을 고른 **그 방향**(`SelectHits` 가 같은 식으로 세운다). 사건이 그 값을
            // 그대로 나르므로 참격 자국이 판정과 다른 방향을 가리킬 수 없다(제약 13 — 뷰는 다시 재지 않는다).
            var shapeAxis = math.normalizesafe(new float2(primaryPos.x - u.Position.x, primaryPos.z - u.Position.z));
            int hitCount = SelectHits(ctx, u, atk, primary, primaryPos, tileSize);

            // 가디언 대표 — **실제로 때린 적**을 주 대상으로 세운다(C6). 넉백·로그가
            // 주 대상과 그 좌표를 읽으므로 불일치를 여기서 없앤다.
            if (hitCount > 0 && _hit[0] != null)
            {
                primary = _hit[0].Id;
                primaryPos = _hit[0].Position;
                primaryBody = _hit[0].HitRadius;
            }

            for (int k = 0; k < hitCount; k++)
                ApplyOutputs(ctx, u, atk, _hit[k], heavyMul * ModMulFor(atk, _hit[k]));

            // 히트 구동 어그로 — Combat 은 「때렸다」 사실만 전달하고, 수용량·선점 게이트는
            // 받는 쪽(`AiMovePhase.GrantAggro`)이 갖는다.
            if (atk.AggroCapacity > 0)
                for (int k = 0; k < hitCount; k++)
                    ctx.World.AggroRequests.Add(AggroRequest.Hit(_hit[k].Id, u.Id));

            // 넉업 — 때린 **전원**에게 짧은 기절. 넉백·수면(주 대상 1체)과 스코프가 다르다.
            if (atk.Cc.KnockupSeconds > 0f)
                for (int k = 0; k < hitCount; k++) RequestKnockup(ctx, u, atk, _hit[k]);

            // 넉백·수면 — **주 대상 1체.**
            var primaryUnit = hitCount > 0 ? _hit[0] : null;
            if (primaryUnit != null)
            {
                if (atk.Cc.KnockbackDistance > 0f && atk.Cc.KnockbackDuration > 0f)
                    RequestKnockback(ctx, u, atk, primaryUnit);
                if (atk.Cc.SleepSeconds > 0f) RequestSleep(ctx, u, atk, primaryUnit);
            }

            ctx.Bus.Publish(CoreEvent.AttackResolved(ctx.Tick, u, primary, primaryPos,
                                                     primaryBody, hitCount, atk.Period(IntervalMul(u)),
                                                     shapeAxis, atk.Shape, atk.Range));
            FirePatterns(ctx, u, atk, ShotDamage(ctx, u, atk, primary) * heavyMul, AimDirection(u, atk, primaryPos));
            if (primaryUnit != null) ctx.Triggers?.RaiseAttack(u, primaryUnit, primaryPos);
            atk.FrontmostTarget = SimEntityId.None;
        }

        // unit 7a — 피해자별 공격 수식자(③ 최전방 — START 스냅샷의 그 대상만 · ④ 수면 — 잠든 대상만).
        private static float ModMulFor(AttackState atk, Unit victim)
        {
            if (atk.Mods.Count == 0 || victim == null) return 1f;
            float m = AttackMod.SleepMulFor(atk.Mods, victim);
            if (!atk.FrontmostTarget.IsNone && victim.Id == atk.FrontmostTarget) m *= atk.FrontmostMulSnapshot;
            return m;
        }

        // 주 대상 + 부가 타격. **획득은 원, 부가 타격만 도형**이다 — 도형은 넓히지 못한다.
        private int SelectHits(TickContext ctx, Unit u, AttackState atk,
                               SimEntityId primary, float3 primaryPos, float tileSize)
        {
            int desired = math.max(1, atk.TargetCount);
            if (desired > _hit.Length) System.Array.Resize(ref _hit, desired);
            int count = 0;

            // 가디언은 **자석 규칙**으로 고른다(아직 안 물린 적 우선 → 상한이 차면 정리).
            if (atk.AggroCapacity > 0)
            {
                int n = 0;
                for (int i = 0; i < _candCount; i++)
                {
                    var c = _cands[i];
                    if (c.U == u) continue;
                    if ((c.Faction & atk.TargetMask) == 0) continue;
                    if (!LayerBits.CanTarget(atk.TargetLayers, c.Layers)) continue;
                    if (n >= _aggro.Length)
                    {
                        System.Array.Resize(ref _aggro, _aggro.Length * 2);
                        System.Array.Resize(ref _aggroBack, _aggroBack.Length * 2);
                    }
                    _aggro[n] = new AggroCandidate
                    {
                        Pos = c.Pos,
                        BodyRadius = c.Body,
                        Aggroed = c.U.Aggro != null && !c.U.Aggro.Target.IsNone,
                    };
                    _aggroBack[n] = i;
                    n++;
                }
                if (desired > _aggroIdx.Length) System.Array.Resize(ref _aggroIdx, desired);
                int sel = AggroTargeting.SelectTargets(u.Position, atk.Range, tileSize, u.HitRadius,
                                                       u.Aggro != null ? u.Aggro.Held : 0,
                                                       atk.AggroCapacity, in atk.Shape,
                                                       _aggro, n, _aggroIdx, desired);
                for (int s = 0; s < sel; s++) _hit[count++] = _cands[_aggroBack[_aggroIdx[s]]].U;
                return count;
            }

            var primaryUnit = ctx.World.Find(primary);
            if (primaryUnit == null) return 0;
            _hit[count++] = primaryUnit;
            if (desired <= 1) return count;

            // 부가 타격 — **주 대상을 향한 실제 방향**을 축으로 세운 도형 안에서만.
            var dir = new float2(primaryPos.x - u.Position.x, primaryPos.z - u.Position.z);
            if (_candCount > _picked.Length) System.Array.Resize(ref _picked, _candCount);
            for (int i = 0; i < _candCount; i++) _picked[i] = _cands[i].U == primaryUnit;

            // 힐러의 부가 대상도 **가장 다친 순**이다 — 주 대상과 같은 자(`PickTarget` 의
            // `rankByHealth`). 옛 pass 루프가 `rankByHealth` 로 같은 분기를 탔다.
            bool rankByHealth = u.Kind == UnitKind.Defender
                                && atk.TargetMask == (int)Faction.DefenderUnit;

            while (count < desired)
            {
                int pickIdx = -1;
                float pickSq = float.MaxValue;
                int pickSimId = int.MaxValue;
                LowestHealthTargeting.Candidate healPick = default;
                for (int i = 0; i < _candCount; i++)
                {
                    if (_picked[i]) continue;
                    var c = _cands[i];
                    if (c.U == u) continue;
                    if ((c.Faction & atk.TargetMask) == 0) continue;
                    if (!LayerBits.CanTarget(atk.TargetLayers, c.Layers)) continue;
                    // 직업 필터는 **여기서 안 거른다** — 필터는 주 대상 획득(`PickTarget`)만의
                    // 규칙이다. 옛 pass 루프도 진영·층·자기·도형만 봤다(`AttackSystem.cs:1525~1537`).
                    if (!AttackReach.InReachShaped(u.Position, c.Pos, atk.Range, tileSize,
                                                   u.HitRadius, c.Body, in atk.Shape, dir)) continue;
                    float d2 = SqXZ(u.Position, c.Pos);
                    if (rankByHealth)
                    {
                        var hc = new LowestHealthTargeting.Candidate
                        {
                            HpRatio = HealthMath.ComputeRatio(c.U.Health, c.U.MaxHealth),
                            SqDist = d2,
                            SimId = c.SimId,
                        };
                        if (pickIdx < 0 || LowestHealthTargeting.RanksBefore(in hc, in healPick))
                        { healPick = hc; pickIdx = i; }
                    }
                    else if (d2 < pickSq || (d2 == pickSq && c.SimId < pickSimId))
                    { pickSq = d2; pickSimId = c.SimId; pickIdx = i; }
                }
                if (pickIdx < 0) break;
                _picked[pickIdx] = true;
                _hit[count++] = _cands[pickIdx].U;
            }
            return count;
        }

        // 한 공격이 내는 출력들. **적용 자체는 코어의 공용 함수**(`EffectApply.Outputs`)가 한다 —
        // 평타와 탄 착탄(unit 6a2)이 같은 산출물 표를 소비하므로 **다른 자를 쓰면 안 된다.**
        // 여기 남는 것은 「누가 누구를 때렸고 배율이 얼마인가」뿐이다.
        private static void ApplyOutputs(TickContext ctx, Unit u, AttackState atk, Unit victim, float modMul = 1f)
            => EffectApply.Outputs(ctx, u.Id, u, victim, atk.Outputs, EffectApply.DamageMul(u, victim) * modMul);

        // ── 배율 ─────────────────────────────────────────────────────────────

        /// <summary>공속 → 간격 배율. 0 이하는 무효로 보고 1 로 접는다.</summary>
        private static float IntervalMul(Unit u)
        {
            float speed = u.Modifiers.Effective.AttackSpeedMul;
            return speed > 0f ? 1f / speed : 1f;
        }

        /// <summary>
        /// 평타 탄 한 발의 피해 = 공격 산출물 피해 합 × 배율. 단발탄과 연발탄이 **같은 값**을
        /// 쓴다 — 연발의 피해를 정하는 것도 공격 산출물이다(옛 `AttackSystem` 의
        /// `spec.damage = projectileDamage` 「defender damage는 output/modifier가 결정한다」).
        ///
        /// 탄의 피해는 **발사 시점 스냅샷**이다 — 「군중 제어에 걸린 적」 배율도 그때의
        /// 의도 대상을 본다(착탄 시점에 다시 재면 날아가는 동안 풀린 적이 배율을 잃는다).
        /// </summary>
        private static float ShotDamage(TickContext ctx, Unit u, AttackState atk, SimEntityId target)
        {
            float damageMul = EffectApply.DamageMul(u, ctx.World.Find(target));
            float damage = 0f;
            for (int o = 0; o < atk.Outputs.Length; o++)
                if (atk.Outputs[o].Kind == AttackOutputKind.Damage)
                    damage += atk.Outputs[o].Magnitude * damageMul;
            return damage;
        }

        /// <summary>
        /// 이번 공격의 조준 방향(XZ). 커밋된 방향이 있으면 그것, 없으면 주 대상 쪽.
        /// 단발탄의 진행 방향이자 방향 발사 연발의 기준 방향이다.
        /// </summary>
        private static float2 AimDirection(Unit u, AttackState atk, float3 targetPos)
            => atk.HasCommittedDirection
                ? atk.CommittedDirection
                : math.normalizesafe((targetPos - u.Position).xz, new float2(0f, 1f));

        private void EmitProjectile(TickContext ctx, Unit u, AttackState atk,
                                    SimEntityId target, float3 targetPos, float tileSize, float heavyMul = 1f)
        {
            if (atk.ProjectileDefIndex >= ctx.Def.Projectiles.Length)
            {
                ctx.Warn("[Combat] 탄 정의 인덱스가 범위 밖이다 — 발사가 소비됐는데 탄이 없다.");
                return;
            }
            ref var pd = ref ctx.Def.Projectiles[atk.ProjectileDefIndex];

            // unit 7a — 공격 수식자는 **발사 스냅샷**으로 탄에 실린다(피해가 발사 때 정해지는 규약과 같다).
            float damage = ShotDamage(ctx, u, atk, target) * heavyMul * ModMulFor(atk, ctx.World.Find(target));

            var req = ProjectileRequest.Empty;
            req.DefIndex = atk.ProjectileDefIndex;
            req.Movement = (MovementKind)pd.Movement;
            req.Payload = (PayloadKind)pd.Payload;
            req.Owner = u.Id;
            req.OwnerFaction = u.Faction;
            req.TargetMask = atk.TargetMask;
            req.TargetLayers = atk.TargetLayers;
            req.Target = target;
            req.Origin = u.Position;
            req.Impact = targetPos;
            req.Damage = damage;
            // 일반 공격의 탄은 **자리에 떨어지는 것**이다 — 던져서 도달한 좌표이지
            // 누군가의 몸이 아니다. 몸에서 나오는 즉발 폭발(unit 7)이 0 이 아닌 값을 싣는다.
            req.OriginBodyRadius = 0f;
            req.Direction = AimDirection(u, atk, targetPos);
            req.DistanceOverride = pd.MaxDistance > 0f ? pd.MaxDistance : atk.Range * tileSize;
            req.ImpactKnockbackDistance = atk.Cc.KnockbackDistance;
            req.ImpactKnockbackDuration = atk.Cc.KnockbackDuration;
            // unit 7a — 튕김 부여(②). 착탄 칸이 발사 때 고정되는 포물선은 재조준할 대상이 없어 싣지 않는다(계약 4).
            if (req.Movement != MovementKind.BallisticArcToPoint && req.Movement != MovementKind.GrenadeToCell)
                AttackMod.Bounce(atk.Mods, out req.BounceCount, out req.BounceTileRange, out req.BounceDamageMul);
            ctx.World.ProjectileRequests.Add(req);
        }

        // ── 발사 명세 ────────────────────────────────────────────────────────
        //
        // 「누구를·몇 발·어떤 간격·얼마나 벌려」. **탄의 성질은 복제하지 않는다.**
        // 버스트 중에는 쿨다운을 마지막 탄 뒤로 미룬다.
        //
        // ⚠ **전탄의 피해는 `damage`(트리거 시점 공격 실효값)다.** 패턴 저작 피해
        // (`PatternDef.Damage`)는 보스·스킬 경로의 값이고 공격 루프는 읽지 않는다 — unit 7 이
        // 그 경로를 스킬 문맥으로 옮길 때 그쪽에서 읽는다. 라이브 머신거너 패턴 저작이 0 이라
        // 이걸 읽으면 연발 전탄이 피해 0 이 된다(2026-09-24 드리프트 감사 H1).
        //
        // ⚠ **발마다 대상을 안 고르는 패턴(방향 발사)의 기준 방향은 `aim`(트리거 시점 조준)**이다.
        // 대상이 없을 때 「대상 쪽」을 재면 사수 자신의 자리가 나와 기준이 늘 +Z(북쪽)로 접힌다
        // — 적 위치와 무관하게 북쪽으로 쏜다(2026-09-24 드리프트 감사 H3).
        private void FirePatterns(TickContext ctx, Unit u, AttackState atk, float damage, float2 aim)
        {
            if (atk.PatternSlots.Count == 0) return;

            for (int s = 0; s < atk.PatternSlots.Count; s++)
            {
                var slot = atk.PatternSlots[s];
                if (slot.Active) continue;   // 이미 진행 중인 버스트는 트리거를 다시 받지 않는다
                if (slot.PatternDefIndex < 0 || slot.PatternDefIndex >= ctx.Def.Patterns.Length)
                {
                    // C4 — 발동했는데 실행할 팔이 없다. 횟수는 이미 소비된 채로 경고한다.
                    ctx.Warn("[Combat] 발사 명세 슬롯이 가리키는 패턴이 없다 — 발동이 소비됐다.");
                    continue;
                }

                ref var pat = ref ctx.Def.Patterns[slot.PatternDefIndex];
                int shots = pat.ShotCount;
                if (shots <= 0) continue;

                var inst = slot.Instance;
                inst.EnsureCapacity(shots);
                inst.PatternDefIndex = slot.PatternDefIndex;
                inst.LockedTarget = SimEntityId.None;
                inst.Damage = damage;
                inst.AimDirection = aim;
                inst.FromSkill = false;
                inst.MaxDistanceOverride = 0f;
                for (int i = 0; i < shots; i++)
                {
                    inst.Directions[i] = pat.Shots[i].DirectionT;
                    inst.Intervals[i] = pat.Shots[i].IntervalAfterPreviousSec;
                }
                // 탄막 난수 씨앗 = `hash(사수 SimEntityId, 발사 카운터)`. 인스턴스는 버스트마다
                // 다시 시작하므로 카운터는 **durable 소유자**(슬롯)가 든다 — 0 에서 시작하면
                // RoundRobin 이 영원히 같은 순위를 고른다.
                inst.Seed = PatternTargeting.ShotSeed(u.Id.Value, slot.FireCountBase);
                PatternShotRandomizer.Apply(inst.Directions, inst.Intervals,
                                            pat.RandomizeShotsPerTrigger,
                                            pat.RandomIntervalMinSec, pat.RandomIntervalMaxSec, inst.Seed);

                EmitterTick.Begin(ref inst.Runtime, shots, slot.FireCountBase);
                slot.Active = true;

                // 버스트 중 쿨다운 연장 — 다음 트리거는 **마지막 탄이 나간 뒤부터** 기다린다.
                atk.CooldownRemaining += TotalDuration(inst.Intervals, shots);
            }
        }

        // 첫 발의 간격은 계약상 무시한다(트리거 즉시 나간다). 배열이 슬롯 재사용으로
        // 넉넉할 수 있으므로 **저작 발수까지만** 센다.
        private static float TotalDuration(float[] intervals, int shots)
        {
            float d = 0f;
            for (int i = 1; i < shots && i < intervals.Length; i++) d += math.max(0f, intervals[i]);
            return d;
        }

        /// <summary>진행 중인 발사 명세를 전진시킨다. 공격 루프와 별도로 매 틱 돈다.</summary>
        private void StepEmitters(TickContext ctx, float tileSize)
        {
            var units = ctx.World.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                var atk = u.Attack;
                if (atk != null)
                    for (int s = 0; s < atk.PatternSlots.Count; s++) AdvanceSlot(ctx, u, atk, atk.PatternSlots[s], tileSize);

                // unit 7a — **규칙이 연 버스트**(배치 스킬 · 보스 주기 발사). 같은 전진기를 탄다 — 옛 전투도
                // 평타 연발과 스킬 발사가 한 `ProjectileEmitterSystem` 을 지났다(같은 틱에 나간다).
                var bindings = u.Bindings;
                for (int b = 0; b < bindings.Count; b++)
                {
                    var emitters = bindings[b].Emitters;
                    for (int s = 0; s < emitters.Count; s++) AdvanceSlot(ctx, u, atk, emitters[s], tileSize);
                }
            }
        }

        private void AdvanceSlot(TickContext ctx, Unit u, AttackState atk, PatternSlotState slot, float tileSize)
        {
            if (!slot.Active) return;
            var inst = slot.Instance;

            int fire = EmitterTick.Advance(ref inst.Runtime, ctx.Dt, inst.Intervals);
            for (int f = 0; f < fire; f++) EmitPatternShot(ctx, u, atk, inst, tileSize);

            if (EmitterTick.IsComplete(in inst.Runtime))
            {
                // durable 카운터를 되돌려 받는다 — 다음 버스트가 이어서 센다.
                slot.FireCountBase = inst.Runtime.FireCount;
                slot.Active = false;
            }
        }

        private void EmitPatternShot(TickContext ctx, Unit u, AttackState atk,
                                     EmitterInstance inst, float tileSize)
        {
            ref var pat = ref ctx.Def.Patterns[inst.PatternDefIndex];
            int shotIndex = inst.Runtime.ShotIndex;
            inst.Runtime.ShotIndex++;
            inst.Runtime.FireCount++;

            if (pat.BarrelProjectileDefIndex < 0
                || pat.BarrelProjectileDefIndex >= ctx.Def.Projectiles.Length)
            {
                ctx.Warn("[Combat] 패턴에 탄이 없다 — 발사가 소비됐다.");
                return;
            }
            ref var bd = ref ctx.Def.Projectiles[pat.BarrelProjectileDefIndex];

            // unit 7a — 스킬 버스트의 대상은 **시전자의 상대 진영 유닛**(옛 `targetFaction = hostIsEnemy ? Defender : Enemy`)
            // 이고 층은 시전자의 공격 층이다. 평타 연발은 공격 마스크를 그대로 쓴다.
            int targetMask = inst.FromSkill ? (int)FactionRelation.OpponentUnitsOf(u.Faction)
                                            : (atk != null ? atk.TargetMask : 0);
            byte targetLayers = atk != null ? atk.TargetLayers : (byte)0;
            float attackRange = atk != null ? atk.Range : 0f;

            // 후보 풀 — **반경 게이트가 선택 앞**에 온다. 좁힌 결과를 제자리에서 압축해
            // 선택 함수가 받는 인덱스 공간과 바깥 인덱스 공간을 하나로 유지한다
            // (지역 인덱스를 밖으로 흘리면 잠금 경로가 엉뚱한 유닛을 가리킨다).
            float2 hostXZ = new float2(u.Position.x / tileSize, u.Position.z / tileSize);
            int n = 0;
            for (int i = 0; i < _candCount; i++)
            {
                var c = _cands[i];
                if (c.U == u) continue;
                if ((c.Faction & targetMask) == 0) continue;
                if (!LayerBits.CanTarget(targetLayers, c.Layers)) continue;
                if (n >= _patternXZ.Length) GrowPatternScratch();
                _patternXZ[n] = new float2(c.Pos.x / tileSize, c.Pos.z / tileSize);
                _patternIds[n] = c.SimId;
                _patternBody[n] = c.Body;
                n++;
            }
            int scoped = PatternScope.FilterByReach(_patternXZ, _patternBody, n, hostXZ,
                                                    pat.ScopeTileRange, u.HitRadius, _patternScope);
            for (int k = 0; k < scoped; k++)
            {
                int src = _patternScope[k];
                _patternXZ[k] = _patternXZ[src];
                _patternIds[k] = _patternIds[src];
                _patternBody[k] = _patternBody[src];
            }

            // unit 7a — **「한 발이 반경 안 전원에게」**(`FanOutToAllCandidates` — 캐논의 1:1 융단폭격).
            // 이 발이 스코프 안 후보 **전원**에게 1발씩 나간다. ⚠ **적 조준 궤적 전용**이다(옛
            // `ProjectileEmitterSystem.cs:221` 의 `binding == Entity`) — 칸 조준은 발사 시점 칸에 고정돼 한 탄에
            // 조준이 둘이 된다. 조건이 거짓이면 아래 단일 선택으로 흘러 **조용히 한 발**이 되므로, 그 조합은 bake 가
            // 거절한다. 후보 0 = 이 발은 소비된다(위상 보존).
            if (pat.FanOutToAllCandidates
                && MovementBinding.Of((MovementKind)bd.Movement) == BindingClass.Entity)
            {
                FanOut(ctx, u, inst, in pat, pat.BarrelProjectileDefIndex, in bd, scoped, shotIndex,
                       targetMask, targetLayers, attackRange, tileSize);
                return;
            }

            SimEntityId target = SimEntityId.None;
            float3 targetPos = u.Position;
            if (scoped > 0)
            {
                // 잠금은 **인덱스가 아니라 id** 다 — 후보 스냅샷은 틱 로컬이라 인덱스를 잠그면
                // 틱을 넘는 버스트에서 같은 인덱스가 다른 유닛을 가리킨다.
                if (!pat.ReselectPerShot && !inst.LockedTarget.IsNone
                    && ctx.World.Find(inst.LockedTarget) != null)
                {
                    target = inst.LockedTarget;
                }
                else
                {
                    var rule = (PatternSelectionRule)pat.Selection;
                    int pick = PatternTargeting.Select(_patternXZ, _patternIds, scoped, rule,
                                                       inst.Runtime.FireCount, hostXZ);
                    if (pick >= 0)
                    {
                        target = new SimEntityId(_patternIds[pick]);
                        if (!pat.ReselectPerShot) inst.LockedTarget = target;
                    }
                }
            }
            var tu = ctx.World.Find(target);
            if (tu != null) targetPos = tu.Position;

            var req = ProjectileRequest.Empty;
            req.DefIndex = pat.BarrelProjectileDefIndex;
            req.Movement = (MovementKind)bd.Movement;
            req.Payload = (PayloadKind)bd.Payload;
            req.Owner = u.Id;
            req.OwnerFaction = u.Faction;
            req.TargetMask = targetMask;
            req.TargetLayers = targetLayers;
            req.Target = target;
            req.Origin = u.Position;
            req.Impact = targetPos;
            req.Damage = inst.Damage;
            req.OriginBodyRadius = 0f;
            req.SwingIndex = shotIndex;
            req.FlightTime = pat.TelegraphSec;
            float t = shotIndex < inst.Directions.Length ? inst.Directions[shotIndex] : 0f;
            // 고른 대상이 있으면 그쪽, 없으면(선정 규칙 없음 · 후보 0) 트리거 시점 조준 방향.
            float2 baseDir = tu != null
                ? math.normalizesafe((targetPos - u.Position).xz, inst.AimDirection)
                : inst.AimDirection;
            req.Direction = PatternDirection.Resolve(baseDir, pat.MinAngleDeg, pat.MaxAngleDeg, t);
            req.DistanceOverride = inst.MaxDistanceOverride > 0f ? inst.MaxDistanceOverride
                                 : bd.MaxDistance > 0f ? bd.MaxDistance : attackRange * tileSize;
            ctx.World.ProjectileRequests.Add(req);
        }

        // 스코프 안 후보 전원에게 한 발씩. 쓸어가는 순서는 **row-major 칸 순위**(같은 칸은 후보 순위) — 목록 순서가
        // 화면에 그대로 보이므로 결정론으로 못박는다. 시차는 **칸마다** 준다(적 수가 아니라 칸 수가 쓸어가는 길이).
        private void FanOut(TickContext ctx, Unit u, EmitterInstance inst, in PatternDef pat, int barrel,
                            in ProjectileDef bd, int scoped, int shotIndex, int targetMask, byte targetLayers,
                            float attackRange, float tileSize)
        {
            if (scoped <= 0) return;
            if (_fanOrder.Length < scoped) { _fanOrder = new int[scoped * 2]; _fanRank = new long[scoped * 2]; }
            int width = _map != null ? math.max(1, _map.GridSize.x) : 1;
            for (int k = 0; k < scoped; k++)
            {
                var cand = ctx.World.Find(new SimEntityId(_patternIds[k]));
                int2 cell = cand != null && _map != null ? _map.CellOf(cand.Position) : int2.zero;
                _fanOrder[k] = k;
                _fanRank[k] = (long)cell.y * width + cell.x;
            }
            for (int a = 1; a < scoped; a++)
            {
                int key = _fanOrder[a];
                long rank = _fanRank[a];
                int b = a - 1;
                while (b >= 0 && (_fanRank[b] > rank || (_fanRank[b] == rank && _fanOrder[b] > key)))
                {
                    _fanOrder[b + 1] = _fanOrder[b];
                    _fanRank[b + 1] = _fanRank[b];
                    b--;
                }
                _fanOrder[b + 1] = key;
                _fanRank[b + 1] = rank;
            }

            float stagger = math.max(0f, pat.FanOutStaggerSec);
            long prevRank = long.MinValue;
            int cellSlot = -1;
            for (int k = 0; k < scoped; k++)
            {
                if (_fanRank[k] != prevRank) { prevRank = _fanRank[k]; cellSlot++; }
                var cand = ctx.World.Find(new SimEntityId(_patternIds[_fanOrder[k]]));
                if (cand == null) continue;
                var req = ProjectileRequest.Empty;
                req.DefIndex = barrel;
                req.Movement = (MovementKind)bd.Movement;
                req.Payload = (PayloadKind)bd.Payload;
                req.Owner = u.Id;
                req.OwnerFaction = u.Faction;
                req.TargetMask = targetMask;
                req.TargetLayers = targetLayers;
                req.Target = cand.Id;          // 이 발의 **유일한** 조준
                req.Origin = u.Position;
                req.Impact = cand.Position;
                req.Damage = inst.Damage;
                req.OriginBodyRadius = 0f;
                req.SwingIndex = shotIndex;
                // 시차는 **예고 시간**에 준다 — 발사는 한 틱에 다 나가고 착탄만 순서대로 밀린다(연타로 읽힌다).
                req.FlightTime = pat.TelegraphSec + cellSlot * stagger;
                req.Direction = math.normalizesafe((cand.Position - u.Position).xz, new float2(0f, 1f));
                req.DistanceOverride = bd.MaxDistance > 0f ? bd.MaxDistance : attackRange * tileSize;
                ctx.World.ProjectileRequests.Add(req);
            }
        }

        private int[] _fanOrder = new int[16];
        private long[] _fanRank = new long[16];

        private void GrowPatternScratch()
        {
            System.Array.Resize(ref _patternXZ, _patternXZ.Length * 2);
            System.Array.Resize(ref _patternIds, _patternIds.Length * 2);
            System.Array.Resize(ref _patternBody, _patternBody.Length * 2);
            System.Array.Resize(ref _patternScope, _patternScope.Length * 2);
        }

        // ── ③ 피해 ───────────────────────────────────────────────────────────
        //
        // 인박스에 쌓인 것을 **한 번에** 적용한다:
        //   받는 피해 배율 → 실드 흡수 → 체력 → 킬 귀속 → 사망 표시
        //
        // ⚠ 「완전 흡수 = 피격 아님」은 조건식을 안 바꾸고 성립한다 — 이후 분기 전부가
        // **관통분**을 본다.
        private void StepDamage(TickContext ctx)
        {
            var units = ctx.World.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                var inbox = u.Inbox;

                // **무적이 아니라 드랍**이다 — 궁극기로 판 밖에 나간 자의 인박스를 비운다(C12).
                // 쿼리에서 빼면 피해가 적립됐다가 착지 틱에 통째로 터진다(지연 폭탄).
                // 따름정리: **공중에서 죽는 일이 없다** = 착지가 보장된다.
                if (u.Progressive != null && u.Progressive.LeapActive)
                {
                    inbox.Damage.Clear();
                    inbox.Heal.Clear();
                    continue;
                }
                // **체력을 다른 담당자가 드는 개체**(마음 타워)는 이 단계가 통째로 건너뛴다 —
                // 인박스도 **안 비운다**. 비우면 그 담당자가 받을 것이 사라지고, 안 건너뛰면
                // 최대 체력 0 짜리 개체가 매 틱 죽는다. 드레인의 주인은 플래그를 세운 쪽이다.
                if (u.HealthExternal) continue;

                if (u.Dead) { inbox.Damage.Clear(); inbox.Heal.Clear(); continue; }

                // ── 실드 부여 드레인 ──
                // **지난 틱에 스테이징된 것**만 소모한다(C17 — 부여만 한 틱 늦는 비대칭).
                if (inbox.Shield.Count > 0)
                {
                    for (int k = 0; k < inbox.Shield.Count; k++)
                        ShieldMath.Merge(u.Shield.Slots, inbox.Shield[k].Source, inbox.Shield[k].Amount);
                    inbox.Shield.Clear();
                }

                float regen = u.RegenPerSec;
                if (inbox.Damage.Count == 0 && inbox.Heal.Count == 0 && regen <= 0f) continue;

                float total = 0f;
                SimEntityId killer = SimEntityId.None;
                float killerAmount = 0f;
                for (int k = 0; k < inbox.Damage.Count; k++)
                {
                    var e = inbox.Damage[k];
                    total += e.Amount;
                    KillAttribution.Consider(e.Amount, e.Source, ref killer, ref killerAmount);
                }
                total *= u.DamageTakenMul;

                float preShield = total;
                bool shieldBroke = false;
                if (u.Shield.Any && total > 0f)
                {
                    float before = ShieldMath.Sum(u.Shield.Slots);
                    total = ShieldMath.Absorb(u.Shield.Slots, total);
                    if (before > 0f && ShieldMath.Sum(u.Shield.Slots) <= 0f) shieldBroke = true;
                }
                float absorbed = preShield - total;

                float pulse = 0f;
                for (int k = 0; k < inbox.Heal.Count; k++) pulse += inbox.Heal[k];
                // **재생은 피해 그릇 유무와 무관하다**(C24 → 분리). 옛 전투는 한 단계가
                // 성격이 다른 일을 겸직해 「피해 버퍼가 하나도 없으면 재생도 멈추는」 결합이 있었다.
                float heal = pulse + regen * ctx.Dt;

                float newHp = math.min(u.MaxHealth, u.Health - total + heal);
                u.Health = newHp;
                float ratio = HealthMath.ComputeRatio(newHp, u.MaxHealth);

                // 피해 숫자 — 인박스 항목당 하나이고 **비율은 그 틱 최종값**이다(C7).
                // 실드가 일부만 막으면 관통분 비례로 배분한다.
                if (total > 0f || absorbed > 0f)
                {
                    float pierceRatio = preShield > 0f ? total / preShield : 1f;
                    for (int k = 0; k < inbox.Damage.Count; k++)
                    {
                        var e = inbox.Damage[k];
                        float applied = e.Amount * u.DamageTakenMul * pierceRatio;
                        if (applied <= 0f) continue;
                        ctx.Bus.Publish(CoreEvent.DamageApplied(ctx.Tick, u, e.Source, applied,
                                                                absorbed, ratio));
                    }
                }
                if (shieldBroke) ctx.Bus.Publish(CoreEvent.ShieldBroken(ctx.Tick, u));
                // unit 7a — 감지자 사실 ①: 피격 N회(관통 피해 · **살아남음** · 체력은 이 피격 뒤) → 실드 파열(사망과
                // 독립 — 관통 킬 틱에도). 옛 순서(피격 카운터 → 파열) 그대로다.
                if (total > 0f && newHp > 0f) ctx.Triggers?.RaiseDamaged(u);
                if (shieldBroke) ctx.Triggers?.RaiseShieldBreak(u);
                // 회복 펄스만 연출한다 — 초당 재생은 조용히 흐른다(매 틱 신호는 노이즈다).
                if (pulse > 0f) ctx.Bus.Publish(CoreEvent.HealApplied(ctx.Tick, u, pulse));

                // 피격 시 수면 해제 — **실제로 피해를 입었을 때만**이고 기절은 안 깬다.
                // ⚠ 요청만 쌓는다. 같은 틱에 걸린 수면을 거르는 것은 `FlushCc` 다(C9).
                if (total > 0f) _pendingWake.Add(new WakeRequest { Target = u.Id });

                inbox.Damage.Clear();
                inbox.Heal.Clear();

                if (newHp > 0f) continue;

                // ── 사망 표시 ──
                u.Dead = true;
                u.DeathTick = ctx.Tick;
                // **피해로 죽었을 때만** 낸다 — 분열·처치 보상의 사건이다.
                // 출처 없는 죽음(지속 피해·자해·환경)은 미귀속이고 이 사건이 안 난다(의도).
                if (!killer.IsNone) ctx.Bus.Publish(CoreEvent.UnitSlain(ctx.Tick, killer, u));
                // unit 7a — 감지자 사실 ②: 처치(시체 폭발 · 잿불). 자리·몸 = 죽은 적(발화 시점 스냅샷).
                if (!killer.IsNone) ctx.Triggers?.RaiseKill(ctx.World.Find(killer), u);
                ctx.World.InterruptProgress(u, ProgressInterrupt.Death, ctx.Tick);
            }
        }

        // ── ④ 후처리 ─────────────────────────────────────────────────────────
        //
        // C9 의 나머지 절반. 군중 제어 요청을 **피해 뒤**에 줄로 옮기고, 기상 요청은
        // **그 틱에 수면이 걸린 대상**을 뺀다.
        private void FlushCc(TickContext ctx)
        {
            for (int i = 0; i < _pendingCc.Count; i++) ctx.World.RequestCc(_pendingCc[i]);
            _pendingCc.Clear();

            for (int i = 0; i < _pendingWake.Count; i++)
            {
                var w = _pendingWake[i];
                bool sleptThisTick = false;
                var reqs = ctx.World.CcRequests;
                for (int k = 0; k < reqs.Count; k++)
                {
                    if (reqs[k].Kind != CcRequestKind.Sleep) continue;
                    if (reqs[k].Target != w.Target) continue;
                    sleptThisTick = true;
                    break;
                }
                // **내가 때린 피해가 내가 건 잠을 깨우지 않는다.** 옛 전투는 시스템 순서가
                // 우연히 보장했을 뿐이라 명시 가드가 없었다 — 여기가 그 가드다.
                if (!sleptThisTick) ctx.World.WakeRequests.Add(w);
            }
            _pendingWake.Clear();

            // ── unit 6a — 슬롯 적용 → 기상 → 감쇠 ──
            //
            // 순서가 규칙이다. 기상이 적용 **뒤**라야 「지난 틱에 걸린 잠」만 깨고, 감쇠가
            // 맨 뒤라야 이번 틱에 걸린 것이 한 틱은 산다. 감쇠가 **이동 뒤·피해 뒤**인 것도
            // 옛 `CcDecaySystem` 의 자리와 같다.
            ApplyCc(ctx);
            ApplyWake(ctx);
            // unit 7a — 호접몽 완주 판정. **기상 뒤 · 감쇠 앞**(옛 `DreamCocoonSystem` 의 자리) — 피격으로 깨면 이
            // 틱에 파탄이 보이고, 감쇠가 잠을 먼저 거두는 틱에는 완주 여유(ε)가 판정을 지킨다.
            StepCocoon(ctx);
            DecayCc(ctx);
        }

        // 요청 → 슬롯. **자격 판정은 여기 없다** — 문이 `BattleWorld.RequestCc` 하나이고
        // 거기서 이미 거점 면역·보스 면역을 봤다(가드를 둘로 나누면 하나가 언젠가 샌다).
        private static void ApplyCc(TickContext ctx)
        {
            var reqs = ctx.World.CcRequests;
            for (int i = 0; i < reqs.Count; i++)
            {
                var req = reqs[i];
                var victim = ctx.World.Find(req.Target);
                if (victim == null) continue;

                CcSlotKind slot;
                switch (req.Kind)
                {
                    case CcRequestKind.Impulse: slot = CcSlotKind.Impulse; break;
                    case CcRequestKind.Stun: slot = CcSlotKind.Stun; break;
                    case CcRequestKind.Sleep: slot = CcSlotKind.Sleep; break;
                    default:
                        // `Slow` 는 **저작 토큰**이고 런타임 슬롯이 아니다 — 감속은 이동속도
                        // 모디파이어로 간다(6b 가 그 생산자). 여기 온 것은 배선 실수이고,
                        // 크기를 안 나르는 요청이라 조용히 흘리면 감속이 통째로 사라진다.
                        ctx.Warn("[Combat] 감속을 군중 제어 요청으로 보냈다 — 이동속도 모디파이어로 걸어야 한다.");
                        continue;
                }

                if (victim.Cc.Apply(slot, req.Seconds, req.Vector, req.Source))
                    ctx.Bus.Publish(CoreEvent.CcApplied(ctx.Tick, victim, req.Source, slot, req.Seconds));
            }
            reqs.Clear();
        }

        // 피격 기상 — **수면만** 풀린다(기절은 안 깬다). 같은 틱에 걸린 수면은 위 필터가
        // 이미 걸렀으므로 여기 오는 것은 「지난 틱 이전의 잠」뿐이다(C9 의 나머지 절반).
        private static void ApplyWake(TickContext ctx)
        {
            var wakes = ctx.World.WakeRequests;
            for (int i = 0; i < wakes.Count; i++)
            {
                var victim = ctx.World.Find(wakes[i].Target);
                if (victim == null) continue;
                if (victim.Cc.Clear(CcSlotKind.Sleep))
                    ctx.Bus.Publish(CoreEvent.CcCleared(ctx.Tick, victim, CcSlotKind.Sleep,
                                                        CcClearReason.WokeUp));
            }
            wakes.Clear();
        }

        private static void StepCocoon(TickContext ctx)
        {
            var units = ctx.World.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                var pg = u.Progressive;
                if (pg == null || !pg.CocoonActive || u.Dead) continue;
                // 잠이 먼저 풀렸다(피격) = 파탄 — 보상 없이 감시만 걷는다.
                if (!u.Cc.IsActive(CcSlotKind.Sleep) && pg.CocoonRemaining > 0f) { pg.CocoonActive = false; continue; }
                pg.CocoonRemaining -= ctx.Dt;
                if (pg.CocoonRemaining > 0f) continue;
                pg.CocoonActive = false;
                // 완주 — 저작 배율을 **영구** 스탯으로(분류는 `ModifierAuthoring` 한 곳).
                ModifierAuthoring.FromMultiplier(pg.CocoonMult, out var op, out var mag);
                EffectApply.Stat(ctx, u.Id, u, u, (StatKind)pg.CocoonStat, op, mag, float.PositiveInfinity,
                                 // unit 7b — 칸 판별자 = 그 카드 규칙의 `InstanceId`(카드 칸 — 붙일 때마다 새 칸, 옛 `_dcStackCounter++`).
                                 // 배치 칸(`OnPlace`)에 두면 유닛 저작 스택 id 와 같은 번호판을 써 서로를 덮는다.
                                 SlotTag.OfCard(pg.CocoonStackId), 0f, ModifierOrigin.Dreamcatcher);
            }
        }

        private static void DecayCc(TickContext ctx)
        {
            var units = ctx.World.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (!u.Cc.Any) continue;
                int cleared = u.Cc.Decay(ctx.Dt);
                if (cleared == 0) continue;
                for (int k = 0; k < 3; k++)
                    if ((cleared & (1 << k)) != 0)
                        ctx.Bus.Publish(CoreEvent.CcCleared(ctx.Tick, u, (CcSlotKind)k,
                                                            CcClearReason.Expired));
            }
        }

        // ── ⑤ 소멸 ───────────────────────────────────────────────────────────
        //
        // **`BattleWorld.Destroy` 가 유일한 경로**다(계약 7). 이번 틱에 표시된 것은 **남긴다** —
        // 표시 틱 ≠ 소멸 틱이라야 시체가 자기 자리를 읽을 창이 생긴다.
        private void StepDestroy(TickContext ctx)
        {
            var units = ctx.World.Units;
            _toDestroy.Clear();
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (!u.Dead) continue;
                // **죽은 채 배치 중이면 조용히 걷기만 한다** — 「시체는 배치되지 않는다」.
                if (u.Deploying) { _toDestroy.Add(u.Id); continue; }
                if (u.DeathTick >= 0 && u.DeathTick >= ctx.Tick) continue;   // 이번 틱 표시분은 한 틱 남긴다
                _toDestroy.Add(u.Id);
            }
            for (int i = 0; i < _toDestroy.Count; i++)
            {
                // unit 7a — 감지자 사실 ③: 자기 죽음(작별 선물). **모든 사망 경로가 합류하는 자리**(피해 · 치명
                // 타이머)라 여기서 올린다 — 앞당기면 피해로 죽은 경우만 터진다. 진영·몸·자리는 **파괴 직전** 값이다.
                var dying = ctx.World.Find(_toDestroy[i]);
                if (dying != null) ctx.Triggers?.RaiseDeath(dying);
                _map?.Occupancy.Release(_toDestroy[i]);
                ctx.World.Destroy(_toDestroy[i], ctx.Tick);
            }
        }

        // ── ⑥ 경계 ───────────────────────────────────────────────────────────
        //
        // 치명 타이머 — 시간이 끝나면 **죽는다**(unit 7a — 옛 `LethalTimerSystem` 이 `DeadTag` 를 붙였다).
        // 출처가 없으므로 처치 보상이 안 난다(자해 = 미귀속). 실드·받는 피해 배율을 지나지 않는다 — 피해가 아니라
        // 선고다. 표시 틱 = 이 틱, 소멸 = 다음 틱(사망 2단계 그대로 — 작별 선물이 그 창에서 난다).
        private void StepThreshold(TickContext ctx)
        {
            var units = ctx.World.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                var pg = u.Progressive;
                if (pg == null || !pg.LethalActive || u.Dead) continue;
                pg.LethalRemaining -= ctx.Dt;
                if (pg.LethalRemaining > 0f) continue;
                pg.LethalActive = false;
                u.Dead = true;
                u.DeathTick = ctx.Tick;
                ctx.World.InterruptProgress(u, ProgressInterrupt.Death, ctx.Tick);
            }
        }

        // ── ⑦ 도약 · 순간이동 ────────────────────────────────────────────────
        //
        // 궁극기 도약 = 이탈(피격 불가 · 잠금 + 무적 **원자 개시**) → 예고 → 강습 → 착지 슬램.
        // 시퀀스를 코어가 소유하는 이유: 예고 시간은 **회피 창이자 피해 게이트** = 게임 규칙이다.
        //
        // ⚠ 착지 슬램은 **자리에 떨어지는 것**이다(2026-09-07 사용자 정정). 보스가 «지정한
        // 좌표»에 내리는 것이라 그 몸이 붙지 않는다 — `OriginBodyRadius = 0`.
        // ⚠ 즉발(`flightTime == 0`)로 판별하지 말 것. 자폭·시체 폭발과 배선이 같아 보이지만
        // 형이 다르다.
        private void StepLeap(TickContext ctx)
        {
            var units = ctx.World.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                var pg = u.Progressive;
                if (pg == null || !pg.LeapActive) continue;

                pg.LeapRemaining -= ctx.Dt;
                if (pg.LeapRemaining > 0f) continue;

                // ① 순간이동 — 위치는 이동이 소유하므로 요청으로 넘긴다.
                if (u.Move != null) { u.Move.HasBlink = true; u.Move.BlinkTo = pg.LandingWorld; }
                else u.Position = pg.LandingWorld;

                // ② 착지 슬램 — 자리형(0 몸).
                if (pg.SlamDamage > 0f && pg.SlamProjectileDefIndex >= 0)
                {
                    var req = ProjectileRequest.Empty;
                    req.DefIndex = pg.SlamProjectileDefIndex;
                    req.Movement = MovementKind.SkyFall;
                    req.Payload = PayloadKind.TileAoe;
                    req.Owner = u.Id;
                    req.OwnerFaction = u.Faction;
                    req.TargetMask = u.Attack != null ? u.Attack.TargetMask : 0;
                    req.TargetLayers = u.Attack != null ? u.Attack.TargetLayers : (byte)0;
                    req.Origin = pg.LandingWorld;
                    req.Impact = pg.LandingWorld;
                    req.Damage = pg.SlamDamage;
                    req.ImpactTileRange = pg.SlamTileRange;
                    req.OriginBodyRadius = 0f;   // ⚠ 자리형 — 「몸이 내리찍는 것」이 아니다
                    req.FlightTime = 0f;         // 예고가 이미 시간을 벌었다
                    ctx.World.ProjectileRequests.Add(req);
                }

                // ③ 상태 해제 — 무적과 잠금이 **함께** 떨어진다(붙을 때와 대칭).
                pg.LeapActive = false;
                if (u.Move != null) u.Move.Locked = false;

                ctx.Bus.Publish(CoreEvent.LeapDescend(ctx.Tick, u, pg.LandingWorld, ultimate: true));
            }
            StepHop(ctx);
        }

        // unit 7d — **일반 도약의 착지 슬램**(짱쎈). 옛 전투는 뷰가 도착한 시각(브리지 코루틴 0.83초)에 브리지가 슬램 탄을 쐈다 —
        // 그 시각을 판의 시계로 옮긴다: 비행 창(`Movement.BossLeapFlightSeconds`)이 끝나는 틱.
        // ⚠ **자리에 떨어지는 것**이다(2026-09-07 사용자 결정 — 운석과 같은 형). 원점 몸 0 · 칸 반폭 — 궁극기 강습과 같은 규칙.
        // ⚠ 비행 중 죽으면 슬램 없이 끝난다(옛 `abandoned` — 중단 정책 표가 `HopActive` 를 걷는다).
        // ⚠ 통행 층은 **안 거른다**(0) — 옛 슬램 요청이 `targetTraversalLayers` 를 비워 뒀다. 궁극기 강습(공격 층)과 다르다.
        private static void StepHop(TickContext ctx)
        {
            var units = ctx.World.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                var pg = u.Progressive;
                if (pg == null || !pg.HopActive) continue;
                pg.HopRemaining -= ctx.Dt;
                if (pg.HopRemaining > 0f) continue;
                pg.HopActive = false;
                if (u.Dead) continue;

                if (pg.HopSlamDamage > 0f && pg.HopSlamDefIndex >= 0)
                {
                    var req = ProjectileRequest.Empty;
                    req.DefIndex = pg.HopSlamDefIndex;
                    req.Movement = MovementKind.SkyFall;
                    req.Payload = PayloadKind.TileAoe;
                    req.Owner = u.Id;              // 킬 귀속만 보스로(옛 `owner = evt.entity`) — 피해는 고정값이다
                    req.OwnerFaction = u.Faction;
                    req.TargetMask = u.Attack != null ? u.Attack.TargetMask : 0;
                    req.TargetLayers = 0;
                    req.Origin = pg.HopLanding;
                    req.Impact = pg.HopLanding;
                    req.Damage = pg.HopSlamDamage;
                    req.ImpactTileRange = pg.HopSlamTileRange;
                    req.OriginBodyRadius = 0f;   // ⚠ 자리형 — 「몸이 내리찍는 것」이 아니다
                    req.FlightTime = 0f;         // 비행 창이 이미 시간을 벌었다
                    ctx.World.ProjectileRequests.Add(req);
                }
                ctx.Bus.Publish(CoreEvent.LeapDescend(ctx.Tick, u, pg.HopLanding, ultimate: false));
            }
        }

        // ── ⑧ 실드 부여 스테이징 ──────────────────────────────────────────────
        //
        // C17 의 비대칭을 만드는 **유일한 자리**. 이번 틱에 쌓인 부여를 다음 틱 드레인으로 넘긴다.
        private void StageShields(TickContext ctx)
        {
            var units = ctx.World.Units;
            for (int i = 0; i < units.Count; i++) units[i].Inbox.StageShield();
        }

        // ── 군중 제어 부여 ───────────────────────────────────────────────────

        private void RequestKnockback(TickContext ctx, Unit u, AttackState atk, Unit victim)
        {
            if (IsImmune(ctx, victim, CcRequestKind.Impulse)) return;
            if (victim.Move == null) return;
            // **방향을 모르는 대상은 밀리지 않는다**(C8). 스폰 직후·고정 구조물이 그렇다 —
            // 0 방향으로 밀면 원점으로 빨려든다.
            float2 travel = victim.Move.LastMoveDir;
            if (math.lengthsq(travel) <= 1e-6f) return;
            // 미는 방향은 **적이 가던 방향의 반대** 하나다(2026-08-17 사용자 결정 B).
            // 상대속도 합산은 은퇴했다 — 지나쳐 가는 적을 골 쪽으로 밀어줬다.
            float2 v = -math.normalize(travel) * (atk.Cc.KnockbackDistance / atk.Cc.KnockbackDuration);
            _pendingCc.Add(CcRequest.Push(victim.Id, new float3(v.x, 0f, v.y),
                                          atk.Cc.KnockbackDuration, u.Id));
        }

        private void RequestSleep(TickContext ctx, Unit u, AttackState atk, Unit victim)
        {
            if (IsImmune(ctx, victim, CcRequestKind.Sleep)) return;
            _pendingCc.Add(CcRequest.Of(victim.Id, CcRequestKind.Sleep, atk.Cc.SleepSeconds, u.Id));
        }

        private void RequestKnockup(TickContext ctx, Unit u, AttackState atk, Unit victim)
        {
            if (IsImmune(ctx, victim, CcRequestKind.Stun)) return;
            // 심에서 넉업의 실체는 **짧은 기절**이다. 그래서 띄우는 연출은 **띄운 쪽이 따로
            // 신호한다** — 뷰가 군중 제어 종류로 판단하면 일반 기절까지 떠오른다.
            _pendingCc.Add(CcRequest.Of(victim.Id, CcRequestKind.Stun, atk.Cc.KnockupSeconds, u.Id));
            ctx.Bus.Publish(CoreEvent.Knockup(ctx.Tick, victim, atk.Cc.KnockupSeconds,
                                              atk.Cc.KnockupVisualHeight));
        }

        /// <summary>
        /// 이 대상은 군중 제어를 못 받는다. 보스 면역(기절·수면·넉백 **출처 불문**)과
        /// 거점 면역(F3)이 **같은 술어**에 있는 이유: 둘 다 「이 대상에게는 이 축이 아예
        /// 없다」는 말이고, 둘을 나누면 새 효과가 한쪽만 물어본다.
        /// </summary>
        private static bool IsImmune(TickContext ctx, Unit victim, CcRequestKind kind)
            => !EffectEligibility.AcceptsCc(victim, kind);

        // ── 공통 ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 직업 필터. **필터의 존재가 게이트다** — 필터가 있으면 마스크 0 은 아무도 못 때린다
        /// (옛 `AttackSystem` 의 `hasFilter`). 직업이 없는 후보(적·거점, -1)는 거르지 않는다.
        /// </summary>
        private static bool ClassAllowed(AttackState atk, int cls)
            => !atk.HasClassFilter || cls < 0 || (atk.ClassMask & (1 << cls)) != 0;

        private bool Legal(in Candidate c, AttackState atk)
        {
            if ((c.Faction & atk.TargetMask) == 0) return false;
            return LayerBits.CanTarget(atk.TargetLayers, c.Layers);
        }

        private static float SqXZ(float3 a, float3 b)
        {
            float dx = b.x - a.x, dz = b.z - a.z;
            return dx * dx + dz * dz;
        }

        private void GrowCandidates()
        {
            int n = _cands.Length * 2;
            System.Array.Resize(ref _cands, n);
            System.Array.Resize(ref _picked, n);
        }

        /// <summary>
        /// 정의표 한 줄에서 공격 상태를 세운다. 스폰하는 쪽(커맨드·웨이브·소환)이 **모두**
        /// 이 함수를 지나야 「어떤 경로로 태어났나」가 공격 규칙을 바꾸지 않는다.
        /// </summary>
        public static AttackState BuildAttackState(in UnitDef d, MatchDefinition def = null,
                                                   UnitPartPool parts = null)
            => BuildAttackState(in d.Attack, d.AttackRange, d.AttackCooldown, d.HitDelaySeconds,
                                d.AttackTargetCount, d.AggroCapacity,
                                TargetDefaults.ResolveDefender(d.TargetFactions), def, parts);

        /// <summary>
        /// 거점(본능·적 마음)의 공격. **마스크의 「0」이 유닛·적과 뜻이 다르다** — 저쪽은
        /// 「미저작 = 기본값」이고 여기는 **「아무도 안 때린다」**다. 거점은 편이 배치에서
        /// 오므로 SO 가 자기 상대를 모르고(방어 본능과 적 본능이 같은 SO 일 수 있다),
        /// 「모르면 상대 진영 전부」로 접으면 방어 본능이 방어유닛을 쏜다.
        /// </summary>
        public static AttackState BuildAttackState(in StructureDef d, MatchDefinition def = null,
                                                   UnitPartPool parts = null)
            => BuildAttackState(in d.Attack, d.AttackRange, d.AttackCooldown, d.HitDelaySeconds,
                                d.AttackTargetCount, 0, d.TargetFactions, def, parts);

        public static AttackState BuildAttackState(in EnemyDef d, MatchDefinition def = null,
                                                   UnitPartPool parts = null)
            => BuildAttackState(in d.Attack, d.AttackRange, d.AttackCooldown, d.HitDelaySeconds,
                                d.AttackTargetCount, 0,
                                TargetDefaults.ResolveEnemy(d.TargetFactions), def, parts);

        // 표 밖을 가리키는 참조는 **여기서 한 번** 접는다. 매 RESOLVE 에서 접으면 같은 경고가
        // 초당 수십 번 나고, 그보다 나쁘게 **미저작 0 이 탄 0번을 조용히 쏜다.**
        private static int ClampRef(int index, int count)
            => index >= 0 && index < count ? index : -1;

        private static AttackState BuildAttackState(in AttackDef a, float range, float cooldown,
                                                    float hitDelay, int targetCount,
                                                    int aggroCapacity, int targetMask,
                                                    MatchDefinition def, UnitPartPool parts)
        {
            int projectiles = def != null ? def.Projectiles.Length : 0;
            int units = def != null ? def.Units.Length : 0;
            // F4 — 스폰도 틱 중에 돈다(웨이브·소환). 풀이 있으면 빌린다.
            var s = parts != null ? parts.RentAttack() : new AttackState();
            Fill(s, in a, range, cooldown, hitDelay, targetCount, aggroCapacity, targetMask,
                 projectiles, units);
            return s;
        }

        // 빌려 온 인스턴스를 **덮어쓴다.** 새로 만들지 않는 이유는 F4 와 같다 —
        // 스폰도 틱 중에 도는 일이고, 그때마다 공격 상태 하나가 쓰레기가 됐다.
        private static void Fill(AttackState s, in AttackDef a, float range, float cooldown,
                                 float hitDelay, int targetCount, int aggroCapacity, int targetMask,
                                 int projectiles, int units)
        {
            s.Range = range;
            s.Interval = cooldown;
            s.HitDelay = hitDelay;
            s.TargetCount = math.max(1, targetCount);
            s.TargetMask = targetMask;
            s.TargetLayers = (byte)a.TargetLayers;
            s.PriorityClass = a.PriorityClass;
            s.ClassMask = a.ClassMask;
            s.HasClassFilter = a.HasClassFilter;
            s.Unarmed = a.Unarmed;
            s.Mode = (TargetMode)a.Mode;
            s.Policy = (AttackPolicy)a.Policy;
            s.ProjectileDefIndex = ClampRef(a.ProjectileDefIndex, projectiles);
            s.Outputs = a.Outputs ?? System.Array.Empty<AttackOutputDef>();
            s.AggroCapacity = aggroCapacity;
            s.BossImmune = a.BossImmune;
            s.Shape = new AttackShapeBaked
            {
                kind = (byte)a.ShapeKind,
                sinHalf = a.ShapeSinHalf,
                cosHalf = a.ShapeCosHalf,
                halfWidth = a.ShapeHalfWidth,
            };
            s.Cc = new CcOnHit
            {
                KnockbackDistance = a.KnockbackDistance,
                KnockbackDuration = a.KnockbackDuration,
                SleepSeconds = a.SleepOnHitSec,
                KnockupSeconds = a.KnockupOnHitSec,
                KnockupVisualHeight = a.KnockupVisualHeight,
            };
            s.Bomb = new BombSpec
            {
                ProjectileDefIndex = ClampRef(a.BombProjectileDefIndex, projectiles),
                Damage = a.BombDamage,
                AoeTileRange = a.BombAoeTileRange,
                AoeTargetCap = a.BombAoeTargetCap,
                TravelSeconds = a.BombTravelSeconds,
                FuseSeconds = a.BombFuseSeconds,
                ArcHeight = a.BombArcHeight,
            };
            s.Summon = new SummonSpec { PatrolDefIndex = ClampRef(a.SummonPatrolDefIndex, units) };

            // unit 7a — 저작 공격 수식자. 인스턴스를 돌려쓰지 않는다(카운터가 새 개체에서 0 부터).
            s.Mods.Clear();
            int modCount = a.Mods != null ? a.Mods.Length : 0;
            for (int i = 0; i < modCount; i++) s.Mods.Add(new AttackModState { Def = a.Mods[i] });

            // 발사 명세 슬롯 — **개수만 맞추고 객체는 돌려쓴다.** 슬롯은 발사 인스턴스와 그
            // 간격·방향 배열을 들고 있어서 버리면 다음 대여가 통째로 다시 할당한다.
            int want = a.PatternDefIndices != null ? a.PatternDefIndices.Length : 0;
            while (s.PatternSlots.Count < want) s.PatternSlots.Add(new PatternSlotState());
            while (s.PatternSlots.Count > want) s.PatternSlots.RemoveAt(s.PatternSlots.Count - 1);
            for (int i = 0; i < want; i++)
            {
                var slot = s.PatternSlots[i];
                slot.PatternDefIndex = a.PatternDefIndices[i];
                slot.FireCountBase = 0;
                slot.Active = false;
                slot.Instance.LockedTarget = SimEntityId.None;
                slot.Instance.Runtime = default;
            }
        }
    }
}