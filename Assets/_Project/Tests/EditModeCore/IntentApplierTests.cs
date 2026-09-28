using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;
using Wassup.BattleCore.Combat.Projectile;
using Wassup.BattleCore.Effects;
using Wassup.BattleCore.Trigger;
using Wassup.Skills;
using static Wassup.Tests.EditMode.Core.CoreTriggerFixtures;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 7a — 쓰기 표면: SimIntent 24 + MetaIntent 2 · 원자 개시(S19).
    //
    // 각 의도가 **이미 있는 관문**으로 배달되는지를 본다(판정은 관문의 것이다 — 여기서 새로 판정하지 않는다).
    [TestFixture]
    public class IntentApplierTests
    {
        private BattleMatch _m;
        private Unit _d, _e;
        private List<string> _said;

        private static SkillEntityId S(Unit u) => CoreSkillContext.ToSkill(u.Id);

        [SetUp]
        public void SetUp()
        {
            var def = CoreCombatFixtures.Definition(defenderDamage: 0f, enemyHealth: 500f);
            AddBlastProjectile(def);
            def.Projectiles[0].Movement = (int)MovementKind.HomingToEntity;
            def.Projectiles[0].Payload = (int)PayloadKind.SingleSplash;
            AddBlastProjectile(def);                 // 1 = 폭발
            def.Hazards = new[] { new HazardDef { Id = "fixture_zone", Radius = 1, Lifetime = 5f } };
            def.Patterns = new[]
            {
                new PatternDef
                {
                    Id = "fixture_pattern", BarrelProjectileDefIndex = 0, Damage = 7f,
                    Shots = new[] { new PatternShotDef { DirectionT = 0.5f } },
                },
            };
            def.ConfigHash = def.ComputeConfigHash();
            _m = CoreMatchFixtures.BeginBattle(def);
            _said = new List<string>();
            _m.Report = _said.Add;
            _d = SpawnDefender(_m, new int2(5, 2));
            _e = SpawnEnemy(_m, new int2(6, 2));
        }

        private void Apply(SimIntent i) => _m.Intents.Apply(in i);

        [Test]
        public void 피해는_출처를_실어_인박스로_간다()
        {
            Apply(new SimIntent { Kind = SimIntentKind.DealDamage, Target = S(_e), Source = S(_d), Amount = 12f });
            Assert.AreEqual(1, _e.Inbox.Damage.Count);
            Assert.AreEqual(_d.Id, _e.Inbox.Damage[0].Source, "스킬로 죽인 적도 처치 사건(점수·각성)을 낸다");
        }

        [Test]
        public void 회복_스탯_스택_지속피해는_부여_관문을_지난다()
        {
            _e.Health = 100f;
            Apply(new SimIntent { Kind = SimIntentKind.Heal, Target = S(_e), Amount = 5f });
            Assert.AreEqual(1, _e.Inbox.Heal.Count);

            Apply(new SimIntent
            {
                Kind = SimIntentKind.ApplyStatModifier, Target = S(_e), Source = S(_d),
                Selector = (int)SkillStatKind.MoveSpeedMul, Op = SkillCombineOp.FromAuthoredMultiplier,
                Origin = SkillModifierOrigin.OnPlace, Amount = 0.5f, Duration = 2f,
            });
            Assert.AreEqual(0.5f, _e.Modifiers.Effective.MoveSpeedMul, 1e-5f, "깎는 디버프 = 곱셈 버킷");

            Apply(new SimIntent { Kind = SimIntentKind.ApplyStack, Target = S(_e), Source = S(_d),
                                  Selector = (int)SkillStackKind.Bleed, Count = 2, Duration = 3f });
            Assert.AreEqual(2, _e.Stacks.CountOf(StackKind.Bleed));

            Apply(new SimIntent { Kind = SimIntentKind.ApplyDot, Target = S(_e), Source = S(_d),
                                  Amount = 3f, HitThreshold = 0.5f, Duration = 2f });
            Assert.AreEqual(1, _e.Dot.Count);
            Assert.AreEqual(DotOrigin.OnPlace, _e.Dot.Slots[0].Origin, "배치 스킬 파이프라인(2축 키의 출처)");
        }

        [Test]
        public void 군중_제어는_면역_관문을_지나고_감속_토큰은_말한다()
        {
            Apply(new SimIntent { Kind = SimIntentKind.ApplyCc, Target = S(_e), Source = S(_d),
                                  Selector = (int)SkillCcKind.Stun, Duration = 1f });
            Apply(new SimIntent { Kind = SimIntentKind.ApplyCc, Target = S(_e), Source = S(_d),
                                  Selector = (int)SkillCcKind.Impulse, Duration = 0.2f, Amount = 3f,
                                  DirectionXZ = new float2(1f, 0f) });
            Assert.AreEqual(2, _m.World.CcRequests.Count);
            Assert.AreEqual(CcRequestKind.Stun, _m.World.CcRequests[0].Kind, "이름으로 번역(값이 다르다)");
            Assert.AreEqual(3f, _m.World.CcRequests[1].Vector.x, 1e-5f);
            Apply(new SimIntent { Kind = SimIntentKind.ApplyCc, Target = S(_e), Selector = (int)SkillCcKind.Slow, Duration = 1f });
            Assert.IsTrue(_said.Exists(s => s.Contains("런타임 슬롯이 아니다")), "저작 토큰(감속·지속 피해)은 말한다");
            _m.Tick();
            Assert.IsTrue(_e.Cc.IsActive(CcSlotKind.Stun));

            Apply(new SimIntent { Kind = SimIntentKind.ClearCc, Target = S(_e), Selector = (int)SkillCcKind.Stun });
            Assert.IsFalse(_e.Cc.IsActive(CcSlotKind.Stun));
        }

        [Test]
        public void 실드_도발_위협_순간이동()
        {
            Apply(new SimIntent { Kind = SimIntentKind.GrantShield, Target = S(_d), Source = S(_d), Amount = 30f });
            Assert.AreEqual(1, _d.Inbox.ShieldPending.Count, "한 틱 늦는 비대칭(C17)은 관문이 그대로 진다");

            Apply(new SimIntent { Kind = SimIntentKind.Taunt, Target = S(_e), Source = S(_d), Duration = 2f });
            Assert.AreEqual(1, _m.World.AggroRequests.Count);

            Apply(new SimIntent { Kind = SimIntentKind.CreditThreat, Target = S(_e), Source = S(_d) });
            Assert.IsTrue(_said.Exists(s => s.Contains("7d")), "조용히 버리지 않는다");

            Apply(new SimIntent { Kind = SimIntentKind.Blink, Target = S(_e), Position = new float3(2f, 0f, 2f) });
            Assert.IsTrue(_e.Move.HasBlink, "위치의 주인은 이동이다");
        }

        [Test]
        public void 탄_두_갈래_대상탄과_자리_폭발()
        {
            Apply(new SimIntent { Kind = SimIntentKind.SpawnProjectile, Target = S(_e), Source = S(_d),
                                  Position = _d.Position, Amount = 9f, DataIndex = 0 });
            Apply(new SimIntent { Kind = SimIntentKind.SpawnProjectile, Target = SkillEntityId.None, Source = S(_d),
                                  Position = _e.Position, Amount = 9f, DataIndex = 1, TileRange = 1,
                                  OriginBodyRadius = 0.75f, Telegraph = true, Duration = 0.5f });
            var reqs = _m.World.ProjectileRequests;
            Assert.AreEqual(2, reqs.Count);
            Assert.AreEqual(_e.Id, reqs[0].Target);
            Assert.AreEqual(MovementKind.SkyFall, reqs[1].Movement);
            Assert.AreEqual(0.75f, reqs[1].OriginBodyRadius, "제약 13 — 원점의 몸이 경계 너머까지");
            Assert.AreEqual(1, reqs[1].TelegraphTileRange, "착탄 예고 반경 = 스킬 intent 값");
            Apply(new SimIntent { Kind = SimIntentKind.SpawnProjectile, Source = S(_d), DataIndex = -1 });
            Assert.AreEqual(2, reqs.Count, "탄 정의가 없으면 버리고 말한다");
            Assert.IsTrue(_said.Count > 0);

            Apply(new SimIntent { Kind = SimIntentKind.SpawnOrbitProjectile, Source = S(_d), Position = _d.Position,
                                  Amount = 2f, Radius = 1.5f, Duration = 3f, Phase = 1f, DataIndex = 0 });
            Assert.AreEqual(MovementKind.OrbitAroundPoint, reqs[2].Movement);
            Assert.AreEqual(1.5f, reqs[2].DistanceOverride);
        }

        [Test]
        public void 발사_명세는_바인딩의_슬롯에서_버스트를_연다()
        {
            var probe = new ProbeSkill();
            var rule = Probe(TriggerKind.None, probe);
            var b = CoreTriggerFixtures.AttachRuntime(_m, _d, rule, 0);
            _m.Intents.Begin(b, _d.Faction, null);
            Apply(new SimIntent { Kind = SimIntentKind.EmitPattern, Source = S(_d), PatternIndex = 0 });
            _m.Intents.End();
            Assert.AreEqual(1, b.Emitters.Count);
            Assert.IsTrue(b.Emitters[0].Active);
            Assert.AreEqual(7f, b.Emitters[0].Instance.Damage, "스킬 경로 = 패턴 저작 피해");
            var spawned = CoreCombatFixtures.Listen(_m, CoreEventKind.ProjectileSpawned);
            _m.Tick(); _m.Tick();
            Assert.AreEqual(1, spawned.Count);
            Assert.IsFalse(b.Emitters[0].Active, "완주");
        }

        [Test]
        public void 장판과_장_셋()
        {
            Apply(new SimIntent { Kind = SimIntentKind.SpawnZoneCarrier, Source = S(_d), Cell = new int2(6, 2), DataIndex = 0 });
            Assert.AreEqual(1, _m.World.Hazards.Count);
            Apply(new SimIntent { Kind = SimIntentKind.SpawnFieldCarrier, Selector = (int)SkillFieldKind.AllyBuff,
                                  Selector2 = (int)SkillStatKind.DamageMul, Cell = new int2(5, 2), TileRange = 1, Amount = 1.5f, Duration = 2f });
            Apply(new SimIntent { Kind = SimIntentKind.SpawnFieldCarrier, Selector = (int)SkillFieldKind.Pull,
                                  Cell = new int2(6, 2), TileRange = 2, Amount = 3f, Duration = 2f });
            Apply(new SimIntent { Kind = SimIntentKind.SpawnFieldCarrier, Selector = (int)SkillFieldKind.Portal,
                                  Cell = new int2(1, 1), Cell2 = new int2(8, 1), Duration = 2f });
            Assert.AreEqual(3, _m.World.Fields.Count);
            Assert.AreEqual(FieldKind.Portal, _m.World.Fields[2].Kind);
            Assert.AreEqual(0f, _m.World.Fields[2].Range, "포탈 입구 = 그 칸 자체(칸 반폭은 판정 진입점의 성질)");
        }

        [Test]
        public void 원자_개시_궁극기는_잠금과_무적이_함께_선다()
        {
            var ascends = CoreCombatFixtures.Listen(_m, CoreEventKind.LeapAscend);
            Apply(new SimIntent { Kind = SimIntentKind.BeginUltimateLeap, Target = S(_e), Cell = new int2(3, 2),
                                  Position = new float3(3f, 0f, 2f), Duration = 1f, Amount = 10f, TileRange = 1, DataIndex = 1 });
            Assert.IsTrue(_e.Progressive.LeapActive, "무적(판 밖)");
            Assert.IsTrue(_e.Move.Locked, "잠금");
            Assert.IsFalse(_e.IsTargetable());
            _m.Tick();
            Assert.AreEqual(1, ascends.Count);
            Assert.AreEqual(1, ascends[0].Arg, "궁극기 이탈");
            // unit 8a2 행 2 — 이탈 사건이 착지 슬램 반경을 **값으로** 싣는다(뷰의 착지 예고가 도약자를 되묻지 않게).
            Assert.AreEqual(1, ascends[0].AreaTiles, "이탈 사건의 AreaTiles = 슬램 칸 수");
            Assert.AreEqual(0f, ascends[0].SiteTarget.OriginBody, "착지 자리는 자리형(원점 항 = 칸 반폭)");
        }

        [Test]
        public void 원자_개시_호접몽은_잠과_감시가_함께_서고_완주하면_보상이다()
        {
            Apply(new SimIntent { Kind = SimIntentKind.BeginDreamCocoon, Target = S(_d), Source = S(_d), Duration = 0.5f,
                                  Selector = (int)SkillStatKind.DamageMul, Amount = 2f });
            Assert.IsTrue(_d.Cc.IsActive(CcSlotKind.Sleep), "잠");
            Assert.IsTrue(_d.Progressive.CocoonActive, "감시");
            CoreCombatFixtures.Tick(_m, 40);
            Assert.IsFalse(_d.Progressive.CocoonActive);
            Assert.AreEqual(2f, _d.Modifiers.Effective.DamageMul, 1e-5f, "완주 보상 — 영구 스탯");

            // 파탄 — 잠이 먼저 풀리면 보상이 없다.
            Apply(new SimIntent { Kind = SimIntentKind.BeginDreamCocoon, Target = S(_e), Source = S(_e), Duration = 0.5f,
                                  Selector = (int)SkillStatKind.DamageMul, Amount = 3f });
            _e.Cc.Clear(CcSlotKind.Sleep);
            CoreCombatFixtures.Tick(_m, 40);
            Assert.AreEqual(1f, _e.Modifiers.Effective.DamageMul, 1e-5f);
        }

        [Test]
        public void 자기_공격_지연_충전_치명_보상배율()
        {
            _d.Attack.CooldownRemaining = 0.2f;
            Apply(new SimIntent { Kind = SimIntentKind.DelaySelfAttack, Target = S(_d), Duration = 1f });
            Assert.AreEqual(1f, _d.Attack.CooldownRemaining, 1e-5f);
            Apply(new SimIntent { Kind = SimIntentKind.DelaySelfAttack, Target = S(_d), Duration = 0.1f });
            Assert.AreEqual(1f, _d.Attack.CooldownRemaining, 1e-5f, "이미 걸린 대기를 줄이지 않는다");

            Apply(new SimIntent { Kind = SimIntentKind.GrantCharge, Target = S(_d), Amount = 1f });
            Assert.AreEqual(1, _d.Progressive.Charge);

            Apply(new SimIntent { Kind = SimIntentKind.StartLethalTimer, Target = S(_e), Duration = 0.1f });
            CoreCombatFixtures.Tick(_m, 8);
            Assert.IsTrue(_e.Dead || _m.World.Find(_e.Id) == null, "치명 타이머 = 죽음(옛 `DeadTag`)");

            Apply(new SimIntent { Kind = SimIntentKind.ScaleKillReward, Target = S(_d), Amount = 2f });
            Assert.AreEqual(2f, _d.AwakeningRewardMul, 1e-5f);
        }

        [Test]
        public void 보고와_연출()
        {
            Apply(new SimIntent { Kind = SimIntentKind.Report, Report = SkillReport.NoLandingSpot });
            Assert.IsTrue(_said.Exists(s => s.Contains("착지점")));
            var knock = CoreCombatFixtures.Listen(_m, CoreEventKind.Knockup);
            var vis = CoreCombatFixtures.Listen(_m, CoreEventKind.SkillVisual);
            Apply(new SimIntent { Kind = SimIntentKind.PlayVisual, Selector = (int)SkillVisualKind.KnockupHop,
                                  Target = S(_e), Duration = 0.3f, Amount = 1f });
            Apply(new SimIntent { Kind = SimIntentKind.PlayVisual, Selector = (int)SkillVisualKind.Beam,
                                  Source = S(_d), Target = S(_e), Duration = 2f, DataIndex = 3 });
            _m.Tick();
            Assert.AreEqual(1, knock.Count);
            Assert.AreEqual(1, vis.Count, "다른 사건이 안 나르는 연출은 조용히 버리지 않는다");
            Assert.AreEqual(3, vis[0].DefIndex);
        }

        [Test]
        public void 메타_의도_둘은_판_자원_담당자로_간다()
        {
            float before = _m.Cost.Current;
            _m.Intents.Apply(new MetaIntent { Kind = MetaIntentKind.GainCost, Amount = 3f });
            Assert.AreEqual(before + 3f, _m.Cost.Current, 1e-4f);
            int said = _said.Count;
            _m.Intents.Apply(new MetaIntent { Kind = MetaIntentKind.ReduceSkillCooldown, Amount = 1f });
            Assert.AreEqual(said, _said.Count, "손패 담당자가 받았다(없으면 말한다)");
        }

        [Test]
        public void 의도_종류_24개가_전부_배달된다()
        {
            // 어휘가 늘면 이 수가 빨개진다 — 새 종류를 `Apply` 에 안 넣으면 「모르는 의도」로 말한다.
            int kinds = System.Enum.GetValues(typeof(SimIntentKind)).Length - 1;   // None 제외
            Assert.AreEqual(24, kinds);
            int metas = System.Enum.GetValues(typeof(MetaIntentKind)).Length - 1;
            Assert.AreEqual(2, metas);
            for (int k = 1; k <= kinds; k++)
            {
                _said.Clear();
                Apply(new SimIntent { Kind = (SimIntentKind)k, DataIndex = -1, PatternIndex = -1 });
                Assert.IsFalse(_said.Exists(s => s.Contains("모르는 의도")), ((SimIntentKind)k).ToString());
            }
        }
    }
}
