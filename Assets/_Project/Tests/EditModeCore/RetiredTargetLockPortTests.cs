using NUnit.Framework;
using Unity.Mathematics;
using Somnia.Battle.Skills;
using Somnia.Battle.BattleCore;
using Somnia.Battle.BattleCore.Combat;
using Somnia.Battle.UnitAi;
using static Somnia.Battle.Tests.EditMode.Core.CoreCombatFixtures;

namespace Somnia.Battle.Tests.EditMode.Core
{
    // battle-core-rebuild unit 9 구현 2 — **지속 락** 규칙을 옛 테스트에서 코어로 옮긴다.
    //
    // 대상: `ledgers/retire-test-pairs.md` 「규칙 누락 의심 — 모아 보기」 5·6·7·8
    // (DefenderLock · NearestLock · EnemyBehavior(FocusUntilDead) · TargetPersistence).
    // 코어의 락은 `AttackState.Lock` 하나이고 방어유닛·적이 같은 블록을 지난다(`CombatPhase.PickTarget`).
    // 옛 단언의 `FocusTarget.current` 가 여기서는 `Attack.Lock` 이다.
    //
    // ⚠ 수치는 픽스처다. 기대값은 픽스처 값에서 파생한다.
    public class RetiredTargetLockPortTests
    {
        private static BattleMatch Match(MatchDefinition def)
        {
            var m = new BattleMatch(def);
            m.Begin();
            return m;
        }

        private static void Rehash(MatchDefinition def) => def.ConfigHash = def.ComputeConfigHash();

        private static float3 Far => new float3(9.5f, 0f, 4.5f);   // 어떤 픽스처 사거리로도 닿지 않는 판 안 칸

        private static Unit SpawnDefender(BattleMatch m, int2 cell)
        {
            m.Apply(Command.DebugSpawnDefender(0, cell));
            return m.World.Units[m.World.Units.Count - 1];
        }

        private static Unit SpawnEnemy(BattleMatch m, int2 cell)
        {
            m.Apply(Command.DebugSpawnEnemy(0, cell));
            return m.World.Units[m.World.Units.Count - 1];
        }

        private static void Kill(Unit u) => u.Inbox.Damage.Add(new DamageEntry { Amount = 1e9f, Source = SimEntityId.None });

        private static void Stun(BattleMatch m, Unit u, float seconds)
            => m.World.RequestCc(CcRequest.Of(u.Id, CcRequestKind.Stun, seconds, SimEntityId.None));

        // ═════════════════════════════════════════════════════════════════════
        // 5. DefenderLock — 방어유닛 지속 락
        // ═════════════════════════════════════════════════════════════════════

        private static MatchDefinition DefenderLockDef(float hitDelay = 0f, int aggroCapacity = 0)
            => Definition(defenderDamage: 4f, defenderRange: 3f, defenderCooldown: 0.05f,
                          defenderHitDelay: hitDelay, enemyHealth: 1e6f, aggroCapacity: aggroCapacity);

        // 옛 DefenderLockTests::Defender_KeepsLock_WhenACloserEnemyArrives — 더 가까운 적이 와도 갈아타지 않는다
        [Test]
        public void 방어유닛은_더_가까운_적이_와도_문_대상을_놓지_않는다()
        {
            var m = Match(DefenderLockDef());
            var d = SpawnDefender(m, new int2(2, 2));
            var first = SpawnEnemy(m, new int2(4, 2));
            Tick(m, 1);
            Assert.AreEqual(first.Id, d.Attack.Lock, "첫 대상을 잠근다");

            SpawnEnemy(m, new int2(3, 2));   // 더 가까운 적이 흘러 들어온다
            Tick(m, 10);
            Assert.AreEqual(first.Id, d.Attack.Lock, "더 가까운 적이 와도 갈아타지 않는다 — 원칙 1");
        }

        // 옛 DefenderLockTests::Defender_ReleasesLock_WhenTargetLeavesRange — 사거리 이탈은 해제 사유
        [Test]
        public void 방어유닛은_문_대상이_사거리를_벗어나면_다른_적으로_넘어간다()
        {
            var m = Match(DefenderLockDef());
            var d = SpawnDefender(m, new int2(2, 2));
            var locked = SpawnEnemy(m, new int2(3, 2));
            var other = SpawnEnemy(m, new int2(4, 2));
            Tick(m, 1);
            Assert.AreEqual(locked.Id, d.Attack.Lock);

            locked.Position = Far;
            Tick(m, 10);
            Assert.AreEqual(other.Id, d.Attack.Lock, "사거리 이탈은 해제 사유다");
        }

        // 옛 DefenderLockTests::Defender_ReleasesLock_WhenTargetDies — 사망은 해제 사유
        [Test]
        public void 방어유닛은_문_대상이_죽으면_다른_적으로_넘어간다()
        {
            var m = Match(DefenderLockDef());
            var d = SpawnDefender(m, new int2(2, 2));
            var locked = SpawnEnemy(m, new int2(3, 2));
            var other = SpawnEnemy(m, new int2(4, 2));
            Tick(m, 1);
            Assert.AreEqual(locked.Id, d.Attack.Lock);

            Kill(locked);
            Tick(m, 10);
            Assert.AreEqual(other.Id, d.Attack.Lock, "사망은 해제 사유다");
        }

        // 옛 DefenderLockTests::Cc_ClearsTheDefenderLock_AndTheNextPickIsFresh — 행동 불능이 락을 비우고, 깨면 새로 고른다
        [Test]
        public void 방어유닛은_행동_불능이_풀리면_옛_락을_잇지_않고_새로_고른다()
        {
            var m = Match(DefenderLockDef());
            var d = SpawnDefender(m, new int2(2, 2));
            var far = SpawnEnemy(m, new int2(5, 2));
            Tick(m, 1);
            Assert.AreEqual(far.Id, d.Attack.Lock, "처음엔 이쪽뿐이라 잠긴다");

            var move = d.Move;
            d.Move = new MoveState { Locked = true };   // 행동 잠금(만료 시계는 이 규칙 밖이다)
            Tick(m, 1);
            Assert.IsTrue(d.Attack.Lock.IsNone, "잠긴 동안엔 비어 있다");

            var near = SpawnEnemy(m, new int2(3, 2));   // 잠긴 동안 더 가까운 적 등장
            d.Move = move;
            Tick(m, 5);
            Assert.AreEqual(near.Id, d.Attack.Lock, "깨어나면 옛 락을 이어받지 않고 새로 고른다");
        }

        // 옛 DefenderLockTests::DuringWindup_TheCommittedTargetWins_OverTheLock — 진행 중 스윙의 커밋은 락이 못 밀어낸다
        [Test]
        public void 선딜_중_커밋은_락이_밀어내지_못한다()
        {
            var m = Match(DefenderLockDef(hitDelay: 0.3f));
            var d = SpawnDefender(m, new int2(2, 2));
            var target = SpawnEnemy(m, new int2(4, 2));
            Tick(m, 1);
            Assert.AreEqual(target.Id, d.Attack.CommittedTarget, "선딜 시작 시 커밋된다");

            SpawnEnemy(m, new int2(3, 2));
            Tick(m, 1);
            Assert.AreEqual(target.Id, d.Attack.CommittedTarget, "진행 중 스윙의 커밋은 그대로다");
        }

        // 옛 DefenderLockTests::Healer_DoesNotLock_LowestHealthRerankingIsItsIdentity — 힐러는 락 제외(최저 체력 재랭킹)
        [Test]
        public void 힐러는_락을_받지_않고_매번_가장_다친_아군을_다시_고른다()
        {
            var def = DefenderLockDef();
            def.Units[0].TargetFactions = (int)Faction.DefenderUnit;
            def.Units[0].Attack.Outputs = new[] { new AttackOutputDef { Kind = AttackOutputKind.Heal, Magnitude = 1f } };
            Rehash(def);
            var m = Match(def);
            var resolved = Listen(m, CoreEventKind.AttackResolved);
            var healer = SpawnDefender(m, new int2(2, 2));
            var a = SpawnDefender(m, new int2(3, 2));
            var b = SpawnDefender(m, new int2(4, 2));
            a.Health = a.MaxHealth * 0.5f;
            b.Health = b.MaxHealth * 0.8f;

            Tick(m, 5);
            Assert.IsTrue(healer.Attack.Lock.IsNone, "힐러는 락을 받지 않는다 — 제외는 누락이 아니라 계약이다");

            b.Health = b.MaxHealth * 0.1f;   // 이제 b 가 더 다쳤다
            resolved.Clear();
            Tick(m, 10);
            SimEntityId last = SimEntityId.None;
            foreach (var r in resolved) if (r.A == healer.Id) last = r.B;
            Assert.AreEqual(b.Id, last, "다음 회복은 지금 가장 다친 아군이다");
        }

        // 옛 DefenderLockTests::Guardian_DoesNotLock_TheAggroMagnetNeedsFreshPicks — 가디언은 락 제외(자석)
        [Test]
        public void 가디언은_락을_받지_않는다()
        {
            var m = Match(DefenderLockDef(aggroCapacity: 3));
            var g = SpawnDefender(m, new int2(2, 2));
            SpawnEnemy(m, new int2(3, 2));
            Tick(m, 5);
            Assert.IsTrue(g.Attack.Lock.IsNone, "가디언은 락을 받지 않는다 — 어그로 자석이 새 픽을 요구한다");
        }

        // 옛 DefenderLockTests::FrontmostCardHolder_DoesNotLock_TheCardPromisesFreshFrontmostEachAttack — 최전방 수식자는 락 제외
        [Test]
        public void 최전방_수식자_보유자는_락을_받지_않는다()
        {
            var def = DefenderLockDef();
            def.Units[0].Attack.Mods = new[] { new AttackModDef { Kind = AttackModKind.FrontmostTarget, DamageMul = 1.2f } };
            Rehash(def);
            var m = Match(def);
            var d = SpawnDefender(m, new int2(2, 2));
            SpawnEnemy(m, new int2(3, 2));
            Tick(m, 5);
            Assert.IsTrue(d.Attack.WantsFrontmost, "전제");
            Assert.IsTrue(d.Attack.Lock.IsNone, "매 공격마다 지금의 최전방 — 지속 락과 정면 충돌한다");
        }

        // 옛 DefenderLockTests::DefenderWithEnemyBehavior_IsLeftToTheEnemyBlock_NotDoubleLocked — 락 모드 None 이면 아무 데서도 안 잠근다
        [Test]
        public void 락_모드가_없는_유닛은_잠그지_않는다()
        {
            // 옛 파일의 관측(「targetMode None 이면 락이 아무 데서도 안 걸린다」)만 옮겼다. 두 블록의 이중 잠금은
            // 코어에 블록이 하나라 성립하지 않는다(이식 제외 — 기계).
            var m = Match(Definition(defenderDamage: 4f, mode: TargetMode.None, enemyHealth: 1e6f));
            var d = SpawnDefender(m, new int2(2, 2));
            SpawnEnemy(m, new int2(3, 2));
            Tick(m, 5);
            Assert.IsTrue(d.Attack.Lock.IsNone);
        }

        // 옛 DefenderLockTests::LockReleases_WhenTheTargetLeavesTheMask — 락은 공격 마스크를 다시 본다
        [Test]
        public void 락_대상이_공격_마스크_밖으로_나가면_놓는다()
        {
            var m = Match(DefenderLockDef());
            var d = SpawnDefender(m, new int2(2, 2));
            var foe = SpawnEnemy(m, new int2(3, 2));
            Tick(m, 1);
            Assert.AreEqual(foe.Id, d.Attack.Lock);

            d.Attack.TargetMask = (int)Faction.BlockingHazard;   // 도발 해제의 마스크 원복 같은 런타임 변경
            Tick(m, 5);
            Assert.IsTrue(d.Attack.Lock.IsNone, "마스크 밖으로 나간 대상은 놓는다 — 마스크 밖을 계속 때리면 안 된다");
        }

        // ═════════════════════════════════════════════════════════════════════
        // 6. NearestLock — Nearest 적도 문다(보스 포함) · 행동 불능이 락을 비운다
        // ═════════════════════════════════════════════════════════════════════

        private static MatchDefinition EnemyLockDef(float hitDelay = 0f, bool boss = false,
                                                    TargetMode mode = TargetMode.Nearest, float range = 3f)
        {
            var def = Definition(defenderDamage: 0f, enemyDamage: 4f, enemyRange: range, enemyHealth: 1e6f);
            def.Enemies[0].HitDelaySeconds = hitDelay;
            def.Enemies[0].Attack.BossImmune = boss;
            def.Enemies[0].Attack.Mode = (int)mode;
            def.Units[0].Health = 1e6f;
            Rehash(def);
            return def;
        }

        // 옛 NearestLockTests::NearestEnemy_KeepsLock_WhenACloserDefenderAppears — Nearest 적도 문 대상을 유지
        [Test]
        public void 최근접_적도_더_가까운_방어유닛이_와도_문_대상을_놓지_않는다()
        {
            var m = Match(EnemyLockDef());
            var e = SpawnEnemy(m, new int2(2, 2));
            var first = SpawnDefender(m, new int2(4, 2));
            Tick(m, 1);
            Assert.AreEqual(first.Id, e.Attack.Lock, "첫 대상을 잠근다");

            SpawnDefender(m, new int2(3, 2));
            Tick(m, 10);
            Assert.AreEqual(first.Id, e.Attack.Lock, "더 가까운 대상이 나타나도 갈아타지 않는다 — 원칙 2");
        }

        // 옛 NearestLockTests::NearestEnemy_ReleasesLock_WhenTargetLeavesRange — 사거리 이탈은 해제 사유(D2)
        [Test]
        public void 최근접_적은_문_대상이_사거리를_벗어나면_넘어간다()
        {
            var m = Match(EnemyLockDef());
            var e = SpawnEnemy(m, new int2(2, 2));
            var locked = SpawnDefender(m, new int2(3, 2));
            var other = SpawnDefender(m, new int2(4, 2));
            Tick(m, 1);
            Assert.AreEqual(locked.Id, e.Attack.Lock);

            locked.Position = Far;
            Tick(m, 10);
            Assert.AreEqual(other.Id, e.Attack.Lock, "사거리 이탈은 해제 사유다(D2)");
        }

        // 옛 NearestLockTests::Boss_KeepsLock_LikeAnyOtherNearestEnemy — 보스도 예외가 아니다(D4)
        [Test]
        public void 보스도_한_놈을_물면_그놈만_팬다()
        {
            var m = Match(EnemyLockDef(boss: true));
            var boss = SpawnEnemy(m, new int2(2, 2));
            var first = SpawnDefender(m, new int2(4, 2));
            Tick(m, 1);
            Assert.AreEqual(first.Id, boss.Attack.Lock);

            SpawnDefender(m, new int2(3, 2));
            Tick(m, 10);
            Assert.AreEqual(first.Id, boss.Attack.Lock, "보스도 예외가 아니다");
        }

        // 옛 NearestLockTests::Cc_ClearsTheLock_WhileActionLocked — 행동 정지 중엔 락이 비어 있다
        [Test]
        public void 적은_기절한_동안_락이_비어_있다()
        {
            var m = Match(EnemyLockDef());
            var e = SpawnEnemy(m, new int2(2, 2));
            SpawnDefender(m, new int2(4, 2));
            Tick(m, 1);
            Assert.IsFalse(e.Attack.Lock.IsNone, "먼저 잠긴다");

            Stun(m, e, 1f);
            Tick(m, 2);
            Assert.IsTrue(e.ActionLocked, "전제: 기절");
            Assert.IsTrue(e.Attack.Lock.IsNone, "행동정지 중엔 락이 비어 있다 — 깨어날 때 새로 고르기 위한 상태");
        }

        // 옛 NearestLockTests::AfterCcEnds_ThePickIsMadeFresh_NotResumedFromTheOldLock — 깨어나면 새로 고른다(D5)
        [Test]
        public void 적은_기절이_풀리면_옛_락을_잇지_않고_새로_고른다()
        {
            const float stunSec = 0.1f;
            var m = Match(EnemyLockDef());
            var e = SpawnEnemy(m, new int2(2, 2));
            var far = SpawnDefender(m, new int2(5, 2));
            Tick(m, 1);
            Assert.AreEqual(far.Id, e.Attack.Lock, "처음엔 이쪽뿐이라 잠긴다");

            Stun(m, e, stunSec);
            Tick(m, 2);
            Assert.IsTrue(e.Attack.Lock.IsNone, "자는 동안엔 비어 있다");
            var near = SpawnDefender(m, new int2(3, 2));
            Tick(m, (int)math.ceil(stunSec * 60f) + 10);
            Assert.IsFalse(e.ActionLocked, "전제: 기절이 풀렸다");
            Assert.AreEqual(near.Id, e.Attack.Lock, "깨어나면 옛 락을 이어받지 않고 새로 고른다");
        }

        // 옛 NearestLockTests::CcDuringWindup_DoesNotStealTheCommittedTarget — 기절은 락만 비우고 진행 중 커밋은 못 뺏는다
        [Test]
        public void 선딜_중_기절은_락만_비우고_커밋은_살려_둔다()
        {
            var m = Match(EnemyLockDef(hitDelay: 0.3f));
            var e = SpawnEnemy(m, new int2(2, 2));
            var target = SpawnDefender(m, new int2(4, 2));
            Tick(m, 1);
            Assert.AreEqual(target.Id, e.Attack.CommittedTarget, "선딜 시작 시 커밋된다");

            Stun(m, e, 1f);
            Tick(m, 2);
            Assert.IsTrue(e.Attack.Lock.IsNone, "락은 비었다");
            Assert.AreEqual(target.Id, e.Attack.CommittedTarget, "커밋은 살아 있다 — 진행 중 스윙은 겨눈 대상에 꽂힌다");
        }

        // 옛 NearestLockTests::Mirror_DoesNotFallToMarching_WhileTheLockIsValid — 락이 유효하면 교전 상태
        [Test]
        public void 락이_유효하면_적은_교전_상태다()
        {
            var m = Match(EnemyLockDef());
            var e = SpawnEnemy(m, new int2(2, 2));
            SpawnDefender(m, new int2(3, 2));
            Tick(m, 5);
            Assert.IsFalse(e.Attack.Lock.IsNone);
            Assert.AreEqual(AiState.Engaging, e.Ai.Enemy, "공격 루프와 상태 판정이 갈리면 「락은 있는데 Marching」 데드락");
        }

        // ═════════════════════════════════════════════════════════════════════
        // 7. EnemyBehavior — FocusUntilDead
        // ═════════════════════════════════════════════════════════════════════

        // 옛 EnemyBehaviorTests::FocusUntilDead_Locks_Holds_ThenReselectsOnDeath — 물면 죽을 때까지, 죽으면 다시 고른다
        [Test]
        public void 집중_적은_물면_더_가까운_대상이_와도_유지하고_죽으면_다시_고른다()
        {
            var m = Match(EnemyLockDef(mode: TargetMode.FocusUntilDead, range: 5f));
            var f = SpawnEnemy(m, new int2(2, 2));
            var a = SpawnDefender(m, new int2(4, 2));   // 최근접
            SpawnDefender(m, new int2(6, 2));           // 더 멀다
            Tick(m, 1);
            Assert.AreEqual(a.Id, f.Attack.Lock, "최근접을 문다");

            SpawnDefender(m, new int2(3, 2));   // a 보다 가깝다
            Tick(m, 1);
            Assert.AreEqual(a.Id, f.Attack.Lock, "더 가까운 대상이 와도 유지");

            Kill(a);
            Tick(m, 3);
            Assert.IsFalse(f.Attack.Lock.IsNone, "문 대상이 죽으면 다시 고른다");
            Assert.AreNotEqual(a.Id, f.Attack.Lock);
        }

        // 옛 EnemyBehaviorTests::FocusUntilDead_OutOfRange_ReleasesLock_NoFireWhenNoOtherTarget — 이탈 = 해제, 대체 후보 없으면 발사 없음
        [Test]
        public void 집중_적은_문_대상이_사거리_밖이면_놓고_대체가_없으면_안_쏜다()
        {
            var m = Match(EnemyLockDef(mode: TargetMode.FocusUntilDead, range: 5f));
            var f = SpawnEnemy(m, new int2(2, 2));
            var a = SpawnDefender(m, new int2(3, 2));
            Tick(m, 1);
            Assert.AreEqual(a.Id, f.Attack.Lock);

            a.Position = Far;
            float before = a.Health;
            Tick(m, 70);   // 쿨(1초)이 돌고도 남는다 — 안 쏜 이유가 쿨이 아니게
            Assert.IsTrue(f.Attack.Lock.IsNone, "이탈 = 해제(D2)");
            Assert.AreEqual(before, a.Health, 1e-4f, "사거리 밖 대상은 안 맞는다");
        }

        // 옛 EnemyBehaviorTests::Nearest_RepicksClosest_UnlikeFocus — 락 모드가 없으면 매 공격 최근접
        [Test]
        public void 락_모드가_없는_적은_매_공격마다_최근접을_다시_고른다()
        {
            // 옛 픽스처의 `Nearest` 는 FocusTarget 을 안 붙인 모양이었다 — 그 뒤 라이브 스폰은 Nearest 에도 락을
            // 붙였다(target-persistence unit 3, `BattleBridge.cs:10941`). 옛 단언이 증언하던 성질(「락 없으면 매번
            // 최근접」)은 코어에서 `TargetMode.None` 의 것이다.
            var m = Match(EnemyLockDef(mode: TargetMode.None, range: 5f));
            var e = SpawnEnemy(m, new int2(2, 2));
            var a = SpawnDefender(m, new int2(4, 2));
            Tick(m, 1);
            Assert.Less(a.Health, a.MaxHealth, "처음엔 a");

            var c = SpawnDefender(m, new int2(3, 2));   // 더 가깝다
            float aBefore = a.Health;
            Tick(m, 61);
            Assert.Less(c.Health, c.MaxHealth, "다음 공격은 지금의 최근접");
            Assert.AreEqual(aBefore, a.Health, 1e-4f, "옛 대상은 더 안 맞는다");
            Assert.IsTrue(e.Attack.Lock.IsNone);
        }

        // ═════════════════════════════════════════════════════════════════════
        // 8. TargetPersistence — 유지 술어 + 집중 적의 이탈 전환(판)
        // ═════════════════════════════════════════════════════════════════════

        private static bool Keeps(float gapTiles, float range)
            => TargetPersistence.KeepsLock(true, float3.zero, new float3(gapTiles, 0f, 0f), range, 1f, 0.25f, 0.25f);

        // 옛 TargetPersistenceTests::KeepsLock_AliveAndInRange_Keeps / KeepsLock_OutOfRange_Releases / KeepsLock_Dead_Releases — 유지 술어의 세 답
        [Test]
        public void 유지_술어는_살아_있고_사거리_안이면_붙들고_이탈_사망이면_놓는다()
        {
            const float range = 3f;
            Assert.IsTrue(Keeps(2f, range));
            Assert.IsTrue(Keeps(range + 0.5f, range), "양쪽 몸을 더한 경계는 포함");
            Assert.IsFalse(Keeps(range + 2f, range), "사거리 이탈은 해제 사유(D2)");
            Assert.IsFalse(TargetPersistence.KeepsLock(false, float3.zero, float3.zero, range, 1f, 0.25f, 0.25f),
                "죽으면 놓는다");
        }

        // 옛 TargetPersistenceTests::Maintain_IsWiderThanAcquire_ByExactlyTheHysteresis — 유지는 획득보다 정확히 h 만큼 넓다
        [Test]
        public void 유지는_획득보다_정확히_히스테리시스만큼_넓다()
        {
            const float range = 3f;
            float h = TargetPersistence.HysteresisTiles;
            float justOutside = range + 0.5f + h * 0.5f;
            Assert.IsFalse(AttackReach.InReach(float3.zero, new float3(justOutside, 0f, 0f), range, 1f, 0.25f, 0.25f),
                "획득은 이미 놓쳤다");
            Assert.IsTrue(Keeps(justOutside, range), "유지는 아직 붙든다 — 이 틈이 없으면 경계에서 락이 진동한다");
            Assert.IsFalse(Keeps(range + 0.5f + h * 2f, range), "h 를 넘으면 유지도 해제");
        }

        // 옛 TargetPersistenceTests::Hysteresis_CoversMeasuredJitter_ButStaysFarBelowTheOldSlack — h 는 실측 지터(≈0.05)를 덮고 옛 슬랙 0.5 의 절반 아래
        [Test]
        public void 히스테리시스는_실측_지터를_덮되_옛_슬랙보다_훨씬_작다()
        {
            Assert.Greater(TargetPersistence.HysteresisTiles, 0.06f, "관측 지터(≈0.05)를 여유 있게 덮지 못한다");
            Assert.Less(TargetPersistence.HysteresisTiles, 0.25f, "「지나쳐 갔는데 붙들고 있는」 상태가 돌아온다(D2)");
        }

        // 옛 TargetPersistenceTests::FocusEnemy_LockedDefenderLeavesRange_SwitchesToAnotherInRange — 이탈하면 사거리 안의 다른 방어유닛으로
        [Test]
        public void 집중_적은_문_대상이_이탈하면_사거리_안의_다른_방어유닛을_때린다()
        {
            var m = Match(EnemyLockDef(mode: TargetMode.FocusUntilDead));
            var e = SpawnEnemy(m, new int2(2, 2));
            var locked = SpawnDefender(m, new int2(3, 2));   // 최근접 → 잠긴다
            var other = SpawnDefender(m, new int2(4, 2));    // 사거리 안 대체 후보
            Tick(m, 1);
            Assert.AreEqual(locked.Id, e.Attack.Lock);

            locked.Position = Far;
            float otherBefore = other.Health;
            Tick(m, 70);
            Assert.Less(other.Health, otherBefore, "사거리 안의 다른 방어유닛을 때려야 한다(B2)");
            Assert.AreEqual(other.Id, e.Attack.Lock, "락이 새 대상으로 넘어간다");
        }

        // 옛 TargetPersistenceTests::FocusEnemy_LockedLeavesRange_FsmDoesNotFallToMarching — 대체가 있으면 행진으로 안 떨어진다
        [Test]
        public void 집중_적은_문_대상이_이탈해도_대체가_있으면_교전_상태다()
        {
            var m = Match(EnemyLockDef(mode: TargetMode.FocusUntilDead));
            var e = SpawnEnemy(m, new int2(2, 2));
            var locked = SpawnDefender(m, new int2(3, 2));
            SpawnDefender(m, new int2(4, 2));
            Tick(m, 1);
            locked.Position = Far;
            Tick(m, 5);
            Assert.AreEqual(AiState.Engaging, e.Ai.Enemy, "사거리 안에 대상이 있으면 Engaging(Marching = B2)");
        }

        // 옛 TargetPersistenceTests::FocusEnemy_NoOtherTargetInRange_MarchesOn — 놓았는데 아무도 없으면 행진이 맞다
        [Test]
        public void 집중_적은_놓았는데_사거리_안에_아무도_없으면_행진한다()
        {
            var m = Match(EnemyLockDef(mode: TargetMode.FocusUntilDead));
            var e = SpawnEnemy(m, new int2(2, 2));
            var locked = SpawnDefender(m, new int2(4, 2));
            Tick(m, 1);
            locked.Position = Far;
            Tick(m, 5);
            Assert.AreEqual(AiState.Marching, e.Ai.Enemy);
        }

        // 옛 TargetPersistenceTests::FocusEnemy_LockedStaysInRange_DoesNotSwitchToNearer — 사거리 안이면 더 가까운 대상이 와도 유지
        [Test]
        public void 집중_적은_문_대상이_사거리_안이면_더_가까운_대상을_안_때린다()
        {
            var m = Match(EnemyLockDef(mode: TargetMode.FocusUntilDead));
            var e = SpawnEnemy(m, new int2(2, 2));
            var locked = SpawnDefender(m, new int2(5, 2));
            Tick(m, 1);
            Assert.AreEqual(locked.Id, e.Attack.Lock);

            var nearer = SpawnDefender(m, new int2(3, 2));
            float nearerBefore = nearer.Health;
            Tick(m, 70);
            Assert.AreEqual(locked.Id, e.Attack.Lock, "사거리 안이면 더 가까운 대상이 와도 유지");
            Assert.AreEqual(nearerBefore, nearer.Health, 1e-4f);
        }
    }
}
