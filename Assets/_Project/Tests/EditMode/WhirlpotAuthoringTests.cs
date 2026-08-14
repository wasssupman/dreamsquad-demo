using NUnit.Framework;
using UnityEditor;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // elite-whirlpot unit 2 — Whirlpot 저작 pin.
    //
    // 이 엘리트는 **메커닉이 없다**(README 계약 1). 능력이 payload 가 아니라 저작 3칸
    // (`Melee` · `attackRange` · `attackTargetCount`)에서 나오므로 **저작이 곧 계약**이고,
    // 배선이 어긋나도 잡아줄 중간 계층이 없다. 그래서 여기서 못 박는다.
    //
    // 애니 이름은 문자열 고정에 그치지 않고 `FindAnimation` 으로 **실존까지** 확인한다 —
    // Spine 의 FindAnimation 은 대소문자를 구분하고 못 찾으면 **조용히** 아무 것도 재생하지
    // 않는다(`SlimeSplitAuthoringTests` 가 남긴 교훈). cloud-pot 은 벤더 예제라 애니 이름을
    // 사람이 외울 수 없고, 이 이름들은 바이너리 `.skel` 에서 추출한 것이라 더욱 그렇다.
    public class WhirlpotAuthoringTests
    {
        private const string Path = "Assets/_Project/Data/Enemies/Enemy_Whirlpot.asset";
        private const string CatalogPath = "Assets/_Project/Data/EnemyCatalog.asset";
        private const string LocomotionAnim = "pot-moving-followed-by-rain";

        private static AttackUnitData Load()
        {
            var u = AssetDatabase.LoadAssetAtPath<AttackUnitData>(Path);
            Assert.IsNotNull(u, $"에셋을 찾지 못했다: {Path}");
            return u;
        }

        [Test]
        public void Whirlpot_IsElite_ButCarriesNoMechanic()
        {
            var u = Load();

            Assert.AreEqual(EnemyTier.Elite, u.tier, "Whirlpot 은 엘리트다");
            Assert.IsTrue(u.nightmareMechanics == null || u.nightmareMechanics.Length == 0,
                "★이 엘리트의 능력은 메커닉이 아니라 저작된 공격 축이다(README 계약 1). "
                + "메커닉을 붙이면 DcTriggerSlot 버퍼까지 따라붙어 설계 의도가 흐려진다");
        }

        // 회오리의 실체 = melee/outputs 경로의 광역. `attackTargetCount` 가 1 로 돌아가면
        // 이 적은 그냥 느린 근접 딜러가 되고 정체성이 사라진다.
        [Test]
        public void Whirl_IsTheBaseAttack_MeleeAoe_WithNoSeparateSingleHit()
        {
            var u = Load();

            Assert.AreEqual(EnemyAttackMethod.Melee, u.attackMethod,
                "★`attackTargetCount` 는 melee/outputs 경로 전용이다 — Projectile 이면 광역이 안 나온다");
            Assert.Greater(u.attackTargetCount, 1,
                "★1 이면 회오리가 아니라 단타다");
            Assert.GreaterOrEqual(u.attackRange, 1f,
                "반경 = attackRange 다(README 계약 5) — 0 이면 아무도 안 맞는다");
            Assert.AreEqual(EngageMovement.Halt, u.engageMovement,
                "「멈춰 서서 돈다」의 절반은 이 한 칸이다");

            Assert.IsNotNull(u.outputs);
            Assert.AreEqual(1, u.outputs.Length,
                "출력은 하나뿐이고 그것이 회오리 피해다 — 별도 단일 타격은 존재하지 않는다");
            Assert.AreEqual(AttackOutputKind.Damage, u.outputs[0].kind);
            Assert.Greater(u.outputs[0].magnitude, 0f, "0 이면 회오리가 피해를 안 준다");

            Assert.IsNull(u.projectile, "근접이므로 투사체가 없어야 한다");
        }

        // 유일한 1슬롯 컨셉(`Concept_Heavy`)이 Tanker 만 필터한다. 엘리트는 maxPerWave 1 이라
        // 그 슬롯에 뽑히면 웨이브가 1기로 붕괴한다 — 실제로 이 적을 Tanker 로 저작했을 때
        // `WaveConceptAuthoringTests.EliteWaves_DoNotCollapseToASingleUnit` 이 잡아냈다.
        // 그쪽은 «뽑혔을 때» 만 보는 시드 의존 관측이고, 이 단언은 원인을 결정론으로 막는다.
        [Test]
        public void Whirlpot_IsNotTankerClass_SoItCannotLandInTheOneSlotHeavyConcept()
        {
            Assert.AreNotEqual(EnemyClass.Tanker, Load().enemyClass,
                "★엘리트를 Tanker 로 저작하면 안 된다 — Concept_Heavy 는 슬롯이 1개이고 Tanker 만 "
                + "필터하므로, maxPerWave 1 인 엘리트가 그 슬롯에 들어가면 웨이브가 1기로 붕괴한다");
        }

        [Test]
        public void Whirlpot_AnimationNames_ActuallyExistInTheCloudPotSkeleton()
        {
            var u = Load();
            Assert.IsNotNull(u.skeletonDataAsset, "스켈레톤 미배선");

            var data = u.SpineSkeletonDataAsset.GetSkeletonData(false);
            Assert.IsNotNull(data, "SkeletonData 로드 실패");

            Assert.AreEqual(LocomotionAnim, u.idleAnimation,
                "화분은 걷지 않는다 — 루프 하나를 idle/walk 둘에 쓴다(드래곤의 flying 선례)");
            Assert.AreEqual(LocomotionAnim, u.walkAnimation);
            Assert.IsNotNull(data.FindAnimation(u.idleAnimation),
                $"★'{u.idleAnimation}' 가 cloud-pot 스켈레톤에 없다 — Spine 은 조용히 아무 것도 "
                + "재생하지 않으므로 화분이 setup pose 로 굳는다");

            Assert.IsTrue(string.IsNullOrEmpty(u.attackAnimation),
                "★attack 은 빈 값이어야 한다 — PlayAttack 이 early-return 해서 로코모션 루프가 "
                + "끊기지 않는다. 「돈다」는 이펙트가 전담한다");
            Assert.IsTrue(string.IsNullOrEmpty(u.deathAnimation),
                "death 빈 값 = 즉시 Destroy(드래곤·슬라임과 같은 저작)");

            Assert.IsNotNull(u.visualMaterial,
                "visualMaterial 이 null 이면 스폰이 포기된다(슬라임 저작 pin 과 같은 이유)");
        }

        // 계약 7 — 「어느 적이 회오리를 갖는가」는 프리팹 유무가 결정한다. 그 규율이 실제로
        // 지켜지고 있는지는 «나만 갖고 있다» 로만 관측된다: 누군가 편의로 전 적에 프리팹을
        // 달거나 `attackTargetCount > 1` 판정으로 바꾸면 탱커·짱쎈에도 회오리가 생긴다.
        [Test]
        public void AttackVfxPrefab_IsWiredOnWhirlpot_AndOnNoOtherEnemy()
        {
            var whirlpot = Load();
            Assert.IsNotNull(whirlpot.attackVfxPrefab,
                "★프리팹이 비면 회오리가 사라진다 — 피해만 들어가고 화면에 아무것도 없다");
            Assert.Greater(whirlpot.attackVfxScalePerTile, 0f,
                "타일당 스케일이 0 이면 연출이 점으로 찌그러진다");

            var catalog = AssetDatabase.LoadAssetAtPath<EnemyCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, $"카탈로그를 찾지 못했다: {CatalogPath}");
            Assert.Contains(whirlpot, catalog.units, "Whirlpot 이 카탈로그에 등록돼 있어야 한다");

            foreach (var other in catalog.units)
            {
                if (other == null || other == whirlpot) continue;
                Assert.IsNull(other.attackVfxPrefab,
                    $"'{other.displayName}' 에 공격 VFX 프리팹이 붙었다 — 이 축은 유닛별 opt-in 이고 "
                    + "지금 의도된 보유자는 Whirlpot 하나다. 두 번째가 정당하면 이 단언을 함께 갱신할 것");
            }
        }
    }
}
