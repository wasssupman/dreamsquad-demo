using NUnit.Framework;
using Somnia.Battle.UnitAi;

namespace Somnia.Battle.Tests.EditMode.Core
{
    // 옛 `DefenderAiTests` · `UnitActionPhaseTests` · `EnemyAiStateTransitionTests` 의 복사본
    // (battle-core-rebuild unit 3). 적응한 것: **없다** — `Somnia.Battle.UnitAi` 는 엔진 무참조라
    // 술어가 그대로 산다.
    //
    // 왜 복사하나(중복처럼 보이는 이유): 옛 lane(`Somnia.Battle.Tests.EditMode`)은 Entities 를
    // 참조해 **헤드리스에서 돌지 않는다.** 이 진리표는 새 코어의 공격 루프가 매 틱 보는
    // 것이라 코어 lane 안에서 초록이어야 한다. 옛 사본은 unit 9 에서 사라진다.
    public class UnitAiRulesTests
    {
        private static DefenderAiInput In(bool deploying = false, bool locked = false, bool swinging = false,
                                          DefenderAttackPolicy policy = DefenderAttackPolicy.Target,
                                          bool summonAlive = false)
            => new DefenderAiInput
            {
                deploying = deploying, actionLocked = locked, swinging = swinging,
                policy = policy, summonAlive = summonAlive,
            };

        // ── 방어유닛 진리표 ──────────────────────────────────────────────────

        [Test]
        public void 배치중이_모든_것보다_위다()
        {
            Assert.AreEqual(DefenderAiState.Deploying,
                DefenderAi.Resolve(In(deploying: true, locked: true, swinging: true,
                                      policy: DefenderAttackPolicy.Summon, summonAlive: true)));
        }

        [Test]
        public void 랭크는_잠김_교전중_유지중_대기_순이다()
        {
            Assert.AreEqual(DefenderAiState.Locked,
                DefenderAi.Resolve(In(locked: true, swinging: true,
                                      policy: DefenderAttackPolicy.Summon, summonAlive: true)));
            Assert.AreEqual(DefenderAiState.Engaging,
                DefenderAi.Resolve(In(swinging: true, policy: DefenderAttackPolicy.Summon, summonAlive: true)));
            Assert.AreEqual(DefenderAiState.Sustaining,
                DefenderAi.Resolve(In(policy: DefenderAttackPolicy.Summon, summonAlive: true)));
            Assert.AreEqual(DefenderAiState.Ready, DefenderAi.Resolve(In()));
        }

        [Test]
        public void 유지중은_소환_정책에만_있다()
        {
            Assert.AreEqual(DefenderAiState.Ready,
                DefenderAi.Resolve(In(policy: DefenderAttackPolicy.Target, summonAlive: true)));
            Assert.AreEqual(DefenderAiState.Ready,
                DefenderAi.Resolve(In(policy: DefenderAttackPolicy.Bomb, summonAlive: true)));
            Assert.AreEqual(DefenderAiState.Ready,
                DefenderAi.Resolve(In(policy: DefenderAttackPolicy.Summon, summonAlive: false)));
        }

        [Test]
        public void 공격_시작은_대기와_유지중_둘이다()
        {
            // **유지중도 시도한다** — 소환사는 소환물이 살아 있어도 쿨을 돌려야 하기 때문이다(C2).
            Assert.IsTrue(DefenderAi.CanStartAttack(DefenderAiState.Ready, true));
            Assert.IsTrue(DefenderAi.CanStartAttack(DefenderAiState.Sustaining, true));
            Assert.IsFalse(DefenderAi.CanStartAttack(DefenderAiState.Ready, false));
            Assert.IsFalse(DefenderAi.CanStartAttack(DefenderAiState.Engaging, true));
            Assert.IsFalse(DefenderAi.CanStartAttack(DefenderAiState.Locked, true));
            Assert.IsFalse(DefenderAi.CanStartAttack(DefenderAiState.Deploying, true));
        }

        // ── 행동 단계 ────────────────────────────────────────────────────────

        [Test]
        public void 잠금이_스윙보다_위다()
        {
            Assert.AreEqual(ActionPhase.Free, UnitActionPhase.Resolve(false, false));
            Assert.AreEqual(ActionPhase.Swinging, UnitActionPhase.Resolve(false, true));
            Assert.AreEqual(ActionPhase.Locked, UnitActionPhase.Resolve(true, false));
            Assert.AreEqual(ActionPhase.Locked, UnitActionPhase.Resolve(true, true));
        }

        [Test]
        public void 시작은_자유일_때만이고_스윙은_어디서든_끝난다()
        {
            Assert.IsTrue(UnitActionPhase.CanStartAction(ActionPhase.Free));
            Assert.IsFalse(UnitActionPhase.CanStartAction(ActionPhase.Swinging));
            Assert.IsFalse(UnitActionPhase.CanStartAction(ActionPhase.Locked));
            // 행동 잠금은 **START 만** 막는다 — 이미 시작한 스윙은 완료된다.
            Assert.IsTrue(UnitActionPhase.CanResolveSwing(ActionPhase.Locked));
            Assert.IsTrue(UnitActionPhase.CanResolveSwing(ActionPhase.Swinging));
        }

        // ── 적 FSM ───────────────────────────────────────────────────────────

        [Test]
        public void 어그로가_없으면_사격_대상_유무로_갈린다()
        {
            Assert.AreEqual(AiState.Engaging, EnemyAi.Evaluate(false, false, true));
            Assert.AreEqual(AiState.Marching, EnemyAi.Evaluate(false, false, false));
        }

        [Test]
        public void 어그로는_사격_대상을_덮는다()
        {
            // 배타성이 load-bearing 이다 — 「가디언 없으면 최근접」으로 풀면 가는 길에 만난
            // 유닛과 싸우느라 가디언에 영영 도착하지 않는다.
            Assert.AreEqual(AiState.Standoff, EnemyAi.Evaluate(true, true, false));
            Assert.AreEqual(AiState.Chasing, EnemyAi.Evaluate(true, false, true));
            Assert.AreEqual(AiState.Standoff, EnemyAi.Evaluate(true, true, true));
        }
    }
}
