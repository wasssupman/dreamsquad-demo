using System.Collections.Generic;
using NUnit.Framework;
using Wassup.BattleCore.Combat.Projectile;
using Wassup.BattleCore.Trigger;

namespace Wassup.Tests.EditMode.Core
{
    // unified-effect-layer unit 5 — **저작 조합 검증 한 함수**(`EffectComboRule`)의 표.
    //
    // ① 라이브 조합(`census.md` 표 1 — 카드 · 유닛 능력 · 악몽)은 전부 허용이다(계약 6 — 라이브 무변).
    // ② 옛 「출처 관례」 거절 중 이 unit 이 푼 것(칸 탄 × 대상 탄 · 발사 명세 × 주기 밖 · 남의 배치)은 허용이다.
    // ③ 남은 거절은 사유 셋 중 하나로만 답한다.
    // ④ 답은 숙주·부착 사실이 같으면 같다 — 입력에 출처 칸이 **없다**는 것 자체가 계약이라, 여기서는
    //    「어느 숙주 사실이 답을 가르는가」를 전수로 고정한다.
    [TestFixture]
    public class EffectComboRuleTests
    {
        // 숙주 셋 — 출처가 아니라 사실이다. 카드는 「놓인 방어유닛에 뒤에 붙는다」, 유닛 능력은 「방어유닛이 들고 태어난다」, 악몽은 「적이 들고 태어난다」.
        private static EffectCombo Attached(TriggerKind t, EffectKind p)
            => new EffectCombo { Trigger = t, Payload = p, BindsAfterPlacement = true };

        private static EffectCombo Innate(TriggerKind t, EffectKind p)
            => new EffectCombo { Trigger = t, Payload = p };

        private static EffectCombo Enemy(TriggerKind t, EffectKind p)
            => new EffectCombo { Trigger = t, Payload = p, HostIsEnemy = true };

        private static EffectCombo WithShot(EffectCombo c, BindingClass b, bool fanOut = false)
        {
            c.HasProjectile = true;
            c.Binding = b;
            c.FanOut = fanOut;
            return c;
        }

        private static IEnumerable<TestCaseData> LiveCombos()
        {
            // 1a · 1b — 카드(부착)
            yield return Case("카드 찌르기 바늘", WithShot(Attached(TriggerKind.AttackN, EffectKind.ProjectileToTarget), BindingClass.Entity));
            yield return Case("카드 부메랑", WithShot(Attached(TriggerKind.AttackN, EffectKind.ProjectileToTarget), BindingClass.Direction));
            yield return Case("카드 서리 화살", Attached(TriggerKind.AttackN, EffectKind.ApplyCcToTarget));
            yield return Case("카드 잿불 물기", Attached(TriggerKind.AttackN, EffectKind.ApplyStackToTarget));
            yield return Case("카드 광란", Attached(TriggerKind.AttackN, EffectKind.SelfStatBuff));
            yield return Case("카드 작별 선물", Attached(TriggerKind.OnDeath, EffectKind.SelfTileAoe));
            yield return Case("카드 시체 폭발", Attached(TriggerKind.OnKill, EffectKind.SelfTileAoe));
            yield return Case("카드 퇴근 운석", Attached(TriggerKind.OnRetire, EffectKind.SelfTileAoe));
            yield return Case("카드 인수인계", Attached(TriggerKind.OnRetire, EffectKind.RecallAttachedToFront));
            yield return Case("카드 궁지 폭발", Attached(TriggerKind.OnDamagedN, EffectKind.SelfTileAoe));
            yield return Case("카드 실드 파열", Attached(TriggerKind.OnShieldBreak, EffectKind.SelfTileAoe));
            yield return Case("카드 진동 갑주", Attached(TriggerKind.HealthThreshold, EffectKind.SelfTileAoe));
            yield return Case("카드 실드 자장가", Attached(TriggerKind.OnShieldBreak, EffectKind.AreaSleep));
            yield return Case("카드 가시 갑옷", Attached(TriggerKind.OnDamagedN, EffectKind.NextAttackDoubleFire));
            yield return Case("카드 최후의 저항", Attached(TriggerKind.HealthThreshold, EffectKind.SelfStatBuff));
            yield return Case("카드 탐식", Attached(TriggerKind.OnKill, EffectKind.SelfStatBuff));
            yield return Case("카드 잿불 장판", Attached(TriggerKind.OnKill, EffectKind.SpawnHazard));
            yield return Case("카드 불꽃 회전", Attached(TriggerKind.PeriodicTimer, EffectKind.SelfOrbitProjectile));
            yield return Case("카드 불나방떼", WithShot(Attached(TriggerKind.PeriodicTimer, EffectKind.EmitProjectilePattern), BindingClass.Entity));
            // 1c — 유닛 능력(타고남)
            yield return Case("캐논 폭격", WithShot(Innate(TriggerKind.OnPlace, EffectKind.EmitProjectilePattern), BindingClass.Entity, fanOut: true));
            yield return Case("저격 배치 사격", WithShot(Innate(TriggerKind.OnPlace, EffectKind.EmitProjectilePattern), BindingClass.Direction));
            yield return Case("폭탄맨 통", WithShot(Innate(TriggerKind.OnPlace, EffectKind.EmitProjectilePattern), BindingClass.Cell));
            yield return Case("브루저 충격", Innate(TriggerKind.OnPlace, EffectKind.SelfTileAoe));
            yield return Case("말파이트 지진", Innate(TriggerKind.OnPlace, EffectKind.AreaCc));
            yield return Case("버스터즈 빔", Innate(TriggerKind.OnPlace, EffectKind.AreaDot));
            yield return Case("난도질꾼 출혈", Innate(TriggerKind.OnPlace, EffectKind.AreaApplyStack));
            yield return Case("배스티온 도발(가디언)", Innate(TriggerKind.OnPlace, EffectKind.AreaTaunt));
            yield return Case("가디언 오라", Innate(TriggerKind.OnPlace, EffectKind.AllyStatAura));
            yield return Case("궁수 감속 오라", Innate(TriggerKind.OnPlace, EffectKind.OpponentStatAura));
            yield return Case("실드셔틀 광역 실드", Innate(TriggerKind.OnPlace, EffectKind.GrantShield));
            yield return Case("정찰병 코스트", Innate(TriggerKind.OnPlace, EffectKind.GainCost));
            yield return Case("레인저 쿨감", Innate(TriggerKind.OnPlace, EffectKind.ReduceSkillCooldown));
            yield return Case("실드 캐스트", Innate(TriggerKind.PeriodicTimer, EffectKind.GrantShield));
            // 1d — 악몽(적 타고남)
            yield return Case("짱쎈 지진", Enemy(TriggerKind.HealthThreshold, EffectKind.SelfTileAoe));
            yield return Case("짱쎈 도약", Enemy(TriggerKind.HealthThreshold, EffectKind.SelfBlink));
            yield return Case("짱쎈 궁극기", Enemy(TriggerKind.HealthThreshold, EffectKind.UltimateLeap));
            yield return Case("마메모 자장가", Enemy(TriggerKind.PeriodicTimer, EffectKind.AreaSleep));
            yield return Case("마메모 자기 실드", Enemy(TriggerKind.HealthThreshold, EffectKind.GrantShield));
            yield return Case("마메모 광역 실드", Enemy(TriggerKind.PeriodicTimer, EffectKind.GrantShield));
            yield return Case("나이트메어 폭격", WithShot(Enemy(TriggerKind.PeriodicTimer, EffectKind.EmitProjectilePattern), BindingClass.Cell));
            yield return Case("나이트메어 미사일", WithShot(Enemy(TriggerKind.PeriodicTimer, EffectKind.EmitProjectilePattern), BindingClass.Entity));
            yield return Case("나이트메어 바람 오라", Enemy(TriggerKind.PeriodicTimer, EffectKind.AllyMoveSpeedAura));
            yield return Case("드래곤 브레스", Enemy(TriggerKind.AttackN, EffectKind.AreaBreath));
            yield return Case("슬라임 분열", Enemy(TriggerKind.OnDeath, EffectKind.SplitOnDeath));
        }

        private static TestCaseData Case(string name, EffectCombo c) => new TestCaseData(c).SetName("라이브_" + name.Replace(' ', '_'));

        [TestCaseSource(nameof(LiveCombos))]
        public void 라이브_조합은_허용이다(EffectCombo c)
            => Assert.AreEqual(ComboVerdict.Allowed, EffectComboRule.Check(in c));

        [Test]
        public void 옛_출처_관례_거절은_풀렸다()
        {
            // 타격 운석 — 칸 결합 탄 × 대상 탄(옛 카드 빌더 「셀 바인딩 탄은 미배선」).
            Assert.AreEqual(ComboVerdict.Allowed, EffectComboRule.Check(WithShot(Attached(TriggerKind.AttackN, EffectKind.ProjectileToTarget), BindingClass.Cell)));
            // AA — 카드 × 남의 배치 × 발사 명세(옛 「OnPlace 불가」 · 「발사 명세는 주기만」).
            var aa = WithShot(Attached(TriggerKind.OnPlace, EffectKind.EmitProjectilePattern), BindingClass.Entity, fanOut: true);
            aa.Subject = BindingSubject.Any;
            Assert.AreEqual(ComboVerdict.Allowed, EffectComboRule.Check(in aa));
            // 발사 명세 × 타격 · 피격 · 처치 · 경계 · 실드 파열(카드도 유닛도).
            foreach (var t in new[] { TriggerKind.AttackN, TriggerKind.OnDamagedN, TriggerKind.OnKill, TriggerKind.HealthThreshold, TriggerKind.OnShieldBreak })
            {
                Assert.AreEqual(ComboVerdict.Allowed, EffectComboRule.Check(WithShot(Attached(t, EffectKind.EmitProjectilePattern), BindingClass.Entity)), t.ToString());
                Assert.AreEqual(ComboVerdict.Allowed, EffectComboRule.Check(WithShot(Innate(t, EffectKind.EmitProjectilePattern), BindingClass.Entity)), t.ToString());
            }
            // 피격 N × 아무 효과 · 장판 × 처치 밖 · 궤도 탄 × 주기 밖 · 실드 × 반경(트리거 무관).
            Assert.AreEqual(ComboVerdict.Allowed, EffectComboRule.Check(Attached(TriggerKind.OnDamagedN, EffectKind.AreaSleep)));
            Assert.AreEqual(ComboVerdict.Allowed, EffectComboRule.Check(Attached(TriggerKind.PeriodicTimer, EffectKind.SpawnHazard)));
            Assert.AreEqual(ComboVerdict.Allowed, EffectComboRule.Check(Innate(TriggerKind.AttackN, EffectKind.SelfOrbitProjectile)));
            Assert.AreEqual(ComboVerdict.Allowed, EffectComboRule.Check(Enemy(TriggerKind.HealthThreshold, EffectKind.GrantShield)));
        }

        [Test]
        public void 남은_거절은_사유_셋이다()
        {
            // 붙는 순간 이미 지난 자기 사건 · 사건이 없다.
            Assert.AreEqual(ComboVerdict.NeverFires, EffectComboRule.Check(Attached(TriggerKind.OnPlace, EffectKind.SelfStatBuff)), "카드 × 자기 배치");
            Assert.AreEqual(ComboVerdict.NeverFires, EffectComboRule.Check(Enemy(TriggerKind.OnPlace, EffectKind.AreaSleep)), "적 × 배치");
            Assert.AreEqual(ComboVerdict.NeverFires, EffectComboRule.Check(Enemy(TriggerKind.OnRetire, EffectKind.SelfTileAoe)), "적 × 퇴근");
            Assert.AreEqual(ComboVerdict.NeverFires, EffectComboRule.Check(Innate(TriggerKind.None, EffectKind.SelfStatBuff)), "트리거 없음");
            // 원점을 못 낸다.
            var anyKill = Attached(TriggerKind.OnKill, EffectKind.SelfStatBuff);
            anyKill.Subject = BindingSubject.Any;
            Assert.AreEqual(ComboVerdict.NoOrigin, EffectComboRule.Check(in anyKill), "남의 사건은 배치만");
            Assert.AreEqual(ComboVerdict.NoOrigin, EffectComboRule.Check(WithShot(Innate(TriggerKind.OnPlace, EffectKind.EmitProjectilePattern), BindingClass.Cell, fanOut: true)), "전원 × 칸 결합");
            Assert.AreEqual(ComboVerdict.NoOrigin, EffectComboRule.Check(WithShot(Innate(TriggerKind.OnPlace, EffectKind.EmitProjectilePattern), BindingClass.Direction, fanOut: true)), "전원 × 방향 결합");
            // 효과가 그 원점 형을 못 받는다.
            Assert.AreEqual(ComboVerdict.ShapeMismatch, EffectComboRule.Check(Attached(TriggerKind.OnKill, EffectKind.BountyMark)), "부착 즉시 전용 × 사건");
            Assert.AreEqual(ComboVerdict.ShapeMismatch, EffectComboRule.Check(Attached(TriggerKind.OnRetire, EffectKind.SelfStatBuff)), "퇴근 × 자기 버프");
            Assert.AreEqual(ComboVerdict.ShapeMismatch, EffectComboRule.Check(Enemy(TriggerKind.OnDeath, EffectKind.AreaSleep)), "죽음 × 살아 있는 주체 효과");
            var taunt = Innate(TriggerKind.OnPlace, EffectKind.AreaTaunt);
            taunt.HostCannotHoldAggro = true;
            Assert.AreEqual(ComboVerdict.ShapeMismatch, EffectComboRule.Check(in taunt), "도발 × 가디언 아님");
        }

        [Test]
        public void 답을_가르는_숙주_사실은_정해진_자리에서만_갈린다()
        {
            // 전수: 트리거 × 주체 × 효과 × (탄 없음 · 결합 셋) × 전원. 숙주 사실 하나만 바꿨을 때 답이 갈리는 곳이
            // 그 사실의 규칙 자리뿐인가 — 새 분기가 몰래 한 숙주 사실에 기대면 여기서 빨개진다.
            var triggers = (TriggerKind[])System.Enum.GetValues(typeof(TriggerKind));
            var payloads = (EffectKind[])System.Enum.GetValues(typeof(EffectKind));
            var subjects = new[] { BindingSubject.Self, BindingSubject.Any };
            int checkedCombos = 0;
            foreach (var t in triggers)
            foreach (var s in subjects)
            foreach (var p in payloads)
            for (int shot = -1; shot <= (int)BindingClass.Direction; shot++)
            foreach (var fan in new[] { false, true })
            {
                var c = new EffectCombo { Trigger = t, Subject = s, Payload = p, HasProjectile = shot >= 0, Binding = shot >= 0 ? (BindingClass)shot : default, FanOut = fan };
                var baseV = EffectComboRule.Check(in c);

                var late = c; late.BindsAfterPlacement = true;
                if (EffectComboRule.Check(in late) != baseV)
                    Assert.IsTrue(t == TriggerKind.OnPlace && s == BindingSubject.Self, $"부착 시점이 {t}×{s}×{p} 를 갈랐다");

                var aggro = c; aggro.HostCannotHoldAggro = true;
                if (EffectComboRule.Check(in aggro) != baseV)
                    Assert.AreEqual(EffectKind.AreaTaunt, p, $"가디언 여부가 {t}×{p} 를 갈랐다");

                var enemy = c; enemy.HostIsEnemy = true;
                if (EffectComboRule.Check(in enemy) != baseV)
                    Assert.IsTrue(t == TriggerKind.OnPlace || t == TriggerKind.OnRetire, $"진영이 {t}×{p} 를 갈랐다");
                checkedCombos++;
            }
            Assert.Greater(checkedCombos, 0);
        }

        // skill-data-table unit 4 — 시전(액티브 카드). 시전 ⇔ 액티브 효과 · 주인 없는 시전이라 비율형은 기준 없음 · 숙주 사실은 안 본다.
        [Test]
        public void 시전은_액티브_효과와만_짝이고_비율형은_기준이_없다()
        {
            for (var k = EffectKind.ActiveMeteor; k <= EffectKind.ActivePortal; k++)
            {
                var cast = new EffectCombo { Trigger = TriggerKind.Cast, Payload = k, CastHasNoOwner = true };
                Assert.AreEqual(ComboVerdict.Allowed, EffectComboRule.Check(in cast), k.ToString());
                var enemy = cast; enemy.HostIsEnemy = true; enemy.BindsAfterPlacement = true; enemy.HostCannotHoldAggro = true;
                Assert.AreEqual(ComboVerdict.Allowed, EffectComboRule.Check(in enemy), k + " — 숙주 사실은 시전을 가르지 않는다");
                var ratio = cast; ratio.Magnitude = MagnitudeMode.OwnerStatRatio;
                Assert.AreEqual(ComboVerdict.NoRatioBasis, EffectComboRule.Check(in ratio), k + " — 주인 없는 시전");
                var onEvent = new EffectCombo { Trigger = TriggerKind.AttackN, Payload = k };
                Assert.AreEqual(ComboVerdict.ShapeMismatch, EffectComboRule.Check(in onEvent), k + " — 액티브 효과는 사건에 못 단다");
                Assert.IsFalse(SkillRouting.IsSkill(k), k + " — 라우팅 표 밖(실행자는 카드 빌더가 id 로)");
            }
            var castTap = new EffectCombo { Trigger = TriggerKind.Cast, Payload = EffectKind.ProjectileToTarget };
            Assert.AreEqual(ComboVerdict.ShapeMismatch, EffectComboRule.Check(in castTap), "시전은 액티브 효과만");
            Assert.IsFalse(SkillRouting.HasDetector(TriggerKind.Cast, false), "시전은 사건이 아니다 — 감지자 없음");
        }

        [Test]
        public void 사유_문안은_셋이다()
        {
            Assert.AreEqual("원점을 못 낸다", EffectComboRule.Describe(ComboVerdict.NoOrigin));
            Assert.AreEqual("효과가 그 원점 형을 못 받는다", EffectComboRule.Describe(ComboVerdict.ShapeMismatch));
            StringAssert.StartsWith("붙는 순간 이미 지난 자기 사건", EffectComboRule.Describe(ComboVerdict.NeverFires));
            // skill-data-table unit 3 — 비율형 수치 사유 둘이 뒤에 붙었다(append-only).
            StringAssert.StartsWith("비율 기준이 없다", EffectComboRule.Describe(ComboVerdict.NoRatioBasis));
            StringAssert.StartsWith("그 효과에는 비율 칸이 없다", EffectComboRule.Describe(ComboVerdict.NoRatioField));
            Assert.AreEqual(6, System.Enum.GetValues(typeof(ComboVerdict)).Length, "허용 + 원점 사유 셋 + 비율 사유 둘");
        }
    }
}
