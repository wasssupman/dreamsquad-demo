using NUnit.Framework;
using UnityEditor;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // bonus-wave-pull 계약 4 ↔ 계약 12 — 이 둘은 한 줄로 묶여 있다.
    //
    // 트리거 카운터는 「보너스 적이었나」를 **SO 동일성**으로 가른다
    // (`BattleBridge.DrainEnemyKilledEvents`: `killedType == bonusWaveData.enemyUnit`).
    // 킬 드레인 시점엔 엔티티가 이미 파괴돼 태그를 못 읽기 때문이다.
    //
    // 그 판별이 옳으려면 **그 SO 로 태어난 적은 보너스 웨이브 출신뿐**이어야 한다 = 계약 4다.
    // 누군가 「드림 샤드를 일반 웨이브에도 섞자」며 덱 풀에 넣으면:
    //   · 일반 웨이브의 그 적 처치가 트리거 카운터에서 빠져 실효 임계가 올라간다(버튼이 잘 안 뜬다)
    //   · 컴파일도 통과하고 다른 테스트도 전부 초록이다
    // 그래서 여기서 못박는다. 정말 넣어야 한다면 먼저 판별을 이벤트 페이로드로 바꿔라.
    public class BonusEnemyNotInDeckTests
    {
        [Test]
        public void 보너스_적은_어느_덱_풀에도_없다()
        {
            var bonus = AssetDatabase.LoadAssetAtPath<BonusWaveData>(
                "Assets/_Project/Data/BonusWaveData.asset");
            Assert.IsNotNull(bonus, "BonusWaveData.asset 을 찾지 못했다");
            Assert.IsNotNull(bonus.enemyUnit, "BonusWaveData.enemyUnit 미할당");

            foreach (var guid in AssetDatabase.FindAssets("t:AttackDeck"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var deck = AssetDatabase.LoadAssetAtPath<AttackDeck>(path);
                if (deck == null) continue;

                foreach (var unit in deck.ResolveAttackUnitPool())
                    Assert.AreNotSame(bonus.enemyUnit, unit,
                        $"{deck.name}: 보너스 적({bonus.enemyUnit.displayName})이 덱 풀에 있다 — " +
                        "계약 4 위반이고, 그 순간 계약 12 의 트리거 판별(SO 동일성)이 함께 깨진다. " +
                        "정말 넣으려면 판별을 이벤트 페이로드 기반으로 먼저 바꿔라.");

                if (deck.bossUnit != null)
                    Assert.AreNotSame(bonus.enemyUnit, deck.bossUnit, $"{deck.name}: 보너스 적이 bossUnit 이다");
                if (deck.bossPool != null)
                    foreach (var b in deck.bossPool)
                        Assert.AreNotSame(bonus.enemyUnit, b, $"{deck.name}: 보너스 적이 보스 풀에 있다");
            }
        }

        // 계약 12 의 불변식 — OnValidate 가 인스펙터 입력만 막으므로 저작된 실물도 확인한다.
        // N <= enemyCount 면 보너스 웨이브가 자기 자신을 재발화한다.
        [Test]
        public void 킬_임계가_마리수보다_크다()
        {
            var bonus = AssetDatabase.LoadAssetAtPath<BonusWaveData>(
                "Assets/_Project/Data/BonusWaveData.asset");
            Assert.IsNotNull(bonus);
            Assert.Greater(bonus.killThreshold, bonus.enemyCount,
                "killThreshold <= enemyCount 면 보너스 웨이브가 스스로를 재발화한다(계약 12)");
        }
    }
}
