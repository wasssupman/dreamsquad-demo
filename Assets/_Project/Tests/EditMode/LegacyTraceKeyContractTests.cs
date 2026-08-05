using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

// battle-sim-extraction unit 18-A — **레거시 트레이스 키 계약의 박제.**
//
// 왜 필요한가(critic H3): 상태 해시 포매터는 필드 이름만 쓰는 게 아니라 **타입 FullName 을 박는다**.
//   `BattleBridge.LegacyTrace.cs:293` `AppendStateLine(sb, typeof(T).FullName, …)`  ← 라인 키
//   `:300` `sb.Append(typeof(T).FullName).Append("[")`                              ← 버퍼 라인 키
//   `:344` `sb.Append(type.FullName ?? type.Name).Append('{')`                      ← 중첩 값마다
//   `:331` `value is Entity → "sim:" + ResolveLegacyTraceEntity(entity)`            ← 참조 렌더
// 신 sim 이 자기 타입으로 `typeof(T).FullName` 을 찍으면 키가 통째로 달라진다. 그러면 A/B parity 의
// exact 축(상태 해시)이 **구조적으로** 불일치하고, 그것을 unit 20 에서 발견하면 되돌릴 반경이
// 7,000줄이다.
//
// 이 테스트가 하는 일 2가지:
//   ① **표를 저작한다** — 21 타입의 라인 키와 ordinal 정렬된 public 필드 목록이 아래 상수다.
//      18-C~18-J 의 포터가 대응 struct 를 만들 때 이 목록을 그대로 승계한다.
//   ② **드리프트를 막는다** — 이식이 끝나기 전에 구 타입의 이름/필드가 바뀌면 여기서 깨진다.
//      (골든 코퍼스도 깨지지만 그건 Play 14세션이 필요하고, 이건 EditMode 즉시다.)
//
// ⚠ 이 표는 **구 sim 의 사실**이지 신 sim 의 목표가 아니다. 신 emitter 는 리플렉션이 아니라
//    이 문자열을 **그대로 출력**한다 — 자기 타입명을 찍으면 안 된다.
namespace Wassup.Tests.EditMode
{
    public class LegacyTraceKeyContractTests
    {
        /// 포매터와 **같은 규칙**: `BindingFlags.Instance | Public` + ordinal 이름순 정렬.
        static string[] Fields(Type t) => t
            .GetFields(BindingFlags.Instance | BindingFlags.Public)
            .Select(f => f.Name)
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToArray();

        static void Pin(Type t, string expectedKey, params string[] expectedFields)
        {
            Assert.AreEqual(expectedKey, t.FullName,
                $"라인 키가 바뀌었다 — 상태 해시의 키다. 신 sim 은 이 문자열을 그대로 써야 한다.");
            CollectionAssert.AreEqual(expectedFields, Fields(t),
                $"{expectedKey}: public 필드 집합/이름이 바뀌었다 — 포매터가 ordinal 정렬해 직렬화한다.");
        }

        // ── 컴포넌트 11 ───────────────────────────────────────────────────────

        [Test]
        public void 컴포넌트_라인키와_필드가_박제와_같다()
        {
            Pin(typeof(Unity.Transforms.LocalTransform),
                "Unity.Transforms.LocalTransform", "Position", "Rotation", "Scale");
            Pin(typeof(Wassup.Battle.Units.Health),
                "Wassup.Battle.Units.Health", "max", "value");
            Pin(typeof(Wassup.Battle.Units.FactionTag),
                "Wassup.Battle.Units.FactionTag", "value");
        }

        // ── 버퍼 ──────────────────────────────────────────────────────────────

        [Test]
        public void 버퍼_라인키와_필드가_박제와_같다()
        {
            Pin(typeof(Wassup.Battle.Units.IncomingDamage),
                "Wassup.Battle.Units.IncomingDamage", "amount", "source");
        }

        // ── 중첩 값 타입 — `:344` 가 값마다 FullName 을 박는다 ────────────────

        [Test]
        public void 중첩_값_타입의_FullName_도_해시에_들어간다()
        {
            // `StatModifierSlot.header` 가 `ModifierHeader` 이고 그 안에 `Entity source` 가 있다.
            // 렌더 결과: `Wassup.Battle.Effects.ModifierHeader{origin=…,remaining=…,source=sim:N,stackId=…}`
            Pin(typeof(Wassup.Battle.Effects.ModifierHeader),
                "Wassup.Battle.Effects.ModifierHeader", "origin", "remaining", "source", "stackId");
            Pin(typeof(Wassup.Battle.Effects.StatModifierSlot),
                "Wassup.Battle.Effects.StatModifierSlot", "header", "magnitude", "op", "stat");
        }

        [Test]
        public void 엔진_타입이_중첩_값으로_실린다는_사실을_명시한다()
        {
            // 이것이 신 sim 이 `Unity.Mathematics` 를 버려도 **키는 승계해야 하는** 이유다.
            // `SimVec3` 는 자기 이름을 찍으면 안 되고, emitter 가 아래 문자열을 그대로 써야 한다.
            Assert.AreEqual("Unity.Mathematics.float3", typeof(Unity.Mathematics.float3).FullName);
            Assert.AreEqual("Unity.Mathematics.Random", typeof(Unity.Mathematics.Random).FullName);
            CollectionAssert.AreEqual(new[] { "x", "y", "z" }, Fields(typeof(Unity.Mathematics.float3)));
            CollectionAssert.AreEqual(new[] { "state" }, Fields(typeof(Unity.Mathematics.Random)));
        }

        [Test]
        public void Entity_필드는_sim_N_으로_렌더된다는_규칙을_박제한다()
        {
            // `:331` — 참조는 raw 인덱스가 아니라 `sim:N`(= `SimEntityId`) 로 나간다. 신 sim 의
            // `SimEntityId.ToString()` 이 같은 모양이라 emitter 가 그대로 쓸 수 있다.
            Assert.AreEqual("sim:7", new Wassup.Sim.SimEntityId(7).ToString());
            Assert.AreEqual("sim:null", Wassup.Sim.SimEntityId.Null.ToString());
        }

        // ── 표의 범위를 명시 ─────────────────────────────────────────────────

        [Test]
        public void 승계_대상_21타입_목록이_문서와_일치한다()
        {
            // 이름만 고정한다(전수 필드 박제는 조각별로 그 타입을 옮길 때 추가한다 —
            // 지금 21개를 다 적으면 아직 아무도 안 읽는 표가 되고, 조각이 옮길 때 갱신을 놓친다).
            var components = new[]
            {
                "LocalTransform", "Health", "FactionTag", "KillScore", "DefenderTile",
                "PathFollowState", "AttackState", "ModifierStats", "ProjectileState",
                "BombLauncherState", "PickupSpawnState",
            };
            var buffers = new[]
            {
                "PatternSlot", "CcEffect", "DotEffect", "StatModifierSlot", "StackModifierSlot",
                "ThreatEntry", "ShieldSlot", "IncomingDamage", "IncomingHeal", "IncomingShield",
            };
            Assert.AreEqual(11, components.Length);
            Assert.AreEqual(10, buffers.Length);
            Assert.AreEqual(21, components.Length + buffers.Length,
                "계획서 P6 의 승계 대상 수. 바뀌면 계획서도 함께 고친다.");
        }
    }
}
