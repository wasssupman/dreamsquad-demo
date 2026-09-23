using NUnit.Framework;
using Wassup.BattleCore.Effects;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 6a — 지속 피해 슬롯.
    [TestFixture]
    public class DotSetTests
    {
        private const float Dt = 1f / 60f;

        /// <summary>한 틱 지급량. 호출부(`FieldPrepPhase`)가 하는 일을 그대로 접는다.</summary>
        private static float StepAll(DotSet set)
        {
            float total = 0f;
            for (int i = 0; i < set.Count; i++)
            {
                set.Step(i, Dt, out int ticks, out float perTick, out float continuous);
                total += continuous + ticks * perTick;
            }
            set.RemoveExpired();
            return total;
        }

        // ── 2축 키 ───────────────────────────────────────────────────────────

        [Test]
        public void 출처와_원소_둘_중_하나만_달라도_슬롯이_갈린다()
        {
            var set = new DotSet();
            Assert.IsTrue(set.Apply(DotOrigin.Stack, DotElement.Bleed, 5f, 0.5f, 4f));
            Assert.IsTrue(set.Apply(DotOrigin.Zone, DotElement.Fire, 10f, 1f, 4f),
                "다른 파이프라인의 다른 원소");
            Assert.IsTrue(set.Apply(DotOrigin.Stack, DotElement.Fire, 3f, 1f, 4f),
                "같은 화염이라도 파이프라인이 다르면 자기 슬롯이다");
            Assert.IsFalse(set.Apply(DotOrigin.Stack, DotElement.Bleed, 7f, 0.5f, 4f),
                "둘이 같으면 갱신");
            Assert.AreEqual(3, set.Count);
        }

        [Test]
        public void 장판에서_나오면_장판_요율이_멈춘다()
        {
            // 한 슬롯을 공유하던 시절엔 나중에 온 쪽이 값·주기를 덮어쓰고 남은 시간만 max 로
            // 남아, 출혈 중인 적이 장판을 밟았다 **나와도 장판 요율로 계속 타는** 과피해가 났다.
            var set = new DotSet();
            set.Apply(DotOrigin.Stack, DotElement.Bleed, 5f, 1f, 60f);   // 출혈 — 초당 5, 길다

            // 장판 위 — 매 틱 갱신되는 짧은 지속(옛 `restDuration` 규약: 「나가면 0.2초 뒤」).
            float onZone = 0f;
            for (int t = 0; t < 600; t++)
            {
                set.Apply(DotOrigin.Zone, DotElement.Fire, 20f, 1f, 0.2f);
                onZone += StepAll(set);
            }
            Assert.AreEqual(2, set.Count, "장판 위에서는 둘이 같이 탄다");
            Assert.Greater(onZone, 200f, "10초 동안 출혈 50 + 장판 200 이 들어온다");

            // 장판 밖 — 갱신이 멈춘다. 0.2초(12틱) 뒤 불이 꺼진다.
            for (int t = 0; t < 20; t++) StepAll(set);
            Assert.AreEqual(1, set.Count);
            Assert.AreEqual(DotElement.Bleed, set.Slots[0].Element, "출혈만 남는다");

            // 그 뒤 10초 동안 받는 피해는 **출혈 요율뿐**이다(초당 5 → 약 50).
            float offZone = 0f;
            for (int t = 0; t < 600; t++) offZone += StepAll(set);
            Assert.Less(offZone, 100f, "장판 요율(초당 20)이 새면 200 을 넘는다");
            Assert.Greater(offZone, 20f, "출혈까지 같이 꺼지면 0 이 된다");
        }

        [Test]
        public void 다중_공격자_도트는_합산되지_않는다()
        {
            // 난도질꾼 2기가 물어도 출혈은 한 슬롯이고 남은 시간만 긴 쪽이다
            // (2026-07-30 사용자 결정). 2축 키의 **귀결이지 버그가 아니다.**
            var set = new DotSet();
            set.Apply(DotOrigin.Stack, DotElement.Bleed, 5f, 1f, 3f);
            set.Apply(DotOrigin.Stack, DotElement.Bleed, 5f, 1f, 6f);
            Assert.AreEqual(1, set.Count);
            Assert.AreEqual(5f, set.Slots[0].Scalar, 1e-4f, "요율이 두 배가 되지 않는다");
            Assert.AreEqual(6f, set.Slots[0].Remaining, 1e-4f, "시간만 긴 쪽이다");
        }

        // ── 틱 ───────────────────────────────────────────────────────────────

        [Test]
        public void 새로_걸린_지속_피해는_진입_즉시_1회_준다()
        {
            // F7 — 신규 슬롯이 타이머를 가득 채운 채로 시작하는 규약이고, 생산자 셋이 전부
            // 여기 기대고 있다.
            var set = new DotSet();
            set.Apply(DotOrigin.Zone, DotElement.Fire, 7f, 1f, 3f);
            Assert.AreEqual(7f, StepAll(set), 1e-4f, "첫 틱이 즉발이다");
            Assert.AreEqual(0f, StepAll(set), 1e-4f, "다음은 1초 뒤다");
        }

        [Test]
        public void 연속_지속_피해는_값이_DPS_다()
        {
            var set = new DotSet();
            set.Apply(DotOrigin.OnPlace, DotElement.None, 60f, 0f, 1f);
            Assert.AreEqual(1f, StepAll(set), 1e-4f, "초당 60 = 틱당 1");
        }

        [Test]
        public void 주기가_바뀌면_진행률이_비례로_넘어간다()
        {
            // 큰 주기에서 쌓인 타이머가 작은 주기로 그대로 넘어가면 조기 발동한다(F6).
            var set = new DotSet();
            set.Apply(DotOrigin.Zone, DotElement.Fire, 10f, 2f, 30f);
            StepAll(set);                                // 첫 틱 즉발 → 타이머 ≈ 0
            for (int t = 0; t < 60; t++) StepAll(set);   // 1초 진행 → 주기 2 의 절반

            set.Apply(DotOrigin.Zone, DotElement.Fire, 10f, 1f, 30f);
            Assert.AreEqual(0.5f, set.Slots[0].TickTimer, 0.03f,
                "진행률 50% 가 새 주기의 50% 로 넘어간다(환산이 없으면 1.0 = 즉시 발동)");
        }

        [Test]
        public void 지급은_앞에서부터_제거는_뒤에서부터다()
        {
            // F8 — 역순 지급이면 여러 도트가 걸린 대상의 피해 숫자 표시 순서가 조용히 뒤집힌다.
            // 삽입 순서가 곧 그 순서이고, **가운데가 만료돼도 나머지 순서는 유지된다.**
            var set = new DotSet();
            set.Apply(DotOrigin.Stack, DotElement.Bleed, 1f, 1f, 30f);
            set.Apply(DotOrigin.Zone, DotElement.Fire, 2f, 1f, 0.2f);
            set.Apply(DotOrigin.OnPlace, DotElement.Poison, 3f, 1f, 30f);

            Assert.AreEqual(DotElement.Bleed, set.Slots[0].Element);
            Assert.AreEqual(DotElement.Fire, set.Slots[1].Element);
            Assert.AreEqual(DotElement.Poison, set.Slots[2].Element);

            for (int t = 0; t < 20; t++) StepAll(set);   // 가운데만 만료
            Assert.AreEqual(2, set.Count);
            Assert.AreEqual(DotElement.Bleed, set.Slots[0].Element);
            Assert.AreEqual(DotElement.Poison, set.Slots[1].Element);
        }

        [Test]
        public void 스택_종류는_원소로_접히고_기믹_스택은_그림이_없다()
        {
            Assert.AreEqual(DotElement.Bleed, DotElementMap.FromStack(StackKind.Bleed));
            Assert.AreEqual(DotElement.Fire, DotElementMap.FromStack(StackKind.Fire));
            Assert.AreEqual(DotElement.None, DotElementMap.FromStack(StackKind.Fatigue),
                "피로도는 전투 도트를 만들지 않으므로 오라 대상이 아니다");
        }

        [Test]
        public void 사건이_나르는_묶음은_출처와_원소를_둘_다_되돌린다()
        {
            int arg = DotElementMap.PackArg(DotOrigin.Zone, DotElement.Fire);
            Assert.AreEqual(202, arg, "골든에서 눈으로 읽는 숫자다");
            Assert.AreEqual(DotOrigin.Zone, DotElementMap.OriginOfArg(arg));
            Assert.AreEqual(DotElement.Fire, DotElementMap.ElementOfArg(arg));
        }

        [Test]
        public void 안전_상한이_무한_루프를_막는다()
        {
            float timer = 0f;
            Assert.AreEqual(DotTick.MaxTicksPerFrame, DotTick.Advance(ref timer, 1e-6f, 10f));
            Assert.AreEqual(0, DotTick.Advance(ref timer, 0f, 1f), "연속은 호출부가 따로 처리한다");
        }
    }
}
