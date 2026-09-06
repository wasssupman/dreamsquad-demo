using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using Wassup.Battle.Combat;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // enemy-detection-range unit 1 — **마이그레이션 누락을 잡는 유일한 그물.**
    //
    // 이 unit 이 `tier == Boss` 폴백을 없앴다(부착 조건이 `Boss || huntsDefenders` → `UsesDetection`).
    // 그래서 **새 보스를 저작하면서 `detectionRange` 를 빼먹으면 조용히 사냥을 잃는다** — 화면에는
    // 「보스가 방어유닛을 안 찾아다닌다」로만 보이고, 컴파일도 다른 테스트도 통과한다.
    // `EnemyTierBakeTests` 는 **합성 SO** 만 보므로 그 구멍을 못 막는다. 여기가 실제 `.asset` 을
    // 읽는 유일한 자리다.
    //
    // 목록을 늘리려면 **그 적이 왜 감지를 갖는지 여기 적어야 한다** — 그게 이 테스트의 역할이다
    // (`AuthoredTargetMaskTests.OnlySpecialEnemies_NarrowTheirTargets` 와 같은 형태).
    public class DetectionRangeAuthoringTests
    {
        // 무제한 사냥(`< 0`) = 「방어유닛을 전멸시켜야 거점으로 향한다」. leak-proof 가 여기 달려 있다.
        private static readonly HashSet<string> Unlimited = new HashSet<string>
        {
            "boss_jjangssen", "boss_mamemo", "boss_nightmare",   // 보스 3종 — 구 `tier == Boss` 폴백의 승계
            "dream_shard",                                        // 보너스 웨이브 — BonusWaveData 가 무제한을 요구한다
        };

        // 유한 반경 = 「반경 안의 방어유닛을 발견하면 경로를 벗어나 달려든다」.
        private static readonly Dictionary<string, float> Finite = new Dictionary<string, float>
        {
            // unit 6 — 교전형. 실측 payload 상위 + 컨셉(선봉·탱커)이 「몸으로 밀고 들어가 싸운다」.
            { "vanguard", 3f },
            { "tanker", 3f },

            // unit 8 — **비행 2종.** 이들이 unit 6 에서 빠져 있던 것은 「비행은 감지 대상이
            // 아니다」라는 판단 때문이 **아니다** — 사냥 이동을 만들던 공용 필드가 지상 마스크로만
            // 구워져 비행이 벽 위에서 조용히 죽었기 때문이다(구현 한계). unit 8 이 추격판을
            // 「내 통행 층으로 · 그 대상까지」로 바꾸면서 그 한계가 사라졌고, 규칙이 층을
            // 언급하지 않으므로(계약 13) **여기 들어오는 데 새 근거가 필요 없다.**
            // ⚠ 비행은 배치지 위를 지날 수 있어, 감지가 걸리면 **배치 구역 위로 파고든다.**
            // 지상 감지 적에는 없던 성질이다 — 「미끼로 유인한다」의 대가로 의도한 것이다.
            { "skimmer", 3f },
            { "dragon", 3f },
        };

        private static IEnumerable<AttackUnitData> AllEnemies()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:AttackUnitData"))
            {
                var so = AssetDatabase.LoadAssetAtPath<AttackUnitData>(AssetDatabase.GUIDToAssetPath(guid));
                if (so != null) yield return so;
            }
        }

        [Test]
        public void 무제한_사냥_저작이_빠지지_않았다()
        {
            var seen = new HashSet<string>();
            foreach (var so in AllEnemies())
            {
                if (!Unlimited.Contains(so.id)) continue;
                seen.Add(so.id);
                Assert.Less(so.detectionRange, 0f,
                    $"'{so.id}' 의 detectionRange 가 음수(무제한)가 아니다 — 티어 폴백이 없어졌으므로 " +
                    "이 저작이 빠지면 사냥을 조용히 잃는다(unit 1)");
                Assert.IsTrue(so.HasUnlimitedDetection, $"'{so.id}' 가 무제한으로 안 읽힌다");
            }
            CollectionAssert.AreEquivalent(Unlimited, seen,
                "무제한 목록의 id 가 에셋과 안 맞는다 — 리네임했거나 에셋이 사라졌다");
        }

        [Test]
        public void 유한_감지_저작이_기대값과_같다()
        {
            var seen = new HashSet<string>();
            foreach (var so in AllEnemies())
            {
                if (!Finite.TryGetValue(so.id, out float expected)) continue;
                seen.Add(so.id);
                Assert.AreEqual(expected, so.detectionRange, 1e-4f,
                    $"'{so.id}' 의 감지 반경이 바뀌었다 — 밸런스 변경이면 이 표를 같이 고쳐라");
                Assert.Greater(so.detectionRange, so.attackRange,
                    $"'{so.id}': 감지 반경이 공격 사거리 이하면 아무 일도 하지 않는다(계약 8)");
            }
            CollectionAssert.AreEquivalent(Finite.Keys, seen, "유한 감지 목록의 id 가 에셋과 안 맞는다");
        }

        // ★ 이 테스트가 그물의 본체다 — **목록에 없는 적이 감지를 켜면 여기서 막힌다.**
        // 근거 없이 켜지는 것을 막는 것이 목적이므로, 늘리려면 위 표에 이유와 함께 추가해야 한다.
        [Test]
        public void 목록에_없는_적은_감지가_꺼져_있다()
        {
            foreach (var so in AllEnemies())
            {
                if (Unlimited.Contains(so.id) || Finite.ContainsKey(so.id)) continue;
                Assert.AreEqual(0f, so.detectionRange, 1e-4f,
                    $"'{so.id}' 가 감지를 켰다. 의도라면 위 표에 근거와 함께 추가하라 — " +
                    "감지는 「걷는 시간을 싸우는 시간으로」 바꾸므로 밸런스 축을 건드린다");
                Assert.IsFalse(so.UsesDetection, $"'{so.id}' 가 감지를 쓰는 것으로 읽힌다");
            }
        }

        // 거점 전담 적은 계약 4 로 **자동 배제**된다(방어유닛이 legal 후보에 없다).
        // 저작으로도 0 을 명시해 「켜 뒀는데 아무 일도 안 난다」는 혼동을 남기지 않는다.
        [Test]
        public void 거점_전담_적은_감지가_꺼져_있다()
        {
            foreach (var so in AllEnemies())
            {
                int mask = EnemyTargetDefaults.Resolve((int)so.targetFactions);
                if ((mask & Wassup.Battle.Units.Factions.AnyUnit) != 0) continue;
                Assert.AreEqual(0f, so.detectionRange, 1e-4f,
                    $"'{so.id}' 는 유닛을 안 노리는데 감지가 켜져 있다 — 계약 4 로 후보가 0 이라 " +
                    "사냥 필드 재빌드만 켜지는 순수 낭비다");
            }
        }
    }
}
