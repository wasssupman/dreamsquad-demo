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

            Assert.Greater(SkillDispatchSystemBase.ExecutedCount, 0,
                "concrete 가 한 번도 안 불렸다 — 라우팅이 끊겼거나 legacy arm 이 먼저 가로챈다. " +
                "특성화 그물은 이 경우에도 초록이므로 이 단언이 유일한 증인이다.");
        }
    }
}
