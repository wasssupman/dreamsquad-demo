using NUnit.Framework;
using UnityEditor;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // elite-enemy-tier unit 6 — 슬라임 저작 pin. 분열은 «SO 를 드레인에서 직독» 하는 구조라
    // (unit 5 ②) **저작이 곧 계약**이다: 슬롯도 이벤트 필드도 없어서 배선이 어긋나면 잡아줄
    // 중간 계층이 없다. 그래서 여기서 참조·수량·재귀 차단·애니 이름을 못 박는다.
    //
    // 애니 이름을 테스트하는 이유: sack 은 소문자 `walk`/`fall-in` 인데 기존 적 전원은 대문자
    // `Walk`/`Attack1` 이다. Spine 의 FindAnimation 은 대소문자를 구분하고 못 찾으면 **조용히**
    // 아무 것도 재생하지 않는다(ResolveAnimation 이 null 반환) — 오타가 침묵하는 자리다.
    public class SlimeSplitAuthoringTests
    {
        private const string ParentPath = "Assets/_Project/Data/Enemies/Enemy_Slime.asset";
        private const string MidPath = "Assets/_Project/Data/Enemies/Enemy_Slime_Mid.asset";
        private const string ChildPath = "Assets/_Project/Data/Enemies/Enemy_Slime_Small.asset";

        private static AttackUnitData Load(string path)
        {
            var u = AssetDatabase.LoadAssetAtPath<AttackUnitData>(path);
            Assert.IsNotNull(u, $"에셋을 찾지 못했다: {path}");
            return u;
        }

        [Test]
        public void Parent_IsElite_AndDeclaresSplitOnDeath()
        {
            var parent = Load(ParentPath);

            Assert.AreEqual(EnemyTier.Elite, parent.tier, "슬라임은 엘리트다");
            Assert.IsNotNull(parent.nightmareMechanics);
            Assert.AreEqual(1, parent.nightmareMechanics.Length, "메커니즘은 «특수 1개» 가 엘리트 컨셉이다");

            var m = parent.nightmareMechanics[0];
            Assert.AreEqual(DcTriggerKind.OnDeath, m.trigger.kind);
            Assert.AreEqual(DcPayloadKind.SplitOnDeath, m.payload.kind);
            Assert.IsNotNull(m.payload.splitUnit, "splitUnit 이 비면 죽어도 안 갈라진다");
            Assert.GreaterOrEqual(m.payload.magnitude, 1f, "자식 수가 1 미만이면 분열이 소멸이다");
        }

        // 2단계 분열: 슬라임 → 중간 ×2 → 작은 ×4. **배선과 출력 형상**만 못 박는다.
        //
        // ⚠ 체력·공격력 수치는 단언하지 않는다 — **시트가 정본**이다(사용자 결정 2026-08-17).
        // 예전엔 「단계마다 체력 절반」·「공격력 그대로 계승」을 리터럴로 걸었는데, 시트에서
        // 슬라임 사슬을 500/250/150 으로 조정하자 밸런스 조정이 곧 테스트 실패가 됐다.
        // 테스트가 지킬 것은 **배선이 끊기면 죽어도 안 갈라진다** 는 구조이지 튜닝 값이 아니다
        // (test-procedure 의 「밸런스 수치를 리터럴로 못박지 않는다」 규율).
        [Test]
        public void Chain_IsParentToMidToSmall_WithInheritedOutputShape()
        {
            var parent = Load(ParentPath);
            var mid = Load(MidPath);
            var small = Load(ChildPath);

            Assert.AreSame(mid, SplitChain.NextInChain(parent), "슬라임 → 중간 배선(guid)");
            Assert.AreSame(small, SplitChain.NextInChain(mid), "중간 → 작은 배선(guid)");
            Assert.IsNull(SplitChain.NextInChain(small), "작은 슬라임에서 사슬이 끝나야 한다");

            foreach (var u in new[] { mid, small })
            {
                Assert.Greater(u.health, 0f, $"{u.displayName}: 체력 0 이면 스폰 즉시 소멸한다");
                Assert.IsNotNull(u.outputs);
                Assert.AreEqual(parent.outputs.Length, u.outputs.Length, $"{u.displayName}: 공격 출력 형상 계승");
                for (int i = 0; i < parent.outputs.Length; i++)
                    Assert.AreEqual(parent.outputs[i].kind, u.outputs[i].kind,
                        $"{u.displayName}: 출력 종류가 갈리면 분열체가 다른 공격을 한다");
            }
        }

        // 재귀 차단은 세대 카운터가 아니라 **사슬이 유한하다는 사실**이다.
        // (초판은 «자식이 메커닉을 갖지 않는다» 로 고정했는데, 2단계가 의도가 되면서
        //  그 단언은 거짓이 됐다 — 판정을 사슬 검증으로 옮겼다.)
        [Test]
        public void SplitChain_Terminates_WithoutCycle()
        {
            Assert.IsTrue(SplitChain.Validate(Load(ParentPath), out string err), err);

            var small = Load(ChildPath);
            Assert.IsTrue(small.nightmareMechanics == null || small.nightmareMechanics.Length == 0,
                "마지막 단계가 메커니즘을 가지면 사슬이 안 끝난다");
            Assert.AreEqual(EnemyTier.Normal, small.tier, "분열체는 일반 등급이다");
            Assert.AreEqual(EnemyTier.Normal, Load(MidPath).tier, "분열체는 일반 등급이다");
        }

        // 분열은 **각성**을 나누지 않는다 — 총량은 엘리트 본체 하나 몫이고 단계를 늘려도
        // 안 변한다. 안정도 피해(놓쳤을 때의 대가)는 자식에게 남겨 마릿수만큼 커진다.
        //
        // three-minute-kill-race unit 1 — **점수는 이제 마릿수만큼 늘어난다**(결정 A,
        // 2026-08-16): 1킬 = 1점에 예외가 없어 분열체도 1점이다. 「분열체는 0점」 단언은
        // 그래서 삭제했다 — 슬라임 편성량은 웨이브 밸런스에서 조정할 문제이지 점수 산식의
        // 예외가 아니다. 각성 쪽 0 은 그대로다(처치 7회짜리 각성 농장 방지).
        [Test]
        public void SplitOffspring_GiveNoAwakening_ButStillHurtOnLeak()
        {
            var parent = Load(ParentPath);
            Assert.Greater(parent.awakeningReward, 0, "본체가 각성을 낸다");

            foreach (string path in new[] { MidPath, ChildPath })
            {
                var u = Load(path);
                Assert.AreEqual(0, u.awakeningReward,
                    $"{u.displayName}: 분열체가 각성을 주면 처치 7회짜리 각성 농장이 된다(보스보다 많아진다)");
                Assert.GreaterOrEqual(u.stabilityDamage, 1,
                    $"{u.displayName}: 유출 대가는 0 이면 안 된다 — 분열이 «놓쳐도 무해» 가 된다");
            }
        }

        // 부모가 웨이포인트 경로를 쓰면 자식이 부모의 진행도를 못 물려받아
        // **이미 지난 지점으로 되돌아간다**(README 계약 3).
        [Test]
        public void Parent_DoesNotUseWaypointPath()
        {
            Assert.AreEqual(-1, Load(ParentPath).waypointPathIndex);
            Assert.AreEqual(-1, Load(ChildPath).waypointPathIndex);
        }

        [Test]
        public void Both_UseSackSkeleton_WithLowercaseAnimationNames()
        {
            foreach (string path in new[] { ParentPath, MidPath, ChildPath })
            {
                var u = Load(path);
                Assert.IsNotNull(u.skeletonDataAsset, $"{u.displayName}: 스켈레톤 미배선");
                // sack 의 실제 애니는 `fall-in`·`walk` 2종뿐이다(바이너리 실측).
                Assert.AreEqual("walk", u.idleAnimation, $"{u.displayName}: sack 은 idle 애니가 없어 walk 를 쓴다");
                Assert.AreEqual("walk", u.walkAnimation, $"{u.displayName}");
                Assert.AreEqual("fall-in", u.attackAnimation, $"{u.displayName}: 공격 = 몸통 내리찍기");
                Assert.IsTrue(string.IsNullOrEmpty(u.deathAnimation),
                    $"{u.displayName}: death 는 빈 값이어야 한다 — 「죽으면 그냥 분리」(빈 값 = 즉시 Destroy)");
                Assert.IsNotNull(u.visualMaterial,
                    $"{u.displayName}: visualMaterial 이 null 이면 SpawnUnit 이 스폰을 포기한다");
            }
        }

        // 분열체는 웨이브 생성 대상이 아니다 — 분열로만 등장한다(Enemy_Skimmer 선례).
        [TestCase(MidPath)]
        [TestCase(ChildPath)]
        public void Offspring_IsNotInAnyLiveDeckPool(string offspringPath)
        {
            var child = Load(offspringPath);
            string[] decks =
            {
                "Deck_Serpent", "Deck_Coil", "Deck_Twin", "Deck_Spiral",
                "Deck_Zig", "Deck_Hook",
            };
            foreach (string name in decks)
            {
                var deck = AssetDatabase.LoadAssetAtPath<AttackDeck>(
                    $"Assets/_Project/Scripts/Data/Decks/{name}.asset");
                if (deck?.attackUnitPool == null) continue;
                foreach (var u in deck.attackUnitPool)
                    Assert.AreNotSame(child, u,
                        $"{name}: {child.displayName} 이 웨이브 풀에 있다 — 분열로만 등장해야 한다");
            }
        }
    }
}
