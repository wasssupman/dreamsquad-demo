using NUnit.Framework;
using Unity.Mathematics;
using Wassup.Battle.Units;
using Wassup.BattleCore;
using static Wassup.Tests.EditMode.Core.CoreCombatFixtures;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 3 「나중에 고친 것」 — **부가 타격(2번째 이후 대상) 선정**이 옛 규칙과
    // 갈렸던 두 자리(2026-09-24 도달 패리티 감사). 옛 정본 = `Battle/Combat/AttackSystem.cs` 의
    // 비가디언 다중 타격 루프(pass 루프).
    //
    //   D1 — 힐러의 부가 대상도 **가장 다친 순**이다(옛 `rankByHealth` 분기 → `LowestHealthTargeting`).
    //   D2 — 직업 필터는 **주 대상 획득만** 거른다. 부가 타격 루프에는 필터가 없다.
    //
    // ⚠ 픽스처 수치는 게임 값이 아니다 — 묻는 것은 「순위·자격이 어느 규칙을 따르나」다.
    public class SecondaryTargetingTests
    {
        private static BattleMatch Match(MatchDefinition def)
        {
            var m = new BattleMatch(def);
            m.Begin();
            return m;
        }

        // 공격하지 않는 아군/표적 한 줄. 정의 인덱스 2 로 붙는다(0 = 픽스처 방어유닛, 1 = 순찰 소환물).
        private static void AddPassiveUnit(MatchDefinition def, int role)
        {
            var baseRow = def.Units[0];
            var row = baseRow;
            row.Id = "fixture_passive_" + role;
            row.Role = role;
            row.AttackTargetCount = 1;
            row.TargetFactions = 0;
            var atk = AttackDef.Default();
            atk.Outputs = System.Array.Empty<AttackOutputDef>();
            row.Attack = atk;
            var units = new UnitDef[def.Units.Length + 1];
            System.Array.Copy(def.Units, units, def.Units.Length);
            units[def.Units.Length] = row;
            def.Units = units;
        }

        private static Unit DefenderAt(BattleMatch m, int2 cell)
        {
            var units = m.World.Units;
            Unit best = null; float bestSq = float.MaxValue;
            for (int i = 0; i < units.Count; i++)
            {
                if (units[i].Kind != UnitKind.Defender) continue;
                var p = units[i].Position;
                float d = math.lengthsq(new float2(p.x, p.z) - new float2(cell.x + 0.5f, cell.y + 0.5f));
                if (d < bestSq) { bestSq = d; best = units[i]; }
            }
            return best;
        }

        [Test]
        public void 힐러의_2_3번째_회복_대상은_가까운_순이_아니라_가장_다친_순이다()
        {
            // 힐러 = 아군(방어유닛)을 겨누는 방어유닛, 3체 회복. 사거리 안의 네 아군을
            // **가까운 순 ≠ 다친 순**이 되게 놓는다:
            //   A 거리 1 · 90%   B 거리 2 · 80%   C 거리 3 · 30%   D 거리 4 · 50%
            // 옛 규칙: 주 = C(최저), 부가 = D(50%) → B(80%). A 는 안 받는다.
            // 가까운 순이면 부가 = A → B 이고 D 가 빠진다.
            var def = Definition(defenderDamage: 0f, defenderRange: 4f, defenderTargetCount: 3);
            def.Units[0].TargetFactions = (int)Faction.DefenderUnit;
            def.Units[0].Attack.Outputs = new[]
            {
                new AttackOutputDef { Kind = AttackOutputKind.Heal, Magnitude = 10f },
            };
            AddPassiveUnit(def, role: 0);
            def.ConfigHash = def.ComputeConfigHash();

            var m = Match(def);
            m.Apply(Command.DebugSpawnDefender(0, new int2(2, 2)));   // 힐러
            m.Apply(Command.DebugSpawnDefender(2, new int2(3, 2)));   // A
            m.Apply(Command.DebugSpawnDefender(2, new int2(4, 2)));   // B
            m.Apply(Command.DebugSpawnDefender(2, new int2(5, 2)));   // C
            m.Apply(Command.DebugSpawnDefender(2, new int2(6, 2)));   // D
            Tick(m, 1);

            var healer = DefenderAt(m, new int2(2, 2));
            var a = DefenderAt(m, new int2(3, 2));
            var b = DefenderAt(m, new int2(4, 2));
            var c = DefenderAt(m, new int2(5, 2));
            var d = DefenderAt(m, new int2(6, 2));
            Assert.IsNotNull(healer); Assert.IsNotNull(a); Assert.IsNotNull(b); Assert.IsNotNull(c); Assert.IsNotNull(d);
            Assert.AreNotSame(healer, a);
            a.Health = a.MaxHealth * 0.9f;
            b.Health = b.MaxHealth * 0.8f;
            c.Health = c.MaxHealth * 0.3f;
            d.Health = d.MaxHealth * 0.5f;
            float a0 = a.Health, b0 = b.Health, c0 = c.Health, d0 = d.Health;

            Tick(m, 90);   // 쿨다운 1초 — 첫 회복 한 번이 들어갈 만큼만

            Assert.Greater(c.Health, c0, "주 대상 = 가장 다친 C");
            Assert.Greater(d.Health, d0, "두 번째로 다친 D 가 부가 회복을 받는다(가까운 순이면 빠진다)");
            Assert.Greater(b.Health, b0, "세 번째로 다친 B");
            Assert.AreEqual(a0, a.Health, 1e-3f, "가장 가깝지만 가장 덜 다친 A 는 3체 안에 못 든다");
        }

        [Test]
        public void 적의_부가_타격은_가까운_순이다()
        {
            // 짝 — 적을 겨누는 공격의 부가 타격은 옛 규칙도 최근접이다(`d2 < passSq`).
            var def = Definition(defenderDamage: 5f, defenderRange: 4f, defenderTargetCount: 2);
            var m = Match(def);
            m.Apply(Command.DebugSpawnDefender(0, new int2(2, 2)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(3, 2)));   // 주 대상
            m.Apply(Command.DebugSpawnEnemy(0, new int2(4, 2)));   // 가까운 부가 — 맞는다
            m.Apply(Command.DebugSpawnEnemy(0, new int2(6, 2)));   // 먼 부가 — 2체 밖
            foreach (var u in m.World.Units) if (u.Move != null) u.Move.Speed = 0f;
            Tick(m, 1);
            var units = m.World.Units;
            Assert.Less(units[1].Health, units[1].MaxHealth);
            Assert.Less(units[2].Health, units[2].MaxHealth);
            Assert.AreEqual(units[3].MaxHealth, units[3].Health, 1e-3f);
        }

        [Test]
        public void 직업_필터가_있는_적의_부가_타격은_필터를_안_거른다()
        {
            // 옛 `AttackSystem`: 직업 필터(`hasFilter`·`filterMask`)는 **주 대상 획득 루프에만** 있다.
            // 다중 타격 pass 루프는 진영·층·자기·도형만 본다. 그래서 필터가 허용한 직업을
            // 주 대상으로 문 적의 광역은 옆의 **불허 직업**도 함께 친다.
            var def = Definition(defenderDamage: 0f, enemyDamage: 10f, enemyRange: 1f);
            def.Units[0].Role = 3;
            def.Enemies[0].AttackTargetCount = 2;
            def.Enemies[0].Attack.HasClassFilter = true;
            def.Enemies[0].Attack.ClassMask = 1 << 3;
            AddPassiveUnit(def, role: 2);
            def.ConfigHash = def.ComputeConfigHash();

            var m = Match(def);
            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 2)));   // 허용 직업(3)
            m.Apply(Command.DebugSpawnDefender(2, new int2(6, 2)));   // 불허 직업(2)
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 2)));
            var allowed = DefenderAt(m, new int2(4, 2));
            var barred = DefenderAt(m, new int2(6, 2));
            Assert.AreNotSame(allowed, barred);
            float allowed0 = allowed.Health, barred0 = barred.Health;

            Tick(m, 120);

            Assert.Less(allowed.Health, allowed0, "주 대상 = 허용 직업");
            Assert.Less(barred.Health, barred0, "부가 타격은 직업 필터를 거치지 않는다(옛 pass 루프)");
        }

        [Test]
        public void 직업_필터는_주_대상에서는_여전히_거른다()
        {
            // 짝 — 불허 직업만 사거리에 있으면 주 대상이 없고, 주 대상이 없으면 부가 타격도 없다.
            var def = Definition(defenderDamage: 0f, enemyDamage: 10f, enemyRange: 1f);
            def.Units[0].Role = 2;
            def.Enemies[0].AttackTargetCount = 2;
            def.Enemies[0].Attack.HasClassFilter = true;
            def.Enemies[0].Attack.ClassMask = 1 << 3;
            def.ConfigHash = def.ComputeConfigHash();

            var m = Match(def);
            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 2)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 2)));
            var d = First(m, UnitKind.Defender);
            float before = d.Health;
            Tick(m, 120);
            Assert.AreEqual(before, d.Health, 1e-3f);
        }
    }
}
