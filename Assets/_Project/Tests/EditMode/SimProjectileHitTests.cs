using System.Collections.Generic;
using NUnit.Framework;
using Wassup.Sim;
using Wassup.Sim.Combat;
using Wassup.Sim.Effects;
using Wassup.Sim.Movement;
using Wassup.Sim.Units;

namespace Wassup.Tests.EditMode
{
    /// <summary>
    /// battle-sim-extraction unit 18-H/3 — 착탄 축(#27) 이식의 오라클.
    ///
    /// 세 payload arm 이 **서로 다른 피해 출처**를 쓴다는 것이 이 시스템의 함정이다 —
    /// 단일 착탄은 출력 버퍼(있으면)·경로와 타일은 `state.damage`. 바운스 감쇠가 한쪽만
    /// 건드리면 조용히 안 깎이므로 양쪽을 각각 고정한다.
    /// </summary>
    public class SimProjectileHitTests
    {
        private SimWorld _world;
        private SimChannels _channels;
        private ProjectileHitSystem _sut;

        [SetUp]
        public void SetUp()
        {
            _world = new SimWorld(new SimConfig(1u, 1u));
            _channels = new SimChannels();
            _sut = new ProjectileHitSystem(_channels);
            _world.SetDeltaTime(0.1f);
        }

        private void Field(float tileSize = 1f)
        {
            var f = _world.Create();
            _world.Set(f, new FlowFieldSingleton
            {
                flow = new SimVec2[1], dist = new int[1],
                gridSize = new SimInt2(128, 128), tileSize = tileSize, origin = default,
            });
        }

        private SimEntityId Enemy(SimVec3 pos)
        {
            var e = _world.Create();
            _world.Set(e, new AttackUnitTag());
            _world.Set(e, SimTransform.FromPosition(pos));
            _world.AddBuffer<IncomingDamage>(e);
            return e;
        }

        private SimEntityId Defender(SimVec3 pos)
        {
            var e = _world.Create();
            _world.Set(e, new DefenderUnitTag());
            _world.Set(e, SimTransform.FromPosition(pos));
            _world.AddBuffer<IncomingDamage>(e);
            return e;
        }

        private SimEntityId Shot(ProjectileState state, SimVec3 pos)
        {
            var e = _world.Create();
            _world.Set(e, new ProjectileTag());
            _world.Set(e, state);
            _world.Set(e, SimTransform.FromPosition(pos));
            return e;
        }

        private float DamageOn(SimEntityId e)
        {
            var buf = _world.GetBuffer<IncomingDamage>(e);
            float sum = 0f;
            if (buf != null) for (int i = 0; i < buf.Count; i++) sum += buf[i].amount;
            return sum;
        }

        // ═════ SingleSplash ══════════════════════════════════════════════════

        [Test]
        public void SingleSplash_UsesStateDamage_WhenNoOutputBuffer()
        {
            var target = Enemy(new SimVec3(1, 0, 0));
            var shot = Shot(new ProjectileState
            {
                payload = PayloadKind.SingleSplash, target = target,
                damage = 25f, impactReached = true,
            }, new SimVec3(1, 0, 0));

            _sut.Run(_world);

            Assert.AreEqual(25f, DamageOn(target), 1e-4f);
            Assert.IsFalse(_world.Exists(shot), "바운스가 없으면 소비된다");
            Assert.AreEqual(1, _channels.ProjectileHit.Count);
        }

        [Test]
        public void SingleSplash_OutputBufferWins_OverStateDamage()
        {
            var target = Enemy(new SimVec3(1, 0, 0));
            var shot = Shot(new ProjectileState
            {
                payload = PayloadKind.SingleSplash, target = target,
                damage = 999f, impactReached = true,
            }, new SimVec3(1, 0, 0));
            _world.AddBuffer<AttackOutputElement>(shot).Add(new AttackOutputElement
            {
                value = new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 7f },
            });

            _sut.Run(_world);

            Assert.AreEqual(7f, DamageOn(target), 1e-4f, "버퍼가 있으면 그게 피해의 출처다");
        }

        [Test]
        public void SingleSplash_DispatchesEveryOutputKind()
        {
            var target = Enemy(new SimVec3(1, 0, 0));
            _world.AddBuffer<IncomingHeal>(target);
            var shot = Shot(new ProjectileState { payload = PayloadKind.SingleSplash, target = target, impactReached = true },
                            new SimVec3(1, 0, 0));
            var outs = _world.AddBuffer<AttackOutputElement>(shot);
            outs.Add(new AttackOutputElement { value = new AttackOutput { kind = AttackOutputKind.Heal, magnitude = 4f } });
            outs.Add(new AttackOutputElement { value = new AttackOutput { kind = AttackOutputKind.ApplyStat, stat = StatKind.MoveSpeedMul, magnitude = 0.5f, duration = 2f } });
            outs.Add(new AttackOutputElement { value = new AttackOutput { kind = AttackOutputKind.ApplyStack, stackKind = StackKind.Fire, magnitude = 1f, duration = 3f } });

            _sut.Run(_world);

            Assert.AreEqual(4f, _world.GetBuffer<IncomingHeal>(target)[0].amount, 1e-4f);
            var stat = _channels.StatApply.Drain()[0];
            Assert.AreEqual(ModifierOrigin.OnHit, stat.origin);
            Assert.AreEqual(shot, stat.source, "⚠ 스탯은 **투사체**가 source (알려진 누적 병리, 밸런스와 함께 처리)");
            var stack = _channels.StackApply.Drain()[0];
            Assert.AreEqual(StackDefaults.MaxStack, stack.maxStack, "저작 미지정 → 폴백 상한");
        }

        [Test]
        public void SingleSplash_StackSourceIsShooter_SoThresholdsCanEverBeReached()
        {
            // ⚠ 투사체를 source 로 실으면 병합 키가 매 히트 새 슬롯이라 스택이 영원히 1 이다.
            var shooter = Defender(new SimVec3(0, 0, 0));
            var target = Enemy(new SimVec3(1, 0, 0));
            var shot = Shot(new ProjectileState
            {
                payload = PayloadKind.SingleSplash, target = target, owner = shooter, impactReached = true,
            }, new SimVec3(1, 0, 0));
            _world.AddBuffer<AttackOutputElement>(shot).Add(new AttackOutputElement
            {
                value = new AttackOutput { kind = AttackOutputKind.ApplyStack, stackKind = StackKind.Fire, magnitude = 1f },
            });

            _sut.Run(_world);

            Assert.AreEqual(shooter, _channels.StackApply.Drain()[0].source, "사수가 source — 근접 경로와 같은 규약");
        }

        [Test]
        public void Splash_SkipsDirectTarget_AndRespectsRadius()
        {
            var target = Enemy(new SimVec3(0, 0, 0));
            var near = Enemy(new SimVec3(1f, 0, 0));
            var far = Enemy(new SimVec3(5f, 0, 0));
            Shot(new ProjectileState
            {
                payload = PayloadKind.SingleSplash, target = target, damage = 10f,
                onHitEffect = OnHitEffectType.Splash, splashRadius = 2f, splashDamageMul = 0.5f,
                impactReached = true,
            }, new SimVec3(0, 0, 0));

            _sut.Run(_world);

            Assert.AreEqual(10f, DamageOn(target), 1e-4f, "직격은 스플래시를 겹쳐 맞지 않는다");
            Assert.AreEqual(5f, DamageOn(near), 1e-4f);
            Assert.AreEqual(0f, DamageOn(far), 1e-4f);
            Assert.AreEqual(1, _channels.ProjectileHit.Count, "연출은 직격 하나뿐");
        }

        [Test]
        public void PriorityMultiplier_HitsOnlyThatVictim_HeavyHitsEveryone()
        {
            var target = Enemy(new SimVec3(0, 0, 0));
            var near = Enemy(new SimVec3(1f, 0, 0));
            Shot(new ProjectileState
            {
                payload = PayloadKind.SingleSplash, target = target, damage = 10f,
                onHitEffect = OnHitEffectType.Splash, splashRadius = 2f, splashDamageMul = 1f,
                priorityTarget = target, priorityDamageMul = 2f, heavyDamageMul = 3f,
                impactReached = true,
            }, new SimVec3(0, 0, 0));

            _sut.Run(_world);

            Assert.AreEqual(60f, DamageOn(target), 1e-4f, "10 × prio 2 × heavy 3");
            Assert.AreEqual(30f, DamageOn(near), 1e-4f, "스플래시는 heavy 만 — prio 는 그 대상 전용");
        }

        [Test]
        public void HitFlash_PreservesOriginalScale_OnBackToBackHits()
        {
            var target = Enemy(new SimVec3(0, 0, 0));
            _world.Set(target, new HitFlashTag { remaining = 0.01f, duration = 0.15f, originalScale = 1f });
            var t2 = _world.Get<SimTransform>(target);
            t2.Scale = 1.4f; // 이미 부푼 상태
            _world.Set(target, t2);

            Shot(new ProjectileState { payload = PayloadKind.SingleSplash, target = target, damage = 1f, impactReached = true },
                 new SimVec3(0, 0, 0));
            _sut.Run(_world);

            Assert.AreEqual(1f, _world.Get<HitFlashTag>(target).originalScale, 1e-4f,
                "⚠ 덮어쓰면 부푼 값이 새 원본이 되어 유닛이 영구히 커진다");
            Assert.AreEqual(0.15f, _world.Get<HitFlashTag>(target).remaining, 1e-4f, "타이머는 갱신");
        }

        [Test]
        public void Bounce_ReHomesSameEntity_AndDecaysBothDamageSources()
        {
            Field();
            var first = Enemy(new SimVec3(0, 0, 0));
            var second = Enemy(new SimVec3(1f, 0, 0));
            var shot = Shot(new ProjectileState
            {
                payload = PayloadKind.SingleSplash, target = first, damage = 100f,
                bounceRemaining = 2, bounceTileRange = 3, bounceDamageMul = 0.5f,
                impactReached = true,
            }, new SimVec3(0, 0, 0));
            _world.AddBuffer<AttackOutputElement>(shot).Add(new AttackOutputElement
            {
                value = new AttackOutput { kind = AttackOutputKind.Damage, magnitude = 40f },
            });

            _sut.Run(_world);

            Assert.IsTrue(_world.Exists(shot), "같은 엔티티가 살아남는다 — 뷰/트레일 연속");
            var st = _world.Get<ProjectileState>(shot);
            Assert.AreEqual(second, st.target);
            Assert.AreEqual(1, st.bounceRemaining);
            Assert.IsFalse(st.impactReached, "다음 홉을 위해 리셋");
            Assert.AreEqual(50f, st.damage, 1e-4f, "state.damage 감쇠");
            Assert.AreEqual(20f, _world.GetBuffer<AttackOutputElement>(shot)[0].value.magnitude, 1e-4f,
                "⚠ 출력 버퍼가 실제 출처다 — 여기를 안 깎으면 바운스가 감쇠하지 않는다");
        }

        [Test]
        public void Bounce_DiesWhenNoCandidateInRange()
        {
            Field();
            var only = Enemy(new SimVec3(0, 0, 0));
            var shot = Shot(new ProjectileState
            {
                payload = PayloadKind.SingleSplash, target = only, damage = 10f,
                bounceRemaining = 3, bounceTileRange = 2, bounceDamageMul = 1f, impactReached = true,
            }, new SimVec3(0, 0, 0));

            _sut.Run(_world);

            Assert.IsFalse(_world.Exists(shot), "홉이 남아도 후보가 없으면 소비된다");
        }

        // ═════ PathHit ═══════════════════════════════════════════════════════

        private SimEntityId PathShot(float damage, int pierce, SimVec3 prev, SimVec3 curr, bool ended = false)
        {
            var e = Shot(new ProjectileState
            {
                payload = PayloadKind.PathHit, movement = MovementKind.DirectionalLinear,
                damage = damage, pierceRemaining = pierce, hitThreshold = 0.5f,
                direction = new SimVec2(1f, 0f), prevPos = prev, impactReached = ended,
            }, curr);
            _world.AddBuffer<PathHitRecord>(e);
            return e;
        }

        [Test]
        public void PathHit_TakesFrontMostFirst_WithinPierceBudget()
        {
            // 스냅샷 순서는 의미가 없다 — 뒤쪽 적을 먼저 만들어 둔다.
            var back = Enemy(new SimVec3(4f, 0, 0));
            var front = Enemy(new SimVec3(1f, 0, 0));
            PathShot(10f, pierce: 1, prev: new SimVec3(0, 0, 0), curr: new SimVec3(5f, 0, 0));

            _sut.Run(_world);

            Assert.AreEqual(10f, DamageOn(front), 1e-4f, "관통 1 은 가장 앞에서 멈춘다");
            Assert.AreEqual(0f, DamageOn(back), 1e-4f);
        }

        [Test]
        public void PathHit_RecordPreventsReHitAcrossFrames()
        {
            var victim = Enemy(new SimVec3(1f, 0, 0));
            var shot = PathShot(10f, pierce: 5, prev: new SimVec3(0, 0, 0), curr: new SimVec3(2f, 0, 0));

            _sut.Run(_world);
            Assert.AreEqual(10f, DamageOn(victim), 1e-4f);

            // 같은 적이 여전히 반경 안 — 기록이 없으면 매 프레임 다시 맞는다.
            var st = _world.Get<ProjectileState>(shot);
            st.prevPos = new SimVec3(2f, 0, 0);
            _world.Set(shot, st);
            _sut.Run(_world);

            Assert.AreEqual(10f, DamageOn(victim), 1e-4f, "⚠ 대상당 최대 1회");
        }

        [Test]
        public void PathHit_SurvivesWhileBudgetRemainsAndFlightContinues()
        {
            var victim = Enemy(new SimVec3(1f, 0, 0));
            var shot = PathShot(10f, pierce: 3, prev: new SimVec3(0, 0, 0), curr: new SimVec3(2f, 0, 0));

            _sut.Run(_world);

            Assert.IsTrue(_world.Exists(shot));
            Assert.AreEqual(2, _world.Get<ProjectileState>(shot).pierceRemaining);
        }

        [Test]
        public void PathHit_DiesWhenRangeEnds_EvenWithBudgetLeft()
        {
            var shot = PathShot(10f, pierce: 3, prev: new SimVec3(0, 0, 0), curr: new SimVec3(2f, 0, 0), ended: true);
            _sut.Run(_world);
            Assert.IsFalse(_world.Exists(shot), "impactReached = 사거리 끝 = 소멸 신호");
        }

        [Test]
        public void PathHit_BounceConvertsToHoming_AndStripsOutputs()
        {
            Field();
            var hit = Enemy(new SimVec3(1f, 0, 0));
            var next = Enemy(new SimVec3(2f, 0, 0));
            var shot = PathShot(10f, pierce: 1, prev: new SimVec3(0, 0, 0), curr: new SimVec3(1.2f, 0, 0));
            var st = _world.Get<ProjectileState>(shot);
            st.bounceRemaining = 1; st.bounceTileRange = 3; st.bounceDamageMul = 0.5f;
            _world.Set(shot, st);
            _world.AddBuffer<AttackOutputElement>(shot).Add(new AttackOutputElement
            {
                value = new AttackOutput { kind = AttackOutputKind.ApplyStat, stat = StatKind.MoveSpeedMul, magnitude = 0.5f },
            });

            _sut.Run(_world);

            var after = _world.Get<ProjectileState>(shot);
            Assert.AreEqual(MovementKind.HomingToEntity, after.movement, "방향 → 호밍 전환");
            Assert.AreEqual(PayloadKind.SingleSplash, after.payload, "스윕 → 단일 착탄");
            Assert.AreEqual(next, after.target);
            Assert.AreEqual(5f, after.damage, 1e-4f);
            Assert.IsFalse(_world.HasBuffer<AttackOutputElement>(shot),
                "⚠ 출력을 떼지 않으면 경로 히트엔 안 걸리던 상태이상이 바운스 홉에만 걸린다");
        }

        [Test]
        public void PathHit_NoVictimThisFrame_MeansNoBounceAnchor()
        {
            Field();
            Enemy(new SimVec3(9f, 0, 0)); // 스윕 밖
            var shot = PathShot(10f, pierce: 1, prev: new SimVec3(0, 0, 0), curr: new SimVec3(1f, 0, 0), ended: true);
            var st = _world.Get<ProjectileState>(shot);
            st.bounceRemaining = 2; st.bounceTileRange = 20;
            _world.Set(shot, st);

            _sut.Run(_world);

            Assert.IsFalse(_world.Exists(shot), "튕길 기준점이 없으면 그대로 소멸 — 프레임 넘긴 기억은 없다");
        }

        // ═════ TileAoe ═══════════════════════════════════════════════════════

        [Test]
        public void TileAoe_HitsEveryEnemyInChebyshevRange_FromTheImpactCell()
        {
            Field();
            var inside = Enemy(new SimVec3(1f, 0, 1f));  // 체비셰프 1
            var outside = Enemy(new SimVec3(3f, 0, 0f)); // 체비셰프 3
            Shot(new ProjectileState
            {
                payload = PayloadKind.TileAoe, impact = new SimVec3(0, 0, 0),
                impactTileRange = 2, damage = 12f, impactReached = true,
            }, new SimVec3(0, 0, 0));

            _sut.Run(_world);

            Assert.AreEqual(12f, DamageOn(inside), 1e-4f);
            Assert.AreEqual(0f, DamageOn(outside), 1e-4f);
        }

        [Test]
        public void TileAoe_FactionSwitchesThePool()
        {
            Field();
            var enemy = Enemy(new SimVec3(0, 0, 0));
            var defender = Defender(new SimVec3(0, 0, 0));
            Shot(new ProjectileState
            {
                payload = PayloadKind.TileAoe, impact = new SimVec3(0, 0, 0),
                impactTileRange = 2, damage = 9f, impactReached = true,
                targetFaction = ProjectileTargetFaction.Defender,
            }, new SimVec3(0, 0, 0));

            _sut.Run(_world);

            Assert.AreEqual(9f, DamageOn(defender), 1e-4f);
            Assert.AreEqual(0f, DamageOn(enemy), 1e-4f, "기본값 Enemy 를 명시로 뒤집었다");
        }

        [Test]
        public void TileAoe_CapTakesNearestOnly_ZeroMeansUnlimited()
        {
            Field();
            var near = Enemy(new SimVec3(0.5f, 0, 0));
            var mid = Enemy(new SimVec3(1.2f, 0, 0));
            var far = Enemy(new SimVec3(1.8f, 0, 0));
            Shot(new ProjectileState
            {
                payload = PayloadKind.TileAoe, impact = new SimVec3(0, 0, 0),
                impactTileRange = 3, damage = 5f, aoeTargetCap = 2, impactReached = true,
            }, new SimVec3(0, 0, 0));

            _sut.Run(_world);

            Assert.AreEqual(5f, DamageOn(near), 1e-4f);
            Assert.AreEqual(5f, DamageOn(mid), 1e-4f);
            Assert.AreEqual(0f, DamageOn(far), 1e-4f, "cap 2 — 가까운 순으로 절단");
        }

        [Test]
        public void TileAoe_CcOnlyBomb_SkipsDamageAppend()
        {
            Field();
            var victim = Enemy(new SimVec3(0, 0, 0));
            Shot(new ProjectileState
            {
                payload = PayloadKind.TileAoe, impact = new SimVec3(0, 0, 0),
                impactTileRange = 2, damage = 0f, impactReached = true,
                ccKind = (byte)CcKind.Sleep, ccDuration = 2.5f,
            }, new SimVec3(0, 0, 0));

            _sut.Run(_world);

            Assert.AreEqual(0, _world.GetBuffer<IncomingDamage>(victim).Count, "데미지 0 이면 append 자체가 없다");
            var cc = _channels.EnemyCc.Drain();
            Assert.AreEqual(1, cc.Count);
            Assert.AreEqual(CcKind.Sleep, cc[0].effect.kind);
            Assert.AreEqual(2.5f, cc[0].effect.remainingTime, 1e-4f);
        }

        [Test]
        public void TileAoe_HitEventCarriesTheCell_NotAVictim()
        {
            Field(tileSize: 2f);
            Enemy(new SimVec3(0, 0, 0));
            Shot(new ProjectileState
            {
                payload = PayloadKind.TileAoe, impact = new SimVec3(6f, 0, 4f),
                impactTileRange = 3, damage = 1f, dataIndex = 5, impactReached = true,
            }, new SimVec3(0, 0, 0));

            _sut.Run(_world);

            var evt = _channels.ProjectileHit.Drain()[0];
            Assert.AreEqual(new SimVec3(6f, 0, 4f), evt.position, "대상이 아니라 착탄 셀");
            Assert.AreEqual(PayloadKind.TileAoe, evt.payload);
            Assert.AreEqual(6f, evt.radiusWorld, 1e-4f, "tileRange 3 × tileSize 2");
            Assert.AreEqual(5, evt.dataIndex);
        }

        // ═════ 위협 귀속 ══════════════════════════════════════════════════════

        [Test]
        public void ThreatCredit_RequiresDefenderOwnerAndVictimTable()
        {
            var shooter = Defender(new SimVec3(0, 0, 0));
            var boss = Enemy(new SimVec3(1f, 0, 0));
            _world.AddBuffer<ThreatEntry>(boss);
            Shot(new ProjectileState
            {
                payload = PayloadKind.SingleSplash, target = boss, damage = 30f,
                owner = shooter, impactReached = true,
            }, new SimVec3(1, 0, 0));

            // 표가 없는 일반 적 — 같은 사수가 쏴도 귀속되지 않는다.
            var mob = Enemy(new SimVec3(5f, 0, 0));
            Shot(new ProjectileState
            {
                payload = PayloadKind.SingleSplash, target = mob, damage = 30f,
                owner = shooter, impactReached = true,
            }, new SimVec3(5, 0, 0));

            // 브리지 캐스트(owner Null) — 보스를 때려도 귀속 없음.
            Shot(new ProjectileState
            {
                payload = PayloadKind.SingleSplash, target = boss, damage = 30f, impactReached = true,
            }, new SimVec3(1, 0, 0));

            _sut.Run(_world);

            var credits = _channels.ThreatHit.Drain();
            Assert.AreEqual(1, credits.Count, "표 보유 + 방어유닛 owner 조합만");
            Assert.AreEqual(boss, credits[0].victim);
            Assert.AreEqual(shooter, credits[0].attacker);
            Assert.AreEqual(30f, credits[0].amount, 1e-4f);
        }

        // ═════ 순수 계산 ══════════════════════════════════════════════════════

        [Test]
        public void AoeTargetCap_ZeroOrNegative_ReturnsEveryIndexInOrder()
        {
            var results = new List<int>();
            AoeTargetCap.SelectNearest(new List<float> { 9f, 1f, 4f }, 0, results);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, results, "무제한은 **인덱스 순서 그대로**");
            AoeTargetCap.SelectNearest(new List<float> { 9f, 1f }, -3, results);
            CollectionAssert.AreEqual(new[] { 0, 1 }, results);
        }

        [Test]
        public void AoeTargetCap_PicksNearest_TiesToLowerIndex()
        {
            var results = new List<int>();
            AoeTargetCap.SelectNearest(new List<float> { 9f, 1f, 4f }, 2, results);
            CollectionAssert.AreEqual(new[] { 1, 2 }, results);

            AoeTargetCap.SelectNearest(new List<float> { 5f, 5f, 5f }, 2, results);
            CollectionAssert.AreEqual(new[] { 0, 1 }, results, "동률은 앞 인덱스");
        }

        [Test]
        public void AoeTargetCap_CapAboveTotal_IsClamped()
        {
            var results = new List<int>();
            AoeTargetCap.SelectNearest(new List<float> { 2f }, 9, results);
            CollectionAssert.AreEqual(new[] { 0 }, results);
        }

        [Test]
        public void ThreatTable_LeaderIsHighestDamage_TiesToLowerSimId()
        {
            var a = _world.Create(); // simId 1
            var b = _world.Create(); // simId 2
            var entries = new List<ThreatEntry>
            {
                new ThreatEntry { attacker = b, cumulativeDamage = 50f },
                new ThreatEntry { attacker = a, cumulativeDamage = 50f },
            };
            var alive = new List<bool> { true, true };

            Assert.AreEqual(a, ThreatTable.Leader(entries, alive), "동률은 낮은 simId");

            entries[0] = new ThreatEntry { attacker = b, cumulativeDamage = 51f };
            Assert.AreEqual(b, ThreatTable.Leader(entries, alive));
        }

        [Test]
        public void ThreatTable_SkipsDeadAndNullAttackers()
        {
            var a = _world.Create();
            var entries = new List<ThreatEntry>
            {
                new ThreatEntry { attacker = a, cumulativeDamage = 99f },
                new ThreatEntry { attacker = SimEntityId.Null, cumulativeDamage = 999f },
            };
            Assert.AreEqual(SimEntityId.Null, ThreatTable.Leader(entries, new List<bool> { false, true }),
                "산 공격자가 없으면 Null — 호출부가 폴백한다");
            Assert.AreEqual(a, ThreatTable.Leader(entries, new List<bool> { true, true }));
        }

        [Test]
        public void ThreatTable_AccumulateFoldsPerAttacker()
        {
            var a = _world.Create();
            var b = _world.Create();
            var table = new List<ThreatEntry>();
            ThreatTable.Accumulate(table, a, 10f);
            ThreatTable.Accumulate(table, b, 5f);
            ThreatTable.Accumulate(table, a, 7f);

            Assert.AreEqual(2, table.Count, "공격자당 한 줄");
            Assert.AreEqual(17f, table[0].cumulativeDamage, 1e-4f, "감쇠 없이 누적");
        }
    }
}
