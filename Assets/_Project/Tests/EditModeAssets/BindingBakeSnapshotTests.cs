using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wassup.BattleCore;
using Wassup.BattleCoreUnity;
using Wassup.Data;

namespace Wassup.Tests.EditModeAssets
{
    // unified-effect-layer unit 5 — **유닛 능력 · 악몽을 구운 규칙 줄이 굳힌 파일과 같다**(카드는 `CardBakeSnapshotTests` 가 잰다).
    //
    // unit 5 가 두 빌더의 거절 분기를 검증 한 함수로 모은다. 「라이브 무변」(계약 6)의 증거가 이 파일이다 — 빌더를 바꾸기
    // **전** 코드로 구운 기준선을 커밋해 두고, 바꾼 뒤 같은지 본다. 굽는 중 빌더가 낸 로그(경고·오류)도 싣는다 — 거절 문구나
    // 클램프가 거절로 바뀌면 규칙 줄이 같아도 여기서 드러난다. 카드 굽기 로그도 같은 이유로 여기 싣는다(카드 줄은 저쪽 파일).
    //
    // 대상 = `Data/**` 의 `DefenderUnitData`(유닛 능력 · 실드 캐스트) · `AttackUnitData`(악몽) 전부, **경로 순**.
    // 규칙도 수식자도 로그도 없는 에셋은 머리줄도 안 쓴다(능력 없는 유닛이 늘어도 파일이 안 흔들린다).
    //
    // ⚠ **기준선이 없으면 이 테스트가 한 번 쓰고 빨갛게 끝난다**(「기준선 생성됨 — 커밋 필요」). 헤드리스로는 SO 를 못 읽어
    // Unity 에서만 구울 수 있다. 그 뒤로는 읽기만 한다 — 의도한 변경이면 파일을 지우고 다시 돌려 diff 를 같은 커밋에 싣는다.
    public class BindingBakeSnapshotTests
    {
        public const string SnapshotPath = "Assets/_Project/Tests/EditModeAssets/Fixtures/binding_bake_snapshot.txt";
        private const string Header = "# unified-effect-layer 5 — 유닛 능력 · 악몽 굽기 스냅샷(+ 카드 굽기 로그). 손으로 고치지 말 것: 파일을 지우고 테스트를 돌리면 다시 굽는다\n";
        private const string DataRoot = "Assets/_Project/Data";

        private static List<T> AssetsByPath<T>(string filter) where T : Object
        {
            var paths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets(filter, new[] { DataRoot })) paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            paths.Sort(System.StringComparer.Ordinal);
            var list = new List<T>();
            foreach (var p in paths)
            {
                var a = AssetDatabase.LoadAssetAtPath<T>(p);
                if (a != null && !list.Contains(a)) list.Add(a);
            }
            return list;
        }

        // 굽는 동안 빌더가 낸 로그를 모은다. 로그는 규칙이 아니지만 «왜 그 줄이 없나»의 증언이다.
        private sealed class LogTap : System.IDisposable
        {
            public readonly List<string> Lines = new List<string>();

            public LogTap()
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
                Application.logMessageReceived += OnLog;
            }

            private void OnLog(string message, string stack, LogType type)
            {
                if (message.StartsWith("[BindingDefinitionBuilder]") || message.StartsWith("[CardDefinitionBuilder]") || message.StartsWith("[BindingSpecBuilder]"))
                    Lines.Add("[log " + type + "] " + message.Replace("\n", " / "));
            }

            public void Dispose()
            {
                Application.logMessageReceived -= OnLog;
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
            }
        }

        public static string Bake()
        {
            var units = AssetsByPath<DefenderUnitData>("t:DefenderUnitData");
            var enemies = AssetsByPath<AttackUnitData>("t:AttackUnitData");
            Assert.IsNotEmpty(units, "방어유닛 에셋이 없다");
            Assert.IsNotEmpty(enemies, "적 에셋이 없다");

            var def = new MatchDefinition { Units = new UnitDef[units.Count], Enemies = new EnemyDef[enemies.Count] };
            for (int i = 0; i < units.Count; i++) def.Units[i] = MatchDefinitionBuilder.ToUnitDef(units[i]);
            var projectiles = new List<ProjectileData>();
            var patterns = new List<ProjectilePatternData>();
            var sb = new StringBuilder(Header);
            using (var tap = new LogTap())
            {
                BindingDefinitionBuilder.Fill(def, units, enemies.ToArray(), projectiles, patterns,
                                              System.Array.Empty<HazardSO>(), new MatchViewAssets());
                for (int i = 0; i < units.Count; i++) AppendHost(sb, def, false, i, AssetDatabase.GetAssetPath(units[i]));
                for (int i = 0; i < enemies.Count; i++) AppendHost(sb, def, true, i, AssetDatabase.GetAssetPath(enemies[i]));
                // 규칙이 가리키는 탄·패턴 자산(정의표 탄 표는 `CombatDefinitionBuilder` 가 굳힌다 — 여기선 어느 자산인가만).
                for (int i = 0; i < projectiles.Count; i++) sb.Append("[projectile ").Append(i).Append("] ").Append(projectiles[i] != null ? projectiles[i].name : "null").Append('\n');
                for (int i = 0; i < patterns.Count; i++) sb.Append("[pattern ").Append(i).Append("] ").Append(patterns[i] != null ? patterns[i].name : "null").Append('\n');
                sb.Append("[bake log]\n");
                foreach (var l in tap.Lines) sb.Append(l).Append('\n');
            }

            // 카드 굽기 로그 — 카드 줄은 `card_bake_snapshot.txt`, 여기는 거절·경고 문구만.
            var cards = CardEffectWitnessTests.Cards();
            using (var tap = new LogTap())
            {
                CardBakeSnapshotTests.Bake(cards);
                sb.Append("[card bake log]\n");
                foreach (var l in tap.Lines) sb.Append(l).Append('\n');
            }
            return sb.ToString();
        }

        private static void AppendHost(StringBuilder sb, MatchDefinition def, bool enemy, int row, string path)
        {
            string body = CardProbe.CanonicalHostRulesText(def, enemy, row);
            if (body.Length == 0) return;
            sb.Append(enemy ? "[enemy " : "[unit ").Append(path).Append("]\n").Append(body);
        }

        private static string FirstDiff(string expected, string actual)
        {
            var a = expected.Split('\n');
            var b = actual.Split('\n');
            string host = "?";
            for (int i = 0; i < System.Math.Max(a.Length, b.Length); i++)
            {
                string x = i < a.Length ? a[i] : "<끝>";
                string y = i < b.Length ? b[i] : "<끝>";
                if (x.StartsWith("[unit ") || x.StartsWith("[enemy ") || x.StartsWith("[bake log") || x.StartsWith("[card bake log")) host = x;
                if (x != y) return $"{i + 1}번째 줄({host}): 굳힌 값 `{x}` ↔ 지금 `{y}`";
            }
            return null;
        }

        [Test]
        public void 유닛_능력과_악몽_굽기가_굳힌_스냅샷과_같다()
        {
            string now = Bake();
            if (!File.Exists(SnapshotPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SnapshotPath));
                File.WriteAllText(SnapshotPath, now, new UTF8Encoding(false));
                AssetDatabase.ImportAsset(SnapshotPath);
                Assert.Fail("기준선 생성됨 — 커밋 필요: " + SnapshotPath);
            }
            string committed = File.ReadAllText(SnapshotPath).Replace("\r\n", "\n");
            string diff = FirstDiff(committed, now);
            Assert.IsNull(diff, "유닛 능력 · 악몽 굽기가 스냅샷과 다르다 — 의도한 변경이면 파일을 지우고 다시 굽는다: " + diff);
        }

        [Test]
        public void 반증_규칙을_든_에셋이_있다()
        {
            // 빈 파일끼리 같다는 초록을 막는다 — 라이브 저작(census 표 1c · 1d)은 규칙 레일 유닛 능력 18 · 악몽 저작 6.
            string now = Bake();
            int units = 0, enemies = 0;
            foreach (var line in now.Split('\n'))
            {
                if (line.StartsWith("[unit ")) units++;
                if (line.StartsWith("[enemy ")) enemies++;
            }
            TestContext.WriteLine($"규칙을 든 유닛 {units} · 적 {enemies}");
            Assert.Greater(units, 0, "규칙을 든 방어유닛이 하나도 없다 — 스냅샷이 아무것도 증언하지 않는다");
            Assert.Greater(enemies, 0, "규칙을 든 적이 하나도 없다");
        }
    }
}
