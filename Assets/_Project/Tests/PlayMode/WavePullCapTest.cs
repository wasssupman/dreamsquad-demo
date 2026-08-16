using System.Collections;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.Bridge;
using Wassup.Core;

namespace Wassup.Tests.PlayMode
{
    // wave-pull-revival unit 0 — 겹침 상한의 **리셋 규칙**을 고정한다.
    //
    // 상한 산식 자체(_pullsSinceClear < max)는 비교 한 줄이라 버그가 날 자리가 아니다.
    // 실제로 틀리는 곳은 «언제 0 으로 돌아가는가»다: 전멸에서만 돌아가야 하고,
    // 타임아웃 진행이나 시간 경과로는 회복되면 안 된다(가만히 있기가 예산을 벌면 안 됨).
    //
    // 두 층 분리도 함께 고정한다 — 상한은 TryPullNextWave(규칙)에만 걸리고
    // ForceNextWave(기제)는 뚫려 있어야 한다. 기존 스모크 3종이 후자를 판 진행 동력으로 쓴다.
    public class WavePullCapTest
    {
        private BattleBridge _bridge;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // 한 클래스가 BattleScene 을 여러 번 재로드하면 직전 판의 UI Sequence 가
            // 언로드에 잘리면서 PrimeTween 이 «OnComplete 무시» 에러를 남긴다. 이 판의
            // 당김 규칙과 무관한 잡음이라 무시한다(프로젝트 선례: StructureLivePlayTest 등).
            LogAssert.ignoreFailingMessages = true;
            yield return BattleBridgeTestAccess.LoadBattleScene();
            _bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.IsNotNull(_bridge, "BattleBridge 를 찾지 못했다");
            _bridge.StartBattle();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_bridge != null) _bridge.StopBattle();
            yield return null;
            LogAssert.ignoreFailingMessages = false;
        }

        private int NextWaveIndex() => (int)BattleBridgeTestAccess.Field(_bridge, "_nextWaveIndex");
        private int PullsSinceClear() => (int)BattleBridgeTestAccess.Field(_bridge, "_pullsSinceClear");

        [UnityTest]
        public IEnumerator 상한까지_당기면_그다음_당김은_거부된다()
        {
            int cap = _bridge.PullsRemaining;
            Assert.Greater(cap, 0, "판 시작 직후에는 당길 수 있어야 한다");

            for (int i = 0; i < cap; i++)
                Assert.IsTrue(_bridge.TryPullNextWave(), $"{i + 1}번째 당김이 상한 안인데 거부됐다");

            Assert.IsFalse(_bridge.PullAllowed, "상한에 닿았는데 여전히 당길 수 있다고 한다");
            Assert.AreEqual(0, _bridge.PullsRemaining, "남은 횟수가 0 이어야 한다");

            int before = NextWaveIndex();
            Assert.IsFalse(_bridge.TryPullNextWave(), "상한 초과 당김이 통과했다");
            Assert.AreEqual(before, NextWaveIndex(),
                "거부된 당김이 웨이브 인덱스를 밀었다 — 큐잉이 실제로 일어났다는 뜻");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 상한에_닿아도_기제는_뚫려_있다()
        {
            for (int i = 0, cap = _bridge.PullsRemaining; i < cap; i++) _bridge.TryPullNextWave();
            Assert.IsFalse(_bridge.PullAllowed, "전제: 상한에 닿아 있어야 한다");

            // 기존 스모크 2종(TallyFlowTest·MovementIntegritySmokeTest)이
            // 이 경로를 판 진행 동력으로 연타한다. 여기가 막히면 그들이 타임아웃으로 죽는다.
            int before = NextWaveIndex();
            _bridge.ForceNextWave();
            Assert.AreEqual(before + 1, NextWaveIndex(),
                "ForceNextWave(기제)가 상한에 막혔다 — 두 층 분리가 깨졌다");
            yield return null;
        }

        // ★ 이 파일의 가장 값싼 회귀 보험이다.
        //
        // 나머지 케이스는 전부 «회복되지 않는다»(부정)만 고정한다. 그래서 `cleared` 분기의
        // 리셋(`if (cleared) _pullsSinceClear = 0;`)이 **통째로 사라져도 다 초록**이고,
        // 플레이어는 한 판에 3번만 당길 수 있는 게임을 하게 된다. 긍정 케이스가 없으면
        // 상한 설계의 절반(«정리하면 돌아온다»)이 테스트되지 않는다.
        [UnityTest]
        public IEnumerator 필드를_비우면_예산이_돌아온다()
        {
            for (int i = 0, cap = _bridge.PullsRemaining; i < cap; i++) _bridge.TryPullNextWave();
            Assert.IsFalse(_bridge.PullAllowed, "전제: 상한에 닿아 있어야 한다");

            // 전멸 진행 분기(cleared)를 태운다. 실제 전투로 적을 다 잡는 것은 디펜더 배치가
            // 필요해 스모크 범위를 넘으므로, 그 분기가 보는 것과 **같은 조건**을 만든다:
            // _pending 을 비우고 살아 있는 적을 모두 제거한다.
            var pending = (System.Collections.IList)BattleBridgeTestAccess.Field(_bridge, "_pending");
            pending.Clear();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var alive = BattleBridgeTestAccess.SnapshotAttackers(em);
            foreach (var e in alive)
                if (em.Exists(e)) em.DestroyEntity(e);

            // 다음 프레임의 QueueDueWaves 가 cleared 를 보고 진행 + 리셋한다.
            int before = NextWaveIndex();
            for (int f = 0; f < 6 && NextWaveIndex() == before; f++) yield return null;

            Assert.AreEqual(0, PullsSinceClear(),
                "필드를 비웠는데 당김 예산이 0 으로 돌아오지 않았다 — " +
                "cleared 분기의 리셋이 죽었다(플레이어는 판당 상한만큼만 당길 수 있게 된다)");
            Assert.IsTrue(_bridge.PullAllowed, "예산이 돌아왔으면 다시 당길 수 있어야 한다");
        }

        [UnityTest]
        public IEnumerator 판을_다시_시작하면_예산이_0으로_돌아간다()
        {
            _bridge.TryPullNextWave();
            Assert.AreEqual(1, PullsSinceClear(), "전제: 한 번 당겨 예산이 소모돼 있어야 한다");

            _bridge.StopBattle();
            Assert.AreEqual(0, PullsSinceClear(), "StopBattle 이 예산을 리셋하지 않았다(계약 9)");

            _bridge.StartBattle();
            yield return null;
            Assert.AreEqual(0, PullsSinceClear(), "StartBattle 후에도 이전 판 예산이 이월됐다(계약 9)");
        }

        // 작성 플랜(저작 타임라인이 정본인 테스트 모드)은 전멸 리셋 분기를 구조적으로
        // 지나지 않는다 — 상한을 걸면 3회 뒤 **영구 잠김**이고 도크가 「적을 정리해야 열림」
        // 이라는 **틀린 사유**를 무한 표시한다. 면제가 지워지면 여기서 빨개진다.
        [UnityTest]
        public IEnumerator 작성_플랜에서는_상한이_적용되지_않는다()
        {
            BattleBridgeTestAccess.SetField(_bridge, "_usingAuthoredPlan", true);
            BattleBridgeTestAccess.SetField(_bridge, "_pullsSinceClear", 999);

            Assert.IsTrue(_bridge.PullAllowed,
                "작성 플랜인데 상한에 걸렸다 — 회복 사건이 없는 모드라 영구 잠김이 된다");
            Assert.Greater(_bridge.PullsRemaining, 0, "잠금 사유를 표시할 근거가 없어야 한다");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 타임아웃_진행으로는_예산이_회복되지_않는다()
        {
            for (int i = 0, cap = _bridge.PullsRemaining; i < cap; i++) _bridge.TryPullNextWave();
            int spent = PullsSinceClear();
            Assert.Greater(spent, 0, "전제: 예산이 소모돼 있어야 한다");

            // **전제 고정**: 방금 당긴 웨이브들이 아직 _pending 에 남아 있어 «필드가 비었다»가
            // 성립하지 않는다. 이걸 단언하지 않으면 전멸 리셋(정상 동작)과 타임아웃 리셋(버그)을
            // 구분하지 못해 테스트가 무엇을 증명하는지 알 수 없게 된다.
            var pending = (System.Collections.ICollection)
                BattleBridgeTestAccess.Field(_bridge, "_pending");
            Assert.Greater(pending.Count, 0,
                "전제: 스폰 대기가 남아 있어야 «정리되지 않은 상태»의 타임아웃을 볼 수 있다");

            // 실시간 20초를 기다리는 대신 상한 간격 기준 시각을 과거로 밀어 타임아웃 분기를
            // 즉시 성립시킨다(전멸 분기는 위 전제로 막혀 있다).
            int before = NextWaveIndex();
            BattleBridgeTestAccess.SetField(_bridge, "_waveStartSec", -1000f);
            for (int f = 0; f < 4 && NextWaveIndex() == before; f++) yield return null;

            Assert.AreEqual(before + 1, NextWaveIndex(),
                "타임아웃 자동 진행이 일어나지 않았다 — 이 테스트의 전제가 성립하지 않는다");
            Assert.AreEqual(spent, PullsSinceClear(),
                "타임아웃 진행이 당김 예산을 회복시켰다 — «가만히 있기»가 예산을 벌어준다");
            Assert.IsFalse(_bridge.PullAllowed, "여전히 상한에 닿아 있어야 한다");
        }
    }
}
