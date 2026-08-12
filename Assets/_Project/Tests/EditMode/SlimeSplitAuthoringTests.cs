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

        [Test]
        public void Child_IsReferencedByParent_AndHasHalfHealth_InheritingAttack()
        {
            var parent = Load(ParentPath);
            var child = Load(ChildPath);

            Assert.AreSame(child, parent.nightmareMechanics[0].payload.splitUnit,
                "부모의 splitUnit 이 Enemy_Slime_Small 을 가리키지 않는다(guid 오배선)");

            // 사용자 지정: 기본 스탯 체력의 50% · 공격력은 그대로 계승
            Assert.AreEqual(parent.health * 0.5f, child.health, 0.01f,
                "자식 체력은 부모의 50% 다");
            Assert.IsNotNull(parent.outputs);
            Assert.IsNotNull(child.outputs);
            Assert.AreEqual(parent.outputs.Length, child.outputs.Length, "공격 출력 형상이 계승돼야 한다");
            for (int i = 0; i < parent.outputs.Length; i++)
            {
                Assert.AreEqual(parent.outputs[i].kind, child.outputs[i].kind);
                Assert.AreEqual(parent.outputs[i].magnitude, child.outputs[i].magnitude, 0.01f,
                    "공격력은 그대로 계승한다(사용자 지정)");
            }
        }

        // 재귀 차단이 세대 카운터가 아니라 **이 한 칸**이다.
        [Test]
        public void Child_HasNoMechanics_SoSplitCannotRecurse()
        {
            var child = Load(ChildPath);
            Assert.IsTrue(child.nightmareMechanics == null || child.nightmareMechanics.Length == 0,
                "자식이 메커니즘을 가지면 무한 분열이 열린다");
            Assert.AreEqual(EnemyTier.Normal, child.tier, "자식은 일반 등급이다");
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
            foreach (string path in new[] { ParentPath, ChildPath })
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

        // 자식은 웨이브 생성 대상이 아니다 — 분열로만 등장한다(Enemy_Skimmer 선례).
        [Test]
        public void Child_IsNotInAnyLiveDeckPool()
        {
            var child = Load(ChildPath);
            string[] decks =
            {
                "Deck_Serpent", "Deck_Coil", "Deck_Twin", "Deck_Spiral",
                "Deck_Zig", "Deck_Hook", "Deck_Endless",
            };
            foreach (string name in decks)
            {
                var deck = AssetDatabase.LoadAssetAtPath<AttackDeck>(
                    $"Assets/_Project/Scripts/Data/Decks/{name}.asset");
                if (deck?.attackUnitPool == null) continue;
                foreach (var u in deck.attackUnitPool)
                    Assert.AreNotSame(child, u,
                        $"{name}: 작은 슬라임이 웨이브 풀에 있다 — 분열로만 등장해야 한다");
            }
        }
    }
}
