using System.Collections.Generic;
using Wassup.Sim.Effects;
using Wassup.Sim.Movement;
using Wassup.Sim.Units;

namespace Wassup.Sim.Combat
{
    /// <summary>
    /// battle-sim-extraction unit 18-I/2 — 캡처 #33(P8). 구 `AttackSystem`(1,729줄) 이식.
    ///
    /// ⚠ **arm 단위로 채워지는 중이다.** 지금 있는 것은 **arm B(캐스트 사건 드레인)** 하나이고,
    /// 남은 arm(타겟팅 · START · RESOLVE · Outputs · 스냅샷 확장)은 재정리 문서
    /// (`m1_unit18_replan.md` §남은 arm 지도)의 순서대로 들어온다. 1,729줄을 한 번에 옮기면
    /// 이식 도중 끊기고, 반쯤 옮겨진 공격 루프가 이 spec 에서 되돌리기 가장 비싼 상태다.
    ///
    /// 통합 공격자 루프의 계약(구 sim 그대로): `AttackState` + 위치를 가진 엔티티는 방어유닛이든
    /// 적이든 **한 루프**에서 공격자로 참여하고, 진영 고유 동작은 태그 분기로 갈린다.
    /// </summary>
    public sealed class AttackSystem
    {
        private readonly SimChannels _channels;
        private readonly SimCommandBuffer _ecb = new SimCommandBuffer();

        // ── 후보 스냅샷 (arm A 의 조각) ────────────────────────────────────────
        // arm A(선두 스냅샷 구축)는 마지막에 옮기지만, 캐스트 드레인이 폴백 선정에 이 풀을
        // 먼저 요구한다. 쿼리 조건은 구 sim 의 것을 그대로 옮겼으므로 arm A 는 이 셋을
        // 확장하기만 한다(다시 만들지 않는다).
        private readonly List<SimEntityId> _targetEntities = new List<SimEntityId>();
        private readonly List<SimVec3> _targetPositions = new List<SimVec3>();
        private readonly List<Faction> _targetFactions = new List<Faction>();

        /// 니들 폴백 선정용 scratch — 후보 수는 스냅샷 길이로 고정이라 재사용한다.
        /// ⚠ **스냅샷과 index 평행**이어야 한다(`SelectNearest` 가 돌려주는 index 로
        /// <see cref="_targetEntities"/> 를 인덱싱한다).
        private readonly List<NearestTargeting.Candidate> _needleScratch =
            new List<NearestTargeting.Candidate>();

        private readonly HashSet<SimEntityId> _castCountedHosts = new HashSet<SimEntityId>();

        public AttackSystem(SimChannels channels) => _channels = channels;

        /// <summary>
        /// 이번 틱에 **캐스트로** 공격 사건을 카운트한 host 집합(attack-decoupling 계약 2).
        ///
        /// ⚠ 이것은 **arm E(RESOLVE)로 가는 seam** 이다 — 그 arm 이 들어오면 여기 있는 host 는
        /// RESOLVE 의 카운팅 블록을 건너뛴다. 계약 2("host 당 사건 지점 1개")를 데이터 모양이
        /// 아니라 **코드로** 보장하는 자리다: 예전에는 캐스터의 `attackRange` 가 0 인 덕에
        /// RESOLVE 에 못 가는 것으로 우연히 성립했는데, 유닛 스탯 시트가 캐스터 `attackRange` 를
        /// 3 으로 확정하면서 그 우연이 깨졌다(캐스트 + RESOLVE 로 2카운트 → `AttackN` 카드 발동
        /// 주기가 절반). 시트가 정본이므로 여기서 막는다.
        ///
        /// 틱마다 <see cref="Run"/> 선두에서 비워진다 — 프레임 로컬이다.
        /// </summary>
        public IReadOnlyCollection<SimEntityId> CastCountedHosts => _castCountedHosts;

        public void Run(SimWorld world)
        {
            _castCountedHosts.Clear();

            // ⚠ 구 sim 의 `RequireForUpdate<AttackState>` 자리다. `HazardCastSystem` 과 같은 사정으로
            //   **증발이 아니라 이사**다 — 루프 밖에 스냅샷 구축이 있어서, 이게 없으면 공격자가
            //   0 명인 동안에도 매 틱 전 유닛을 훑는다(배치 전 구간이 통째로 그렇다).
            //   부수효과: 공격자가 없는 프레임엔 캐스트 채널도 드레인되지 않고 쌓인다.
            //   그것이 구 sim 의 동작이다(시스템이 안 돌면 큐도 안 비는 것) — 재현 대상이다.
            bool hasAttacker = false;
            foreach (var _ in world.With<AttackState>()) { hasAttacker = true; break; }
            if (!hasAttacker) return;

            // 그리드 폴백은 프레임 불변이라 한 번만 푼다. 구 sim 은 폭탄 분기·attacker 루프·
            // 캐스트 드레인이 각자 삼항식을 반복했다(캐스트 쪽은 후보 루프 **안**).
            // ⚠ 폴백 128×128 은 구 sim 의 값이다 — 필드가 없는 프레임의 셀 계산이 여기 걸린다.
            bool hasFlowField = SimSingleton.TryGet<FlowFieldSingleton>(world, out var flowField);
            float tileSize = hasFlowField ? flowField.tileSize : 1f;
            SimInt2 gridSize = hasFlowField ? flowField.gridSize : new SimInt2(128, 128);
            SimVec3 ffOrigin = hasFlowField ? flowField.origin : default;

            BuildTargetSnapshot(world);

            DrainCastEvents(world, tileSize, gridSize, ffOrigin);
            RunAttackerLoop(world, world.DeltaTime, tileSize, gridSize, ffOrigin);

            _ecb.Playback(world);
        }

        /// <summary>
        /// **통합 공격자 루프** — 방어유닛과 적이 이 쿼리 하나를 공유한다. 진영 고유 동작은
        /// 태그 분기로 갈리므로 쿼리에 공격자 태그 필터가 없다.
        ///
        /// ⚠ `AttackState` 쓰기는 **각 반복 끝에서 한 번** write-back 한다(구 sim 의 `RefRW` 는
        /// 즉시 쓰기였지만, 반복 안에서 이 엔티티의 `AttackState` 를 다시 읽는 코드가 없어
        /// 결과가 같다). **모든 탈출 경로가 write-back 을 지나야 한다** — 빠뜨리면 쿨다운 틱이
        /// 조용히 사라진다. 그래서 분기는 `continue` 대신 헬퍼가 처리하고 여기서 한 번만 쓴다.
        /// </summary>
        private void RunAttackerLoop(SimWorld world, float dt, float tileSize, SimInt2 gridSize, SimVec3 ffOrigin)
        {
            foreach (var attackerEntity in world.With<AttackState>())
            {
                if (world.Has<PendingDeployment>(attackerEntity)) continue;
                // 구 쿼리는 `LocalTransform` 도 요구한다 — 위치 없는 공격자는 참여하지 않는다.
                if (!world.TryGet<SimTransform>(attackerEntity, out var transform)) continue;

                var attack = world.Get<AttackState>(attackerEntity);

                // 쿨다운을 **먼저** 굴린다.
                if (attack.cooldownRemaining > 0f)
                    attack.cooldownRemaining = SimMath.Max(0f, attack.cooldownRemaining - dt);

                // combat-action-lock — Sleep/Stun 은 공격 **START** 만 막는다. 쿨다운 틱은 위에서
                // 이미 굴렸고(→ 깨어나면 즉시 공격) 진행 중 스윙의 RESOLVE 도 완료된다.
                // leap-flight-state — 도약 비행도 **같은 술어에 OR 로 합류**한다. 쿼리에서 빼면
                // 쿨다운 틱과 진행 중 스윙까지 얼어붙어 CC 와 규약이 갈린다.
                bool actionLocked =
                    CcActionLock.IsLocked(world.GetBuffer<CcEffect>(attackerEntity))
                    || world.Has<LeapFlight>(attackerEntity);

                // bomb-thrower-defender — 폭탄맨은 타겟 없이 쿨다운마다 방향×N 칸에 폭탄을 굴린다
                // (blind bombardment). 타겟팅/RESOLVE 경로를 타지 않으므로 여기서 끝낸다.
                if (world.Has<BombLauncherState>(attackerEntity))
                {
                    RunBombThrower(world, attackerEntity, ref attack, transform.Position, actionLocked,
                                   tileSize, gridSize, ffOrigin);
                    world.Set(attackerEntity, attack);
                    continue;
                }

                // summon-patrol-defender — 소환사는 **타겟을 고르지 않고** 순찰병을 유지한다.
                // 폭탄맨과 같은 자리(타겟 선정 앞)에서 끝낸다 — 타겟을 요구하는 RESOLVE 에 두면
                // 소환사의 근접 사거리 안에 적이 들어와야만 소환돼, 순찰병이 마중 나갈 시간이 없다.
                if (world.Has<SummonerState>(attackerEntity))
                {
                    RunSummoner(world, attackerEntity, ref attack, transform.Position, actionLocked,
                                tileSize, gridSize, ffOrigin);
                    world.Set(attackerEntity, attack);
                    continue;
                }

                world.Set(attackerEntity, attack);
            }
        }

        /// <summary>
        /// arm C/2 — 소환사 분기. 순찰병 **1기를 유지**하는 것이 이 유닛의 공격이다.
        ///
        /// ⚠ **첫 소환에만 거점 구역 게이트**가 걸린다 — 폭탄맨의 blind bombardment 를 그대로
        /// 따르지 않는 지점이다. 판정 중심은 **소환사 셀**이다: 실제 거점은 소비 지점이 walk 셀로
        /// 스냅해 정하는데 첫 소환 전엔 그게 아직 없고, 스냅 상한이 leash 반경이라 소환사 셀 기준
        /// 구역이 실제 구역을 보수적으로 감싼다. 그래서 요청의 `ownerCell` 도 **같은 셀**이다.
        ///
        /// ⚠ 게이트가 닫혀 있으면 **쿨다운을 리셋하지 않는다** — 만료 상태로 대기하다 적이 구역에
        /// 들어온 프레임에 즉시 소환한다("구역에 들어오면 부른다" 가 규칙이므로 리셋하면 최대 한 쿨
        /// 늦게 반응한다). 그 대가로 게이트가 닫힌 소환사는 매 프레임 타겟 스냅샷을 훑지만, 진영
        /// 미스매치를 즉시 건너뛰는 짧은 루프이고 게이트는 첫 소환 한 번뿐이다.
        ///
        /// ⚠ 이 함수는 <see cref="SummonerState"/> 를 **쓰지 않는다**. `hasSummonedOnce` 의 writer 는
        /// **순찰병이 실제로 생성된 시점** 하나다 — 요청을 stage 할 때 켜면 스냅 실패로 소환이 취소된
        /// 경우에도 게이트가 소비된다.
        /// </summary>
        private void RunSummoner(
            SimWorld world, SimEntityId attackerEntity, ref AttackState attack, SimVec3 sPos,
            bool actionLocked, float tileSize, SimInt2 gridSize, SimVec3 ffOrigin)
        {
            if (actionLocked || attack.cooldownRemaining > 0f) return;

            var summoner = world.Get<SummonerState>(attackerEntity);

            // ⚠ **양방향 대칭 생존 술어.** `current` 가 `Null` 이 아닌지만 보면 파괴된 순찰병의
            //   stale 핸들로 소환사가 영구 대기한다.
            bool alivePatrol = !summoner.current.IsNull
                && world.Exists(summoner.current)
                && !world.Has<DeadTag>(summoner.current)
                && world.Has<Health>(summoner.current)
                && world.Get<Health>(summoner.current).value > 0f;

            SimInt2 sCell = GridMath.WorldToCell(sPos, tileSize, gridSize, ffOrigin);
            bool gateOpen = summoner.hasSummonedOnce;
            if (!gateOpen && !alivePatrol && summoner.patrolDataIndex >= 0)
            {
                for (int ti = 0; ti < _targetEntities.Count && !gateOpen; ti++)
                {
                    if (((int)_targetFactions[ti] & (int)Faction.Enemy) == 0) continue;
                    if (world.Has<PastGoalTag>(_targetEntities[ti])) continue; // 유출 대기 적은 부르는 이유가 못 된다
                    SimInt2 eCell = GridMath.WorldToCell(_targetPositions[ti], tileSize, gridSize, ffOrigin);
                    if (PatrolAreaMath.IsInArea(eCell, sCell, summoner.leashTileRadius))
                        gateOpen = true;
                }
            }

            if (gateOpen && !alivePatrol && summoner.patrolDataIndex >= 0)
            {
                var req = new PatrolSpawnRequest
                {
                    owner = attackerEntity,
                    ownerCell = sCell, // 게이트 판정과 **같은 셀**이어야 한다
                    patrolDataIndex = summoner.patrolDataIndex,
                    leashTileRadius = summoner.leashTileRadius,
                };
                _ecb.Defer(w =>
                {
                    var carrier = w.Create();
                    w.Set(carrier, new PatrolRequestCarrier());
                    w.Set(carrier, req);
                });
                // 소환 = 이 유닛의 공격 사건. 애니/SFX 는 여기서 신호한다.
                _channels.UnitAttackVisual.Enqueue(new UnitAttackVisualEvent
                {
                    attacker = attackerEntity,
                    targetWorld = sPos,
                    attackAnimPeriod = attack.cooldownDuration,
                });
            }

            // 게이트가 열렸으면 성사 여부와 무관하게 리셋한다(스냅 실패로 취소된 경우 포함) —
            // 요청을 stage 한 프레임에 이미 리셋되므로 드레인이 한 프레임 늦어도 중복 소환이 없다.
            if (gateOpen) attack.cooldownRemaining = attack.cooldownDuration;
        }

        /// <summary>
        /// arm C/1 — 폭탄맨 분기. **타겟을 고르지 않는다**(계약 2, blind bombardment).
        ///
        /// ⚠ 쿨다운 리셋은 `landValid` **밖**이다 — 격자 밖을 보고 있어 발사가 거절된 프레임도
        /// 쿨다운은 돈다(재스캔 스팸 방지). 반대로 **공격 사건은 `landValid` 안**에서만 난다:
        /// 폭탄이 실제로 손을 떠난 프레임만 `AttackN` 1카운트다.
        ///
        /// ⚠ `rng` write-back 도 `landValid` 안이다 — 거절된 프레임은 draw 자체가 없어 스트림이
        /// 전진하지 않는다. 한 draw 라도 어긋나면 그 뒤 모든 확률 판정이 갈린다.
        /// </summary>
        private void RunBombThrower(
            SimWorld world, SimEntityId attackerEntity, ref AttackState attack, SimVec3 bPos,
            bool actionLocked, float tileSize, SimInt2 gridSize, SimVec3 ffOrigin)
        {
            if (actionLocked || attack.cooldownRemaining > 0f) return;
            if (!world.TryGet<DeployedFacing>(attackerEntity, out var facing)) return;
            if (!world.TryGet<ProjectileRef>(attackerEntity, out var bProjRef)) return;

            var bomb = world.Get<BombLauncherState>(attackerEntity);
            SimInt2 bCasterCell = GridMath.WorldToCell(bPos, tileSize, gridSize, ffOrigin);
            BombLanding.ResolveCell(bCasterCell, facing.value, bomb.landingTiles, gridSize,
                                    out SimInt2 landCell, out bool landValid);
            if (landValid)
            {
                SimVec3 landWorld = GridMath.CellToWorldCenter(landCell, tileSize, 0f, ffOrigin);
                // 3종 균등(1/3): 0 피해 · 1 수면 · 2 스턴. 캐스터별 rng advance.
                int bombType = bomb.rng.NextInt(0, 3);
                world.Set(attackerEntity, bomb); // rng 상태 저장
                float bDamage = 0f; byte bCcKind = 0; float bCcDur = 0f;
                if (bombType == 0) bDamage = bomb.dmgBombDamage;
                else if (bombType == 1) { bCcKind = (byte)CcKind.Sleep; bCcDur = bomb.sleepSec; }
                else { bCcKind = (byte)CcKind.Stun; bCcDur = bomb.stunSec; }

                // ⚠ 요청이 **캐리어가 아니라 공격자 본인**에 붙는다 — 주 발사의 자리다
                // (`ProjectileRequestCarrier` 는 같은 프레임의 **부가** 발사용).
                _ecb.Set(attackerEntity, new ProjectileSpawnRequest
                {
                    movement = MovementKind.GrenadeToCell,
                    payload = PayloadKind.TileAoe,
                    origin = bPos,
                    impact = landWorld,
                    impactTileRange = bomb.aoeTileRange,
                    aoeTargetCap = bomb.aoeTargetCap,
                    flightTime = bomb.travelSec, // 거리 무관 고정 — 요청이 싣고 온다
                    fuseSec = bomb.fuseSec,
                    arcHeight = bomb.arcHeight,
                    damage = bDamage,
                    ccKind = bCcKind,
                    ccDuration = bCcDur,
                    bombType = (byte)bombType,
                    dataIndex = bProjRef.dataIndex,
                    visualScale = bProjRef.visualScale,
                    owner = attackerEntity,
                    targetFaction = ProjectileTargetFaction.Enemy,
                });

                // 던지기 애니 + facing(착지셀 방향). ⚠ `attackAnimPeriod` 가 **속도 배율을 타지
                // 않은 raw `cooldownDuration`** 이다 — START 경로와 다르고, 구 sim 의 값이다.
                _channels.UnitAttackVisual.Enqueue(new UnitAttackVisualEvent
                {
                    attacker = attackerEntity,
                    targetWorld = landWorld,
                    attackAnimPeriod = attack.cooldownDuration,
                });

                // 폭탄이 **실제로 손을 떠난** 프레임만 1카운트. 이 host 는 RESOLVE 로 가지
                // 않으므로 여기가 유일한 사건 지점이다(계약 1·2).
                FireAttackNSlots(world, attackerEntity, bPos, bCasterCell,
                                 SimWarningCode.BombThrowUnhandledPayload, tileSize, gridSize, ffOrigin);
            }

            // 발사 성사/off-grid 무관 쿨다운 리셋(blind bombardment, 재스캔 스팸 방지).
            attack.cooldownRemaining = attack.cooldownDuration;
        }

        /// <summary>
        /// **host 가 대상을 주지 않는** 사건 지점의 `AttackN` 슬롯 처리 — 캐스트 드레인과
        /// 폭탄 발사가 공유한다. 구 sim 은 이 블록을 두 곳에 복붙했고 차이는 경고 문구뿐이었다.
        ///
        /// ⚠ **RESOLVE 는 이 헬퍼를 쓰지 않는다**(arm E). 거기는 host 가 대상을 확정해 주고
        /// 게이트(`DcTrigger.GatePass`)도 평가하므로 같은 함수가 아니다 — 억지로 합치면
        /// "게이트를 볼지" 가 인자로 새어나와 두 계약이 한 함수에서 갈린다.
        /// </summary>
        private void FireAttackNSlots(
            SimWorld world, SimEntityId host, SimVec3 origin, SimInt2 hostCell,
            SimWarningCode unhandled, float tileSize, SimInt2 gridSize, SimVec3 ffOrigin)
        {
            var slots = world.GetBuffer<DcTriggerSlot>(host);
            if (slots == null) return;

            for (int si = 0; si < slots.Count; si++)
            {
                var slot = slots[si];
                if (slot.trigger != DcTriggerKind.AttackN) continue;
                // ⚠ 게이트를 보지 않는다 — 처형타(AttackN × EventTarget)는 대상이 있는
                //   RESOLVE 전용이고, 여기 host 들은 대상을 모른다.
                ushort cc = slot.counter;
                bool fired = DcTrigger.Tick(ref cc, slot.period);
                slot.counter = cc;
                slots[si] = slot;
                if (!fired) continue;

                // 발동 = 카운터 소비 성사. payload arm/대상 유무와 무관하게 신호한다.
                _channels.DcTriggerFired.Enqueue(new DcTriggerFiredEvent { host = host });

                // 발동했는데 arm 이 없으면 loud fail — 조용히 카운트만 태우는 것이 이 spec 이
                // 없애려는 병이다(RESOLVE 의 unhandled 규율과 대칭).
                if (slot.payload != DcPayloadKind.ProjectileToTarget)
                {
                    _channels.Warnings.Enqueue(new SimWarning
                    {
                        code = unhandled,
                        entity = host,
                        detail = (int)slot.payload,
                    });
                    continue;
                }

                int pick = PickFallbackTarget(world, host, origin, hostCell,
                                              tileSize, gridSize, ffOrigin, slot.tileRange);
                // pick < 0 = 반경 안에 적이 없다. 카운트는 이미 소비됐다(계약 5).
                if (pick >= 0)
                    SpawnNeedleCarrier(slot, host, origin,
                                       _targetEntities[pick], _targetPositions[pick]);
            }
        }

        /// <summary>
        /// 공격 가능한 대상 풀. 통합 공격자 루프가 **같은 후보 풀**을 쓰고 각자
        /// `AttackState.targetMask` 로 거른다.
        ///
        /// ⚠ 이탈(판 밖) 중인 유닛은 후보에서 빠진다(`UltimateLeapState`) — 화면 밖 보스를 겨누면
        /// 방어유닛들이 빈 타일에 사격하고 데미지 숫자가 허공에 뜬다.
        /// **`LeapFlight` 는 여기 없다** — 일반 도약은 비행 중에도 계속 맞는다.
        /// </summary>
        private void BuildTargetSnapshot(SimWorld world)
        {
            _targetEntities.Clear();
            _targetPositions.Clear();
            _targetFactions.Clear();
            foreach (var e in world.With<FactionTag>())
            {
                if (!world.Has<Health>(e)) continue;
                if (world.Has<PendingDeployment>(e)) continue;
                if (world.Has<DeadTag>(e)) continue;
                if (world.Has<UltimateLeapState>(e)) continue;
                if (!world.TryGet<SimTransform>(e, out var xf)) continue;
                _targetEntities.Add(e);
                _targetPositions.Add(xf.Position);
                _targetFactions.Add(world.Get<FactionTag>(e).value);
            }
        }

        /// <summary>
        /// arm B — attack-decoupling unit 4, 캐스트 사건 드레인(Effects→Combat).
        ///
        /// 공격자 루프 **앞**에서 처리한다: ① 후보 스냅샷·ecb 를 그대로 재사용하고 ② 카운터 변경이
        /// 루프 바깥에서 끝나 HeavyStrike pre-scan 합성 불변식에 영향이 없다. 신규 시스템 0.
        ///
        /// ⚠ **같은 틱 소비가 계약**이다 — `HazardCastSystem` 은 #18(P5) 이고 이 시스템은
        /// #33(P8) 이라 파이프라인이 그것을 강제한다(구 sim 의 `[UpdateBefore]` 자리).
        /// </summary>
        private void DrainCastEvents(SimWorld world, float tileSize, SimInt2 gridSize, SimVec3 ffOrigin)
        {
            var casts = _channels.Cast.Drain();
            for (int ci = 0; ci < casts.Count; ci++)
            {
                var castEvt = casts[ci];
                // stale 드롭 — enqueue 후 드레인 전에 캐스터가 죽는 창이 있다. 카드가 없는
                // 캐스터도 같은 조건으로 걸러진다(생산자 게이트와 이중 방어).
                if (!world.HasBuffer<DcTriggerSlot>(castEvt.caster)) continue;
                // ⚠ 캐스트가 이 host 의 이번 프레임 "공격 사건" 이다(캐스트 우선). 발동 슬롯이
                //   하나도 없어도 **사건은 났다** — 아래 슬롯 루프의 성과와 무관하게 기록한다.
                _castCountedHosts.Add(castEvt.caster);

                SimInt2 casterCell = GridMath.WorldToCell(castEvt.casterPos, tileSize, gridSize, ffOrigin);
                FireAttackNSlots(world, castEvt.caster, castEvt.casterPos, casterCell,
                                 SimWarningCode.CastEventUnhandledPayload, tileSize, gridSize, ffOrigin);
            }
        }

        /// <summary>
        /// host 가 대상을 확정해 주지 않는 아키타입(폭탄맨·캐스터)의 폴백 선정.
        ///
        /// 후보 조립이 구 sim 에서 두 곳에 복붙돼 있었고, 정작 실수하기 쉬운 부분(진영 마스크·
        /// 자기 제외·그리드 변환·`PastGoal`)이 테스트 밖에 남아 있었다 — 실제로 `PastGoalTag`
        /// 제외가 두 곳 모두에서 누락됐다.
        ///
        /// ⚠ 진영이 **Enemy 로 고정**돼 있다. 호출처가 전부 defender 게이트 안이라 니들의 재조준
        /// 후보 풀(적 전용)과 진영이 맞기 때문이다. 적이 니들을 쏘게 되는 날 이 전제가 깨지면
        /// 아군 오사가 되므로, 그때 진영을 인자로 올려야 한다.
        /// </summary>
        private int PickFallbackTarget(
            SimWorld world, SimEntityId self, SimVec3 selfPos, SimInt2 selfCell,
            float tileSize, SimInt2 gridSize, SimVec3 gridOrigin, int tileRange)
        {
            _needleScratch.Clear();
            for (int i = 0; i < _targetEntities.Count; i++)
            {
                var e = _targetEntities[i];
                bool eligible = e != self
                    && ((int)_targetFactions[i] & (int)Faction.Enemy) != 0
                    && !world.Has<PastGoalTag>(e); // 유출 대기 적에 니들을 낭비하지 않는다
                SimVec3 p = _targetPositions[i];
                SimInt2 c = GridMath.WorldToCell(p, tileSize, gridSize, gridOrigin);
                _needleScratch.Add(new NearestTargeting.Candidate
                {
                    eligible = eligible,
                    tileDist = GridMath.ChebyshevDistance(c, selfCell),
                    sqDist = SimMath.DistanceSq(selfPos, p),
                    simId = e.Value,
                });
            }
            return NearestTargeting.SelectNearest(_needleScratch, tileRange);
        }

        /// <summary>
        /// 니들 발사의 **단일 창구**. 캐리어 스폰이 구 sim 에서 세 곳(RESOLVE / 폭탄 발사 /
        /// 캐스트 드레인)에 복붙돼 있었고, `ProjectileSpawnRequest` 는 필드가 많아 방향탄 bounce
        /// 개통처럼 필드가 하나 늘 때 사본들이 조용히 뒤처졌다.
        ///
        /// ⚠ 생성이 <see cref="SimCommandBuffer.Defer"/> 인 것은 구 ECB 의 재생 시점을 그대로
        /// 옮긴 것이다(`ProjectileEmitterSystem` 선례). 발동은 N 회에 1번이라 hot path 가 아니다.
        /// </summary>
        private void SpawnNeedleCarrier(
            in DcTriggerSlot slot, SimEntityId owner, SimVec3 origin,
            SimEntityId target, SimVec3 targetPos)
        {
            var req = new ProjectileSpawnRequest
            {
                movement = MovementKind.HomingToEntity,
                payload = PayloadKind.SingleSplash,
                target = target,
                origin = origin,
                damage = slot.magnitude, // flat — 계약 7(공격자 damageMul 미적용)
                speed = slot.speed,
                hitThreshold = slot.hitThreshold,
                visualScale = slot.visualScale,
                dataIndex = slot.projectileDataIndex,
                owner = owner,
                // 대상이 맞기 전에 죽으면 같은 반경 안에서 다시 겨눈다. 니들은 N 회에 한 번
                // 나오는 자원이라 허공에 사라지면 그 주기가 통째로 버려진다.
                retargetTileRange = slot.tileRange,
            };
            _ecb.Defer(w =>
            {
                var carrier = w.Create();
                w.Set(carrier, req);
                w.Set(carrier, new ProjectileRequestCarrier());
            });

            _channels.AttackOutputLog.Enqueue(new AttackOutputLogEvent
            {
                attacker = owner,
                kind = AttackOutputKind.Damage,
                magnitude = slot.magnitude,
                duration = 0f,
                sourcePos = origin,
                targetPos = targetPos,
            });
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-I/2 — 공격 클러스터.
    ///
    /// 둘이 **두 phase 에 갈린다**(P5 · P8). 그 갈림이 계약이다 — 캐스트 성사(#18)가 공격
    /// 사건이고 그것을 카운터로 옮기는 것은 #33 이라, #18 → #33 이 **같은 틱 안에서 앞뒤**여야
    /// 한다. 뒤집히면 "가끔 한 프레임 늦게 나감" 이 된다.
    ///
    /// ⚠ **#18 이 여기 있는 이유**: 해저드 캐스트는 Effects 시스템이지만 자기 클러스터가 없었다
    /// (18-I/1 이 시스템만 옮기고 등록을 미뤘다). `SimPipeline` 은 **번호 중복만 막고 누락은
    /// 못 막으므로** 조립 지점이 늘어나기 전에 소비자(#33)와 같은 클러스터에 넣는다.
    /// </summary>
    public sealed class AttackCluster
    {
        public HazardCastSystem HazardCast { get; }
        public AttackSystem Attack { get; }

        public AttackCluster(SimChannels channels)
        {
            HazardCast = new HazardCastSystem(channels);
            Attack = new AttackSystem(channels);
        }

        public IEnumerable<SimStep> Steps()
        {
            yield return new SimStep(18, SimPhase.PostMoveCast, nameof(HazardCastSystem), HazardCast.Run);
            yield return new SimStep(33, SimPhase.Attack, nameof(AttackSystem), Attack.Run);
        }
    }
}
