using NUnit.Framework;
using UnityEditor;
using Wassup.BattleCore;
using Wassup.BattleCoreUnity;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // battle-core-rebuild unit 6b2 — 시즌 기믹 **저작 → 정의표**.
    //
    // 제약 6 — 기믹 4종의 수치는 전량 `GimmickDef` 에서 온다. 옛 config 싱글턴 4개가 하던 복사를
    // 빌더가 대신하고, 그 값이 **canonical text 에 실려** `configHash` 가 저작 변경을 본다.
    // 값은 에셋에서 읽어 대조한다 — 밸런스 수치를 리터럴로 박지 않는다(test-procedure 규율).
    [TestFixture]
    public class GimmickAuthoringTests
    {
        private static T Load<T>(string name) where T : GimmickData
        {
            var guids = AssetDatabase.FindAssets($"{name} t:{typeof(T).Name}");
            Assert.IsNotEmpty(guids, $"{name} 가 없다");
            return AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        private static MatchDefinition EmptyDef() => new MatchDefinition();

        [Test]
        public void 레드불_수치가_전량_옮겨진다()
        {
            var so = Load<RedBullGimmickData>("Gimmick_RedBull");
            var row = MatchDefinitionBuilder.ToGimmickDef(so, EmptyDef());
            Assert.AreEqual(GimmickKind.RedBull, row.Kind);
            Assert.AreEqual(so.gimmickId, row.Id);
            Assert.AreEqual(so.redbullSpawnInterval, row.RedBull.SpawnInterval);
            Assert.AreEqual(so.redbullLifetime, row.RedBull.Lifetime);
            Assert.AreEqual(so.maxActivePickups, row.RedBull.MaxActive);
            Assert.AreEqual(so.lastRunAttackSpeedMul, row.RedBull.LastRunAttackSpeedMul);
            Assert.AreEqual(so.lastRunDuration, row.RedBull.LastRunDuration);
            Assert.AreEqual(so.lastRunDamageFraction, row.RedBull.LastRunDamageFraction);
        }

        [Test]
        public void 온천_수치가_전량_옮겨진다()
        {
            var so = Load<OnsenGimmickData>("Gimmick_Onsen");
            var row = MatchDefinitionBuilder.ToGimmickDef(so, EmptyDef());
            Assert.AreEqual(GimmickKind.Onsen, row.Kind);
            Assert.AreEqual(so.heatInterval, row.Onsen.HeatInterval);
            Assert.AreEqual((int)so.flipThreshold, row.Onsen.FlipThreshold);
            Assert.AreEqual(so.healPercent, row.Onsen.HealPercent);
            Assert.AreEqual(so.lossPercent, row.Onsen.LossPercent);
            Assert.AreEqual((int)so.heatMaxStack, row.Onsen.HeatMaxStack);
        }

        [Test]
        public void 퇴근_수치가_전량_옮겨진다()
        {
            var so = Load<ClockOutGimmickData>("Gimmick_ClockOut");
            var row = MatchDefinitionBuilder.ToGimmickDef(so, EmptyDef());
            Assert.AreEqual(GimmickKind.ClockOut, row.Kind);
            Assert.AreEqual((int)so.resignationThreshold, row.ClockOut.ResignationThreshold);
            Assert.AreEqual((int)so.meteorCount, row.ClockOut.MeteorCount);
            Assert.AreEqual(so.meteorDamage, row.ClockOut.MeteorDamage);
            Assert.AreEqual(so.meteorTileRange, row.ClockOut.MeteorTileRange);
            Assert.AreEqual(so.meteorWarningSec, row.ClockOut.MeteorWarningSec);
            Assert.AreEqual(so.meteorStaggerSec, row.ClockOut.MeteorStaggerSec);
        }

        [Test]
        public void 번아웃의_피로_스택_자산은_스택_규칙_줄로_들어가고_줄_번호로_가리킨다()
        {
            var so = Load<BurnoutGimmickData>("Gimmick_Burnout");
            Assert.IsNotNull(so.fatigueStack, "라이브 번아웃은 피로 스택 자산을 든다");
            var def = EmptyDef();
            var row = MatchDefinitionBuilder.ToGimmickDef(so, def);
            Assert.AreEqual(GimmickKind.Burnout, row.Kind);
            Assert.AreEqual(so.fatigueInterval, row.Burnout.FatigueInterval);
            Assert.AreEqual((int)so.fatigueAmount, row.Burnout.FatigueAmount);

            Assert.AreEqual(0, row.Burnout.FatigueStackRule, "빈 표 → 끝에 붙은 첫 줄");
            ref var rule = ref def.StackRules[row.Burnout.FatigueStackRule];
            Assert.AreEqual(so.fatigueStack.name, rule.Id);
            Assert.AreEqual((int)Wassup.BattleCore.Effects.StackKind.Fatigue, rule.Kind);
            Assert.AreEqual((int)so.fatigueStack.maxStack, rule.MaxStack);
            Assert.AreEqual(so.fatigueStack.perAppDuration, rule.PerAppDuration);

            // 같은 자산이 이미 표에 있으면 **새 줄을 안 만든다**(F31 — 자산당 한 줄).
            var again = MatchDefinitionBuilder.ToGimmickDef(so, def);
            Assert.AreEqual(row.Burnout.FatigueStackRule, again.Burnout.FatigueStackRule);
            Assert.AreEqual(1, def.StackRules.Length);
        }

        [Test]
        public void 기믹_수치는_configHash_에_실린다()
        {
            var so = Load<RedBullGimmickData>("Gimmick_RedBull");
            var a = EmptyDef();
            a.Gimmicks = new[] { MatchDefinitionBuilder.ToGimmickDef(so, a) };
            var b = EmptyDef();
            var row = MatchDefinitionBuilder.ToGimmickDef(so, b);
            row.RedBull.LastRunDamageFraction += 0.1f;
            b.Gimmicks = new[] { row };
            Assert.AreNotEqual(a.ComputeConfigHash(), b.ComputeConfigHash(), "수치 하나가 바뀌면 해시가 바뀐다");
            StringAssert.Contains("redbull.lastRunDamageFraction", a.CanonicalText());
        }
    }
}
