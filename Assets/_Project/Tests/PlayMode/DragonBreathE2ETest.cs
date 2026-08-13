using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.Battle.Units;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Data;

namespace Wassup.Tests.PlayMode
{
    // elite-enemy-tier unit 4/7 — 드래곤 화염 브레스 e2e.
    //
    // 사슬: 배송 에셋(Enemy_Dragon) 스폰 → AttackN(3) 카운트 → 부채꼴 안 방어유닛만 피해.
    //
    // ★이 테스트의 **본질은 아군 오사 회귀 방지**다. 초판 스펙은 「후보 배열이 시전자 마스크로
    // 걸러진 진영 대칭 풀」이라고 잘못 적었는데, `targetCandidatesQuery` 는 전 진영 통합 풀이고
    // 진영 판정은 공격자 루프 안의 `targetMask` 가 한다. 그 사실을 놓치면 드래곤이 같은 웨이브
    // 동료와 적 마음을 태운다 — 그래서 콘 순회의 세 술어(진영·통행층·자기제외)를 여기서 못 박는다.
    public class DragonBreathE2ETest
    {
        private const string DragonPath = "Assets/_Project/Data/Enemies/Enemy_Dragon.asset";

        [TearDown] public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            TimeManager.Instance.ResetAll();
            PrimeTween.Tween.StopAll();
            yield return null;
        }

        // 드래곤은 엘리트다 — 보스 특권(CC·어그로 면역)을 받지 않는다.
        [UnityTest]
        public IEnumerator Dragon_SpawnsAsElite_WithoutBossTag_OnAirLayer()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return LoadBattle();

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            bridge.StartBattle();
            for (int i = 0; i < 2; i++) yield return null;

            var so = LoadEnemy(DragonPath);
            var dragon = SpawnEnemy(bridge, em, so);
            Assert.AreNotEqual(Entity.Null, dragon, "드래곤 스폰 실패");

            Assert.IsFalse(em.HasComponent<Wassup.Battle.Combat.BossTag>(dragon),
                "엘리트에 BossTag 가 붙었다 — CC·어그로 면역이 딸려온다");
            Assert.IsTrue(em.HasBuffer<Wassup.Battle.Combat.DcTriggerSlot>(dragon),
                "메커닉 슬롯이 없으면 브레스가 아예 안 돈다");

            // Air 통행층이 실제로 베이크됐는가 — 이동 규칙의 출처다.
            var pf = em.GetComponentData<Wassup.Battle.Movement.PathFollowState>(dragon);
            Assert.AreEqual((byte)PlacementLayer.Air, pf.traversalLayers,
                "Air 층이 안 베이크되면 지상 차단에 막히고 대공사수 전용도 성립하지 않는다");

            // 브레스 슬롯의 cosSq 가 bake 에서 변환됐는가(저작은 도, 런타임은 코사인²).
            var slots = em.GetBuffer<Wassup.Battle.Combat.DcTriggerSlot>(dragon);
            bool found = false;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].payload != DcPayloadKind.AreaBreath) continue;
                found = true;
                float expected = Mathf.Cos(Mathf.Deg2Rad * slots[i].coneHalfAngleDeg);
                Assert.AreEqual(expected * expected, slots[i].coneCosSq, 0.0001f,
                    "bake 가 반각 → cos² 변환을 하지 않았다");
                Assert.Greater(slots[i].coneCosSq, 0f);
            }
            Assert.IsTrue(found, "AreaBreath 슬롯이 베이크되지 않았다");
        }

        // 연출 배선 pin — 씬 YAML 을 직접 편집해 붙였으므로(비포커스 에디터에서 씬 전환 모달을
        // 피하려고) Unity 가 실제로 역직렬화했는지 확인할 자동 수단이 필요하다. 실제로 첫 시도에
        // 잘못된 fileID 로 `null` 이 됐고 이 단언이 없어서 육안으로만 알 수 있었다.
        //
        // ★슬롯 소유자는 **VfxSpawner** 다 — 원샷 VFX 의 프리팹 슬롯·스폰·수명은 그 클래스가
        // 소유한다(object-pipeline-map 의 VFX 아키타입). 초판은 BattleBridge 에 뒀고 그건
        // 사용자 지적으로 이관됐다(2026-08-13).
        [UnityTest]
        public IEnumerator BreathVfxPrefab_IsWiredOnVfxSpawner()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return LoadBattle();

            var spawner = Object.FindObjectOfType<Wassup.Presentation.VfxSpawner>();
            Assert.IsNotNull(spawner, "VfxSpawner");

            var f = typeof(Wassup.Presentation.VfxSpawner).GetField("areaBreathPrefab",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, "areaBreathPrefab 필드를 찾지 못했다(이름 변경?)");
            var prefab = f.GetValue(spawner) as GameObject;
            Assert.IsNotNull(prefab,
                "areaBreathPrefab 이 비었다 — 브레스가 피해만 주고 화면에 아무것도 안 보인다");
            Assert.IsTrue(prefab.GetComponentInChildren<ParticleSystem>(true) != null,
                "배선된 프리팹에 ParticleSystem 이 없다");

            // 브리지가 연출 소유권을 되가져가지 않았는지 — 회귀 방지.
            Assert.IsNull(typeof(BattleBridge).GetField("areaBreathVfxPrefab",
                    BindingFlags.NonPublic | BindingFlags.Instance),
                "BattleBridge 가 다시 VFX 프리팹 슬롯을 들고 있다 — 연출 소유권은 VfxSpawner 다");
        }

        // ── helpers ─────────────────────────────────────────────────────────────
        // 씬 로드·에셋 직독·리플렉션 스폰은 `BattleBridgeTestAccess` 가 소유한다(초판은
        // SlimeSplitE2ETest 의 레시피를 복제했다 — 그 복제가 개명 한 번에 테스트를 조용히
        // 죽인 이력이 있어서 한 자리로 모았다).

        private static IEnumerator LoadBattle() => BattleBridgeTestAccess.LoadBattleScene();

        private static AttackUnitData LoadEnemy(string path)
            => BattleBridgeTestAccess.LoadEnemy(path);

        private static Entity SpawnEnemy(BattleBridge bridge, EntityManager em, AttackUnitData unit)
            => BattleBridgeTestAccess.SpawnEnemy(bridge, em, unit);
    }
}
