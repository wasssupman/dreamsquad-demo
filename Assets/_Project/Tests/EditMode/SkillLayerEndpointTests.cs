using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Battle.Units;
using Wassup.Skills;
using Wassup.Skills.Concrete;

namespace Wassup.Tests.EditMode
{
    // 정합성 리뷰(2026-08-26)에서 드러난 **그물 공백 둘**을 메운다.
    //
    // 둘 다 「구조적으로는 참인데 아무도 확인하지 않는」 부류였다 — 이 spec 이 반복해서
    // 잡아낸 실패 형태와 같다(조용한 성공은 조용한 실패와 구분이 안 된다).
    public class SkillLayerEndpointTests
    {
        // ── ① 토대 spec 의 검증 질문에 직접 증인을 세운다 ────────────────────────
        //
        // 「방어유닛이 보스의 도약 스킬을 쓰면 **상대 진영** 밀집 셀로 간다」 —
        // 이것이 「호출자가 곧 소유자」의 가장 선명한 표현이다. 같은 concrete 가
        // 부르는 쪽에 따라 반대편을 고른다. **코드 0줄로.**
        //
        // ⚠ 이 단언이 여태 없었던 이유가 재미있다: 페이크가 밀집 질의를 `false` 로
        // 막아 놔서 concrete 가 첫 줄에서 빠져나갔다. 그물이 «없었던» 게 아니라
        // 그물을 칠 수 없는 상태였다.
        [Test]
        public void SameSkill_PicksOpponentCluster_ForWhoeverCallsIt()
        {
            // 적 셋이 한 칸에, 방어유닛 하나가 다른 칸에.
            int2 EnemyCell = new int2(9, 9);
            int2 DefCell = new int2(2, 2);

            var asDefender = RunBlink(Faction.DefenderUnit, EnemyCell, DefCell);
            Assert.AreEqual(EnemyCell, asDefender,
                "방어유닛이 쓰면 **적** 밀집 셀로 가야 한다 — 검증 질문 그 자체");

            var asEnemy = RunBlink(Faction.EnemyUnit, EnemyCell, DefCell);
            Assert.AreEqual(DefCell, asEnemy,
                "같은 concrete 를 적이 부르면 **방어유닛** 쪽으로 간다 — 코드는 하나다");
        }

        private static int2 RunBlink(Faction casterFaction, int2 enemyCell, int2 defCell)
        {
            var ctx = new TestSkillContext();
            int id = 1;
            ctx.Add(id++, Center(enemyCell), Faction.EnemyUnit);
            ctx.Add(id++, Center(enemyCell), Faction.EnemyUnit);
            ctx.Add(id++, Center(enemyCell), Faction.EnemyUnit);
            ctx.Add(id++, Center(defCell), Faction.DefenderUnit);

            // 시전자는 양쪽 밀집에서 떨어진 곳에 둔다 — 자기 자리가 답에 안 섞이게.
            var casterId = new SkillEntityId(99);
            ctx.Add(99, Center(new int2(20, 20)), casterFaction);
            var caster = CasterRef.OfUnit(casterId, casterFaction);

            new BlinkToClusterSkill().Execute(caster, SkillTarget.None,
                new SkillParams(0, 0, 0, 0, SkillParams.NoDataIndex, 0, 0, 0, 0, 0, 0), ctx);

            var blink = ctx.SimIntents.Find(i => i.Kind == SimIntentKind.Blink);
            Assert.AreNotEqual(default(SimIntent).Kind, blink.Kind, "도약 자체가 안 나갔다");
            return new int2((int)math.floor(blink.Position.x / ctx.TileSize),
                            (int)math.floor(blink.Position.z / ctx.TileSize));
        }

        private static float3 Center(int2 cell) => new float3(cell.x + 0.5f, 0f, cell.y + 0.5f);

        // ── ② 「등록을 잊었다」를 EditMode 가 잡는다 ─────────────────────────────
        //
        // `Register` 한 줄을 빠뜨리면 그 스킬은 **런타임 경고로만** 드러난다 —
        // 라우팅 그물은 컴파일 상수 `Id` 를 묻기 때문에 초록으로 통과한다.
        // `SkillRegistry.RegisteredIds` 는 「저작 전부가 등록됐나」를 테스트가 물을 수
        // 있게 뚫어 둔 창인데 **소비자가 0** 이었다.
        [Test]
        public void EveryConcrete_IsRegistered()
        {
            var missing = ConcreteTypes()
                .Where(t => !InstallSource().Contains($"Concrete.{t.Name}()"))
                .Select(t => t.Name).ToList();

            Assert.IsEmpty(missing,
                "이 concrete 들이 레지스트리에 등록되지 않았다 — 저작이 그 스킬을 가리켜도 " +
                "드레인이 「레지스트리에 없다」로 버린다(런타임 경고로만 보인다). " +
                "BattleBridge.InstallSkillLayer 에 Register 를 추가하라. 누락: " +
                string.Join(", ", missing));
        }

        // id 는 중복 억제 마스크(ulong)의 비트 위치다. 64 를 넘으면 **같은 카드 두 장이
        // 두 번 터지는** 형태가 조용히 부활한다. 런타임 경고가 있지만 그건 이미 늦다.
        [Test]
        public void SkillIds_AreUnique_AndFitTheDedupMask()
        {
            var ids = new Dictionary<int, string>();
            foreach (var t in ConcreteTypes())
            {
                var f = t.GetField("Id", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                Assert.IsNotNull(f, $"{t.Name} 에 `public const int Id` 가 없다");
                int id = (int)f.GetRawConstantValue();

                Assert.AreNotEqual(SkillRegistry.NotRouted, id,
                    $"{t.Name} 이 «스킬 아님» 예약값을 쓴다");
                Assert.Less(id, 64,
                    $"{t.Name} 의 id({id}) 가 중복 억제 마스크 폭(64)을 넘는다 — " +
                    "넘으면 같은 죽음/킬에서 같은 스킬이 두 번 터진다. 마스크를 넓혀야 한다.");
                Assert.IsFalse(ids.ContainsKey(id),
                    $"id {id} 충돌: {ids.GetValueOrDefault(id)} ↔ {t.Name}");
                ids[id] = t.Name;
            }
        }

        private static IEnumerable<Type> ConcreteTypes()
            => typeof(AreaSleepSkill).Assembly.GetTypes()
                .Where(t => !t.IsAbstract && typeof(ISkill).IsAssignableFrom(t)
                            && t.Namespace == "Wassup.Skills.Concrete");

        private static string InstallSource()
        {
            var path = Path.Combine(Application.dataPath, "_Project/Scripts/Bridge/BattleBridge.cs");
            Assert.IsTrue(File.Exists(path), $"브리지를 옮겼으면 이 경로도 옮겨야 한다: {path}");
            return File.ReadAllText(path);
        }
    }
}
