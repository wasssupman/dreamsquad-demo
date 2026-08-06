using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Sim;

namespace Wassup.Tests.EditMode
{
    /// <summary>
    /// battle-sim-extraction unit 18-K/1 — **트레이스 emitter 토대의 드리프트 게이트.**
    ///
    /// `SimLegacyTrace` 는 구 타입의 `FullName` 을 **하드코딩**한다(리플렉션 금지 — 신 타입에
    /// 리플렉션을 걸면 신 이름이 나오고, sim 어셈블리는 구 타입을 참조할 수 없다).
    ///
    /// 그래서 **테스트가 진실을 구 타입에서 유도한다** — 테스트 어셈블리는 양쪽을 다 참조할 수
    /// 있고, 그 창은 구 sim 이 살아 있는 units 18~20 동안뿐이다. 구 타입 이름이나 필드가 바뀌면
    /// 여기서 즉시 깨진다(골든도 깨지지만 그건 Play 14세션이고 이건 EditMode 즉시다).
    /// </summary>
    public class SimLegacyTraceContractTests
    {
        private static string[] Fields(Type t) => t
            .GetFields(BindingFlags.Instance | BindingFlags.Public)
            .Select(f => f.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        /// 하드코딩 키가 구 타입의 `FullName` 과 같은가.
        private static void SameKey(string hardcoded, Type legacy)
            => Assert.AreEqual(legacy.FullName, hardcoded,
                "하드코딩된 라인 키가 구 타입과 갈렸다 — 이 문자열이 곧 상태 해시의 키다.");

        /// 신 타입의 public 필드 집합이 구 타입과 같은가(ordinal 정렬 결과가 곧 렌더 순서다).
        private static void SameFields(Type legacy, Type sim, params string[] simOnlyMissing)
        {
            var expected = Fields(legacy).Where(f => !simOnlyMissing.Contains(f)).ToArray();
            CollectionAssert.AreEqual(expected, Fields(sim),
                $"{legacy.FullName}: 필드 집합/이름이 갈렸다 — 포매터가 ordinal 정렬해 직렬화한다.");
        }

        // ── 키 표 ─────────────────────────────────────────────────────────────

        [Test]
        public void 컴포넌트_키가_구_타입과_같다()
        {
            SameKey(SimLegacyTrace.KeyLocalTransform, typeof(Unity.Transforms.LocalTransform));
            SameKey(SimLegacyTrace.KeyHealth, typeof(Wassup.Battle.Units.Health));
            SameKey(SimLegacyTrace.KeyFactionTag, typeof(Wassup.Battle.Units.FactionTag));
            SameKey(SimLegacyTrace.KeyKillScore, typeof(Wassup.Battle.Units.KillScore));
            SameKey(SimLegacyTrace.KeyDefenderTile, typeof(Wassup.Battle.Units.DefenderTile));
            SameKey(SimLegacyTrace.KeyPathFollowState, typeof(Wassup.Battle.Movement.PathFollowState));
            SameKey(SimLegacyTrace.KeyAttackState, typeof(Wassup.Battle.Combat.AttackState));
            SameKey(SimLegacyTrace.KeyModifierStats, typeof(Wassup.Battle.Effects.ModifierStats));
            SameKey(SimLegacyTrace.KeyProjectileState, typeof(Wassup.Battle.Combat.Projectile.ProjectileState));
            SameKey(SimLegacyTrace.KeyBombLauncherState, typeof(Wassup.Battle.Combat.BombLauncherState));
            SameKey(SimLegacyTrace.KeyPickupSpawnState, typeof(Wassup.Battle.Effects.PickupSpawnState));
        }

        [Test]
        public void 버퍼_키가_구_타입과_같다()
        {
            SameKey(SimLegacyTrace.KeyPatternSlot, typeof(Wassup.Battle.Combat.Projectile.Emission.PatternSlot));
            SameKey(SimLegacyTrace.KeyCcEffect, typeof(Wassup.Battle.Effects.CcEffect));
            SameKey(SimLegacyTrace.KeyDotEffect, typeof(Wassup.Battle.Effects.DotEffect));
            SameKey(SimLegacyTrace.KeyStatModifierSlot, typeof(Wassup.Battle.Effects.StatModifierSlot));
            SameKey(SimLegacyTrace.KeyStackModifierSlot, typeof(Wassup.Battle.Effects.StackModifierSlot));
            SameKey(SimLegacyTrace.KeyThreatEntry, typeof(Wassup.Battle.Combat.ThreatEntry));
            SameKey(SimLegacyTrace.KeyShieldSlot, typeof(Wassup.Battle.Units.ShieldSlot));
            SameKey(SimLegacyTrace.KeyIncomingDamage, typeof(Wassup.Battle.Units.IncomingDamage));
            SameKey(SimLegacyTrace.KeyIncomingHeal, typeof(Wassup.Battle.Units.IncomingHeal));
            SameKey(SimLegacyTrace.KeyIncomingShield, typeof(Wassup.Battle.Units.IncomingShield));
        }

        [Test]
        public void 중첩_값_타입_키가_구_타입과_같다()
        {
            SameKey(SimLegacyTrace.KeyFloat3, typeof(float3));
            SameKey(SimLegacyTrace.KeyFloat2, typeof(float2));
            SameKey(SimLegacyTrace.KeyInt2, typeof(int2));
            SameKey(SimLegacyTrace.KeyRandom, typeof(Unity.Mathematics.Random));
            SameKey(SimLegacyTrace.KeyModifierHeader, typeof(Wassup.Battle.Effects.ModifierHeader));
        }

        // ── 필드 집합 ─────────────────────────────────────────────────────────

        [Test]
        public void 신_타입의_필드가_구_타입과_같다()
        {
            SameFields(typeof(Wassup.Battle.Units.Health), typeof(Wassup.Sim.Units.Health));
            SameFields(typeof(Wassup.Battle.Units.FactionTag), typeof(Wassup.Sim.Units.FactionTag));
            SameFields(typeof(Wassup.Battle.Units.DefenderTile), typeof(Wassup.Sim.Units.DefenderTile));
            SameFields(typeof(Wassup.Battle.Movement.PathFollowState), typeof(Wassup.Sim.Movement.PathFollowState));
            SameFields(typeof(Wassup.Battle.Combat.AttackState), typeof(Wassup.Sim.Combat.AttackState));
            SameFields(typeof(Wassup.Battle.Effects.ModifierStats), typeof(Wassup.Sim.Effects.ModifierStats));
            SameFields(typeof(Wassup.Battle.Combat.BombLauncherState), typeof(Wassup.Sim.Combat.BombLauncherState));
            SameFields(typeof(Wassup.Battle.Effects.CcEffect), typeof(Wassup.Sim.Effects.CcEffect));
            SameFields(typeof(Wassup.Battle.Effects.DotEffect), typeof(Wassup.Sim.Effects.DotEffect));
            SameFields(typeof(Wassup.Battle.Effects.StatModifierSlot), typeof(Wassup.Sim.Effects.StatModifierSlot));
            SameFields(typeof(Wassup.Battle.Effects.StackModifierSlot), typeof(Wassup.Sim.Effects.StackModifierSlot));
            SameFields(typeof(Wassup.Battle.Effects.ModifierHeader), typeof(Wassup.Sim.Effects.ModifierHeader));
            SameFields(typeof(Wassup.Battle.Combat.ThreatEntry), typeof(Wassup.Sim.Combat.ThreatEntry));
            SameFields(typeof(Wassup.Battle.Units.ShieldSlot), typeof(Wassup.Sim.Units.ShieldSlot));
            SameFields(typeof(Wassup.Battle.Units.IncomingDamage), typeof(Wassup.Sim.Units.IncomingDamage));
            SameFields(typeof(Wassup.Battle.Units.IncomingHeal), typeof(Wassup.Sim.Units.IncomingHeal));
            SameFields(typeof(Wassup.Battle.Units.IncomingShield), typeof(Wassup.Sim.Units.IncomingShield));
        }

        [Test]
        public void SimTransform_은_Rotation_만_빠진다()
        {
            // ⚠ 이것이 18-K 가 결정해야 했던 유일한 필드 차이다 — 나머지 20 타입은 집합이 같다.
            SameFields(typeof(Unity.Transforms.LocalTransform), typeof(Wassup.Sim.Movement.SimTransform),
                       SimLegacyTrace.ExcludedField);
            Assert.AreEqual("Rotation", SimLegacyTrace.ExcludedField);
        }

        // ── 값 렌더 ───────────────────────────────────────────────────────────

        /// 구 포매터를 그대로 옮긴 참조 구현 — 테스트 안에서만 리플렉션을 쓴다.
        private static string LegacyFormat(object value)
        {
            if (value == null) return "null";
            if (value is string text) return text;
            if (value is bool boolean) return boolean ? "true" : "false";
            if (value is float single) return single.ToString("R", CultureInfo.InvariantCulture);
            if (value is double dbl) return dbl.ToString("R", CultureInfo.InvariantCulture);
            Type type = value.GetType();
            if (type.IsEnum) return Convert.ToInt64(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);
            if (type.IsPrimitive || value is decimal)
                return Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;

            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
            Array.Sort(fields, (a, b) => string.CompareOrdinal(a.Name, b.Name));
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

        [Test]
        public void 스칼라_렌더가_구_포매터와_같다()
        {
            foreach (float f in new[] { 0f, -0f, 1f, 0.1f, 1e-38f, 3.4028235e38f, float.Epsilon })
                Assert.AreEqual(LegacyFormat(f), SimLegacyTrace.Float(f), $"float {f}");
            Assert.AreEqual(LegacyFormat(true), SimLegacyTrace.Bool(true));
            Assert.AreEqual(LegacyFormat(false), SimLegacyTrace.Bool(false));
            Assert.AreEqual(LegacyFormat(-7), SimLegacyTrace.Int(-7));
            Assert.AreEqual(LegacyFormat(4294967295u), SimLegacyTrace.UInt(4294967295u));
            Assert.AreEqual(LegacyFormat((byte)200), SimLegacyTrace.Byte(200));
            Assert.AreEqual(LegacyFormat((ushort)65535), SimLegacyTrace.UShort(65535));
        }

        [Test]
        public void enum_은_정수값으로_나간다()
        {
            Assert.AreEqual(LegacyFormat(Wassup.Battle.Effects.CcKind.Stun),
                            SimLegacyTrace.Enum(Wassup.Sim.Effects.CcKind.Stun),
                            "enum 은 이름이 아니라 정수다 — 그래서 append-only 가 계약이다");
        }

        [Test]
        public void 중첩_벡터_렌더가_구_포매터와_같다()
        {
            Assert.AreEqual(LegacyFormat(new float3(1.5f, -2f, 0.25f)),
                            SimLegacyTrace.Vec3(new SimVec3(1.5f, -2f, 0.25f)));
            Assert.AreEqual(LegacyFormat(new float2(3f, 4f)),
                            SimLegacyTrace.Vec2(new SimVec2(3f, 4f)));
            Assert.AreEqual(LegacyFormat(new int2(-1, 9)),
                            SimLegacyTrace.Int2(new SimInt2(-1, 9)));
            Assert.AreEqual(LegacyFormat(new Unity.Mathematics.Random(12345u)),
                            SimLegacyTrace.Random(new SimRandom(12345u)));
        }

        [Test]
        public void 엔티티_참조는_Null_이_sim_마이너스1_이다()
        {
            // ⚠ `SimEntityId.ToString()`(`sim:null`)과 다르다 — 트레이스는
            //   `ResolveLegacyTraceEntity` 경로라 `Entity.Null` → `-1` 이다.
            Assert.AreEqual("sim:7", SimLegacyTrace.Entity(new SimEntityId(7)));
            Assert.AreEqual("sim:-1", SimLegacyTrace.Entity(SimEntityId.Null));
            Assert.AreNotEqual(SimEntityId.Null.ToString(), SimLegacyTrace.Entity(SimEntityId.Null),
                "ToString 계약과 트레이스 렌더는 서로 다른 규칙이다");
        }

        // ── 라인 조립 ─────────────────────────────────────────────────────────

        [Test]
        public void 버퍼_라인은_길이를_키에_넣고_세미콜론으로_잇는다()
        {
            var sb = new StringBuilder();
            SimLegacyTrace.BufferLine(sb, SimLegacyTrace.KeyIncomingHeal, 2,
                i => SimLegacyTrace.KeyIncomingHeal + "{amount=" + SimLegacyTrace.Float(i + 1f) + "}");

            Assert.AreEqual(
                "Wassup.Battle.Units.IncomingHeal[2]=" +
                "Wassup.Battle.Units.IncomingHeal{amount=1};" +
                "Wassup.Battle.Units.IncomingHeal{amount=2}\n",
                sb.ToString());
        }

        [Test]
        public void 빈_버퍼도_라인을_낸다()
        {
            var sb = new StringBuilder();
            SimLegacyTrace.BufferLine(sb, SimLegacyTrace.KeyCcEffect, 0, _ => "");
            Assert.AreEqual("Wassup.Battle.Effects.CcEffect[0]=\n", sb.ToString(),
                "⚠ 부재와 빈 버퍼는 다른 상태다 — 빈 버퍼도 라인을 낸다");
        }

        [Test]
        public void 엔티티_블록은_열고_닫는다()
        {
            var sb = new StringBuilder();
            SimLegacyTrace.EntityOpen(sb, 3);
            SimLegacyTrace.Line(sb, SimLegacyTrace.KeyHealth, "x");
            SimLegacyTrace.EntityClose(sb, 3);
            Assert.AreEqual("entity+3\nWassup.Battle.Units.Health=x\nentity-3\n", sb.ToString());
        }

        // ── Rotation 정규화 ───────────────────────────────────────────────────

        [Test]
        public void 정규화가_중첩_Rotation_필드를_뗀다()
        {
            // 구 트레이스에서 `Rotation` 은 `LocalTransform` 의 중첩 필드로만 나타난다.
            string legacy = LegacyFormat(Unity.Transforms.LocalTransform.FromPosition(new float3(1f, 2f, 3f)));
            StringAssert.Contains("Rotation=", legacy, "sanity: 구 렌더에는 회전이 들어 있다");

            string stripped = SimLegacyTrace.StripExcludedFields(legacy);
            StringAssert.DoesNotContain("Rotation=", stripped);
            StringAssert.Contains("Position=", stripped, "나머지 필드는 남는다");
            StringAssert.Contains("Scale=", stripped, "⚠ Scale 은 떼면 안 된다 — #24 가 그 값을 움직인다");
        }

        [Test]
        public void 정규화가_양쪽에서_같은_문자열을_만든다()
        {
            var sb = new StringBuilder();
            SimLegacyTrace.Line(sb, SimLegacyTrace.KeyLocalTransform,
                SimLegacyTrace.KeyLocalTransform + "{Position=" + SimLegacyTrace.Vec3(new SimVec3(1f, 2f, 3f))
                + ",Scale=" + SimLegacyTrace.Float(1f) + "}");
            string simSide = sb.ToString();

            string legacySide = "Unity.Transforms.LocalTransform=" +
                LegacyFormat(Unity.Transforms.LocalTransform.FromPosition(new float3(1f, 2f, 3f))) + "\n";

            Assert.AreEqual(SimLegacyTrace.StripExcludedFields(legacySide), simSide,
                "정규화 뒤 두 렌더가 **문자 단위로** 같아야 A/B parity 의 exact 축이 성립한다");
        }

        [Test]
        public void 정규화는_이름이_겹치는_다른_필드를_건드리지_않는다()
        {
            // `RotationSpeed=` 같은 접두 일치를 지우면 안 된다.
            const string s = "T{RotationSpeed=2,Rotation=U{v=1},Scale=1}\n";
            Assert.AreEqual("T{RotationSpeed=2,Scale=1}\n", SimLegacyTrace.StripExcludedFields(s));
        }

        [Test]
        public void 정규화는_없으면_그대로_돌려준다()
        {
            const string s = "battleClock=1.5\nWassup.Battle.Units.Health{max=10,value=3}\n";
            Assert.AreEqual(s, SimLegacyTrace.StripExcludedFields(s));
            Assert.AreEqual("", SimLegacyTrace.StripExcludedFields(""));
            Assert.IsNull(SimLegacyTrace.StripExcludedFields(null));
        }
    }
}
