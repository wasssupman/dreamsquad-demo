using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.Battle.Skills;
using Wassup.Bridge;

namespace Wassup.Tests.PlayMode
{
    // skill-layer-migration — **이전이 실제로 됐는지** 묻는다.
    //
    // ⚠ 이 파일이 존재하는 이유: 기존 특성화 그물은 「자장가가 적을 재운다」를 묻는데,
    // 그건 legacy arm 이 돌아도 참이다. 그래서 라우팅이 잘못 배선돼 concrete 가 한 번도
    // 안 불려도 **그물 전체가 초록**이었다(실제로 한 번 당했다 — 라우팅 분기를 payload
    // 분기들 뒤에 둬서 이전한 스킬 셋이 legacy 를 탔다).
    //
    // 「무엇이 일어났나」와 「누가 했나」는 다른 질문이고, 이전 작업에는 후자가 필요하다.
    public class SkillLayerRoutingTest
    {
        private int _savedMap;

        [SetUp]
        public void PinMap()
        {
            _savedMap = BattleBridgeTestAccess.PinMap();
            SkillDispatchSystemBase.ResetExecutedCount();
        }

        [TearDown]
        public void TearDown() => BattleBridgeTestAccess.RestoreMap(_savedMap);

        // 보스가 도는 판에서 concrete 가 **한 번이라도** 불렸는지.
        // 어느 스킬인지는 안 묻는다 — 라우팅이 살아 있는지만 본다.
        [UnityTest]
        public IEnumerator MigratedSkills_ActuallyRunThroughConcretes()
        {
            yield return BattleBridgeTestAccess.LoadBattleScene();
            SkillDispatchSystemBase.ResetExecutedCount();

            var em = Unity.Entities.World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleBridge");

            var boss = BattleBridgeTestAccess.LoadEnemy("Assets/_Project/Data/Enemies/Enemy_Boss_Mamemo.asset");
            Assert.IsNotNull(boss, "마메모 에셋 — 자장가·가호를 둘 다 든 보스");
            BattleBridgeTestAccess.SpawnEnemy(bridge, em, boss);

            // 주기 트리거가 한 번은 돌 만큼 기다린다. 정확한 발동 수를 단언하지 않는다 —
            // 그건 emergent 타이밍이라 운에 좌우된다(BossLullabyLiveTest 가 그래서 삭제됐다).
            for (int i = 0; i < 300 && SkillDispatchSystemBase.ExecutedCount == 0; i++)
                yield return null;

            Assert.Greater(SkillDispatchSystemBase.ExecutedCountOf(SkillSeam.Periodic), 0,
                "주기 seam 의 concrete 가 한 번도 안 불렸다 — 라우팅이 끊겼거나 legacy arm 이 " +
                "먼저 가로챈다. 특성화 그물은 이 경우에도 초록이므로 이 단언이 유일한 증인이다.");
        }

        // ⚠ **경계 seam 을 따로 묻는다**(투트랙 리뷰 잔여 리스크).
        //
        // 위 테스트는 주기 seam 만 실주행한다. 합계 카운터만 보면 **경계 seam 의 라우팅이
        // 끊겨도** 위가 초록이라 통과한다 — `7f902e55` 가 잡은 실패 유형의 나머지 절반이
        // 정확히 그 모양이다. 꿈의 장막·경계 자폭·궁극기가 전부 그 seam 을 탄다.
        [UnityTest]
        public IEnumerator ThresholdSeam_AlsoRunsThroughConcretes()
        {
            yield return BattleBridgeTestAccess.LoadBattleScene();
            SkillDispatchSystemBase.ResetExecutedCount();

            var em = Unity.Entities.World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleBridge");

            // 짱쎈놈은 경계 슬롯만 넷 든다(자폭·도약×2·궁극기) — 주기 슬롯이 없어서
            // 이 단언이 경계 seam 만 본다는 것이 구조적으로 보장된다.
            var boss = BattleBridgeTestAccess.LoadEnemy(
                "Assets/_Project/Data/Enemies/Enemy_Boss_Jjangssen.asset");
            var e = BattleBridgeTestAccess.SpawnEnemy(bridge, em, boss);

            // 경계는 체력이 내려가야 넘는다. emergent 전투를 기다리지 않고 직접 깎는다 —
            // 그래야 발동이 운에 좌우되지 않는다(BossLullabyLiveTest 가 그래서 삭제됐다).
            var h = em.GetComponentData<Wassup.Battle.Units.Health>(e);
            h.value = h.max * 0.05f;
            em.SetComponentData(e, h);

            for (int i = 0; i < 300 && SkillDispatchSystemBase.ExecutedCountOf(SkillSeam.Threshold) == 0; i++)
                yield return null;

            Assert.Greater(SkillDispatchSystemBase.ExecutedCountOf(SkillSeam.Threshold), 0,
                "경계 seam 의 concrete 가 한 번도 안 불렸다 — 그 seam 의 라우팅이 끊겼다. " +
                "주기 seam 만 보는 그물은 이 상태를 초록으로 통과시킨다.");
        }
    }
}
