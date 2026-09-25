using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using Wassup.BattleCore;
using Wassup.BattleCore.Effects;
using Wassup.BattleCore.Map;
using Wassup.BattleCore.Move;
using Wassup.UnitAi;

namespace Wassup.Tests.EditMode.Core
{
    // battle-core-rebuild unit 9 구현 2 — 옛 ECS 감지·이동 테스트가 증언하던 **규칙**을 코어로 옮긴다.
    //
    // 짝 지도 `ledgers/retire-test-pairs.md` 「규칙 누락 의심」 15~19. 옛 기계(엔티티·흐름장 싱글턴을
    // 손으로 깐 월드)는 옮기지 않았다 — 판을 `BattleMatch` 로 굴리고 판 위에서 보이는 것으로 묻는다.
    // 기존 `DetectionRulesTests` · `MovementRulesTests` · `MovePureMathTests` · `UnitAiRulesTests` ·
    // `PatrolAreaMathTests` 가 이미 진 단언은 되풀이하지 않는다.
    //
    // ⚠ 여기 수치(반경·속도·칸)는 **게임 값이 아니라 픽스처**다. 시간 단언은 코어 상수
    // (`AiMovePhase.GraceSeconds` 등)에서 틱 수로 파생한다.
    public class RetiredDetectionMovePortTests
    {
        private static int Ticks(float seconds) => (int)math.ceil(seconds / BattleMatch.Dt);

        private static BattleMatch Begin(MatchDefinition def)
        {
            def.ConfigHash = def.ComputeConfigHash();
            var m = new BattleMatch(def);
            m.Begin();
            return m;
        }

        private static MatchDefinition Def(MapSnapshot map, float detection, float speed = 0f,
                                           int enemyTraversal = LayerBits.Path, int aggroCapacity = 0)
            => CoreMapFixtures.Definition(map, enemySpeed: speed, detectionRange: detection,
                                          enemyTraversal: enemyTraversal, defenderW: 1, defenderH: 1,
                                          aggroCapacity: aggroCapacity);

        private static Unit Last(BattleMatch m) => m.World.Units[m.World.Units.Count - 1];

        private static Unit Defender(BattleMatch m, int2 cell, int defIndex = 0)
        {
            Assert.IsTrue(m.Apply(Command.DebugSpawnDefender(defIndex, cell)).Accepted, "방어유닛 스폰");
            return Last(m);
        }

        private static Unit Enemy(BattleMatch m, int2 cell, int defIndex = 0)
        {
            Assert.IsTrue(m.Apply(Command.DebugSpawnEnemy(defIndex, cell)).Accepted, "적 스폰");
            return Last(m);
        }

        private static List<CoreEvent> Listen(BattleMatch m, CoreEventKind kind)
        {
            var got = new List<CoreEvent>();
            m.Bus.Subscribe(kind, 0, got.Add);
            return got;
        }

        private static void Tick(BattleMatch m, int n)
        {
            for (int i = 0; i < n; i++) m.Tick();
        }

        // 12×5 빈 판. 적은 (2,2) 에 **서 있고**(속도 0) 방어유닛은 4칸 옆(6,2).
        // 속도 0 이 곧 「자기주도 변위 0」이다 — 막힘 해제 테스트가 이 성질을 쓴다.
        private static (BattleMatch m, Unit enemy, Unit defender) StandingHunter(
            float detection = 5f, float speed = 0f, int aggroCapacity = 0)
        {
            var map = CoreMapFixtures.Open(12, 5, new int2(11, 2));
            var m = Begin(Def(map, detection, speed, aggroCapacity: aggroCapacity));
            var d = Defender(m, new int2(6, 2));
            var e = Enemy(m, new int2(2, 2));
            m.Tick();
            return (m, e, d);
        }

        // ══ 15 · DetectionSystemTests ══════════════════════════════════════════

        // 옛 DetectionSystemTests::무제한이어도_마스크_밖은_발견하지_않는다 — 무제한은 반경만 건너뛴다, 대상 진영 필터는 그대로 지난다
        [Test]
        public void 무제한이어도_대상_진영_밖은_발견하지_않는다()
        {
            var map = CoreMapFixtures.Open(12, 5, new int2(11, 2));
            var def = Def(map, -1f);
            def.Enemies[0].TargetFactions = (int)Wassup.Battle.Units.Faction.DefenderCore;   // 유닛을 안 노린다
            var m = Begin(def);
            Defender(m, new int2(6, 2));
            var e = Enemy(m, new int2(2, 2));
            m.Tick();

            Assert.IsFalse(e.Detection.Hunting, "무제한은 «반경만» 건너뛴다 — 대상 진영 필터는 지나야 한다");
        }

        // 옛 DetectionSystemTests::클래스_마스크_밖은_발견하지_않는다 — 못 때리는(직업 필터 밖) 방어유닛은 감지 후보가 아니다
        [Test]
        [Ignore("unit 9 — 옛 규칙과 다름: 옛 DetectionSystem 은 EnemyTargetFilter.classMask 로 감지 후보를 걸렀는데 " +
                "코어 감지 후보 술어(ReachProbe.IsLegalDetectionTarget, Move/ReachProbe.cs:56-63)는 진영·층만 보고 " +
                "직업 필터(AttackDef.HasClassFilter/ClassMask — 공격 쪽은 CombatPhase.cs:1634 ClassAllowed)를 안 본다")]
        public void 직업_필터_밖의_방어유닛은_발견하지_않는다()
        {
            var map = CoreMapFixtures.Open(12, 5, new int2(11, 2));
            var def = Def(map, 5f);
            def.Units[0].Role = 1;                                   // 픽스처 직업 A
            def.Enemies[0].Attack.HasClassFilter = true;
            def.Enemies[0].Attack.ClassMask = 1 << 2;                // 직업 B 만 때린다
            var m = Begin(def);
            Defender(m, new int2(6, 2));
            var e = Enemy(m, new int2(2, 2));
            m.Tick();

            Assert.IsFalse(e.Detection.Hunting, "직업 필터 밖 방어유닛을 발견했다(계약 4)");
        }

        // 옛 DetectionSystemTests::어그로된_적은_감지하지_않는다 — 어그로가 감지를 이긴다
        [Test]
        public void 어그로된_적은_감지를_놓는다()
        {
            var (m, e, d) = StandingHunter(aggroCapacity: 1);
            Assert.IsTrue(e.Detection.Hunting, "전제 — 감지가 섰다");

            m.World.AggroRequests.Add(AggroRequest.Hit(e.Id, d.Id));
            m.Tick();

            Assert.IsFalse(e.Aggro.Target.IsNone, "전제 — 어그로가 걸렸다");
            Assert.IsFalse(e.Detection.Hunting, "어그로 중인데 감지가 서 있다(계약 2)");
            Assert.IsNull(e.Detection.Chase, "추격판도 돌려준다");
        }

        // 옛 DetectionSystemTests::동거리_후보는_낮은_simId_가_뽑힌다 — 동거리 동률은 낮은 id(결정론)
        [Test]
        public void 동거리_후보는_먼저_난_낮은_id_가_뽑힌다()
        {
            var map = CoreMapFixtures.Open(12, 5, new int2(11, 2));
            var m = Begin(Def(map, 5f));
            var first = Defender(m, new int2(5, 4));     // 위쪽 — 먼저 났다(낮은 id)
            var second = Defender(m, new int2(5, 0));    // 아래쪽 — 같은 거리, 같은 경로 비용
            var e = Enemy(m, new int2(5, 2));
            m.Tick();

            Assert.Less(first.Id.Value, second.Id.Value, "전제 — 먼저 난 쪽이 낮은 id");
            Assert.IsTrue(e.Detection.Hunting);
            Assert.AreEqual(first.Id, e.Detection.Target, "동거리는 낮은 id 가 이겨야 한다(순회 순서에 기대지 않는다)");
        }

        // 옛 DetectionSystemTests::대상이_죽으면_1초간_사냥을_유지한다 — 대상을 잃어도 관성 동안 사냥을 유지하고 대상은 비운다
        [Test]
        public void 대상이_사라지면_관성_동안_사냥을_유지하고_지나면_놓는다()
        {
            var (m, e, d) = StandingHunter();
            Assert.IsTrue(e.Detection.Hunting);

            m.Apply(Command.DebugDestroy(d.Id));
            Tick(m, Ticks(AiMovePhase.GraceSeconds * 0.5f));
            Assert.IsTrue(e.Detection.Hunting, "대상이 사라진 직후에도 관성으로 사냥을 유지한다");
            Assert.IsTrue(e.Detection.Target.IsNone, "대상은 비운다");

            Tick(m, Ticks(AiMovePhase.GraceSeconds * 0.5f) + 10);
            Assert.IsFalse(e.Detection.Hunting, "관성이 만료되면 사냥을 놓는다");
        }

        // 옛 DetectionSystemTests::관성_중_새_후보가_생기면_즉시_채택한다 — 관성 중 새 후보는 즉시 채택, 관성은 꺼진다
        [Test]
        public void 관성_중_새_후보가_생기면_즉시_채택한다()
        {
            var (m, e, d) = StandingHunter();
            m.Apply(Command.DebugDestroy(d.Id));
            Tick(m, 10);
            Assert.IsTrue(e.Detection.Hunting, "전제 — 관성 중");
            Assert.Greater(e.Detection.Grace, 0f, "전제 — 관성이 흐르고 있다");

            var d2 = Defender(m, new int2(6, 3));
            m.Tick();

            Assert.AreEqual(d2.Id, e.Detection.Target, "관성 중 새 후보를 즉시 채택해야 한다");
            Assert.AreEqual(0f, e.Detection.Grace, 1e-6f, "채택하면 관성이 꺼진다");
        }

        // 옛 DetectionSystemTests::관성을_거쳐_다시_물어도_사건이_늘지_않는다 — 관성을 거친 재획득은 새 발견이 아니다
        [Test]
        public void 관성을_거쳐_다시_물어도_발견_사건이_늘지_않는다()
        {
            var map = CoreMapFixtures.Open(12, 5, new int2(11, 2));
            var m = Begin(Def(map, 5f));
            var marks = Listen(m, CoreEventKind.Detected);
            var d = Defender(m, new int2(6, 2));
            var e = Enemy(m, new int2(2, 2));
            m.Tick();
            Assert.AreEqual(1, marks.Count, "전제 — 첫 발견 1건");

            m.Apply(Command.DebugDestroy(d.Id));
            Tick(m, 10);
            Defender(m, new int2(6, 3));
            Tick(m, 30);

            Assert.IsTrue(e.Detection.Hunting);
            Assert.AreEqual(1, marks.Count, "관성 사이 `Hunting` 이 유지되므로 재획득은 새 발견이 아니다");
        }

        // 옛 DetectionSystemTests::유지_중에는_더_가까운_후보로_갈아타지_않는다 — 문 대상은 유지 임계 안이면 안 바뀐다(튐 방지)
        [Test]
        public void 유지_중에는_더_가까운_후보로_갈아타지_않는다()
        {
            var (m, e, far) = StandingHunter();
            Assert.AreEqual(far.Id, e.Detection.Target, "전제 — 먼 쪽을 물었다");

            // 더 가깝지만 공격 사거리 밖(교전 상태로 넘어가지 않게) — 순수하게 「감지 대상 유지」만 묻는다.
            Defender(m, new int2(4, 2));
            Tick(m, 5);

            Assert.AreEqual(far.Id, e.Detection.Target, "유지 임계 안이면 더 가까운 후보가 생겨도 대상이 바뀌면 안 된다");
        }

        // 옛 DetectionSystemTests::사냥_중_2초간_못_움직이면_감지를_놓는다 — 막힘이 임계를 넘으면 놓고 재감지를 억제한다
        [Test]
        public void 사냥_중_못_움직이면_임계_뒤_감지를_놓고_억제한다()
        {
            var (m, e, _) = StandingHunter(speed: 0f);
            Assert.IsTrue(e.Detection.Hunting, "막히기 전에는 감지가 서 있다");

            Tick(m, Ticks(AiMovePhase.StuckReleaseSeconds) + 10);

            Assert.IsFalse(e.Detection.Hunting, "임계 동안 못 갔으면 감지를 놓는다");
            Assert.Greater(e.Detection.Suppress, 0f, "해제 뒤 재감지를 억제한다");
        }

        // 옛 DetectionSystemTests::행동정지_CC_중에는_막힘이_누적되지_않는다 — CC 로 못 움직이는 것은 막힘이 아니다
        [Test]
        public void 행동정지_CC_중에는_막힘이_누적되지_않는다()
        {
            var (m, e, _) = StandingHunter(speed: 0f);
            float stun = AiMovePhase.StuckReleaseSeconds * 3f;
            e.Cc.Apply(CcSlotKind.Stun, stun, float3.zero, SimEntityId.None);

            Tick(m, Ticks(AiMovePhase.StuckReleaseSeconds * 1.5f));

            Assert.IsTrue(e.MovementLocked, "전제 — 기절 중");
            Assert.IsTrue(e.Detection.Hunting, "CC 중 제자리는 막힘이 아니다 — 플레이어가 CC 를 쓸수록 적이 사냥을 그만두면 안 된다");
            Assert.AreEqual(0f, e.Detection.Suppress, 1e-6f);
        }

        // 옛 DetectionSystemTests::무제한_감지는_막힘_해제에서_면제된다 — 무제한 사냥은 막힘 타이머가 취소하지 못한다
        [Test]
        public void 무제한_감지는_막힘_해제에서_면제된다()
        {
            var (m, e, _) = StandingHunter(detection: -1f, speed: 0f);
            Assert.IsTrue(e.Detection.Hunting);

            Tick(m, Ticks(AiMovePhase.StuckReleaseSeconds * 1.5f));

            Assert.IsTrue(e.Detection.Hunting, "무제한 사냥은 막힘 해제 대상이 아니다");
            Assert.AreEqual(0f, e.Detection.Suppress, 1e-6f, "억제도 걸리면 안 된다");
        }

        // 옛 DetectionSystemTests::움직이고_있으면_막힘이_누적되지_않는다 — 이동 중이면 막힘 타이머가 0 이다
        [Test]
        public void 움직이고_있으면_막힘이_누적되지_않는다()
        {
            var map = CoreMapFixtures.Open(14, 5, new int2(13, 2));
            var m = Begin(Def(map, 8f, speed: 2f));
            Defender(m, new int2(9, 2));
            var e = Enemy(m, new int2(1, 2));
            m.Tick();
            Assert.IsTrue(e.Detection.Hunting, "전제 — 감지");

            float x0 = e.Position.x;
            Tick(m, Ticks(1f));

            Assert.Greater(e.Position.x, x0 + 0.5f, "전제 — 걷고 있다");
            Assert.IsTrue(e.Detection.Hunting);
            Assert.AreEqual(0f, e.Detection.Stuck, 1e-6f, "이동 중인데 막힘이 쌓였다");
        }

        // ══ 16 · DetectionChaseFieldTests ══════════════════════════════════════
        //
        // 9×7 판, x=4 열은 장식(지상은 못 지나고 비행만 연다 — `LayerBits.Derive`). 구멍을 뚫으면
        // 「직선으로 가깝지만 경로로 먼」 배치가 된다.

        private const int WallX = 4;

        private static MapSnapshot WallMap(int openY = -1)
        {
            var map = CoreMapFixtures.Open(9, 7, new int2(8, 3));
            for (int y = 0; y < 7; y++)
            {
                if (y == openY) continue;
                CoreMapFixtures.Block(map, new int2(WallX, y));
            }
            return map;
        }

        // 옛 DetectionChaseFieldTests::직선_최근접이_우회면_경로가_짧은_쪽을_고른다 — 탐침한 것 중 경로가 가장 짧은 대상을 고른다
        [Test]
        public void 직선_최근접이_우회면_경로가_짧은_쪽을_고른다()
        {
            var m = Begin(Def(WallMap(openY: 6), 5f));
            var near = Defender(m, new int2(5, 2));   // 직선 2 — 벽을 돌아야 한다
            var far = Defender(m, new int2(0, 2));    // 직선 3 — 직통
            var e = Enemy(m, new int2(3, 2));
            m.Tick();

            Assert.IsTrue(e.Detection.Hunting, "둘 다 갈 수 있으므로 감지는 성립한다");
            Assert.AreEqual(far.Id, e.Detection.Target,
                $"직선 최근접(near={near.Id.Value})은 벽을 크게 돌아야 한다 — 「첫 도달 가능」을 채택하면 적이 목표에서 멀어진다");
        }

        // 옛 DetectionChaseFieldTests::우회가_상한을_넘으면_원래_가던_길로_간다 — 우회가 반경×상한 배수를 넘으면 「갈 수 없다」
        [Test]
        public void 우회가_상한을_넘으면_원래_가던_길로_간다()
        {
            var m = Begin(Def(WallMap(openY: 6), 2f));
            Defender(m, new int2(5, 2));
            var e = Enemy(m, new int2(3, 2));
            m.Tick();

            Assert.IsFalse(e.Detection.Hunting,
                "직선으로는 반경 안이지만 경로는 반경의 상한 배수를 넘는다 — 「갈 수 있다」로 치면 이동과 유지 술어가 갈린다");
        }

        // 옛 DetectionChaseFieldTests::같은_우회도_반경이_넓으면_통과한다 — 상한은 거리가 아니라 반경 대비 비율
        [Test]
        public void 같은_우회도_반경이_넓으면_통과한다()
        {
            var m = Begin(Def(WallMap(openY: 6), 5f));
            var d = Defender(m, new int2(5, 2));
            var e = Enemy(m, new int2(3, 2));
            m.Tick();

            Assert.IsTrue(e.Detection.Hunting, "상한은 `반경 × MaxDetourFactor` 라 반경이 넓어지면 같은 우회가 허용된다");
            Assert.AreEqual(d.Id, e.Detection.Target);
        }

        // 옛 DetectionChaseFieldTests::비행은_벽_너머_방어유닛을_감지한다 — 비행은 벽 너머에도 갈 길이 있다(층 무관 규칙)
        [Test]
        public void 비행은_벽_너머_방어유닛을_감지한다()
        {
            var m = Begin(Def(WallMap(), 6f, enemyTraversal: LayerBits.Air));
            var d = Defender(m, new int2(6, 2));
            var e = Enemy(m, new int2(2, 2));
            m.Tick();

            Assert.IsTrue(e.Detection.Hunting, "비행은 벽 열을 지나므로 「갈 수 있는 경로」가 있다 — 추격판이 지상 마스크로 구워지면 빨갛다");
            Assert.AreEqual(d.Id, e.Detection.Target);
            Assert.IsNotNull(e.Detection.Chase, "대상 지향 추격판이 붙는다");
        }

        // 옛 DetectionChaseFieldTests::지상은_벽_너머_방어유닛에게_가지_않는다 — 경로가 없으면 원래 가던 길, 추격판도 없다
        [Test]
        public void 지상은_벽_너머_방어유닛에게_가지_않는다()
        {
            var m = Begin(Def(WallMap(), 6f));
            Defender(m, new int2(6, 2));
            var e = Enemy(m, new int2(2, 2));
            m.Tick();

            Assert.IsFalse(e.Detection.Hunting, "지상은 벽 열을 못 지나므로 갈 경로가 없다");
            Assert.IsNull(e.Detection.Chase, "경로가 없으면 추격판을 붙이지 않는다");
        }

        // 옛 DetectionChaseFieldTests::지상도_비행도_반경_판정_자체는_같다 — 반경 판정은 층을 안 본다
        [Test]
        public void 반경_밖이면_비행이어도_안_걸린다()
        {
            var m = Begin(Def(WallMap(), 0.5f, enemyTraversal: LayerBits.Air));
            Defender(m, new int2(6, 2));
            var e = Enemy(m, new int2(2, 1));
            m.Tick();

            Assert.IsFalse(e.Detection.Hunting, "반경 판정은 층과 무관하다 — 비행이라고 반경을 건너뛰지 않는다");
        }

        // 옛 DetectionChaseFieldTests::최근접이_도달_불가면_다음_후보를_잡는다 — 「갈 수 있는 적이 있으면」이 규칙이다
        [Test]
        public void 최근접이_도달_불가면_다음_후보를_잡는다()
        {
            var m = Begin(Def(WallMap(), 6f));
            var far = Defender(m, new int2(0, 2));    // 3칸, 같은 쪽
            var near = Defender(m, new int2(5, 2));   // 2칸, 벽 너머
            var e = Enemy(m, new int2(3, 2));
            m.Tick();

            Assert.IsTrue(e.Detection.Hunting, "갈 수 있는 후보가 하나라도 있으면 감지가 성립한다");
            Assert.AreEqual(far.Id, e.Detection.Target,
                $"최근접(near={near.Id.Value}, 벽 너머)은 못 가므로 다음 후보를 잡아야 한다");
        }

        // 옛 DetectionChaseFieldTests::후보_전부_도달_불가면_원래_가던_길로_간다
        [Test]
        public void 후보_전부_도달_불가면_원래_가던_길로_간다()
        {
            var m = Begin(Def(WallMap(), 8f));
            Defender(m, new int2(5, 2));
            Defender(m, new int2(6, 1));
            var e = Enemy(m, new int2(2, 2));
            m.Tick();

            Assert.IsFalse(e.Detection.Hunting, "전부 벽 너머 — 원래 가던 길");
        }

        // 옛 DetectionChaseFieldTests::무제한_감지는_추격판을_굽지_않는다 — 무제한은 반경도 경로도 안 묻고 공용 사냥판을 탄다
        [Test]
        public void 무제한_감지는_추격판을_굽지_않는다()
        {
            var m = Begin(Def(WallMap(), -1f));
            Defender(m, new int2(6, 2));
            var e = Enemy(m, new int2(2, 2));
            m.Tick();

            Assert.IsTrue(e.Detection.Hunting, "무제한은 반경도 경로도 안 묻는다");
            Assert.IsNull(e.Detection.Chase, "무제한은 대상 지향 추격판을 굽지 않는다(공용 사냥판)");
        }

        // ══ 17 · DetectionLeakProofTests ═══════════════════════════════════════
        //
        // 폭 1 복도(y=2)에 골을 **복도 중간**(8,2)에 두고 방어유닛을 그 너머(10,2)에 세운다.
        // 사냥꾼이 방어유닛에게 가려면 반드시 골 칸을 밟는다.

        private static MapSnapshot CorridorWithMidGoal()
        {
            var map = CoreMapFixtures.Open(12, 5, new int2(8, 2), new int2(0, 2));
            for (int y = 0; y < 5; y++)
            for (int x = 0; x < 12; x++)
                if (y != 2) CoreMapFixtures.Block(map, new int2(x, y));
            return map;
        }

        // 옛 DetectionLeakProofTests::유한_감지_적은_사냥_중에도_골에서_공성_전환한다 — 유한 감지는 골 전환을 막지 않는다(유일한 패배 통로)
        [Test]
        public void 유한_감지_적은_사냥_중에도_골에서_공성_전환한다()
        {
            var m = Begin(Def(CorridorWithMidGoal(), 5f, speed: 2f));
            var huntingAtGoal = new List<bool>();
            m.Bus.Subscribe(CoreEventKind.GoalReached, 0, ev =>
            {
                var u = m.World.Find(ev.A);
                huntingAtGoal.Add(u != null && u.Detection != null && u.Detection.Hunting);
            });
            Defender(m, new int2(10, 2));
            m.Apply(Command.DebugSpawnEnemyInLane(0, 0));

            for (int t = 0; t < Ticks(20f) && huntingAtGoal.Count == 0; t++) m.Tick();

            Assert.AreEqual(1, huntingAtGoal.Count, "유한 감지가 골 전환을 막았다 — 감지가 패배 통로의 조절기가 된다");
            Assert.IsTrue(huntingAtGoal[0], "전제 — 골을 밟은 순간 사냥 중이었다(그래야 이 테스트가 감지를 증언한다)");
        }

        // 옛 DetectionLeakProofTests::무제한_사냥꾼은_감지가_꺼진_틈에도_유출하지_않는다 — 유출 면제는 `Hunting` 이 아니라 «무제한 + 사냥판 도달»에 묶인다
        [Test]
        public void 무제한_사냥꾼은_감지가_꺼진_틈에도_유출하지_않는다()
        {
            var m = Begin(Def(CorridorWithMidGoal(), -1f, speed: 2f));
            var goals = Listen(m, CoreEventKind.GoalReached);
            Defender(m, new int2(10, 2));
            m.Apply(Command.DebugSpawnEnemyInLane(0, 0));
            var e = Last(m);

            // 감지 타이머(억제)로 `Hunting` 을 계속 끈다 — 자장가·막힘 해제가 여는 틈의 재현.
            for (int t = 0; t < Ticks(10f); t++)
            {
                e.Detection.Suppress = 1f;
                m.Tick();
            }

            Assert.IsFalse(e.Detection.Hunting, "전제 — 감지가 꺼져 있었다");
            Assert.AreEqual(0, goals.Count, "유출 면제가 `Hunting` 에 묶여 있다 — 감지가 꺼진 틈에 무제한 사냥꾼이 골을 유출한다");
        }

        // 옛 DetectionLeakProofTests::사냥_필드가_도달_불가면_무제한도_공성_전환한다 — 사냥판이 못 닿으면 무제한도 골로 간다
        [Test]
        public void 사냥판이_닿지_않으면_무제한도_골에서_공성_전환한다()
        {
            var m = Begin(Def(CorridorWithMidGoal(), -1f, speed: 2f));
            var goals = Listen(m, CoreEventKind.GoalReached);
            Defender(m, new int2(10, 0));   // 막힌 행 — 사격 칸이 복도에 하나도 없다
            m.Apply(Command.DebugSpawnEnemyInLane(0, 0));

            for (int t = 0; t < Ticks(20f) && goals.Count == 0; t++) m.Tick();

            Assert.AreEqual(1, goals.Count, "방어유닛에게 닿는 사냥판이 없으면 무제한 사냥꾼도 골로 간다");
        }

        // ══ 18 · HuntCloseInLockTests ═══════════════════════════════════════════
        //
        // 방어유닛 몸 0 · 적은 **대각 사격 칸 (4,1)** 중앙. 칸으로는 도착(추격판 0)인데 몸 거리
        // √2 > 사거리 1 + 적 몸 0.25 — 「필드는 도착이라 하고 사거리는 밖이라 한다」가 참인 배치다.

        private static (BattleMatch m, Unit enemy) HunterOnDiagonalFiringCell(float detection)
        {
            var map = CoreMapFixtures.Open(12, 5, new int2(11, 2));
            var def = Def(map, detection, speed: 1f);
            def.Units[0].BodyRadiusTiles = 0f;
            var m = Begin(def);
            Defender(m, new int2(5, 2));
            var e = Enemy(m, new int2(4, 1));
            return (m, e);
        }

        // 옛 HuntCloseInLockTests::Hunter_AtFiringCell_ClosesIn — 사격 칸에 도착했지만 몸 거리가 멀면 사냥꾼은 계속 붙는다(영구 동결 방지)
        [Test]
        public void 사격_칸에_도착했지만_멀면_유한_사냥꾼은_붙는다()
        {
            var (m, e) = HunterOnDiagonalFiringCell(3f);
            float x0 = e.Position.x;
            m.Tick();

            Assert.IsTrue(e.Detection.Hunting, "전제 — 사냥 중");
            Assert.AreEqual(AiState.Marching, e.Ai.Enemy, "전제 — 몸 거리로는 사거리 밖");
            Assert.Greater(e.Position.x, x0 + 1e-4f, "지배축(동률이면 x)으로 붙어야 한다 — 없으면 발사 0 + 이동 0 = 영구 동결");

            Tick(m, Ticks(1f));
            Assert.AreEqual(AiState.Engaging, e.Ai.Enemy, "붙은 끝에 교전에 들어간다");
        }

        // 옛 HuntCloseInLockTests::Hunter_AtFiringCell_ClosesIn (옛 픽스처는 무제한 사냥꾼) — 공용 사냥판 레인도 같은 보정을 받는다
        [Test]
        public void 사격_칸에_도착했지만_멀면_무제한_사냥꾼도_붙는다()
        {
            var (m, e) = HunterOnDiagonalFiringCell(-1f);
            float x0 = e.Position.x;
            m.Tick();

            Assert.IsTrue(e.Detection.Hunting, "전제 — 사냥 중");
            Assert.Greater(e.Position.x, x0 + 1e-4f, "공용 사냥판 레인도 사격 칸에서 붙어야 한다");
        }

        // 옛 HuntCloseInLockTests::LockedHunter_DoesNotCloseIn — 잠긴(기절) 사냥꾼은 보정으로도 걷지 않는다
        [Test]
        public void 기절한_사냥꾼은_붙지_않는다()
        {
            var (m, e) = HunterOnDiagonalFiringCell(3f);
            e.Cc.Apply(CcSlotKind.Stun, 5f, float3.zero, SimEntityId.None);
            float3 p0 = e.Position;
            Tick(m, 30);

            Assert.AreEqual(p0.x, e.Position.x, 1e-6f, "기절했는데 자기주도로 걸었다 — 보정이 자기 이동을 넣었으면 잠금 게이트도 같이 와야 한다");
            Assert.AreEqual(p0.z, e.Position.z, 1e-6f);
        }

        // ── C10 salvage — 순찰병 사격 칸의 몸 거리 보정(`PatrolAreaMath.CloseInDir`) ──
        // 옛 픽스처 기하 그대로: 자기 몸 0.5 · 대상 몸 0(합이 상수 시절과 같다).

        private static float2 PatrolStepAt(int2 self, float3 selfPos, int2 enemyCell, float3 enemyPos, float enemyBody = 0f)
        {
            const int W = 11, H = 11;
            var grid = new int2(W, H);
            var full = new byte[W * H];
            for (int i = 0; i < full.Length; i++) full[i] = 1;
            var area = new byte[W * H];
            var anchor = new int2(5, 5);
            PatrolAreaMath.FillAreaMask(full, grid, anchor, 2, area);
            return PatrolAreaMath.StepDir(area, full, grid, anchor, anchor, 2, self, selfPos, 0.5f,
                                          1, 1f, new[] { enemyCell }, new[] { enemyPos }, new[] { enemyBody },
                                          1, new PatrolScratch(W * H));
        }

        // 옛 PatrolAreaMathTests::OnFiringCell_ButPhysicallyTooFar_KeepsClosing — 칸은 사거리 통과·몸 거리 실패면 한 칸 더 다가간다(C10)
        [Test]
        public void 순찰병은_사격_칸이어도_몸_거리가_멀면_계속_다가간다()
        {
            var dir = PatrolStepAt(new int2(6, 5), new float3(5.6f, 0f, 5f), new int2(7, 5), new float3(7.4f, 0f, 5f));
            Assert.Greater(dir.x, 0.5f, "칸은 인접인데 몸 거리 1.8 — 멈추면 «멈추는데 못 때리는» 교착");
        }

        // 옛 PatrolAreaMathTests::OnFiringCell_AndPhysicallyClose_Stops — 몸 거리로도 사거리 안이면 멈춘다
        [Test]
        public void 순찰병은_사격_칸에서_몸_거리도_안이면_멈춘다()
        {
            var dir = PatrolStepAt(new int2(6, 5), new float3(6f, 0f, 5f), new int2(7, 5), new float3(7f, 0f, 5f));
            Assert.AreEqual(float2.zero, dir);
        }

        // 옛 PatrolAreaMathTests::OnFiringCell_TargetBodyClosesTheGap_Stops — 대상 몸이 간격을 메우면 이미 사거리 안이다
        [Test]
        public void 순찰병은_대상_몸이_간격을_메우면_멈춘다()
        {
            var dir = PatrolStepAt(new int2(6, 5), new float3(5.6f, 0f, 5f), new int2(7, 5), new float3(7.4f, 0f, 5f),
                                   enemyBody: 0.73f);
            Assert.AreEqual(float2.zero, dir, "대상 몸으로 이미 사거리 안인데 계속 다가간다 — 이동이 사거리 술어와 다른 답을 받는다");
        }

        // 옛 PatrolAreaMathTests::OnFiringCell_TooFarDiagonally_ClosesOnDominantAxis — 대각으로 멀어도 멈추지 않는다
        [Test]
        public void 순찰병은_대각으로_멀어도_멈추지_않는다()
        {
            var dir = PatrolStepAt(new int2(6, 5), new float3(5.6f, 0f, 4.6f), new int2(7, 6), new float3(7.4f, 0f, 6.4f));
            Assert.AreNotEqual(float2.zero, dir);
        }

        // ══ 19 · PatrolLayerRoutingTests ═══════════════════════════════════════
        //
        // 6×1 판: 칸 0·1·2 = 걷는 칸(경로 층), 칸 3·4·5 = 배치 칸(지면 층). 두 층이 가로로 이웃하므로
        // 마스크를 틀리게 먹이면 순찰병이 남의 층으로 넘어간다. **판의 순찰 단계**를 지난다.

        private const int PatrolGround = 0, PatrolPath = 1, PatrolBoth = 2, PatrolUnauthored = 3;

        private static BattleMatch LayerBoard()
        {
            var map = new MapSnapshot
            {
                Width = 6,
                Height = 1,
                TileSize = 1f,
                Tiles = new[] { MapTile.Walk, MapTile.Walk, MapTile.Walk, MapTile.Place, MapTile.Place, MapTile.Place },
                Goals = new[] { new int2(0, 0) },
                Spawns = new[] { new int2(2, 0) },
            };
            map.Normalize();
            var def = CoreMapFixtures.Definition(map, defenderW: 1, defenderH: 1);
            def.Units = new[]
            {
                PatrolDef("patrol_ground", LayerBits.Ground),
                PatrolDef("patrol_path", LayerBits.Path),
                PatrolDef("patrol_both", LayerBits.Ground | LayerBits.Path),
                PatrolDef("patrol_unauthored", 0),
            };
            return Begin(def);
        }

        private static UnitDef PatrolDef(string id, int layers) => new UnitDef
        {
            Id = id,
            Health = 60f,
            AttackRange = 1f,
            AttackCooldown = 1f,
            AttackTargetCount = 1,
            BodyRadiusTiles = 0.25f,
            FootprintWidth = 1,
            FootprintHeight = 1,
            PlacementLayers = LayerBits.Ground,
            TraversalLayers = layers,
            MoveSpeed = 1f,
            Attack = AttackDef.Default(),
        };

        // 앵커에 소환하고 **그 자리에서 떨어진 칸**으로 옮긴다 — 앵커에 서 있으면 방향이 0 이라 층을 못 읽는다.
        private static Unit Patrol(BattleMatch m, int defIndex, int atX, int anchorX, int radius = 2)
        {
            Assert.IsTrue(m.Apply(Command.DebugSummonPatrol(defIndex, new int2(anchorX, 0), radius)).Accepted, "순찰 소환");
            var p = Last(m);
            p.Position = m.Map.CenterOf(new int2(atX, 0));
            return p;
        }

        private static float StepX(Unit p) => p.Move.PatrolStep.x;

        // 옛 PatrolLayerRoutingTests::GroundUnit_MovesInsidePlaceRegion_TowardItsAnchor — 지면 순찰병은 배치 칸 안에서 앵커로 간다
        [Test]
        public void 지면_순찰병은_배치_칸_안에서_앵커로_간다()
        {
            var m = LayerBoard();
            var g = Patrol(m, PatrolGround, atX: 5, anchorX: 4);
            m.Tick();
            Assert.Less(StepX(g), 0f, "앵커(4) 쪽 = -x");
        }

        // 옛 PatrolLayerRoutingTests::PathUnit_MovesInsideWalkRegion_TowardItsAnchor — 경로 순찰병은 걷는 칸 안에서 앵커로 간다
        [Test]
        public void 경로_순찰병은_걷는_칸_안에서_앵커로_간다()
        {
            var m = LayerBoard();
            var p = Patrol(m, PatrolPath, atX: 0, anchorX: 1);
            m.Tick();
            Assert.Greater(StepX(p), 0f, "앵커(1) 쪽 = +x");
        }

        // 옛 PatrolLayerRoutingTests::TwoLayers_Coexist_EachStaysInItsOwn + TwoLayers_ReversedCreationOrder_StillCorrect — 한 틱에 층이 다른 두 순찰병이 각자 자기 층을 본다(순서 무관)
        [Test]
        public void 층이_다른_두_순찰병은_한_틱에_각자_자기_층을_본다([Values(false, true)] bool pathFirst)
        {
            var m = LayerBoard();
            Unit g, p;
            if (pathFirst)
            {
                p = Patrol(m, PatrolPath, atX: 0, anchorX: 1);
                g = Patrol(m, PatrolGround, atX: 5, anchorX: 4);
            }
            else
            {
                g = Patrol(m, PatrolGround, atX: 5, anchorX: 4);
                p = Patrol(m, PatrolPath, atX: 0, anchorX: 1);
            }
            m.Tick();

            Assert.Less(StepX(g), 0f, "지면 순찰병 — 앞 유닛의 마스크를 물려받으면 안 된다");
            Assert.Greater(StepX(p), 0f, "경로 순찰병");
        }

        // 옛 PatrolLayerRoutingTests::CombinedLayers_UnitRoamsBothRegions_NoCodeChangeNeeded — 층 조합(지면|경로)은 경계를 넘고, 경로 전용은 지면 위에서 갈 곳이 없다
        [Test]
        public void 지면과_경로를_함께_가진_순찰병은_층_경계를_넘는다()
        {
            var m = LayerBoard();
            var both = Patrol(m, PatrolBoth, atX: 4, anchorX: 2, radius: 3);
            var pathOnly = Patrol(m, PatrolPath, atX: 4, anchorX: 2, radius: 3);
            m.Tick();

            Assert.Less(StepX(both), 0f, "배치 칸(4)에서 걷는 칸의 앵커(2)로 — 층 경계를 넘는다");
            Assert.AreEqual(0f, StepX(pathOnly), 1e-6f, "경로 전용은 지면 위에서 갈 곳이 없다");
        }

        // 옛 PatrolLayerRoutingTests::WrongLayerForAnchor_UnitCannotMove_NegativeControl — 자기 층 밖 앵커면 갈 곳이 없다(음성 대조군)
        [Test]
        public void 자기_층_밖_앵커면_순찰병은_갈_곳이_없다()
        {
            var m = LayerBoard();
            var g = Patrol(m, PatrolGround, atX: 0, anchorX: 1);
            m.Tick();

            Assert.AreEqual(float2.zero, g.Move.PatrolStep, "층 선택이 «아무 마스크나» 쓰면 여기서 움직인다");
        }

        // 옛 PatrolLayerRoutingTests::UnauthoredLayers_FallBackToPath_CurrentBehavior — 통행 층 미저작(0)은 경로 층으로 떨어진다
        [Test]
        public void 통행_층_미저작_순찰병은_경로_층으로_떨어진다()
        {
            var m = LayerBoard();
            var none = Patrol(m, PatrolUnauthored, atX: 0, anchorX: 1);
            var authored = Patrol(m, PatrolPath, atX: 0, anchorX: 1);
            m.Tick();

            Assert.Greater(StepX(authored), 0f, "전제 — 경로 저작은 앵커로 간다");
            Assert.AreEqual(StepX(authored), StepX(none), 1e-6f, "미저작 = 경로 저작과 같다");
        }
    }
}
