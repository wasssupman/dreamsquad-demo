using NUnit.Framework;
using UnityEditor;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // elite-enemy-tier unit 7 — 드래곤 저작 pin.
    //
    // 이 유닛의 두 축은 서로 다른 계약에 묶여 있다:
    //  ① 화염 스택 — `attackCooldown` 이 임의값이 아니다. 화상 지속(4.85s) < 스택 발화 주기
    //     (maxStack × cd) 부등식이 `enemy-fire-stack-shooter` 계약 2·3 이고, cd 를 내리면 펄스가
    //     상시 화상으로 접혀 방어유닛이 확정 사망한다. `targetMode` 도 계약이다 — `Nearest` 면
    //     걸으며 최근접이 바뀌어 **어느 방어유닛도 5스택에 못 간다.**
    //  ② 브레스 — 반각 정의역 (0, 90). 제곱 비교 + 부호 가드의 귀결이고, 45° 는 셀 대각선
    //     경계에 정확히 걸려 부동소수 비교가 동전 던지기가 된다(결정론 요건).
    public class DragonBreathAuthoringTests
    {
        private const string DragonPath = "Assets/_Project/Data/Enemies/Enemy_Dragon.asset";
        private const string KindlerPath = "Assets/_Project/Data/Enemies/Enemy_Kindler.asset";

        private static AttackUnitData Load(string path)
        {
            var u = AssetDatabase.LoadAssetAtPath<AttackUnitData>(path);
            Assert.IsNotNull(u, $"에셋을 찾지 못했다: {path}");
            return u;
        }

        [Test]
        public void Dragon_IsEliteFlyer_WithLift()
        {
            var d = Load(DragonPath);
            Assert.AreEqual(EnemyTier.Elite, d.tier);
            Assert.AreEqual(PlacementLayer.Air, d.EffectiveTraversalLayers,
                "비행 = Air 통행층이다(이동 규칙의 출처). 지상 차단을 무시하고 대공사수만 때린다");
            Assert.Greater(d.flightLift, 0f, "떠 보이는 표현이 없으면 «비행» 이 화면에서 안 읽힌다");
            Assert.AreEqual(-1, d.waypointPathIndex);
        }

        [Test]
        public void Dragon_DeclaresExactlyOneMechanic_AreaBreathOnEveryThirdAttack()
        {
            var d = Load(DragonPath);
            Assert.IsNotNull(d.nightmareMechanics);
            Assert.AreEqual(1, d.nightmareMechanics.Length, "엘리트는 특수 메커니즘 1개다");

            var m = d.nightmareMechanics[0];
            Assert.AreEqual(DcTriggerKind.AttackN, m.trigger.kind);
            Assert.AreEqual(3, m.trigger.period, "«3회 기본공격 이후» 가 저작 의도다");
            Assert.AreEqual(DcPayloadKind.AreaBreath, m.payload.kind);
            Assert.Greater(m.payload.magnitude, 0f, "피해가 0 이면 발동해도 아무 일도 없다");
            Assert.Greater(m.payload.tileRange, 0, "사거리 0 이면 같은 셀만 맞는다");
        }

        // 반각은 **정의역 안 + 경계 밖**이어야 한다.
        [Test]
        public void Dragon_ConeHalfAngle_IsInsideDomain_AndOffTheDiagonalKnifeEdge()
        {
            var m = Load(DragonPath).nightmareMechanics[0];
            Assert.Greater(m.payload.coneHalfAngleDeg, 0f);
            Assert.Less(m.payload.coneHalfAngleDeg, 90f,
                "반각 >= 90 은 제곱 비교의 정의역 밖 — cos²θ = cos²(180−θ) 라 조용히 (180−각) 콘이 된다");
            Assert.AreNotEqual(45f, m.payload.coneHalfAngleDeg,
                "45° 는 셀 대각선 경계에 정확히 걸린다 — 부동소수 비교가 플랫폼별로 갈릴 수 있다");
        }

        // 화염 스택 계약 — 킨들러와 같은 부등식 여유를 갖는지 실제 값으로 확인한다.
        [Test]
        public void Dragon_FireStack_KeepsBurnPulseGap_LikeKindler()
        {
            var d = Load(DragonPath);

            AttackOutput? stack = null;
            foreach (var o in d.outputs)
                if (o.kind == AttackOutputKind.ApplyStack) stack = o;
            Assert.IsTrue(stack.HasValue, "드래곤은 화염 스택 producer 다");
            Assert.AreEqual(Wassup.Battle.Effects.StackKind.Fire, stack.Value.stackKind);
            Assert.AreEqual(5, stack.Value.stackMaxStack,
                "maxStack 은 producer 가 소유하고 StackModifier_Fire 의 atStack 과 명시 일치해야 한다");
            Assert.Greater(stack.Value.duration, d.attackCooldown,
                "perAppDuration <= 공격 쿨다운이면 사격 중 스택이 만료돼 5에 못 간다");

            // 발화 주기 = maxStack × cd. 화상 지속 4.85s 보다 커야 펄스가 «끊긴다».
            float fireCycle = stack.Value.stackMaxStack * d.attackCooldown;
            Assert.Greater(fireCycle, 4.85f,
                $"발화 주기({fireCycle}s) <= 화상 지속(4.85s) — 상시 화상이 되어 방어유닛이 확정 사망한다");

            // 킨들러와 같은 여유(1.15s)를 갖는지 — 같은 계약을 공유한다는 증거.
            var k = Load(KindlerPath);
            AttackOutput? kstack = null;
            foreach (var o in k.outputs)
                if (o.kind == AttackOutputKind.ApplyStack) kstack = o;
            Assert.IsTrue(kstack.HasValue);
            Assert.AreEqual(kstack.Value.stackMaxStack * k.attackCooldown, fireCycle, 0.01f,
                "킨들러와 발화 주기가 다르다 — 둘이 같은 StackModifier_Fire 임계를 공유하므로 여유도 같아야 한다");
        }

        // Nearest 면 걸으며 최근접이 바뀌어 스택이 5에 도달하지 못한다(킨들러 계약이 ★로 표시).
        [Test]
        public void Dragon_FocusesUntilDead_SoStacksCanReachThreshold()
        {
            var d = Load(DragonPath);
            Assert.AreEqual(EnemyTargetMode.FocusUntilDead, d.targetMode,
                "Nearest 면 어느 방어유닛도 5스택에 못 간다 — 화염 설계의 절반이 죽는다");
            Assert.AreEqual(EngageMovement.Halt, d.engageMovement,
                "이동하며 쏘면 대상이 바뀌어 위와 같은 결과가 된다");
        }

        // Dragon 스켈레톤은 애니가 `flying` 하나뿐이다(실측) — 공격/사망은 빈 값이어야 한다.
        [Test]
        public void Dragon_UsesFlyingForLocomotion_AndHasNoOneShotAnimations()
        {
            var d = Load(DragonPath);
            Assert.IsNotNull(d.skeletonDataAsset);
            Assert.AreEqual("flying", d.idleAnimation);
            Assert.AreEqual("flying", d.walkAnimation);
            Assert.IsTrue(string.IsNullOrEmpty(d.attackAnimation),
                "Dragon 에는 attack 애니가 없다 — 빈 값이면 PlayAttack 이 early-return 해 flying 루프가 안 끊긴다");
            Assert.IsTrue(string.IsNullOrEmpty(d.deathAnimation));
            Assert.IsNotNull(d.visualMaterial, "null 이면 스폰이 포기된다");
        }

        // wave-concept-blocks unit 7 — 구 `Dragon_IsNotInAnyLiveDeckPool_Yet` 을 뒤집었다.
        // 그 단언은 «등록은 웨이브 baseline 을 바꾸므로 별도 커밋» 이라는 **연기**를 지키는
        // 것이었고, 그 커밋이 실제로 왔으므로 이제 등록 자체를 지킨다.
        [Test]
        public void Dragon_IsInEveryLiveDeckPool_AndNotAtTheEnd()
        {
            var d = Load(DragonPath);
            string[] decks =
            {
                "Deck_Serpent", "Deck_Coil", "Deck_Twin", "Deck_Spiral",
                "Deck_Zig", "Deck_Hook",
            };
            foreach (string name in decks)
            {
                var deck = AssetDatabase.LoadAssetAtPath<AttackDeck>(
                    $"Assets/_Project/Scripts/Data/Decks/{name}.asset");
                Assert.IsNotNull(deck?.attackUnitPool, name);
                int index = System.Array.IndexOf(deck.attackUnitPool, d);
                Assert.GreaterOrEqual(index, 0,
                    $"{name}: 드래곤이 풀에 없으면 「공습」 컨셉이 스키머만 뽑는다");
                Assert.Less(index, deck.attackUnitPool.Length - 1,
                    $"{name}: 맨 뒤면 ResolveWaveEligibleIndex 전방 순환이 초반 웨이브를 pool[0] 로 쏠리게 한다");
            }
        }
    }
}
