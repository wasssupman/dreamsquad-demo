using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;
using Wassup.BattleCore.Combat.Projectile;
using Wassup.BattleCore.Effects;
using static Wassup.Tests.EditMode.Core.CoreCombatFixtures;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 6a2 — **시전자가 쏘는 모든 탄이 시전자의 착탄 효과를 싣는다.**
    //
    // 옛 전투는 그 주입이 **평타 팔 안**에만 있어서 포물선탄·카드탄·배치 스킬탄이 원천
    // 배제됐다. 그래서 이 파일의 절반은 「평타가 아닌 경로로 나간 탄」을 묻는다 — 평타로만
    // 검증하면 이 unit 이 한 말을 하나도 증언하지 못한다.
    [TestFixture]
    public class ProjectileImbueTests
    {
        private const int Caster = SimEntityId.FirstSpawnValue;   // 첫 스폰 = 시전자

        private static BattleMatch Match(MatchDefinition def)
        {
            var m = new BattleMatch(def);
            m.Begin();
            return m;
        }

        /// <summary>
        /// 시전자가 **혼자 서 있고 평타는 안 나가는** 판. 사거리를 1 로 줄이고 적을 멀리
        /// 두어, 여기서 나가는 탄은 전부 디버그 커맨드(= 공격 루프 밖)가 낸 것이 되게 한다.
        /// </summary>
        private static MatchDefinition Fixture(MovementKind movement,
                                               AttackOutputDef[] authored = null,
                                               ImbueCapDef[] caps = null)
        {
            var def = Definition(defenderDamage: 0f, defenderRange: 1f, enemyHealth: 5000f);
            GiveProjectile(def, movement, PayloadKind.TileAoe, speed: 10f, impactTileRange: 1);
            def.Units[0].Attack.Outputs = authored ?? System.Array.Empty<AttackOutputDef>();
            def.ImbueCaps = caps ?? System.Array.Empty<ImbueCapDef>();
            def.ConfigHash = def.ComputeConfigHash();
            return def;
        }

        private static AttackOutputDef[] FireStack(float magnitude = 1f) => new[]
        {
            new AttackOutputDef
            {
                Kind = AttackOutputKind.ApplyStack,
                StackKind = (int)StackKind.Fire,
                Magnitude = magnitude,
                Duration = 10f,
            },
        };

        private static ImbueCapDef[] Caps(ImbueKey key, float cap) => new[]
        {
            new ImbueCapDef { Kind = (int)key.Kind, Target = key.Target, Op = key.Op, Cap = cap },
        };

        private static Unit Stage(BattleMatch m, int2 casterCell, int2 enemyCell)
        {
            m.Apply(Command.DebugSpawnDefender(0, casterCell));
            m.Apply(Command.DebugSpawnEnemy(0, enemyCell));
            return First(m, UnitKind.Enemy);
        }

        // ── ① 모든 탄 ────────────────────────────────────────────────────────

        [Test]
        public void 카드탄_배치스킬탄_포물선탄이_시전자_착탄_출력을_싣는다()
        {
            // 셋 다 **공격 루프 밖**에서 나간다(생산자 = 디버그 발사 커맨드). 옛 전투가
            // 원천 배제했던 바로 그 셋이고, 그 배제를 푸는 것이 이 unit 의 사용자 결정 ①이다.
            var kinds = new[]
            {
                MovementKind.SkyFall,             // 카드탄 — 하늘에서 떨어진다
                MovementKind.GrenadeToCell,       // 배치 스킬탄 — 굴러가 터진다
                MovementKind.BallisticArcToPoint, // 포물선탄
            };

            foreach (var kind in kinds)
            {
                var m = Match(Fixture(kind, FireStack()));
                var e = Stage(m, new int2(2, 2), new int2(8, 1));

                m.Apply(Command.DebugFireProjectile(0, new SimEntityId(Caster), new int2(8, 1)));
                Tick(m, 90);

                Assert.AreEqual(1, e.Stacks.CountOf(StackKind.Fire),
                    $"{kind} 로 나간 탄이 시전자의 착탄 출력을 안 실었다");
            }
        }

        [Test]
        public void 킨들러형_유닛의_탄이_스택을_건다()
        {
            // 평타 팔로 나가는 탄. unit 3 이 「unit 6 이월」로 남긴 구멍이 여기서 닫힌다 —
            // 그 전까지 원거리 적의 화염 스택은 **발사되고 사라졌다.**
            var def = Definition(defenderDamage: 0f, defenderRange: 4f, enemyHealth: 5000f);
            GiveProjectile(def, MovementKind.HomingToEntity, PayloadKind.SingleSplash);
            def.Units[0].Attack.Outputs = FireStack();
            def.ConfigHash = def.ComputeConfigHash();

            var m = Match(def);
            var e = Stage(m, new int2(4, 1), new int2(6, 1));

            Tick(m, 30);
            Assert.Greater(e.Stacks.CountOf(StackKind.Fire), 0, "탄이 스택을 안 걸었다");
        }

        [Test]
        public void 시전자가_죽은_뒤_착탄해도_부여가_적용된다()
        {
            // 값 스냅샷의 단언. 착탄 시점에 시전자를 되물어 표를 읽으면 여기서 조용히 0 이 된다.
            var m = Match(Fixture(MovementKind.BallisticArcToPoint, FireStack()));
            var e = Stage(m, new int2(2, 2), new int2(8, 1));

            m.Apply(Command.DebugFireProjectile(0, new SimEntityId(Caster), new int2(8, 1)));
            Tick(m, 1);
            Assert.AreEqual(1, m.World.Projectiles.Count, "탄이 떠 있어야 이 테스트가 성립한다");

            m.Apply(Command.DebugDestroy(new SimEntityId(Caster)));
            Assert.IsFalse(m.World.IsAlive(new SimEntityId(Caster)));

            Tick(m, 90);
            Assert.AreEqual(1, e.Stacks.CountOf(StackKind.Fire),
                "사수가 없어졌다고 탄이 나르던 것이 사라지면 안 된다");
        }

        // ── ② 부여 슬롯 ──────────────────────────────────────────────────────

        [Test]
        public void 같은_키에_둘이_걸면_합이고_지속은_긴_쪽이다()
        {
            var set = new ProjectileImbueSet();
            var key = ImbueKey.Stack(StackKind.Fire);
            var a = new SimEntityId(10);
            var b = new SimEntityId(11);

            Assert.IsTrue(set.Grant(a, in key, 1f, 3f), "첫 슬롯이다");
            Assert.IsFalse(set.Grant(a, in key, 2f, 1f), "같은 (출처, 키)는 슬롯을 안 늘린다");
            Assert.IsTrue(set.Grant(b, in key, 4f, 9f), "출처가 다르면 슬롯이 따로 선다");

            Assert.AreEqual(2, set.Count);
            Assert.AreEqual(7f, set.SumOf(in key), 1e-4f, "합이다");
            Assert.AreEqual(9f, set.SecondsOf(in key), 1e-4f, "지속은 긴 쪽이다");
            Assert.AreEqual(0f, set.SumOf(ImbueKey.Stack(StackKind.Ice)), 1e-4f, "다른 키는 안 섞인다");
        }

        [Test]
        public void 회수는_그_출처의_슬롯을_지운다()
        {
            // **항등값 재발행이 아니라 삭제**다(6a 의 F27·F28·F33 이 같은 규율).
            var set = new ProjectileImbueSet();
            var fire = ImbueKey.Stack(StackKind.Fire);
            var bleed = ImbueKey.Stack(StackKind.Bleed);
            var a = new SimEntityId(10);
            var b = new SimEntityId(11);

            set.Grant(a, in fire, 1f, 3f);
            set.Grant(a, in bleed, 1f, 3f);
            set.Grant(b, in fire, 1f, 3f);

            var removed = new List<ImbueSlot>();
            Assert.AreEqual(2, set.Revoke(a, removed), "그 출처가 건 둘만 지워진다");
            Assert.AreEqual(2, removed.Count);
            Assert.AreEqual(1, set.Count);
            Assert.AreEqual(1f, set.SumOf(in fire), 1e-4f, "남의 슬롯은 남는다");
            Assert.AreEqual(0f, set.SumOf(in bleed), 1e-4f);
        }

        [Test]
        public void 키는_스탯과_결합방식을_둘_다_가른다()
        {
            // 접으면 「가산 공속 부여」와 「곱셈 공속 부여」가 한 칸을 다퉈 합이 뜻을 잃는다.
            Assert.AreNotEqual(ImbueKey.Stat(StatKind.AttackSpeedMul, CombineOp.Additive),
                               ImbueKey.Stat(StatKind.AttackSpeedMul, CombineOp.Multiplicative));
            var key = ImbueKey.Stat(StatKind.MoveSpeedMul, CombineOp.Multiplicative);
            Assert.AreEqual(key, ImbueKey.Unpack(key.Pack()), "포장은 왕복이다");
        }

        // ── ③ 상한 ───────────────────────────────────────────────────────────

        [Test]
        public void 부여는_상한을_넘어_실리지_않는다()
        {
            var key = ImbueKey.Stack(StackKind.Fire);
            var m = Match(Fixture(MovementKind.SkyFall, caps: Caps(key, 2f)));
            var e = Stage(m, new int2(2, 2), new int2(8, 1));

            // 합이 4 가 되게 두 번 건다(같은 출처라 슬롯 하나에 쌓인다).
            m.Apply(Command.DebugImbue(new SimEntityId(Caster), in key, 3f, 10f));
            m.Apply(Command.DebugImbue(new SimEntityId(Caster), in key, 1f, 10f));
            m.Apply(Command.DebugFireProjectile(0, new SimEntityId(Caster), new int2(8, 1)));
            Tick(m, 10);

            Assert.AreEqual(2, e.Stacks.CountOf(StackKind.Fire),
                "합(4)이 아니라 상한(2)만큼만 실려야 한다");
        }

        [Test]
        public void 상한_저작이_없는_키는_부여가_거절된다()
        {
            // 「저작이 없다」가 「상한이 없다」로 읽히면 근거 없는 무한 부여가 조용히 성립한다.
            var m = Match(Fixture(MovementKind.SkyFall));
            Stage(m, new int2(2, 2), new int2(8, 1));

            var r = m.Apply(Command.DebugImbue(new SimEntityId(Caster),
                                               ImbueKey.Stack(StackKind.Fire), 3f, 10f));
            Assert.IsFalse(r.Accepted, "상한 없는 부여가 통과했다");
            Assert.IsNull(m.World.Find(new SimEntityId(Caster)).Imbue, "슬롯도 안 생긴다");
        }

        // ── ④ 순서 — 앞에 온 것을 부여가 덮지 않는다 ─────────────────────────

        [Test]
        public void 저작과_부여는_같은_칸에서_합해지고_상한이_접는다()
        {
            // 리뷰 F2. 초판은 「먼저 온 쪽이 이긴다」로 잠가서 킨들러 저작 불 1 + 카드 부여
            // 불 3 이 **1만** 실렸다 — 「합이라면서 왜 안 더해지나」가 된다.
            // 저작 1 + 부여 3 = 4, 상한 2 → **2**(잠김도 아니고 4 도 아니다).
            var key = ImbueKey.Stack(StackKind.Fire);
            var m = Match(Fixture(MovementKind.SkyFall, FireStack(), Caps(key, 2f)));
            var e = Stage(m, new int2(2, 2), new int2(8, 1));

            m.Apply(Command.DebugImbue(new SimEntityId(Caster), in key, 3f, 10f));
            m.Apply(Command.DebugFireProjectile(0, new SimEntityId(Caster), new int2(8, 1)));
            Tick(m, 10);

            Assert.AreEqual(2, e.Stacks.CountOf(StackKind.Fire),
                "1 이면 잠긴 것이고 4 면 상한이 안 걸린 것이다");
        }

        [Test]
        public void 상한_줄이_없으면_부여분만_떨어지고_저작은_남는다()
        {
            // 상한을 저작 칸에도 걸면 여기서 저작이 통째로 사라진다.
            var m = Match(Fixture(MovementKind.SkyFall, FireStack()));
            var e = Stage(m, new int2(2, 2), new int2(8, 1));

            m.Apply(Command.DebugFireProjectile(0, new SimEntityId(Caster), new int2(8, 1)));
            Tick(m, 10);

            Assert.AreEqual(1, e.Stacks.CountOf(StackKind.Fire), "저작은 상한 표와 무관하다");
        }

        [Test]
        public void 피해가_0_인_착탄도_출력을_적용한다()
        {
            // 리뷰 F3 — 순수 디버프 탄이 그 모양이다. 「피해가 없으면 착탄도 없다」로
            // 되돌리면 여기서 빨개진다(주석만으로는 다음 사람이 되돌린다).
            var m = Match(Fixture(MovementKind.SkyFall, FireStack()));
            var e = Stage(m, new int2(2, 2), new int2(8, 1));
            float before = e.Health;

            m.Apply(Command.DebugFireProjectile(0, new SimEntityId(Caster), new int2(8, 1), 0f));
            Tick(m, 10);

            Assert.AreEqual(before, e.Health, 1e-3f, "이 탄은 피해가 0 이다");
            Assert.AreEqual(1, e.Stacks.CountOf(StackKind.Fire), "피해가 0 이어도 스택은 걸린다");
        }

        [Test]
        public void 요청이_명시한_군중_제어를_부여가_덮지_않는다()
        {
            var key = ImbueKey.Cc(CcRequestKind.Stun);
            var m = Match(Fixture(MovementKind.SkyFall, caps: Caps(key, 9f)));
            var e = Stage(m, new int2(2, 2), new int2(8, 1));
            m.Apply(Command.DebugImbue(new SimEntityId(Caster), in key, 9f, 9f));

            // 요청이 군중 제어를 **명시한** 탄. 오늘 이 칸을 채우는 생산자는 없으므로
            // (`CombatPhase` 도 안 채운다) 요청 줄에 직접 넣어 그 경로를 세운다.
            var req = ProjectileRequest.Empty;
            req.DefIndex = 0;
            req.Movement = MovementKind.SkyFall;
            req.Payload = PayloadKind.TileAoe;
            req.Owner = new SimEntityId(Caster);
            req.OwnerFaction = Wassup.Battle.Units.Faction.DefenderUnit;
            req.TargetMask = (int)Wassup.Battle.Units.Faction.EnemyUnit;
            req.Origin = new float3(2f, 0f, 2f);
            req.Impact = new float3(8f, 0f, 1f);
            req.OnHitCc = CcRequestKind.Stun;
            req.OnHitCcSeconds = 2f;
            m.World.ProjectileRequests.Add(req);

            Tick(m, 2);
            float remaining = e.Cc.Slot(CcSlotKind.Stun).Remaining;
            Assert.Greater(remaining, 1.5f, "요청의 기절이 아예 안 걸렸다");
            Assert.Less(remaining, 3f, "부여(9초)가 요청(2초)을 덮었다");
        }

        // ── ⑤ 사건 ───────────────────────────────────────────────────────────

        [Test]
        public void 부여와_회수는_같은_사건으로_말한다()
        {
            var key = ImbueKey.Stack(StackKind.Fire);
            var m = Match(Fixture(MovementKind.SkyFall, caps: Caps(key, 2f)));
            var changed = Listen(m, CoreEventKind.ImbueChanged);
            Stage(m, new int2(2, 2), new int2(8, 1));

            m.Apply(Command.DebugImbue(new SimEntityId(Caster), in key, 5f, 10f));
            Assert.AreEqual(1, changed.Count);
            Assert.AreEqual(key.Pack(), changed[0].Arg);
            Assert.AreEqual(2f, changed[0].Amount, 1e-4f, "사건이 말하는 값 = 한 발이 나를 값");
            Assert.AreEqual(Caster, changed[0].B.Value, "시전자가 대상이다");

            m.Apply(Command.DebugRevokeImbue(new SimEntityId(Caster)));
            Assert.AreEqual(2, changed.Count, "회수도 같은 종류로 말한다");
            Assert.AreEqual(0f, changed[1].Amount, 1e-4f, "남은 것이 없으면 0 이다");
        }

        // ── ⑥ 결정론 ─────────────────────────────────────────────────────────

        [Test]
        public void 부여가_걸린_판도_두_실행이_같다()
        {
            CommandSchedule Schedule() => new CommandSchedule()
                .Add(1, Command.DebugSpawnDefender(0, new int2(2, 2)))
                .Add(2, Command.DebugSpawnEnemy(0, new int2(8, 1)))
                .Add(3, Command.DebugSpawnEnemy(0, new int2(8, 3)))
                .Add(4, Command.DebugImbue(new SimEntityId(Caster),
                                           ImbueKey.Stack(StackKind.Fire), 3f, 6f))
                .Add(6, Command.DebugFireProjectile(0, new SimEntityId(Caster), new int2(8, 1), 5f))
                .Add(20, Command.DebugFireProjectile(0, new SimEntityId(Caster), new int2(8, 3), 5f))
                .Add(40, Command.DebugRevokeImbue(new SimEntityId(Caster)))
                .Add(50, Command.DebugFireProjectile(0, new SimEntityId(Caster), new int2(8, 1), 5f));

            MatchDefinition Def() => Fixture(MovementKind.SkyFall, FireStack(),
                                             Caps(ImbueKey.Stack(StackKind.Fire), 2f));

            var a = CoreHarness.Run(Def(), Schedule(), 300, "imbue");
            var b = CoreHarness.Run(Def(), Schedule(), 300, "imbue");

            Assert.IsNull(a.Trace.DiffAgainst(b.Trace), "부여가 걸린 판에서 트레이스가 갈렸다");
            Assert.AreEqual(a.Trace.Serialize(), b.Trace.Serialize());

            int imbue = 0, stacks = 0;
            for (int i = 0; i < a.Trace.events.Count; i++)
            {
                if (a.Trace.events[i].channel == CoreTraceChannel.ImbueChanged) imbue++;
                if (a.Trace.events[i].channel == CoreTraceChannel.StackChanged) stacks++;
            }
            Assert.AreEqual(2, imbue, "부여 1 + 회수 1 이 트레이스에 남는다");
            Assert.Greater(stacks, 0, "스택이 한 번도 안 걸렸으면 이 판은 아무것도 증언하지 않는다");
        }
    }
}
