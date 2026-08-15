using NUnit.Framework;
using UnityEngine;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    // defender-clock-out unit 5 — 이탈 쿨타임의 **불변식**을 잡는다: 퇴근 대기는 어떤 저작
    // 값으로도 사망 대기를 넘지 못한다. 이 파일이 지키는 것은 초 단위 밸런스가 아니라
    // "방치보다 회수가 이득" 이라는 인센티브의 방향이다 — 그게 뒤집혀도 화면에는 아무
    // 증상이 없어서(unit 5 전 반년간 4초 대 0초로 서 있었다) 테스트 말고는 경보가 없다.
    public class DefenderExitCooldownTests
    {
        private DefenderUnitData _u;

        [SetUp]
        public void SetUp() => _u = ScriptableObject.CreateInstance<DefenderUnitData>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_u);

        [Test]
        public void Defaults_GiveRetireShorterThanDeath()
        {
            // 에셋 YAML 에 키가 없는 기존 유닛 전부가 타는 경로(이니셜라이저).
            Assert.Greater(_u.EffectiveDeathCooldown, 0f, "기본값이 inert 면 이 spec 이 무효다");
            Assert.Less(_u.EffectiveRetireCooldown, _u.EffectiveDeathCooldown);
        }

        [Test]
        public void Retire_NeverExceedsDeath_ForAnyAuthoredRatio()
        {
            _u.deathCooldown = 10f;
            // 마지막 둘은 [Range] 를 우회하는 시트 임포터만이 만들 수 있는 값이다
            // (UnitStatFieldMapper 는 리플렉션으로 필드에 직접 쓴다 → Range·OnValidate 미적용).
            foreach (float ratio in new[] { 0f, 0.4f, 1f, 2.5f, 1000f })
            {
                _u.retireCooldownRatio = ratio;
                Assert.LessOrEqual(_u.EffectiveRetireCooldown, _u.EffectiveDeathCooldown,
                    $"ratio={ratio} 에서 퇴근이 사망보다 길어졌다");
            }
        }

        [Test]
        public void NegativeAuthoring_ClampsToZero_NotNegative()
        {
            // 음수 초가 새면 StartCooldown 은 no-op 이라 조용히 "쿨타임 없음" 이 된다.
            // 그건 사망을 다시 공짜로 만드는 경로다 — 0 으로 접히는 것까지가 계약.
            _u.deathCooldown = -5f;
            _u.retireCooldownRatio = -1f;
            Assert.AreEqual(0f, _u.EffectiveDeathCooldown, 1e-4f);
            Assert.AreEqual(0f, _u.EffectiveRetireCooldown, 1e-4f);
        }

        [Test]
        public void ZeroDeath_MakesBothInert()
        {
            // "0 = inert" 는 이탈 축에서도 유지된다 — 비율이 얼마든 사망이 0 이면 퇴근도 0.
            _u.deathCooldown = 0f;
            _u.retireCooldownRatio = 1f;
            Assert.AreEqual(0f, _u.EffectiveDeathCooldown, 1e-4f);
            Assert.AreEqual(0f, _u.EffectiveRetireCooldown, 1e-4f);
        }

        [Test]
        public void Ratio_ScalesDeathCooldown()
        {
            _u.deathCooldown = 12f;
            _u.retireCooldownRatio = 0.25f;
            Assert.AreEqual(3f, _u.EffectiveRetireCooldown, 1e-4f);
        }
    }
}
