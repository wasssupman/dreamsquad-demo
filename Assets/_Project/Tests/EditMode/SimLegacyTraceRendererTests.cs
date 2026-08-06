using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Wassup.Sim;

namespace Wassup.Tests.EditMode
{
    /// <summary>
    /// battle-sim-extraction unit 18-K/2 — **손으로 쓴 렌더러 21 종의 오라클.**
    ///
    /// ## 왜 값을 손으로 채우지 않나
    ///
    /// 렌더러는 필드 ~150 개를 손으로 잇는다. 그 검증을 위해 **기대 문자열도 손으로 쓰면**
    /// 같은 오타가 양쪽에 들어가 테스트가 통과한다. 그래서 이 테스트는 구·신 struct 를
    /// **같은 알고리즘으로 채운다** — 필드 이름을 ordinal 정렬한 순서로 훑으며 카운터에서
    /// 값을 뽑는다. 이름 집합이 같다는 것은 `SimLegacyTraceContractTests` 가 이미 증명했으므로,
    /// 두 struct 는 **대응 필드에 같은 값**을 갖게 된다.
    ///
    /// 그 다음 구 쪽은 **구 포매터를 그대로 옮긴 리플렉션 참조 구현**으로, 신 쪽은 **렌더러**로
    /// 찍어 문자 단위로 비교한다. 필드를 빠뜨렸거나·순서를 틀렸거나·타입 이름을 잘못 박았으면
    /// 여기서 즉시 빨개진다. 골든으로 알아내려면 Play 14 세션이 걸린다.
    ///
    /// ⚠ 엔티티 참조 필드는 양쪽 다 `Null` 로 둔다 — 값 렌더 규칙(`sim:-1`)은
    /// `SimLegacyTraceContractTests` 가 따로 박제하고, 여기서 검증하는 것은 **자리와 이름**이다.
    /// </summary>
    public class SimLegacyTraceRendererTests
    {
        // ── 구 포매터 참조 구현 ───────────────────────────────────────────────

        /// `BattleBridge.FormatLegacyValue` 를 그대로 옮긴 것. ⚠ `Entity` 분기가 **먼저** 온다 —
        /// 그게 없으면 리플렉션이 `Index`/`Version` 으로 내려가 트레이스와 다른 것을 만든다.
        private static string LegacyFormat(object value)
        {
            if (value == null) return "null";
            if (value is Entity entity)
            {
                Assert.AreEqual(Entity.Null, entity, "픽스처는 엔티티 참조를 Null 로만 채운다");
                return "sim:-1";
            }
            if (value is bool boolean) return boolean ? "true" : "false";
            if (value is float single) return single.ToString("R", CultureInfo.InvariantCulture);
            if (value is double dbl) return dbl.ToString("R", CultureInfo.InvariantCulture);
            Type type = value.GetType();
            if (type.IsEnum) return Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
            if (type.IsPrimitive || value is decimal)
                return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

            FieldInfo[] fields = OrdinalFields(type);
            if (fields.Length == 0) return value.ToString() ?? string.Empty;
            var sb = new StringBuilder();
            sb.Append(type.FullName ?? type.Name).Append('{');
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(fields[i].Name).Append('=').Append(LegacyFormat(fields[i].GetValue(value)));
            }
            return sb.Append('}').ToString();
        }

        private static FieldInfo[] OrdinalFields(Type t)
        {
            FieldInfo[] fields = t.GetFields(BindingFlags.Instance | BindingFlags.Public);
            Array.Sort(fields, (a, b) => string.CompareOrdinal(a.Name, b.Name));
            return fields;
        }

        // ── 픽스처: 두 struct 를 같은 규칙으로 채운다 ──────────────────────────

        /// <summary>
        /// 필드를 채운다. 값은 **필드 이름에서** 유도한다 — 위치 카운터가 아니다.
        ///
        /// ⚠ 초판은 카운터였고 `LocalTransform` 에서 깨졌다. 구에는 있고 신에는 없는 필드
        /// (`Rotation`)가 하나라도 있으면 그 뒤 필드가 전부 밀려 **같은 이름에 다른 값**이
        /// 들어간다. 이름을 축으로 삼으면 빠진 필드가 있어도 나머지가 정렬을 유지한다.
        /// </summary>
        private static object Populate(Type t)
        {
            object box = Activator.CreateInstance(t);
            foreach (FieldInfo f in OrdinalFields(t))
            {
                object v = MakeValue(f.FieldType, NameSeed(f.Name));
                if (v != null) f.SetValue(box, v);
            }
            return box;
        }

        /// 이름 → 작은 양수. 구·신이 같은 이름에 같은 값을 넣기만 하면 되므로 분포는 무관하다.
        private static int NameSeed(string name)
        {
            int h = 17;
            for (int i = 0; i < name.Length; i++) h = unchecked(h * 31 + name[i]);
            return (h & 0x7FFFFFF) % 97 + 1;
        }

        /// 채울 수 없으면 `null` 을 돌려 기본값을 남긴다.
        private static object MakeValue(Type ft, int n)
        {
            if (ft == typeof(float)) return n * 1.5f + 0.25f;
            if (ft == typeof(double)) return n * 1.5d + 0.25d;
            if (ft == typeof(int)) return n * 7;
            if (ft == typeof(uint)) return (uint)(n * 11);
            if (ft == typeof(byte)) return (byte)(n & 0x3F);
            if (ft == typeof(ushort)) return (ushort)(n * 13);
            if (ft == typeof(bool)) return (n % 2) == 0;
            // enum 은 **정수로** 나가므로 같은 정수를 넣으면 양쪽이 같은 문자열을 낸다.
            // 정의에 없는 값이어도 상관없다 — 그게 append-only 계약의 관측 방식이다.
            if (ft.IsEnum) return Enum.ToObject(ft, n % 3);

            if (ft == typeof(float3)) return new float3(n, n + 1, n + 2);
            if (ft == typeof(SimVec3)) return new SimVec3(n, n + 1, n + 2);
            if (ft == typeof(float2)) return new float2(n, n + 1);
            if (ft == typeof(SimVec2)) return new SimVec2(n, n + 1);
            if (ft == typeof(int2)) return new int2(n, n + 1);
            if (ft == typeof(SimInt2)) return new SimInt2(n, n + 1);

            // ⚠ 난수 타입은 **특수 처리하지 않는다.** 아래 중첩 재귀가 `state` 필드를 이름으로
            //   채우므로 구·신이 같은 비트를 받는다. 생성자로 만들면 안 된다 — `SimRandom(seed)`
            //   는 seed 를 그대로 두지 않고 한 스텝 굴리는데 `Unity.Mathematics.Random` 은
            //   다른 방식으로 섞어서, 같은 인자를 줘도 두 `state` 가 갈린다(실측).

            // 엔티티 참조·필드 없는 컨테이너·배열 → 기본값.
            if (ft == typeof(Entity) || ft == typeof(SimEntityId)) return null;
            if (ft.IsArray || (ft.IsGenericType && OrdinalFields(ft).Length == 0)) return null;

            // 남은 것은 중첩 struct — 재귀. 안쪽도 이름으로 채우므로 양쪽이 맞는다.
            if (ft.IsValueType && !ft.IsPrimitive && OrdinalFields(ft).Length > 0)
                return Populate(ft);

            return null;
        }

        /// 구·신을 같은 이름-값 규칙으로 채우고, 구는 참조 포매터·신은 렌더러로 찍어 비교한다.
        private static void AssertRenders(Type legacy, Type sim, Func<object, string> render,
                                          bool stripRotation = false)
        {
            object legacyBox = Populate(legacy);
            object simBox = Populate(sim);
            string expected = LegacyFormat(legacyBox);
            if (stripRotation) expected = SimLegacyTrace.StripExcludedFields(expected);

            Assert.AreEqual(expected, render(simBox),
                $"{legacy.FullName}: 렌더러가 구 포매터와 갈렸다 — 이 문자열이 곧 상태 해시다.");
        }

        // ── 컴포넌트 11 종 ────────────────────────────────────────────────────

        [Test]
        public void 컴포넌트_렌더러가_구_포매터와_같다()
        {
            AssertRenders(typeof(Unity.Transforms.LocalTransform), typeof(Wassup.Sim.Movement.SimTransform),
                v => SimLegacyTrace.TransformValue((Wassup.Sim.Movement.SimTransform)v), stripRotation: true);
            AssertRenders(typeof(Wassup.Battle.Units.Health), typeof(Wassup.Sim.Units.Health),
                v => SimLegacyTrace.HealthValue((Wassup.Sim.Units.Health)v));
            AssertRenders(typeof(Wassup.Battle.Units.FactionTag), typeof(Wassup.Sim.Units.FactionTag),
                v => SimLegacyTrace.FactionTagValue((Wassup.Sim.Units.FactionTag)v));
            AssertRenders(typeof(Wassup.Battle.Units.KillScore), typeof(Wassup.Sim.Units.KillScore),
                v => SimLegacyTrace.KillScoreValue((Wassup.Sim.Units.KillScore)v));
            AssertRenders(typeof(Wassup.Battle.Units.DefenderTile), typeof(Wassup.Sim.Units.DefenderTile),
                v => SimLegacyTrace.DefenderTileValue((Wassup.Sim.Units.DefenderTile)v));
            AssertRenders(typeof(Wassup.Battle.Movement.PathFollowState), typeof(Wassup.Sim.Movement.PathFollowState),
                v => SimLegacyTrace.PathFollowStateValue((Wassup.Sim.Movement.PathFollowState)v));
            AssertRenders(typeof(Wassup.Battle.Combat.AttackState), typeof(Wassup.Sim.Combat.AttackState),
                v => SimLegacyTrace.AttackStateValue((Wassup.Sim.Combat.AttackState)v));
            AssertRenders(typeof(Wassup.Battle.Effects.ModifierStats), typeof(Wassup.Sim.Effects.ModifierStats),
                v => SimLegacyTrace.ModifierStatsValue((Wassup.Sim.Effects.ModifierStats)v));
            AssertRenders(typeof(Wassup.Battle.Combat.Projectile.ProjectileState), typeof(Wassup.Sim.Combat.ProjectileState),
                v => SimLegacyTrace.ProjectileStateValue((Wassup.Sim.Combat.ProjectileState)v));
            AssertRenders(typeof(Wassup.Battle.Combat.BombLauncherState), typeof(Wassup.Sim.Combat.BombLauncherState),
                v => SimLegacyTrace.BombLauncherStateValue((Wassup.Sim.Combat.BombLauncherState)v));
            AssertRenders(typeof(Wassup.Battle.Effects.PickupSpawnState), typeof(Wassup.Sim.Effects.PickupSpawnState),
                v => SimLegacyTrace.PickupSpawnStateValue((Wassup.Sim.Effects.PickupSpawnState)v));
        }

        // ── 버퍼 10 종 + 중첩 3 종 ────────────────────────────────────────────

        [Test]
        public void 버퍼_렌더러가_구_포매터와_같다()
        {
            AssertRenders(typeof(Wassup.Battle.Combat.Projectile.Emission.PatternSlot), typeof(Wassup.Sim.Combat.PatternSlot),
                v => SimLegacyTrace.PatternSlotValue((Wassup.Sim.Combat.PatternSlot)v));
            AssertRenders(typeof(Wassup.Battle.Effects.CcEffect), typeof(Wassup.Sim.Effects.CcEffect),
                v => SimLegacyTrace.CcEffectValue((Wassup.Sim.Effects.CcEffect)v));
            AssertRenders(typeof(Wassup.Battle.Effects.DotEffect), typeof(Wassup.Sim.Effects.DotEffect),
                v => SimLegacyTrace.DotEffectValue((Wassup.Sim.Effects.DotEffect)v));
            AssertRenders(typeof(Wassup.Battle.Effects.StatModifierSlot), typeof(Wassup.Sim.Effects.StatModifierSlot),
                v => SimLegacyTrace.StatModifierSlotValue((Wassup.Sim.Effects.StatModifierSlot)v));
            AssertRenders(typeof(Wassup.Battle.Effects.StackModifierSlot), typeof(Wassup.Sim.Effects.StackModifierSlot),
                v => SimLegacyTrace.StackModifierSlotValue((Wassup.Sim.Effects.StackModifierSlot)v));
            AssertRenders(typeof(Wassup.Battle.Combat.ThreatEntry), typeof(Wassup.Sim.Combat.ThreatEntry),
                v => SimLegacyTrace.ThreatEntryValue((Wassup.Sim.Combat.ThreatEntry)v));
            AssertRenders(typeof(Wassup.Battle.Units.ShieldSlot), typeof(Wassup.Sim.Units.ShieldSlot),
                v => SimLegacyTrace.ShieldSlotValue((Wassup.Sim.Units.ShieldSlot)v));
            AssertRenders(typeof(Wassup.Battle.Units.IncomingDamage), typeof(Wassup.Sim.Units.IncomingDamage),
                v => SimLegacyTrace.IncomingDamageValue((Wassup.Sim.Units.IncomingDamage)v));
            AssertRenders(typeof(Wassup.Battle.Units.IncomingHeal), typeof(Wassup.Sim.Units.IncomingHeal),
                v => SimLegacyTrace.IncomingHealValue((Wassup.Sim.Units.IncomingHeal)v));
            AssertRenders(typeof(Wassup.Battle.Units.IncomingShield), typeof(Wassup.Sim.Units.IncomingShield),
                v => SimLegacyTrace.IncomingShieldValue((Wassup.Sim.Units.IncomingShield)v));
        }

        [Test]
        public void 중첩_렌더러가_구_포매터와_같다()
        {
            AssertRenders(typeof(Wassup.Battle.Effects.ModifierHeader), typeof(Wassup.Sim.Effects.ModifierHeader),
                v => SimLegacyTrace.ModifierHeaderValue((Wassup.Sim.Effects.ModifierHeader)v));
            AssertRenders(typeof(Wassup.Data.PatternSpec), typeof(Wassup.Sim.Combat.PatternSpec),
                v => SimLegacyTrace.PatternSpecValue((Wassup.Sim.Combat.PatternSpec)v));
            AssertRenders(typeof(Wassup.Battle.Combat.Projectile.ProjectileSpawnRequest), typeof(Wassup.Sim.Combat.ProjectileSpawnRequest),
                v => SimLegacyTrace.ProjectileSpawnRequestValue((Wassup.Sim.Combat.ProjectileSpawnRequest)v));
        }

        [Test]
        public void 픽스처가_실제로_값을_채운다()
        {
            // ⚠ 이 게이트가 없으면 위 테스트들이 **전부 기본값 대 기본값**이어도 통과한다.
            var st = (Wassup.Sim.Combat.AttackState)Populate(typeof(Wassup.Sim.Combat.AttackState));
            Assert.AreNotEqual(0f, st.range);
            Assert.AreNotEqual(0f, st.cooldownRemaining);
            Assert.AreNotEqual(0, st.targetMask);
            Assert.AreNotEqual(default(SimVec2).x, st.committedDirection.x, "중첩 값도 채운다");

            // 같은 이름은 구·신에서 같은 값이 된다 — 이 축이 오라클의 전제다.
            var old = (Wassup.Battle.Combat.AttackState)Populate(typeof(Wassup.Battle.Combat.AttackState));
            Assert.AreEqual(old.range, st.range);
            Assert.AreEqual(old.targetMask, st.targetMask);
        }

        // ── 조립 ─────────────────────────────────────────────────────────────

        [Test]
        public void 헤더는_기록기와_같은_12줄을_같은_순서로_낸다()
        {
            // `battleClock`·`simEntityIdCounter` 는 헤더가 아니라 **월드**에서 온다(18-K/3).
            var world = new SimWorld(new SimConfig(1u, 1u));
            world.AdvanceClock(1.5f);
            for (int i = 0; i < 10; i++) world.Create();

            var sb = new StringBuilder();
            SimLegacyTrace.AppendHeader(sb, world, new SimLegacyTraceHeader
            {
                nextWaveIndex = 2, pendingSpawns = 3, goals = 4,
                leakPenalty = 5, killScore = 6, running = true, phase = 7,
                timerRemaining = 8.5f, cost = 9.25f, meteorRngState = 11u,
            });

            Assert.AreEqual(
                "battleClock=1.5\nnextWaveIndex=2\npendingSpawns=3\ngoals=4\n" +
                "leakPenalty=5\nkillScore=6\nrunning=true\nphase=7\n" +
                "timerRemaining=8.5\ncost=9.25\nsimEntityIdCounter=10\nmeteorRng=11\n",
                sb.ToString());
        }

        [Test]
        public void 엔티티_블록은_추적분만_스폰_순번_오름차순으로_낸다()
        {
            var world = new SimWorld(new SimConfig(1u, 1u));
            SimEntityId a = world.Create();
            world.CreateInternal();                       // 캐리어 — 블록에 나오면 안 된다
            SimEntityId b = world.Create();

            world.Set(a, new Wassup.Sim.Units.Health { max = 10f, value = 3f });
            world.Set(b, new Wassup.Sim.Units.KillScore { value = 5 });

            var sb = new StringBuilder();
            SimLegacyTrace.AppendEntities(sb, world);

            Assert.AreEqual(
                "entity+0\nWassup.Battle.Units.Health=Wassup.Battle.Units.Health{max=10,value=3}\nentity-0\n" +
                "entity+1\nWassup.Battle.Units.KillScore=Wassup.Battle.Units.KillScore{value=5}\nentity-1\n",
                sb.ToString());
        }

        [Test]
        public void 태그_네_줄이_기록기_순서를_지킨다()
        {
            var world = new SimWorld(new SimConfig(1u, 1u));
            SimEntityId e = world.Create();
            world.Set(e, new Wassup.Sim.Units.PendingDeployment());
            world.Set(e, new Wassup.Sim.Combat.BossTag());
            world.Set(e, new Wassup.Sim.Units.AttackUnitTag());

            var sb = new StringBuilder();
            SimLegacyTrace.AppendEntities(sb, world);

            Assert.AreEqual("entity+0\ntag=attacker\ntag=boss\ntag=pendingDeployment\nentity-0\n",
                sb.ToString(), "attacker → defender → boss → pendingDeployment 고정 순서");
        }

        [Test]
        public void 부재는_라인이_없고_빈_버퍼는_라인을_낸다()
        {
            var world = new SimWorld(new SimConfig(1u, 1u));
            SimEntityId e = world.Create();
            world.AddBuffer<Wassup.Sim.Units.IncomingHeal>(e);   // 빈 버퍼

            var sb = new StringBuilder();
            SimLegacyTrace.AppendEntities(sb, world);

            Assert.AreEqual("entity+0\nWassup.Battle.Units.IncomingHeal[0]=\nentity-0\n", sb.ToString(),
                "⚠ 부재(다른 9 버퍼)는 침묵하고 빈 버퍼는 `[0]=` 를 낸다 — 다른 상태다");
        }

        [Test]
        public void 버퍼_원소는_세미콜론으로_잇는다()
        {
            var world = new SimWorld(new SimConfig(1u, 1u));
            SimEntityId e = world.Create();
            var buf = world.AddBuffer<Wassup.Sim.Units.IncomingHeal>(e);
            buf.Add(new Wassup.Sim.Units.IncomingHeal { amount = 1f });
            buf.Add(new Wassup.Sim.Units.IncomingHeal { amount = 2f });

            var sb = new StringBuilder();
            SimLegacyTrace.AppendEntities(sb, world);

            StringAssert.Contains(
                "Wassup.Battle.Units.IncomingHeal[2]=" +
                "Wassup.Battle.Units.IncomingHeal{amount=1};Wassup.Battle.Units.IncomingHeal{amount=2}\n",
                sb.ToString());
        }

        [Test]
        public void unkeyed_는_값_문자열로_정렬해_인덱스를_붙인다()
        {
            // 엔티티 신원이 없으니 **값 자체가 정렬 축**이다(구 기록기 그대로).
            var world = new SimWorld(new SimConfig(1u, 1u));
            SimEntityId s1 = world.CreateInternal();
            SimEntityId s2 = world.CreateInternal();
            world.Set(s1, new Wassup.Sim.Effects.PickupSpawnState { elapsed = 9f });
            world.Set(s2, new Wassup.Sim.Effects.PickupSpawnState { elapsed = 1f });

            var sb = new StringBuilder();
            SimLegacyTrace.AppendUnkeyedPickupSpawnState(sb, world);

            List<string> lines = sb.ToString().TrimEnd('\n').Split('\n').ToList();
            Assert.AreEqual(2, lines.Count);
            StringAssert.StartsWith("unkeyed.Wassup.Battle.Effects.PickupSpawnState.0=", lines[0]);
            StringAssert.Contains("elapsed=1", lines[0], "값 ordinal 정렬이라 elapsed=1 이 앞");
            StringAssert.StartsWith("unkeyed.Wassup.Battle.Effects.PickupSpawnState.1=", lines[1]);
            StringAssert.Contains("elapsed=9", lines[1]);
        }

        [Test]
        public void unkeyed_는_추적_엔티티를_세지_않는다()
        {
            // 구 기록기는 `SimEntityId` 를 **가진** 엔티티를 건너뛴다(그건 블록에서 이미 나온다).
            var world = new SimWorld(new SimConfig(1u, 1u));
            world.Set(world.Create(), new Wassup.Sim.Effects.PickupSpawnState { elapsed = 4f });

            var sb = new StringBuilder();
            SimLegacyTrace.AppendUnkeyedPickupSpawnState(sb, world);
            Assert.AreEqual("", sb.ToString());
        }

        [Test]
        public void 전체_조립이_헤더_블록_unkeyed_순서다()
        {
            var world = new SimWorld(new SimConfig(1u, 1u));
            world.Set(world.Create(), new Wassup.Sim.Units.KillScore { value = 2 });
            world.Set(world.CreateInternal(), new Wassup.Sim.Effects.PickupSpawnState { elapsed = 1f });

            world.AdvanceClock(0.5f);
            string s = SimLegacyTrace.BuildStateCanonical(world, default);

            int header = s.IndexOf("battleClock=", StringComparison.Ordinal);
            int block = s.IndexOf("entity+0", StringComparison.Ordinal);
            int unkeyed = s.IndexOf("unkeyed.", StringComparison.Ordinal);
            Assert.AreEqual(0, header);
            Assert.Less(header, block);
            Assert.Less(block, unkeyed);
            StringAssert.Contains("simEntityIdCounter=1\n", s, "추적 1 개 — 캐리어는 세지 않는다");
        }
    }
}
