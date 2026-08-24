using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.Battle.Combat;
using Wassup.Battle.Units;
using Wassup.Bridge;
using Wassup.Core;

namespace Wassup.Tests.PlayMode
{
    // bonus-wave-pull — 계약 10·13 과 스폰 파이프라인을 라이브 판에서 고정한다.
    //
    // EditMode 로는 못 잡는 것만 여기 둔다:
    //  · 계약 10 — 보너스 적이 살아 있어도 **일반 웨이브 진행과 당김 예산이 정상**이다.
    //    (EditMode 는 브리지의 웨이브 케이던스를 돌릴 수 없다.)
    //  · 스폰 파이프라인이 실제로 붙는가 — 태그 2종·포탈 뷰·타임라인.
    //  · 계약 13 — 진행 중 재진입 차단.
    //
    // 맵은 이름으로 핀한다(Duel 만 특수칸을 저작했다). 슬롯 번호 상수를 박으면 풀이 바뀔 때
    // 조용히 다른 판을 재게 된다.
    public class BonusWavePullTest
    {
        private BattleBridge _bridge;
        private int _savedMap;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            LogAssert.ignoreFailingMessages = true;
            // MapSlot 이 "MapDocument_" 접두어를 붙인다 — 이름만 넘긴다.
            _savedMap = BattleBridgeTestAccess.PinMap("Duel");
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
            BattleBridgeTestAccess.RestoreMap(_savedMap);
            yield return null;
            LogAssert.ignoreFailingMessages = false;
        }

        private static EntityManager Em => World.DefaultGameObjectInjectionWorld.EntityManager;
        private IList BonusPending() => (IList)BattleBridgeTestAccess.Field(_bridge, "_bonusPending");
        private bool BonusActive() => (bool)BattleBridgeTestAccess.Field(_bridge, "_bonusWaveActive");
        private int PullsSinceClear() => (int)BattleBridgeTestAccess.Field(_bridge, "_pullsSinceClear");
        private int NextWaveIndex() => (int)BattleBridgeTestAccess.Field(_bridge, "_nextWaveIndex");

        private List<Entity> BonusEnemies()
        {
            var q = Em.CreateEntityQuery(ComponentType.ReadOnly<BonusWaveTag>());
            var arr = q.ToEntityArray(Unity.Collections.Allocator.Temp);
            var list = new List<Entity>();
            for (int i = 0; i < arr.Length; i++) list.Add(arr[i]);
            arr.Dispose();
            return list;
        }

        // 보너스 적이 전부 나올 때까지. 타임라인은 BonusWaveData 저작값이라 상수를 박지 않고
        // «큐가 빌 때까지» 로 기다린다.
        //
        // ★**누적 관측**으로 센다. 「스케줄한 수만큼 나왔나」는 **스폰**의 문제이지 생존의
        // 문제가 아니다 — 이 적은 HP 24 이고, Duel 의 보너스 포탈은 수호 본능(사거리 5, 쿨 1.5초)이
        // 지키는 y=2·y=7 행에 있다. 첫 개체는 두 칸만 걸으면 사거리에 들어가 마지막 개체가
        // 나오기 전에 죽는다 — 정상 전투다.
        // 그래서 «동시 생존 수»(최종이든 최대든)로 단언하면 밸런스가 바뀔 때마다 조용히 빨개진다.
        private readonly HashSet<Entity> _everSeen = new HashSet<Entity>();

        private IEnumerator DrainBonusSpawns(int maxFrames = 900)
        {
            _everSeen.Clear();
            for (int f = 0; f < maxFrames && BonusPending().Count > 0; f++)
            {
                foreach (var e in BonusEnemies()) _everSeen.Add(e);
                yield return null;
            }
            foreach (var e in BonusEnemies()) _everSeen.Add(e);
            Assert.AreEqual(0, BonusPending().Count, "보너스 스폰 큐가 시간 안에 비워지지 않았다");
        }

        [UnityTest]
        public IEnumerator 트리거를_안_채우면_버튼이_안_뜬다()
        {
            Assert.IsFalse(_bridge.BonusPullAvailable,
                "판 시작 직후엔 일반 처치가 0 이라 보너스 당김이 열리면 안 된다");
            Assert.IsFalse(_bridge.TryBonusPull(), "규칙 층이 거부해야 한다");
            Assert.AreEqual(0, BonusPending().Count, "거부됐는데 큐가 찼다");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 기제로_열면_포탈_수만큼_적이_순차로_나온다()
        {
            _bridge.ForceBonusWave();
            Assert.IsTrue(BonusActive(), "보너스 웨이브가 시작되지 않았다");
            int queued = BonusPending().Count;
            Assert.Greater(queued, 0, "스케줄이 비었다 — 맵에 특수칸이 저작돼 있는가?");

            // 즉시 전부 나오면 «순차» 가 아니다. 첫 프레임엔 아직 하나도 안 나와야 한다
            // (첫 스폰은 포탈 등장 지연 + 첫 스폰 지연 뒤다).
            yield return null;
            Assert.AreEqual(0, BonusEnemies().Count, "보너스 적이 지연 없이 즉시 스폰됐다");

            yield return DrainBonusSpawns();
            Assert.AreEqual(queued, _everSeen.Count,
                "스케줄한 수만큼 스폰되지 않았다(누적 관측 기준 — 생존이 아니라 스폰을 잰다)");
        }

        [UnityTest]
        public IEnumerator 특수_적은_태그_두_개를_받는다()
        {
            _bridge.ForceBonusWave();
            yield return DrainBonusSpawns();

            var enemies = BonusEnemies();
            Assert.Greater(enemies.Count, 0, "보너스 적이 하나도 살아있지 않다 — 스폰 자체를 의심하라");
            foreach (var e in enemies)
            {
                Assert.IsTrue(Em.HasComponent<AttackUnitTag>(e), "적 표준 태그가 없다");
                Assert.IsTrue(Em.HasComponent<BonusWaveTag>(e), "BonusWaveTag 미부착");
                // 사냥 성질 — 이게 빠지면 방어유닛을 무시하고 거점으로 직행한다.
                Assert.IsTrue(Em.HasComponent<DefenderHunterTag>(e),
                    "DefenderHunterTag 미부착 — CreateEnemyEntity 부착 지점이 깨졌다");
                // 보스 특권은 받으면 안 된다(계약 6).
                Assert.IsFalse(Em.HasComponent<BossTag>(e),
                    "보너스 적이 BossTag 를 받았다 — CC·어그로 면역이 딸려온다");
            }
        }

        // ★계약 10 의 본체. 이게 빠지면 보너스 적이 살아 있는 동안 일반 웨이브가
        // 20초 상한 구동으로 강등되고 일반 당김 알약이 잠긴 채 남는다.
        [UnityTest]
        public IEnumerator 특수_적이_살아_있어도_일반_진행이_막히지_않는다()
        {
            // 예산을 다 쓰고, 일반 적을 전부 지운 «필드가 비었다» 상태를 만든다.
            for (int i = 0, cap = _bridge.PullsRemaining; i < cap; i++) _bridge.TryPullNextWave();
            Assert.IsFalse(_bridge.PullAllowed, "전제: 상한에 닿아 있어야 한다");

            _bridge.ForceBonusWave();
            yield return DrainBonusSpawns();
            Assert.Greater(BonusEnemies().Count, 0, "전제: 보너스 적이 필드에 있어야 한다");

            var pending = (IList)BattleBridgeTestAccess.Field(_bridge, "_pending");
            pending.Clear();
            var em = Em;
            var special = new HashSet<Entity>(BonusEnemies());
            foreach (var e in BattleBridgeTestAccess.SnapshotAttackers(em))
                if (!special.Contains(e) && em.Exists(e)) em.DestroyEntity(e);

            // 일반 적은 0, 보너스 적은 살아 있다 — 이 상태에서 cleared 가 성립해야 한다.
            int before = NextWaveIndex();
            for (int f = 0; f < 8 && NextWaveIndex() == before; f++) yield return null;

            Assert.Greater(BonusEnemies().Count, 0,
                "전제 붕괴: 보너스 적이 그 사이 사라졌다");
            Assert.AreEqual(0, PullsSinceClear(),
                "보너스 적이 살아 있다고 당김 예산 회복이 막혔다 — 전멸 판정에서 제외되지 않았다(계약 10)");
            Assert.IsTrue(_bridge.PullAllowed, "예산이 돌아왔으면 다시 당길 수 있어야 한다");
        }

        // 계약 13 — 진행 중 재진입 차단. 막지 않으면 포탈이 두 벌 뜨거나 첫 벌이 orphan 이 된다.
        [UnityTest]
        public IEnumerator 진행_중에는_다시_열리지_않는다()
        {
            _bridge.ForceBonusWave();
            Assert.IsTrue(BonusActive());

            // 트리거를 강제로 채워도 진행 중이면 거짓이어야 한다.
            BattleBridgeTestAccess.SetField(_bridge, "_normalKillCount", 99999);
            BattleBridgeTestAccess.SetField(_bridge, "_bonusConsumedKillMark", 0);
            Assert.IsFalse(_bridge.BonusPullAvailable,
                "보너스 웨이브 진행 중인데 버튼이 다시 떴다(계약 13)");
            Assert.IsFalse(_bridge.TryBonusPull(), "진행 중 재진입이 통과했다");
            yield return null;
        }

        // ── unit 9 — 스트레스 게이트 ─────────────────────────────────────────────
        //
        // 마음 HP 를 직접 깎아 스트레스를 만든다. 적에게 맞히는 방식은 타이밍이 판마다 달라
        // «측정이 운» 이 된다(마메모 자장가 계측 테스트가 그래서 삭제됐다).
        private void SetStress(float target01)
        {
            var em = Em;
            var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<Wassup.Battle.Units.GoalTowerTag>(),
                ComponentType.ReadWrite<Health>());
            var arr = q.ToEntityArray(Unity.Collections.Allocator.Temp);
            Assert.Greater(arr.Length, 0, "마음(골 타워)이 없다 — Duel 이 아닌 맵인가?");
            for (int i = 0; i < arr.Length; i++)
            {
                var h = em.GetComponentData<Health>(arr[i]);
                h.value = h.max * (1f - target01);   // 스트레스는 «차오르는» 값이라 반전
                em.SetComponentData(arr[i], h);
            }
            arr.Dispose();
        }

        private void GiveCredit(int kills)
        {
            BattleBridgeTestAccess.SetField(_bridge, "_normalKillCount", kills);
            BattleBridgeTestAccess.SetField(_bridge, "_bonusConsumedKillMark", 0);
        }

        [UnityTest]
        public IEnumerator 스트레스가_높으면_크레딧이_차도_안_뜬다()
        {
            GiveCredit(9999);
            SetStress(0.55f);                 // 스트레스 55
            for (int f = 0; f < 4; f++) yield return null;

            Assert.Greater(_bridge.CurrentStress, 30f, "전제: 스트레스가 문턱 위여야 한다");
            Assert.IsFalse(_bridge.BonusPullAvailable, "스트레스가 높은데 버튼이 떴다");
            Assert.IsTrue(_bridge.BonusPullBlockedByStress,
                "크레딧은 찼는데 막힌 상태 — 그 신호가 꺼져 있으면 «왜 안 뜨지» 에 답할 수 없다");
        }

        // 사용자 시나리오 — 30킬 시점엔 막혀 있다가 스트레스가 내려오면 그때 뜬다.
        [UnityTest]
        public IEnumerator 스트레스가_내려오면_그때_뜬다()
        {
            GiveCredit(9999);
            SetStress(0.55f);
            for (int f = 0; f < 4; f++) yield return null;
            Assert.IsFalse(_bridge.BonusPullAvailable, "전제: 막혀 있어야 한다");

            SetStress(0.10f);                 // 스트레스 10 으로 회복
            for (int f = 0; f < 4; f++) yield return null;
            Assert.IsTrue(_bridge.BonusPullAvailable,
                "스트레스가 문턱 아래로 내려왔는데 버튼이 안 떴다");
        }

        // 래치 — 뜬 뒤에 스트레스가 다시 올라가도 유지된다. 이게 없으면 버튼이 떨린다.
        [UnityTest]
        public IEnumerator 한번_뜨면_스트레스가_올라가도_유지된다()
        {
            GiveCredit(9999);
            SetStress(0.05f);
            for (int f = 0; f < 4; f++) yield return null;
            Assert.IsTrue(_bridge.BonusPullAvailable, "전제: 떠 있어야 한다");

            SetStress(0.85f);                 // 다시 위험해졌다
            for (int f = 0; f < 4; f++) yield return null;
            Assert.IsTrue(_bridge.BonusPullAvailable,
                "등장 조건은 유지 조건이 아니다 — 매 프레임 재평가하면 문턱에서 깜빡인다");
        }

        // 밀린 크레딧이 소비 한 번에 증발하면 안 된다.
        [UnityTest]
        public IEnumerator 밀린_크레딧은_소비해도_남는다()
        {
            GiveCredit(95);                   // 3회분 + 5킬
            SetStress(0.05f);
            for (int f = 0; f < 4; f++) yield return null;
            Assert.IsTrue(_bridge.TryBonusPull(), "첫 회를 쓸 수 있어야 한다");

            int consumed = (int)BattleBridgeTestAccess.Field(_bridge, "_bonusConsumedKillMark");
            Assert.AreEqual(30, consumed,
                "한 회분(30)만 소비해야 한다 — normalKills 로 덮으면 남은 2회분이 증발한다");
            yield return null;
        }

        [UnityTest]
        public IEnumerator 판을_다시_시작하면_특수_상태가_리셋된다()
        {
            _bridge.ForceBonusWave();
            Assert.Greater(BonusPending().Count, 0, "전제: 큐가 차 있어야 한다");

            _bridge.StopBattle();
            _bridge.StartBattle();
            yield return null;

            Assert.AreEqual(0, BonusPending().Count, "이전 판의 보너스 큐가 이월됐다");
            Assert.IsFalse(BonusActive(), "이전 판의 진행 플래그가 이월됐다");
            Assert.AreEqual(0, BonusEnemies().Count, "이전 판의 보너스 적이 남았다");
        }
    }
}
