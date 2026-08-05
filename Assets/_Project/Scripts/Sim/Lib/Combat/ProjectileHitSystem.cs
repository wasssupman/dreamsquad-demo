using System.Collections.Generic;
using Wassup.Sim.Effects;
using Wassup.Sim.Movement;
using Wassup.Sim.Units;

namespace Wassup.Sim.Combat
{
    /// <summary>
    /// battle-sim-extraction unit 18-H/3 — 캡처 #27(P6). 구 `ProjectileHitSystem` 이식.
    ///
    /// 착탄 축 전담: 이동(#26)이 세운 <see cref="ProjectileState.impactReached"/> 를 보고
    /// `PayloadKind` 로 분기한다.
    ///
    /// ⚠ **P6 라서 피해 정산(#34, P9)보다 앞이다** — 여기서 넣은 `IncomingDamage` 는 **같은 틱**에
    /// 소비되고, 모디파이어 enqueue 는 반입(#9, P2)이 이미 지나가 **다음 틱**에 적용된다.
    /// 구 sim 에서 둘 다 tie-break 산물이던 것을 phase 배치가 선언으로 고정한다.
    ///
    /// ⚠ **투사체를 소비하는 것도 여기다.** 살아남는 경우는 둘뿐이다 — 재조준한 바운스,
    /// 그리고 아직 관통 예산이 남은 비행 중 경로탄.
    /// </summary>
    public sealed class ProjectileHitSystem
    {
        private const float HitFlashDuration = 0.15f;

        private readonly SimChannels _channels;
        private readonly SimCommandBuffer _ecb = new SimCommandBuffer();

        // 틱마다 재사용하는 스냅샷 버퍼.
        private readonly List<SimEntityId> _aoeEntities = new List<SimEntityId>();
        private readonly List<SimVec3> _aoePositions = new List<SimVec3>();
        private readonly List<SimEntityId> _defenderEntities = new List<SimEntityId>();
        private readonly List<SimVec3> _defenderPositions = new List<SimVec3>();
        private readonly List<int> _sweptIdx = new List<int>();
        private readonly List<float> _sweptDist = new List<float>();
        private readonly List<int> _inRange = new List<int>();
        private readonly List<float> _inRangeDistSq = new List<float>();
        private readonly List<int> _selectedAoe = new List<int>();

        public ProjectileHitSystem(SimChannels channels) => _channels = channels;

        public void Run(SimWorld world)
        {
            // ── 피해자 풀 스냅샷 ────────────────────────────────────────────
            // ⚠ 판 밖(궁극기 이탈) 적은 splash/TileAoe 피해자도, 바운스 후보도 아니다.
            //   직격 호밍은 이미 target 을 들고 있어 여기서 안 걸러지지만, 그 피해는 #34 의
            //   버퍼 드랍이 잡는다 — 2중 방어가 아니라 **역할 분담**이다.
            _aoeEntities.Clear();
            _aoePositions.Clear();
            foreach (var e in world.With<AttackUnitTag>())
            {
                if (world.Has<UltimateLeapState>(e)) continue;
                if (!world.TryGet<SimTransform>(e, out var xf)) continue;
                _aoeEntities.Add(e);
                _aoePositions.Add(xf.Position);
            }

            // 진영 파라미터화된 TileAoe(보스 융단폭격)용 방어유닛 풀.
            // ⚠ splash 와 bounce 는 **의도적으로 적 풀만** 쓴다.
            _defenderEntities.Clear();
            _defenderPositions.Clear();
            foreach (var e in world.With<DefenderUnitTag>())
            {
                if (!world.TryGet<SimTransform>(e, out var xf)) continue;
                _defenderEntities.Add(e);
                _defenderPositions.Add(xf.Position);
            }

            // 격자 파라미터 — 필드가 없는 이른 프레임에도 안전한 기본값(구 sim 과 같다).
            bool hasFlowField = SimSingleton.TryGet<FlowFieldSingleton>(world, out var flowField);
            float tileSize = hasFlowField ? flowField.tileSize : 1f;
            SimInt2 gridSize = hasFlowField ? flowField.gridSize : new SimInt2(128, 128);
            SimVec3 ffOrigin = hasFlowField ? flowField.origin : default;

            foreach (var entity in world.With<ProjectileTag>())
            {
                if (!world.TryGet<ProjectileState>(entity, out var projectile)) continue;

                // ⚠ **경로탄은 지점 도착이 없다** — 비행 중 매 프레임 해결하므로 이 게이트를
                //   통과해야 한다. 그 arm 에서 `impactReached` 는 "사거리 끝" 이라는 뜻이고,
                //   마지막 스윕 뒤 소멸하라는 신호다.
                if (!projectile.impactReached && projectile.payload != PayloadKind.PathHit) continue;

                bool survives = false;

                // 투사체 단위 위협 게이트. 비행 중 사수가 죽었으면 방어유닛 검사에서 떨어져
                // 귀속이 버려진다 — 무해하다(리더 판정이 어차피 죽은 공격자를 뺀다).
                var threatOwner = projectile.owner;
                bool creditThreat = !threatOwner.IsNull && world.Has<DefenderUnitTag>(threatOwner);

                // 우선 피해: **정확히 그 대상**만 배율을 받는다(스플래시 2차는 기본값).
                var prioTarget = projectile.priorityTarget;
                float prioMul = projectile.priorityDamageMul > 0f ? projectile.priorityDamageMul : 1f;
                // 강공: 이 샷의 **모든** 피해 대상에 곱한다. 위와 곱셈으로 합성된다.
                float heavyMul = projectile.heavyDamageMul > 0f ? projectile.heavyDamageMul : 1f;

                switch (projectile.payload)
                {
                    case PayloadKind.SingleSplash:
                        survives = ResolveSingleSplash(world, entity, ref projectile,
                            threatOwner, creditThreat, prioTarget, prioMul, heavyMul,
                            tileSize, gridSize, ffOrigin);
                        break;

                    case PayloadKind.PathHit:
                        survives = ResolvePathHit(world, entity, ref projectile,
                            threatOwner, creditThreat, prioTarget, prioMul, heavyMul,
                            tileSize, gridSize, ffOrigin);
                        break;

                    case PayloadKind.TileAoe:
                        ResolveTileAoe(world, entity, projectile,
                            threatOwner, creditThreat, prioTarget, prioMul, heavyMul,
                            tileSize, gridSize, ffOrigin);
                        break;

                    default:
                        // 모르는 payload 는 해결하지 않는다. 이동 쪽 default 와 달리 **누수가
                        // 불가능하다** — 아래에서 무조건 소비되기 때문이다.
                        break;
                }

                if (!survives) _ecb.Destroy(entity);
            }

            _ecb.Playback(world);
        }

        // ── SingleSplash ─────────────────────────────────────────────────────

        private bool ResolveSingleSplash(SimWorld world, SimEntityId entity, ref ProjectileState projectile,
            SimEntityId threatOwner, bool creditThreat, SimEntityId prioTarget, float prioMul, float heavyMul,
            float tileSize, SimInt2 gridSize, SimVec3 ffOrigin)
        {
            var target = projectile.target;
            if (target.IsNull || !world.TryGet<SimTransform>(target, out var targetTransform)) return false;
            SimVec3 targetPos = targetTransform.Position;

            // 출력 버퍼가 있으면 그것이 **피해의 출처**다. 없으면 `state.damage` 폴백.
            var outputs = world.GetBuffer<AttackOutputElement>(entity);
            bool handledOutputs = outputs != null;
            if (handledOutputs)
            {
                for (int i = 0; i < outputs.Count; i++)
                {
                    var output = outputs[i].value;
                    switch (output.kind)
                    {
                        case AttackOutputKind.Damage:
                            if (world.HasBuffer<IncomingDamage>(target))
                            {
                                // 바운스는 홉마다 직격 대상이 바뀌므로 A→B→A 면 A 에 다시 적용된다.
                                float dmg = (target == prioTarget ? output.magnitude * prioMul : output.magnitude) * heavyMul;
                                AppendDamage(world, target, dmg, threatOwner, creditThreat);
                            }
                            break;

                        case AttackOutputKind.Heal:
                            if (world.HasBuffer<IncomingHeal>(target))
                                world.GetBuffer<IncomingHeal>(target).Add(new IncomingHeal { amount = output.magnitude });
                            break;

                        case AttackOutputKind.ApplyStat:
                            _channels.StatApply.Enqueue(new StatModifierApplyEvent
                            {
                                target = target,
                                stat = output.stat,
                                op = output.op,
                                magnitude = output.magnitude,
                                duration = output.duration,
                                // ⚠ 아래 ApplyStack 과 달리 여기는 **투사체 엔티티**가 source 다.
                                //   병합 키가 (source, stat, op, stackId) 라 발사마다 새 슬롯이 생겨
                                //   곱연산이 누적된다(×0.6 → 0.6ⁿ). 지금 고치지 않는 이유는 라이브
                                //   밸런스가 바뀌기 때문 — 현재는 모디파이어 클램프가 병리를 경계한다.
                                //   수치 재조정과 한 묶음으로 별도 처리한다.
                                source = entity,
                                stackId = 0,
                                origin = ModifierOrigin.OnHit,
                            });
                            break;

                        case AttackOutputKind.ApplyStack:
                            _channels.StackApply.Enqueue(new StackModifierApplyEvent
                            {
                                target = target,
                                kind = output.stackKind,
                                countDelta = (byte)SimMath.Max(1f, output.magnitude),
                                maxStack = output.stackMaxStack > 0 ? output.stackMaxStack : StackDefaults.MaxStack,
                                perAppDuration = output.duration,
                                // ⚠ 병합 키가 (source, kind) 인데 투사체는 발사마다 새 엔티티다 —
                                //   그걸 실으면 매 히트가 새 슬롯을 만들어 스택이 영원히 1 이고
                                //   임계에 **절대 도달하지 못한다**. 그래서 source 는 **사수**다
                                //   (근접 경로와 같은 규약). Null 폴백은 브리지 캐스트 투사체용.
                                source = !threatOwner.IsNull ? threatOwner : entity,
                            });
                            break;
                    }
                }
            }

            if (!handledOutputs && world.HasBuffer<IncomingDamage>(target))
            {
                float dmg = (target == prioTarget ? projectile.damage * prioMul : projectile.damage) * heavyMul;
                AppendDamage(world, target, dmg, threatOwner, creditThreat);
            }

            // ⚠ **직격 대상당 하나**의 연출 이벤트 — 스플래시 2차는 추가 VFX 를 받지 않는다(의도).
            _channels.ProjectileHit.Enqueue(new ProjectileHitEvent
            {
                position = targetPos,
                dataIndex = projectile.dataIndex,
                payload = PayloadKind.SingleSplash,
                source = entity,
            });

            // 스플래시: 직격 대상 반경 안의 **다른** 적에게 감쇠 피해(중복 피해 방지로 직격 제외).
            if (projectile.onHitEffect == OnHitEffectType.Splash && projectile.splashRadius > 0f)
            {
                float splashRadiusSq = projectile.splashRadius * projectile.splashRadius;
                float splashDamage = projectile.damage * projectile.splashDamageMul * heavyMul;
                for (int i = 0; i < _aoeEntities.Count; i++)
                {
                    var candidate = _aoeEntities[i];
                    if (candidate == target) continue;
                    float dx = _aoePositions[i].x - targetPos.x;
                    float dz = _aoePositions[i].z - targetPos.z;
                    if (dx * dx + dz * dz > splashRadiusSq) continue;
                    if (world.HasBuffer<IncomingDamage>(candidate))
                        AppendDamage(world, candidate, splashDamage, threatOwner, creditThreat);
                }
            }

            FlashVictim(world, target);

            // ── 바운스: **해결 후** 생존 ────────────────────────────────────
            // 위 해결(피해/VFX/플래시)은 그대로 돌았다. 이제 남은 홉과 재조준 후보가 있으면
            // **같은 엔티티**를 다시 겨눈다 — 뷰/트레일 연속성이 공짜로 따라온다.
            if (projectile.bounceRemaining > 0)
            {
                // 방금 맞은 대상을 스냅샷 인덱스로 제외한다. 피해가 지연(버퍼)이라 아직 살아 있다.
                int excludeIdx = -1;
                for (int i = 0; i < _aoeEntities.Count; i++)
                    if (_aoeEntities[i] == target) { excludeIdx = i; break; }

                int nextIdx = BounceRetarget.FindNext(
                    targetPos, excludeIdx, _aoePositions, projectile.bounceTileRange, tileSize, gridSize, ffOrigin);

                if (nextIdx >= 0)
                {
                    float mul = projectile.bounceDamageMul;
                    var next = projectile;
                    next.target = _aoeEntities[nextIdx];
                    next.impactReached = false;
                    next.bounceRemaining = projectile.bounceRemaining - 1;
                    next.damage = projectile.damage * mul;
                    _ecb.Set(entity, next);

                    // ⚠ 출력 버퍼의 Damage 도 같이 감쇠시킨다 — 버퍼가 있으면 **그게** 피해의
                    //   출처이지 `state.damage` 가 아니다. 여기를 빼면 바운스가 감쇠하지 않는다.
                    if (mul != 1f && outputs != null)
                    {
                        for (int oi = 0; oi < outputs.Count; oi++)
                        {
                            var e = outputs[oi];
                            if (e.value.kind != AttackOutputKind.Damage) continue;
                            e.value.magnitude *= mul;
                            outputs[oi] = e;
                        }
                    }
                    return true;
                }
            }
            return false;
        }

        // ── PathHit ──────────────────────────────────────────────────────────

        private bool ResolvePathHit(SimWorld world, SimEntityId entity, ref ProjectileState projectile,
            SimEntityId threatOwner, bool creditThreat, SimEntityId prioTarget, float prioMul, float heavyMul,
            float tileSize, SimInt2 gridSize, SimVec3 ffOrigin)
        {
            if (!world.TryGet<SimTransform>(entity, out var selfTransform)) return false;

            var prev = new SimVec2(projectile.prevPos.x, projectile.prevPos.z);
            var curr = new SimVec2(selfTransform.Position.x, selfTransform.Position.z);
            SimVec2 dir = projectile.direction;
            float radius = projectile.hitThreshold;
            int budget = projectile.pierceRemaining;
            float dmg = projectile.damage;

            // 방향탄 바운스는 **관통을 다 쓴 지점**(마지막 피해자)에서 튕긴다.
            int lastVictimIdx = -1;
            SimVec3 lastVictimPos = default;

            var records = world.GetBuffer<PathHitRecord>(entity);
            _sweptIdx.Clear();
            _sweptDist.Clear();
            for (int i = 0; i < _aoeEntities.Count; i++)
            {
                var victimPos = new SimVec2(_aoePositions[i].x, _aoePositions[i].z);
                if (!SweepHitMath.SegmentHits(prev, curr, victimPos, radius)) continue;
                if (PathHitRecord.Contains(records, _aoeEntities[i])) continue;
                _sweptIdx.Add(i);
                _sweptDist.Add(SimMath.Dot(victimPos - prev, dir));
            }

            // ⚠ **앞쪽부터** 처리한다 — 관통 1 짜리는 자기가 지난 적 중 가장 가까운 적에서
            //   멈춰야 하고, 스냅샷 순서에는 아무 의미가 없다.
            while (budget > 0 && _sweptIdx.Count > 0)
            {
                int nearest = 0;
                for (int k = 1; k < _sweptIdx.Count; k++)
                    if (_sweptDist[k] < _sweptDist[nearest]) nearest = k;

                var victim = _aoeEntities[_sweptIdx[nearest]];
                SimVec3 victimPos = _aoePositions[_sweptIdx[nearest]];
                if (world.HasBuffer<IncomingDamage>(victim))
                {
                    float vdmg = (victim == prioTarget ? dmg * prioMul : dmg) * heavyMul;
                    AppendDamage(world, victim, vdmg, threatOwner, creditThreat);
                }
                (records ?? world.AddBuffer<PathHitRecord>(entity)).Add(new PathHitRecord { value = victim });
                records = world.GetBuffer<PathHitRecord>(entity);

                _channels.ProjectileHit.Enqueue(new ProjectileHitEvent
                {
                    position = victimPos,
                    dataIndex = projectile.dataIndex,
                    payload = PayloadKind.PathHit,
                    source = entity,
                });

                FlashVictim(world, victim);

                lastVictimIdx = _sweptIdx[nearest];
                lastVictimPos = victimPos;

                budget--;
                RemoveAtSwapBack(_sweptIdx, nearest);
                RemoveAtSwapBack(_sweptDist, nearest);
            }

            // 예산 소진 = 다 썼다 · `impactReached` = 사거리를 다 날았다.
            var next = projectile;
            next.pierceRemaining = budget;
            bool dirty = budget != projectile.pierceRemaining;
            bool survives = budget > 0 && !projectile.impactReached;

            // ── 방향탄 × 바운스 ────────────────────────────────────────────
            // 더 뚫을 수 없게 된 순간(예산 소진 또는 사거리 끝) 홉이 남아 있으면 마지막으로
            // 맞힌 적에서 다음 적으로 **호밍 전환**해 재비행한다. 같은 엔티티를 유지하므로
            // 뷰/트레일이 이어진다.
            //
            // ⚠ 계약 둘:
            //  · **마지막 히트 프레임에서만** 튕긴다. 아무도 못 맞히고 사거리 끝에 닿으면 튕길
            //    기준점이 없으므로 그대로 소멸한다(프레임을 넘겨 기억하는 상태를 만들지 않는다).
            //  · **히트 기록을 승계하지 않는다.** 전환 후엔 단일 착탄이라 그 버퍼를 읽지 않는다 —
            //    관통 2 이상 탄이 A→B 를 뚫고 B 에서 A 로 다시 튕길 수 있다(단일 착탄 바운스의
            //    A→B→A 선례와 같다).
            if (!survives && next.bounceRemaining > 0 && lastVictimIdx >= 0)
            {
                int nextIdx = BounceRetarget.FindNext(
                    lastVictimPos, lastVictimIdx, _aoePositions, next.bounceTileRange, tileSize, gridSize, ffOrigin);
                if (nextIdx >= 0)
                {
                    next.movement = MovementKind.HomingToEntity;
                    next.payload = PayloadKind.SingleSplash;
                    next.target = _aoeEntities[nextIdx];
                    next.impactReached = false;
                    next.bounceRemaining -= 1;
                    next.damage *= next.bounceDamageMul;

                    // ⚠ 출력 스냅샷을 **떼어** Damage-only 계약을 유지한다. 경로 arm 은
                    //   `state.damage` 하나만 쓰지만 단일 착탄 arm 은 출력이 있으면 전 kind 를
                    //   디스패치한다 — 그대로 두면 "경로 히트엔 안 걸리던 슬로우가 바운스 홉에만
                    //   걸리는" 비대칭이 생긴다. 저작이 방향탄에 상태이상 출력을 붙이는 순간
                    //   코드 변경 없이 열리는 구멍이라 여기서 닫는다.
                    if (world.HasBuffer<AttackOutputElement>(entity))
                        _ecb.Defer(w => w.RemoveBuffer<AttackOutputElement>(entity));

                    dirty = true;
                    survives = true;
                }
            }
            if (dirty) _ecb.Set(entity, next);
            return survives;
        }

        // ── TileAoe ──────────────────────────────────────────────────────────

        private void ResolveTileAoe(SimWorld world, SimEntityId entity, ProjectileState projectile,
            SimEntityId threatOwner, bool creditThreat, SimEntityId prioTarget, float prioMul, float heavyMul,
            float tileSize, SimInt2 gridSize, SimVec3 ffOrigin)
        {
            SimVec3 impactWorld = projectile.impact;
            SimInt2 centerCell = GridMath.WorldToCell(impactWorld, tileSize, gridSize, ffOrigin);
            int tileRange = projectile.impactTileRange;
            float dmg = projectile.damage;

            bool hitsDefenders = projectile.targetFaction == ProjectileTargetFaction.Defender;
            var victims = hitsDefenders ? _defenderEntities : _aoeEntities;
            var victimPositions = hitsDefenders ? _defenderPositions : _aoePositions;

            _inRange.Clear();
            _inRangeDistSq.Clear();
            for (int i = 0; i < victims.Count; i++)
            {
                SimVec3 vpos = victimPositions[i];
                SimInt2 cell = GridMath.WorldToCell(vpos, tileSize, gridSize, ffOrigin);
                if (!TileAoe.IsInTileRange(cell, centerCell, tileRange)) continue;
                _inRange.Add(i);
                float dx = vpos.x - impactWorld.x;
                float dz = vpos.z - impactWorld.z;
                _inRangeDistSq.Add(dx * dx + dz * dz);
            }
            AoeTargetCap.SelectNearest(_inRangeDistSq, projectile.aoeTargetCap, _selectedAoe);

            byte bombCc = projectile.ccKind;
            float bombCcDur = projectile.ccDuration;
            for (int s = 0; s < _selectedAoe.Count; s++)
            {
                var victim = victims[_inRange[_selectedAoe[s]]];
                // 데미지탄만 `dmg > 0` — 수면/스턴탄은 피해 append 를 건너뛴다.
                if (dmg > 0f && world.HasBuffer<IncomingDamage>(victim))
                {
                    float vdmg = (victim == prioTarget ? dmg * prioMul : dmg) * heavyMul;
                    AppendDamage(world, victim, vdmg, threatOwner, creditThreat);
                }
                if (bombCc != 0)
                    _channels.EnemyCc.Enqueue(new EnemyCcEvent
                    {
                        target = victim,
                        effect = new CcEffect { kind = (CcKind)bombCc, remainingTime = bombCcDur },
                    });
            }

            // ⚠ 착탄 연출은 **대상이 아니라 셀**에 뜬다. 대상별 플래시도 없다 — AOE 가 N 명을
            //   동시에 번쩍이면 시각 소음이다(메테오 선례).
            _channels.ProjectileHit.Enqueue(new ProjectileHitEvent
            {
                position = impactWorld,
                dataIndex = projectile.dataIndex,
                payload = PayloadKind.TileAoe,
                radiusWorld = tileRange * tileSize,
                source = entity,
            });
        }

        // ── 공용 ─────────────────────────────────────────────────────────────

        private void AppendDamage(SimWorld world, SimEntityId victim, float amount,
                                  SimEntityId threatOwner, bool creditThreat)
        {
            world.GetBuffer<IncomingDamage>(victim).Add(new IncomingDamage { amount = amount, source = threatOwner });
            ThreatTable.TryCredit(_channels.ThreatHit, creditThreat, world, victim, threatOwner, amount);
        }

        /// ⚠ 연속 피격은 타이머만 갱신하고 `originalScale` 은 보존한다(덮어쓰면 영구히 커진다).
        private void FlashVictim(SimWorld world, SimEntityId victim)
        {
            if (world.TryGet<HitFlashTag>(victim, out var existing))
            {
                _ecb.Set(victim, new HitFlashTag
                {
                    remaining = HitFlashDuration,
                    duration = HitFlashDuration,
                    originalScale = existing.originalScale,
                });
            }
            else if (world.TryGet<SimTransform>(victim, out var xf))
            {
                _ecb.Set(victim, new HitFlashTag
                {
                    remaining = HitFlashDuration,
                    duration = HitFlashDuration,
                    originalScale = xf.Scale,
                });
            }
        }

        private static void RemoveAtSwapBack<T>(List<T> list, int index)
        {
            list[index] = list[list.Count - 1];
            list.RemoveAt(list.Count - 1);
        }
    }
}
