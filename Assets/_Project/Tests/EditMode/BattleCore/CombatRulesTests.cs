using NUnit.Framework;
using Unity.Mathematics;
using Somnia.Battle.Skills;
using Somnia.Battle.BattleCore;
using Somnia.Battle.BattleCore.Combat.Projectile;
using Somnia.Battle.BattleCore.Effects;
using Somnia.Battle.BattleCore.Map;
using static Somnia.Battle.Tests.EditMode.Core.CoreCombatFixtures;

namespace Somnia.Battle.Tests.EditMode.Core
{
    // battle-core-rebuild unit 3 — 전투 판정의 **필수 규칙**을 판 위에서 증언한다.
    //
    // 순수 함수 테스트(`CombatPureMathTests` 등)와 나눈 이유: 여기 있는 것들은 「어느 단계에서
    // 일어나나」가 규칙의 일부라 함수 하나로는 물을 수 없다. 규칙 번호는 `ledgers/rules.md`
    // 전투 판정 절의 것이다.
    public class CombatRulesTests
    {
        private static BattleMatch Match(MatchDefinition def)
        {
            var m = new BattleMatch(def);
            m.Begin();
            return m;
        }

        // ── C16 · 후보에서 빠지는 것 3종 ─────────────────────────────────────

        [Test]
        public void 배치중_사망_궁극기이탈은_표적이_아니다()
        {
            // 옛 전투는 이 셋을 쿼리(`WithNone`)가 걸러서 우선순위 함수가 둘을 인자로 안 받았다.
            // 쿼리가 사라진 지금 그 표가 조용히 3단으로 줄지 않게 하는 것이 이 술어다.
            var u = new Unit();
            Assert.IsTrue(u.IsTargetable());
            u.Deploying = true; Assert.IsFalse(u.IsTargetable());
            u.Deploying = false; u.Dead = true; Assert.IsFalse(u.IsTargetable());
            u.Dead = false;
            u.Progressive = new ProgressiveStates { LeapActive = true };
            Assert.IsFalse(u.IsTargetable(), "궁극기로 판 밖에 나간 자는 후보가 아니다");
        }

        // ── C19 · 실주기 = max(간격, 선딜) ───────────────────────────────────

        [Test]
        public void 실주기는_간격과_선딜의_큰_쪽이다()
        {
            var atk = new AttackState { Interval = 0.5f, HitDelay = 1.2f };
            Assert.AreEqual(1.2f, atk.Period(1f), 1e-4f);
            atk.HitDelay = 0.1f;
            Assert.AreEqual(0.5f, atk.Period(1f), 1e-4f);
        }

        // ── 쿨다운은 CC 중에도 돈다 ──────────────────────────────────────────

        [Test]
        public void 쿨다운은_행동_불능_중에도_돈다()
        {
            // 묶여 있어도 쿨은 차고 풀리는 즉시 때린다. 쿼리에서 빼면 진행 중 스윙까지 얼어
            // CC 와 규약이 갈린다.
            var m = Match(Definition(defenderCooldown: 1f));
            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 1)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 1)));
            var d = First(m, UnitKind.Defender);

            Tick(m, 1);                       // 첫 공격 — 쿨 1.0 으로 리셋
            d.Move = new MoveState { Locked = true };   // 행동 잠금
            float before = d.Attack.CooldownRemaining;
            Tick(m, 30);
            Assert.Less(d.Attack.CooldownRemaining, before - 0.4f, "잠겨 있어도 쿨은 찬다");
        }

        // ── C13 · 행동 불능 중에는 락을 비우고 재잠금도 건너뛴다 ────────────

        [Test]
        public void 행동_불능이면_락을_비우고_다시_안_잠근다()
        {
            // `else` 로 감싸지 않으면 해제 분기가 그 틱 최근접으로 **즉시 다시 잠근다**
            // (초판이 그랬고 테스트가 잡았다).
            var m = Match(Definition());
            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 1)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 1)));
            var d = First(m, UnitKind.Defender);

            Tick(m, 1);
            Assert.IsTrue(d.Attack.Lock.IsEntity, "평소에는 문다");

            d.Move = new MoveState { Locked = true };
            Tick(m, 1);
            Assert.IsTrue(d.Attack.Lock.IsNone, "행동 불능 틱에는 비어 있어야 한다");
        }

        // ── C1 · 폭탄맨은 던진 그 순간에만 쿨을 돌린다 ──────────────────────

        [Test]
        public void 폭탄맨은_적이_없으면_쿨을_만료로_대기시킨다()
        {
            var def = Definition(policy: AttackPolicy.Bomb);
            GiveProjectile(def, MovementKind.GrenadeToCell, PayloadKind.TileAoe);
            def.Units[0].Attack.Policy = (int)AttackPolicy.Bomb;
            def.Units[0].Attack.BombProjectileDefIndex = 0;
            def.Units[0].Attack.BombDamage = 5f;
            def.ConfigHash = def.ComputeConfigHash();

            var m = Match(def);
            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 1)));
            var d = First(m, UnitKind.Defender);

            Tick(m, 120);
            Assert.AreEqual(0f, d.Attack.CooldownRemaining, 1e-4f,
                "적이 없으면 쿨은 만료 상태로 **대기**한다 — 리셋하면 최대 한 쿨 늦게 던진다");
            Assert.AreEqual(0, m.World.ProjectileRequests.Count + CountProjectiles(m));

            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 1)));
            Tick(m, 1);
            Assert.Greater(d.Attack.CooldownRemaining, 0f, "들어온 그 틱에 던지고 쿨이 리셋된다");
        }

        [Test]
        public void 폭탄맨은_근접_피해를_내지_않는다()
        {
            // C3 — 정책은 **대상을 고르기 전에** 처리하고 루프를 빠져나간다.
            var def = Definition(defenderDamage: 50f, policy: AttackPolicy.Bomb);
            GiveProjectile(def, MovementKind.GrenadeToCell, PayloadKind.TileAoe);
            def.Units[0].Attack.Policy = (int)AttackPolicy.Bomb;
            def.Units[0].Attack.BombProjectileDefIndex = 0;
            def.Units[0].Attack.BombDamage = 0f;   // 폭탄 자체도 무해하게
            def.ConfigHash = def.ComputeConfigHash();

            var m = Match(def);
            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 1)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 1)));
            var e = First(m, UnitKind.Enemy);

            Tick(m, 120);
            Assert.AreEqual(e.MaxHealth, e.Health, 1e-3f, "근접 출력 경로를 타면 안 된다");
        }

        // ── C2 · 소환사는 소환물이 살아 있어도 쿨을 돌린다 ──────────────────

        [Test]
        public void 소환사는_소환물이_살아있어도_쿨을_돌린다()
        {
            // 안 돌리면 소환물이 죽는 즉시 재소환이 되어 동작이 바뀐다.
            var def = Definition(policy: AttackPolicy.Summon);
            def.Units[0].Attack.Policy = (int)AttackPolicy.Summon;
            def.Units[0].Attack.SummonPatrolDefIndex = 1;   // 별도 줄(자기 참조는 무한 소환이다)
            def.ConfigHash = def.ComputeConfigHash();

            var m = Match(def);
            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 1)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 1)));

            Tick(m, 2);
            Assert.IsNotNull(First(m, UnitKind.Patrol), "구역에 적이 있으면 첫 순찰병이 나온다");

            var d = First(m, UnitKind.Defender);
            float before = d.Attack.CooldownRemaining;
            Tick(m, 200);
            Assert.AreEqual(1, CountPatrols(m), "소환물이 살아 있으면 스폰만 건너뛴다");
            Assert.AreNotEqual(before, d.Attack.CooldownRemaining, "그동안에도 쿨은 돈다");
        }

        [Test]
        public void 소환사가_죽으면_순찰병도_소멸_사건을_낸다()
        {
            // unit 2 리뷰 F3 — 초판은 `Dead` 만 세우고 지우지 않아 뷰가 그 순찰병을 영원히
            // 들고 있었다. 소멸 경로는 **하나**이고(`BattleWorld.Destroy`) 순찰 연쇄도 그 길이다.
            var def = Definition(policy: AttackPolicy.Summon);
            def.Units[0].Attack.Policy = (int)AttackPolicy.Summon;
            def.Units[0].Attack.SummonPatrolDefIndex = 1;
            def.ConfigHash = def.ComputeConfigHash();

            var m = Match(def);
            var gone = Listen(m, CoreEventKind.UnitDestroyed);
            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 1)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 1)));
            Tick(m, 2);

            var summoner = First(m, UnitKind.Defender);
            var patrol = First(m, UnitKind.Patrol);
            Assert.IsNotNull(patrol);
            var patrolId = patrol.Id;

            gone.Clear();
            m.Apply(Command.DebugDestroy(summoner.Id));
            Tick(m, 1);
            Assert.IsTrue(patrol.Dead, "표시는 그 틱에");
            Assert.AreEqual(0, CountDestroyed(gone, patrolId), "소멸은 표시 틱이 아니다");

            Tick(m, 1);
            Assert.AreEqual(1, CountDestroyed(gone, patrolId), "다음 틱에 정확히 한 번");
            Assert.IsNull(m.World.Find(patrolId));
        }

        // ── F4 · 부분은 풀에서 빌린다 ───────────────────────────────────────

        [Test]
        public void 개체_부분은_돌려쓴다()
        {
            // 어그로 획득·스폰은 틱 중에 도는 일이다. `new` 로 두면 3분 판에서 수백 개가
            // 쓰레기가 된다 — 부재(null)는 아키타입의 표현이라 그대로 두고 **객체만** 돌려쓴다.
            var m = Match(Definition(defenderDamage: 0f));
            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 1)));
            var first = First(m, UnitKind.Defender);
            int firstId = first.Id.Value;   // ⚠ `first` 자체가 풀로 돌아가 재대여된다 — 값으로 잡는다
            var attack = first.Attack;
            var footprint = first.Footprint;

            m.Apply(Command.Retire(first.Id));
            m.Apply(Command.DebugSpawnDefender(0, new int2(6, 1)));
            var second = First(m, UnitKind.Defender);

            Assert.AreNotEqual(firstId, second.Id.Value, "id 는 한 판 안에서 재사용하지 않는다");
            Assert.AreSame(attack, second.Attack, "공격 상태를 다시 빌려 온다");
            Assert.AreSame(footprint, second.Footprint, "점유도 마찬가지");
            Assert.AreEqual(0f, second.Attack.CooldownRemaining, 1e-5f, "빌려줄 때 비어 있다");
            Assert.IsTrue(second.Attack.Lock.IsNone);
        }

        // ── C17 · 실드 부여만 다음 틱 ────────────────────────────────────────

        [Test]
        public void 실드_부여는_다음_틱에_들어간다()
        {
            // 옛 버퍼가 사라지면 이 한 칸 차이가 조용히 없어진다.
            var m = Match(Definition(defenderDamage: 0f));
            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 1)));
            var d = First(m, UnitKind.Defender);

            d.Inbox.ShieldPending.Add(new ShieldGrant { Source = d.Id, Amount = 40f });
            Tick(m, 1);
            Assert.IsFalse(d.Shield.Any, "부여된 틱에는 아직 슬롯이 없다");
            Tick(m, 1);
            Assert.AreEqual(40f, Somnia.Battle.BattleCore.Combat.ShieldMath.Sum(d.Shield.Slots), 1e-3f);
        }

        [Test]
        public void 피해와_회복은_그_틱에_비운다()
        {
            var m = Match(Definition(defenderDamage: 0f));
            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 1)));
            var d = First(m, UnitKind.Defender);

            d.Inbox.Damage.Add(new DamageEntry { Amount = 7f, Source = SimEntityId.None });
            d.Inbox.Heal.Add(3f);
            Tick(m, 1);
            Assert.AreEqual(0, d.Inbox.Damage.Count);
            Assert.AreEqual(0, d.Inbox.Heal.Count);
            Assert.AreEqual(496f, d.Health, 1e-3f);
        }

        // ── C7 · 피해 숫자의 비율은 그 틱 최종값 ────────────────────────────

        [Test]
        public void 피해_사건은_그_틱_최종_체력_비율을_싣는다()
        {
            var m = Match(Definition(defenderDamage: 0f));
            var damages = Listen(m, CoreEventKind.DamageApplied);
            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 1)));
            var d = First(m, UnitKind.Defender);

            // 같은 틱 두 건 — 둘 다 **최종** 비율을 나른다(뷰가 계산하면 갈린다).
            d.Inbox.Damage.Add(new DamageEntry { Amount = 100f, Source = SimEntityId.None });
            d.Inbox.Damage.Add(new DamageEntry { Amount = 150f, Source = SimEntityId.None });
            Tick(m, 1);

            Assert.AreEqual(2, damages.Count);
            Assert.AreEqual(damages[0].SiteTarget.OriginBody, damages[1].SiteTarget.OriginBody, 1e-5f);
            Assert.AreEqual(0.5f, damages[0].SiteTarget.OriginBody, 1e-3f);
        }

        // ── C12 · 공중에서 죽는 일은 없다 ───────────────────────────────────

        [Test]
        public void 궁극기_이탈은_피해를_버린다()
        {
            // 쿼리에서 빼면 2초 동안 피해가 적립됐다가 착지 틱에 통째로 터진다(지연 폭탄).
            var m = Match(Definition(defenderDamage: 0f));
            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 1)));
            var d = First(m, UnitKind.Defender);
            d.Progressive = new ProgressiveStates { LeapActive = true, LeapRemaining = 99f };

            d.Inbox.Damage.Add(new DamageEntry { Amount = 9999f, Source = SimEntityId.None });
            Tick(m, 1);
            Assert.AreEqual(500f, d.Health, 1e-3f);
            Assert.AreEqual(0, d.Inbox.Damage.Count, "적립이 아니라 드랍이다");
        }

        // ── 사망 2단계 ───────────────────────────────────────────────────────

        [Test]
        public void 사망_표시_틱과_소멸_틱은_다르다()
        {
            // 그 창이 시체 폭발·사직서 드랍·순찰 연쇄가 자기 자리를 읽을 수 있는 유일한 구간이다.
            var m = Match(Definition(defenderDamage: 999f, enemyHealth: 10f));
            var slain = Listen(m, CoreEventKind.UnitSlain);
            var gone = Listen(m, CoreEventKind.UnitDestroyed);
            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 1)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 1)));

            Tick(m, 5);
            Assert.AreEqual(1, slain.Count);
            Assert.AreEqual(1, gone.Count);
            Assert.AreEqual(slain[0].Tick + 1, gone[0].Tick, "표시 다음 틱에 사라진다");
        }

        [Test]
        public void 처치_사건은_피해_사망에서만_난다()
        {
            // 회수(퇴근)는 소멸이지 처치가 아니다 — 분열·보상이 거기서 터지면 안 된다.
            var m = Match(Definition(defenderDamage: 0f));
            var slain = Listen(m, CoreEventKind.UnitSlain);
            var gone = Listen(m, CoreEventKind.UnitDestroyed);
            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 1)));
            var d = First(m, UnitKind.Defender);

            m.Apply(Command.Retire(d.Id));
            Tick(m, 2);
            Assert.AreEqual(1, gone.Count);
            Assert.AreEqual(0, slain.Count);
        }

        [Test]
        public void 출처_없는_사망은_처치가_아니다()
        {
            var m = Match(Definition(defenderDamage: 0f));
            var slain = Listen(m, CoreEventKind.UnitSlain);
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 1)));
            var e = First(m, UnitKind.Enemy);

            e.Inbox.Damage.Add(new DamageEntry { Amount = 9999f, Source = SimEntityId.None });
            Tick(m, 3);
            Assert.AreEqual(0, slain.Count, "지속 피해·자해·환경은 미귀속이다");
        }

        [Test]
        public void 처치_사건은_최대_피해_출처를_싣는다()
        {
            var m = Match(Definition(defenderDamage: 0f));
            var slain = Listen(m, CoreEventKind.UnitSlain);
            m.Apply(Command.DebugSpawnDefender(0, new int2(2, 1)));
            m.Apply(Command.DebugSpawnDefender(0, new int2(8, 1)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 1)));
            var units = m.World.Units;
            var a = units[0]; var b = units[1];
            var e = First(m, UnitKind.Enemy);

            e.Inbox.Damage.Add(new DamageEntry { Amount = 10f, Source = a.Id });
            e.Inbox.Damage.Add(new DamageEntry { Amount = 500f, Source = b.Id });
            Tick(m, 1);
            Assert.AreEqual(1, slain.Count);
            Assert.AreEqual(b.Id.Value, slain[0].A.Value);
        }

        // ── C8 · 방향을 모르는 대상은 밀리지 않는다 ─────────────────────────

        [Test]
        public void 방향을_모르는_대상은_안_밀린다()
        {
            // 스폰 직후·고정 구조물이 그렇다. 0 방향으로 밀면 원점으로 빨려든다.
            var def = Definition(defenderDamage: 1f);
            def.Units[0].Attack.KnockbackDistance = 2f;
            def.Units[0].Attack.KnockbackDuration = 0.2f;
            def.ConfigHash = def.ComputeConfigHash();

            var m = Match(def);
            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 1)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 1)));
            var e = First(m, UnitKind.Enemy);
            e.Move.LastMoveDir = float2.zero;

            Tick(m, 1);
            Assert.AreEqual(0, CountCc(m, CcSlotKind.Impulse));

            e.Move.LastMoveDir = new float2(1f, 0f);
            Tick(m, 61);   // 쿨 1초 뒤의 다음 공격
            Assert.AreEqual(1, CountCc(m, CcSlotKind.Impulse), "방향이 있으면 반대로 민다");
        }

        // ── C9 · 내 피해가 내 수면을 안 깨운다 ──────────────────────────────

        [Test]
        public void 같은_틱에_건_수면은_내_피해가_안_깨운다()
        {
            // 옛 전투는 시스템 순서(피해 N, 수면 N+1)의 **우연**으로 성립했다. 한 틱 안에서
            // 도는 새 코어는 명시 가드가 필요하다.
            var def = Definition(defenderDamage: 5f);
            def.Units[0].Attack.SleepOnHitSec = 3f;
            def.ConfigHash = def.ComputeConfigHash();

            var m = Match(def);
            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 1)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 1)));

            Tick(m, 1);
            // unit 6a — 요청 줄은 이제 같은 틱에 소비된다. 그래서 「줄에 뭐가 남았나」가
            // 아니라 **「그 적이 실제로 자고 있나」**를 묻는다. 가드가 깨지면 같은 틱의
            // 피해가 방금 건 잠을 도로 풀어 여기서 0 이 된다.
            Assert.AreEqual(1, CountCc(m, CcSlotKind.Sleep), "때린 틱에 걸린 잠이 살아남는다");
        }

        [Test]
        public void 지난_틱에_걸린_잠은_피격이_깨운다()
        {
            // 같은 틱 가드의 반대편. 이것까지 막으면 잠든 적이 한 대 더 맞고도 안 깬다(F25).
            var m = Match(Definition(defenderDamage: 5f, defenderCooldown: 0.2f));
            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 1)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 1)));
            var e = First(m, UnitKind.Enemy);

            m.World.RequestCc(CcRequest.Of(e.Id, CcRequestKind.Sleep, 10f, SimEntityId.None));
            Tick(m, 1);
            Assert.IsTrue(e.Cc.IsActive(CcSlotKind.Sleep));

            var cleared = Listen(m, CoreEventKind.CcCleared);
            Tick(m, 30);
            Assert.IsFalse(e.Cc.IsActive(CcSlotKind.Sleep), "맞으면 깬다");
            Assert.AreEqual(1, cleared.Count);
            Assert.AreEqual((int)CcClearReason.WokeUp, (int)cleared[0].Amount);
        }

        // ── C14 · 골을 지난 적도 유효 대상 ──────────────────────────────────

        [Test]
        public void 골을_지난_적도_때린다()
        {
            var m = Match(Definition(defenderDamage: 5f, enemySpeed: 0f));
            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 1)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 1)));
            var e = First(m, UnitKind.Enemy);
            e.Move.PastGoal = true;

            Tick(m, 1);
            Assert.Less(e.Health, e.MaxHealth, "골에 붙어 타워를 때리는 적은 살아 있는 유효 대상이다");
        }

        // ── C11 · 어그로는 배타적이다 ───────────────────────────────────────

        [Test]
        public void 끌려간_적은_가디언만_본다()
        {
            // 「가디언 없으면 최근접」으로 풀면 가는 길에 만난 유닛과 싸우느라 가디언에
            // 영영 도착하지 않는다.
            var m = Match(Definition(defenderDamage: 0f, enemyDamage: 5f, enemyRange: 3f));
            m.Apply(Command.DebugSpawnDefender(0, new int2(5, 1)));   // 가디언 아님(가까움)
            m.Apply(Command.DebugSpawnDefender(0, new int2(6, 1)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(4, 1)));

            var near = m.World.Units[0];
            var far = m.World.Units[1];
            var e = First(m, UnitKind.Enemy);

            // ⚠ 첫 틱은 장애물 시그니처가 바뀌는 틱이라 어그로가 **통째로 풀린다**(낡은 추격판
            // 무효화, unit 2 M10). 그 뒤에 물려야 이 테스트가 배타성만 묻는다.
            Tick(m, 1);
            e.Aggro = new Aggro { Target = far.Id, Capacity = 0 };
            float nearBefore = near.Health;   // 물리기 **전**에 이미 한 대 맞았다(그건 이 규칙 밖)
            float farBefore = far.Health;
            Tick(m, 61);

            Assert.AreEqual(nearBefore, near.Health, 1e-3f, "물린 뒤에는 가는 길의 유닛을 안 때린다");
            Assert.Less(far.Health, farBefore, "가디언만 본다");
        }

        // ── C15 · 거점 특별 취급 없음 ───────────────────────────────────────

        [Test]
        public void 거점은_거리로만_경쟁한다()
        {
            // 마스크를 통과한 후보는 종류를 묻지 않는다. 되살리면 거점이 항상 먼저 맞거나
            // 영영 안 맞는다.
            var def = Definition(defenderDamage: 0f, enemyDamage: 5f, enemyRange: 4f);
            def.ConfigHash = def.ComputeConfigHash();
            var m = Match(def);
            m.Apply(Command.DebugSpawnDefender(0, new int2(6, 1)));
            var unit = First(m, UnitKind.Defender);

            // 거점을 더 멀리 세운다 — 적은 **가까운 유닛**을 때려야 한다.
            var core = m.World.Spawn(UnitKind.Structure, Faction.DefenderCore, -1,
                                     new float3(9f, 0f, 1f), 0.5f, 200f, false, 0);
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 1)));

            Tick(m, 1);
            Assert.Less(unit.Health, unit.MaxHealth);
            Assert.AreEqual(core.MaxHealth, core.Health, 1e-3f);
        }

        // ── C4 · 실행할 팔이 없으면 경고한다 ────────────────────────────────

        [Test]
        public void 팔이_없으면_조용히_넘어가지_않는다()
        {
            var def = Definition(policy: AttackPolicy.Bomb);
            def.Units[0].Attack.Policy = (int)AttackPolicy.Bomb;
            def.Units[0].Attack.BombProjectileDefIndex = -1;   // 팔이 없다
            def.ConfigHash = def.ComputeConfigHash();

            var m = Match(def);
            int warned = 0;
            m.Report = _ => warned++;
            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 1)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 1)));

            Tick(m, 3);
            Assert.Greater(warned, 0, "조용한 무동작 금지가 계약이다");
        }

        // ── 도형 · 다중 타격 ────────────────────────────────────────────────

        [Test]
        public void 부가_타격은_주_대상_방향_도형_안에서만_고른다()
        {
            // 획득·유지·정지는 **원**이다. 도형은 부가 타격에만 곱해지고 넓히지 못한다.
            var def = Definition(defenderDamage: 5f, defenderRange: 4f, defenderTargetCount: 3);
            // 반각 15° 부채꼴 — 주 대상 축에서 크게 벗어난 후보는 빠진다.
            def.Units[0].Attack.ShapeKind = Somnia.Battle.BattleCore.Combat.AttackShapeBaked.SectorKind;
            def.Units[0].Attack.ShapeSinHalf = math.sin(math.radians(15f));
            def.Units[0].Attack.ShapeCosHalf = math.cos(math.radians(15f));
            def.ConfigHash = def.ComputeConfigHash();

            var m = Match(def);
            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 2)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(6, 2)));   // 주 대상(+X 축)
            m.Apply(Command.DebugSpawnEnemy(0, new int2(7, 2)));   // 같은 축 — 맞는다
            m.Apply(Command.DebugSpawnEnemy(0, new int2(4, 4)));   // 축에서 90° — 빠진다
            foreach (var u in m.World.Units) if (u.Move != null) u.Move.Speed = 0f;

            Tick(m, 1);
            var units = m.World.Units;
            Assert.Less(units[1].Health, units[1].MaxHealth);
            Assert.Less(units[2].Health, units[2].MaxHealth);
            Assert.AreEqual(units[3].MaxHealth, units[3].Health, 1e-3f, "도형 밖은 안 맞는다");
        }

        [Test]
        public void 공격_성사_사건은_판정한_도형_축_사거리를_값으로_싣는다()
        {
            // 6c 후속 — 참격 자국은 이 스냅샷으로만 그린다(뷰가 공격자를 되묻지 않는다). 사거리는
            // **런타임 값**이라 저작값이 아니라 RESOLVE 시점의 `Attack.Range` 여야 한다.
            var def = Definition(defenderDamage: 5f, defenderRange: 4f, defenderTargetCount: 3);
            def.Units[0].Attack.ShapeKind = Somnia.Battle.BattleCore.Combat.AttackShapeBaked.SectorKind;
            def.Units[0].Attack.ShapeSinHalf = math.sin(math.radians(15f));
            def.Units[0].Attack.ShapeCosHalf = math.cos(math.radians(15f));
            def.ConfigHash = def.ComputeConfigHash();

            var m = Match(def);
            var resolved = Listen(m, CoreEventKind.AttackResolved);
            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 2)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(4, 5)));   // 주 대상 — +Z 축
            foreach (var u in m.World.Units) if (u.Move != null) u.Move.Speed = 0f;

            Tick(m, 1);
            CoreEvent e = default;
            bool found = false;
            foreach (var r in resolved)
                if (r.Faction == Faction.DefenderUnit) { e = r; found = true; break; }
            Assert.IsTrue(found, "방어유닛의 공격이 성사돼야 한다");

            var attacker = m.World.Units[0];
            Assert.AreEqual(Somnia.Battle.BattleCore.Combat.AttackShapeBaked.SectorKind, e.AttackShape.kind);
            Assert.AreEqual(attacker.Attack.Shape.sinHalf, e.AttackShape.sinHalf, 1e-6f, "반각 = 판정 bake 그대로");
            Assert.AreEqual(attacker.Attack.Range, e.AttackRange, 1e-6f, "사거리 = 런타임 값");
            Assert.AreEqual(0f, e.AttackDir.x, 1e-5f, "축 = 주 대상 방향(+Z)");
            Assert.AreEqual(1f, e.AttackDir.y, 1e-5f);
            Assert.AreEqual(attacker.HitRadius, e.SiteFired.OriginBody, 1e-6f, "원점 항 = 내 몸(따로 나른다)");
        }

        // ── strict lapse ────────────────────────────────────────────────────

        [Test]
        public void 선딜_중_대상이_사라지면_빗나간다()
        {
            // START 모션만 나가고 아무 일도 안 일어난다. 재타겟하지 않는다.
            var def = Definition(defenderDamage: 5f, defenderHitDelay: 0.5f);
            def.ConfigHash = def.ComputeConfigHash();
            var m = Match(def);
            var attacks = Listen(m, CoreEventKind.AttackResolved);
            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 1)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 1)));
            var d = First(m, UnitKind.Defender);
            var only = First(m, UnitKind.Enemy);

            Tick(m, 1);                             // START — 그 적을 커밋
            m.Apply(Command.DebugDestroy(only.Id)); // 커밋 대상 소멸
            Tick(m, 40);

            // 적도 같은 루프에서 때리므로(선딜 0) 공격자를 가려 센다.
            int byDefender = 0;
            for (int i = 0; i < attacks.Count; i++) if (attacks[i].A == d.Id) byDefender++;
            Assert.AreEqual(0, byDefender, "커밋 대상이 사라진 스윙은 빗나간다 — 재타겟하지 않는다");
        }

        // ── C6 · 가디언 대표는 실제로 때린 적이다 ───────────────────────────

        [Test]
        public void 가디언_대표는_실제로_때린_적이다()
        {
            // 넉백·로그가 주 대상과 그 좌표를 읽는다. 자석 규칙이 고른 대상과 「최근접」이
            // 다를 수 있으므로 주 대상을 **실제로 때린 적**으로 정렬해 불일치를 없앤다.
            var def = Definition(defenderDamage: 3f, defenderRange: 4f, aggroCapacity: 2);
            def.Units[0].Attack.KnockbackDistance = 1f;
            def.Units[0].Attack.KnockbackDuration = 0.2f;
            def.ConfigHash = def.ComputeConfigHash();

            var m = Match(def);
            var attacks = Listen(m, CoreEventKind.AttackResolved);
            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 2)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 2)));   // 최근접 · 이미 물린 것으로 표시
            m.Apply(Command.DebugSpawnEnemy(0, new int2(6, 2)));   // 아직 안 물린 것
            var units = m.World.Units;
            // ⚠ **도발 표시**로 물린다. 그냥 물리면 첫 틱의 장애물 재빌드가 통째로 풀어 버린다
            // (낡은 추격판 무효화, unit 2 M10 — 도발만 표시가 남는다).
            units[1].Aggro = new Aggro { Target = units[0].Id, Taunted = true };
            foreach (var u in units) if (u.Move != null) u.Move.Speed = 0f;

            Tick(m, 1);
            // 적도 같은 루프에서 때리므로 가디언의 공격만 가려 본다.
            CoreEvent mine = default;
            int found = 0;
            for (int i = 0; i < attacks.Count; i++)
                if (attacks[i].A == units[0].Id) { mine = attacks[i]; found++; }
            Assert.AreEqual(1, found);
            Assert.AreEqual(units[2].Id.Value, mine.B.Value,
                "여유가 있으면 아직 안 물린 쪽이 대표가 된다 — 최근접이 아니다");
        }

        // ── 넉업은 때린 전원 ────────────────────────────────────────────────

        [Test]
        public void 넉업은_때린_전원에게_걸린다()
        {
            // 넉백·수면(주 대상 1체)과 **스코프가 다르다.** 하나로 합치면 그 유닛의 정체성이
            // 조용히 망가진다.
            var def = Definition(defenderDamage: 1f, defenderRange: 4f, defenderTargetCount: 3);
            def.Units[0].Attack.KnockupOnHitSec = 0.4f;
            def.Units[0].Attack.SleepOnHitSec = 0.4f;
            def.ConfigHash = def.ComputeConfigHash();

            var m = Match(def);
            var knockups = Listen(m, CoreEventKind.Knockup);
            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 2)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 2)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(6, 2)));
            foreach (var u in m.World.Units) if (u.Move != null) u.Move.Speed = 0f;

            Tick(m, 1);
            Assert.AreEqual(2, knockups.Count, "넉업은 전원");
            Assert.AreEqual(1, CountCc(m, CcSlotKind.Sleep), "수면은 주 대상 1체");
        }

        [Test]
        public void 보스는_기절_수면_넉백에_면역이다()
        {
            var def = Definition(defenderDamage: 1f);
            def.Units[0].Attack.SleepOnHitSec = 1f;
            def.Units[0].Attack.KnockupOnHitSec = 1f;
            def.Enemies[0].Attack.BossImmune = true;
            def.ConfigHash = def.ComputeConfigHash();

            var m = Match(def);
            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 1)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 1)));

            Tick(m, 1);
            Assert.AreEqual(0, CountCc(m, CcSlotKind.Sleep) + CountCc(m, CcSlotKind.Stun),
                "출처를 묻지 않는다 — 면역은 대상의 성질이다");
        }

        // ── 히트 구동 어그로 ────────────────────────────────────────────────

        [Test]
        public void 가디언은_때린_적을_끌어온다()
        {
            // Combat 은 「때렸다」 사실만 전달하고, 수용량·선점 게이트는 받는 쪽이 갖는다.
            var m = Match(Definition(defenderDamage: 1f, aggroCapacity: 2));
            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 1)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 1)));
            var d = First(m, UnitKind.Defender);
            var e = First(m, UnitKind.Enemy);

            Tick(m, 3);
            Assert.AreEqual(d.Id.Value, e.Aggro != null ? e.Aggro.Target.Value : -1);
        }

        [Test]
        public void 유닛을_노리지_않는_적은_가디언에게_끌려가지_않는다()
        {
            // 2026-09-24 드리프트 감사 H5 — 마음사냥꾼(`targetFactions` = 방벽·마음·본능, 유닛
            // 비트 없음)이 가디언에게 맞으면 끌려가 **마음을 향한 행진을 멈췄다.** 옛 전투는
            // 부착 한 곳(`AggroStateSystem`)에서 「유닛을 노리지 않는 적은 유인으로 못 막는다」
            // 로 거절했다 — 죽여야만 막히는 적이 이 규칙의 존재 이유다.
            var def = Definition(defenderDamage: 1f, aggroCapacity: 2);
            def.Enemies[0].TargetFactions = (int)(Faction.BlockingHazard | Faction.DefenderCore
                                                  | Faction.DefenderInstinct);
            def.ConfigHash = def.ComputeConfigHash();
            var m = Match(def);
            var acquired = Listen(m, CoreEventKind.AggroAcquired);
            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 1)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 1)));
            var e = First(m, UnitKind.Enemy);

            Tick(m, 3);
            Assert.IsTrue(e.Aggro == null || e.Aggro.Target.IsNone,
                "마음사냥꾼이 가디언에게 유인됐다 — 유닛을 노리지 않는 적은 도발·히트 어그로를 안 받는다");
            Assert.AreEqual(0, acquired.Count);
        }

        // ── 적 공격 저작의 의미(2026-09-24 드리프트 감사) ────────────────────

        private static float DefenderDamageTaken(MatchDefinition def, int ticks = 120)
        {
            var m = Match(def);
            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 1)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 1)));
            var d = First(m, UnitKind.Defender);
            float before = d.Health;
            Tick(m, ticks);
            return before - d.Health;
        }

        [Test]
        public void 비행_적은_지상을_걷는_아군을_멈춰_서서_때린다()
        {
            // 2026-09-24 드리프트 감사 M7 — 적의 공격·정지·감지는 통행 층을 거르지 않는다(옛
            // `targetTraversalLayers` 0). 「자기 통행 층」(하늘)을 대상 층으로 읽으면 경로를
            // 걷는 순찰병이 후보에서 빠지고, 정지 조건도 거짓이라 **옆에 두고 멈추지도 않는다.**
            var def = Definition(defenderDamage: 0f, enemyDamage: 10f);
            def.Enemies[0].TraversalLayers = LayerBits.Air;
            def.Enemies[0].Attack.TargetLayers = 0;
            def.ConfigHash = def.ComputeConfigHash();
            var m = Match(def);
            m.Apply(Command.DebugSpawnDefender(0, new int2(4, 1)));
            m.Apply(Command.DebugSpawnEnemy(0, new int2(5, 1)));
            var d = First(m, UnitKind.Defender);
            // 경로를 걷는 아군(순찰병)의 이동 상태 — 흐름장 슬롯이 있는 층(경로)을 준다.
            d.Move = new MoveState { Speed = 0f, TraversalLayers = LayerBits.Path };
            float before = d.Health;
            Tick(m, 120);
            Assert.Less(d.Health, before, "비행 적이 옆의 지상 순찰병을 안 때렸다");
        }

        [Test]
        public void 걷기만_하는_적은_산출물이_있어도_때리지_않는다()
        {
            var def = Definition(defenderDamage: 0f, enemyDamage: 10f);
            Assert.Greater(DefenderDamageTaken(def), 0f, "전제: 무장한 적은 옆의 방어유닛을 때린다");

            def.Enemies[0].Attack.Unarmed = true;
            def.ConfigHash = def.ComputeConfigHash();
            Assert.AreEqual(0f, DefenderDamageTaken(def), 1e-4f);
        }

        [Test]
        public void 직업_필터는_존재가_게이트다_마스크_0_은_아무도_못_때린다()
        {
            var def = Definition(defenderDamage: 0f, enemyDamage: 10f);
            def.Units[0].Role = 3;
            def.Enemies[0].Attack.ClassMask = 0;
            def.Enemies[0].Attack.HasClassFilter = false;
            def.ConfigHash = def.ComputeConfigHash();
            Assert.Greater(DefenderDamageTaken(def), 0f, "필터가 없으면 직업을 묻지 않는다");

            def.Enemies[0].Attack.HasClassFilter = true;
            def.ConfigHash = def.ComputeConfigHash();
            Assert.AreEqual(0f, DefenderDamageTaken(def), 1e-4f, "필터가 있고 마스크 0 = 아무도 못 때린다");

            def.Enemies[0].Attack.ClassMask = 1 << 3;
            def.ConfigHash = def.ComputeConfigHash();
            Assert.Greater(DefenderDamageTaken(def), 0f, "허용 비트의 직업은 때린다");
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────────────

        private static int CountProjectiles(BattleMatch m) => m.World.Projectiles.Count;

        private static int CountDestroyed(System.Collections.Generic.List<CoreEvent> events, SimEntityId id)
        {
            int n = 0;
            for (int i = 0; i < events.Count; i++) if (events[i].A == id) n++;
            return n;
        }

        private static int CountPatrols(BattleMatch m)
        {
            int n = 0;
            var units = m.World.Units;
            for (int i = 0; i < units.Count; i++) if (units[i].Kind == UnitKind.Patrol) n++;
            return n;
        }

        // unit 6a — 요청 줄은 **같은 틱에 소비된다**(`CombatPhase.FlushCc`). 그래서 이 헬퍼는
        // 「줄에 몇 건이 남았나」가 아니라 **「그 슬롯이 실제로 걸렸나」**를 센다. 요청을 세던
        // 시절의 단언은 소비자가 생긴 순간 전부 0 이 되고, 그 0 은 규칙을 증언하지 않는다.
        private static int CountCc(BattleMatch m, CcSlotKind kind)
        {
            int n = 0;
            var units = m.World.Units;
            for (int i = 0; i < units.Count; i++) if (units[i].Cc.IsActive(kind)) n++;
            return n;
        }
    }
}
