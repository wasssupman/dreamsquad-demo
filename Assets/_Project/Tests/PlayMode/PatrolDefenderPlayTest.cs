using System.Collections;
using System.Reflection;
using NUnit.Framework;
using PrimeTween;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Wassup.Battle.Combat;
using Wassup.Battle.Effects;
using Wassup.Battle.Movement;
using Wassup.Battle.Units;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Data;

namespace Wassup.Tests.PlayMode
{
    // summon-patrol-defender unit 7 — 라이브 씬/에셋을 태우는 e2e.
    // EditMode PatrolSystemIntegrationTests가 시스템 seam을 고정하고, 이 테스트는
    // 배치 SO bake → blind 요청 → Bridge 스폰/뷰 → 수명 순환이 실제로 이어지는지 본다.
    public class PatrolDefenderPlayTest
    {
        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            TimeManager.Instance.ResetAll();
            Tween.StopAll();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Summoner_SpawnsOnePatrol_ThatReceivesSupport_AndRespawns()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return LoadBattleScene();

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();
            var catalog = FindCatalog();
            Assert.IsNotNull(bridge, "BattleBridge present");
            Assert.IsNotNull(gm, "GameManager present");
            Assert.IsNotNull(catalog, "DefenderCatalog present");

            var summonerData = catalog.ById("summoner");
            var healerData = catalog.ById("healer");
            var shieldData = catalog.ById("shield_shuttle");
            Assert.IsNotNull(summonerData, "summoner is directly deployable");
            Assert.IsNotNull(healerData, "healer in catalog");
            Assert.IsNotNull(shieldData, "shield shuttle in catalog");
            Assert.IsNull(catalog.ById("patrol_soldier"),
                "patrol soldier must not be directly exposed in the roster");

            var ability = summonerData.GetAbility<SummonPatrolAbility>();
            Assert.IsNotNull(ability, "summoner ability baked from SO");
            Assert.IsNotNull(ability.patrolUnit, "patrol unit SO wired");

            bridge.SetDefenderPool(new[] { summonerData, healerData, shieldData });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
            yield return null;

            Assert.IsTrue(FindSummonerCell(bridge, summonerData, ability.patrolUnit, out var ownerCell),
                "placeable summoner cell whose own cell (or a cover-local cell) is traversable");
            Assert.IsTrue(bridge.PlaceDefenderAs(ownerCell.x, ownerCell.y, summonerData), "place summoner");
            Assert.IsTrue(PlaceFirstValid(bridge, healerData, out var healerCell), "place healer");
            Assert.IsTrue(PlaceFirstValid(bridge, shieldData, out var shieldCell), "place shield shuttle");

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var summoner = EntityAt(bridge, em, ownerCell);
            var healer = EntityAt(bridge, em, healerCell);
            var shield = EntityAt(bridge, em, shieldCell);
            Assert.AreNotEqual(Entity.Null, summoner, "summoner entity");
            Assert.AreNotEqual(Entity.Null, healer, "healer entity");
            Assert.AreNotEqual(Entity.Null, shield, "shield shuttle entity");
            Assert.IsTrue(em.HasComponent<SummonerState>(summoner), "SummonerState baked");

            bridge.StartBattle();
            OpenSummonGate(em, summoner);
            ForceAttackReady(em, summoner);

            Entity patrol = Entity.Null;
            for (int i = 0; i < 120 && patrol == Entity.Null; i++)
            {
                yield return null;
                patrol = ResolveLivePatrol(em, summoner);
            }
            Assert.AreNotEqual(Entity.Null, patrol, "summon request reaches the live patrol entity");
            Assert.AreEqual(1, CountWith<PatrolAnchor>(em), "one patrol per summoner");

            // unit 9 — 담당 구역이 소환사에게서 나오는지. 라이브 맵이 있어야만 성립하는 축이라
            // EditMode 로 못 내린다(배치 가능 셀·통행 층이 실제 맵 데이터다).
            var owner2 = new int2(ownerCell.x, ownerCell.y);
            var liveAnchor = em.GetComponentData<PatrolAnchor>(patrol);
            Assert.AreEqual(owner2, liveAnchor.cell,
                "박스 중심 = 소환사 셀. 배치 프리뷰가 칠한 중심과 같아야 한다");
            Assert.AreEqual(GridMath.RangeToTiles(summonerData.attackRange), liveAnchor.tileRadius,
                "담당 구역 반경의 유일한 출처는 소환사 attackRange");

            // 스폰/대기 칸은 소환사 **주변**이다 — 같은 칸이면 둘이 겹쳐 서서
            // 소환물이 소환사에 박힌 것으로 읽힌다(사용자 지적 2026-08-10).
            Assert.AreNotEqual(owner2, liveAnchor.homeCell, "집은 소환사 셀이 아니다");
            Assert.LessOrEqual(GridMath.ChebyshevDistance(liveAnchor.homeCell, owner2),
                liveAnchor.tileRadius, "집은 담당 구역 안이다");
            Assert.AreEqual(liveAnchor.homeCell, CellOf(em, patrol), "스폰 위치 = 집");

            // 퇴화 분기 — Path 전용으로 저작된 소환물은 배치지에 설 수 없으므로 구역 안
            // 통행 가능 셀로 물러서야 한다. 물러서지 않으면 설 수 없는 칸을 향해 영원히 전진한다.
            var snap = typeof(BattleBridge).GetMethod(
                "TryGetPatrolHomeCell", BindingFlags.NonPublic | BindingFlags.Instance);
            object[] pathOnly = { owner2, liveAnchor.tileRadius, (byte)PlacementLayer.Path, default(int2) };
            Assert.IsTrue((bool)snap.Invoke(bridge, pathOnly), "Path 전용 소환물도 집을 찾는다");
            var degraded = (int2)pathOnly[3];
            Assert.AreNotEqual(owner2, degraded, "배치지는 Path 를 열지 않는다");
            Assert.LessOrEqual(GridMath.ChebyshevDistance(degraded, owner2),
                liveAnchor.tileRadius, "퇴화 집도 담당 구역 안이어야 한다");

            // ───────── traversal-layers unit 5 — **증상 그대로의 단언: "순찰병이 움직인다"** ─────────
            //
            // 이 단언이 없어서 같은 버그를 세 번 놓쳤다. 당시 `PatrolStep.dir` 은 정상값
            // `(-1,0)` 을 내고 있었고 **위치만** 고정돼 있었다(충돌 NavGrid 가 층을 몰라
            // 배치지를 벽으로 읽었다). 스폰·컴포넌트·앵커·반경·집 좌표 단언은 **전부 통과**했다
            // — 얼어붙은 순찰병도 그 단언들을 통과하기 때문이다.
            //
            // 적 스폰 타이밍에 기대지 않는 결정론 축으로 만든다: 순찰병을 **소환사 셀**
            // (= 배치지, 구역 안)로 옮기면 집으로 복귀해야 한다. 결함 당시엔 바로 그 칸이
            // 벽이라 영원히 clamp 됐으므로, 이 단언은 그 결함에서 반드시 빨갛다.
            var patrolY = em.GetComponentData<LocalTransform>(patrol).Position.y;
            var ownerPos = em.GetComponentData<LocalTransform>(summoner).Position;
            MoveTo(em, patrol, new float3(ownerPos.x, patrolY, ownerPos.z));
            var displaced = CellOf(em, patrol);
            Assert.AreEqual(owner2, displaced, "테스트 전제: 배치지 칸으로 옮겨졌다");

            bool moved = false;
            for (int i = 0; i < 300 && !moved; i++)
            {
                yield return null;
                if (!em.Exists(patrol)) break;
                moved = !CellOf(em, patrol).Equals(displaced);
            }
            Assert.IsTrue(em.Exists(patrol), "관찰 중 순찰병이 살아 있어야 판정이 유효하다");
            Assert.IsTrue(moved,
                "배치지 위의 순찰병이 한 칸도 못 움직이면 통행 층이 충돌 판정에 안 닿은 것이다");

            Assert.IsTrue(em.HasComponent<DefenderUnitTag>(patrol));
            Assert.IsTrue(em.HasComponent<DefenderClassTag>(patrol));
            Assert.IsTrue(em.HasComponent<FactionTag>(patrol));
            Assert.AreEqual(Faction.DefenderUnit, em.GetComponentData<FactionTag>(patrol).value);
            Assert.IsFalse(em.HasComponent<DefenderTile>(patrol),
                "no placement/death-event/awakening farming path");
            Assert.IsFalse(em.HasComponent<AttackUnitTag>(patrol), "not an enemy/leak unit");
            Assert.IsTrue(em.HasComponent<SummonedBy>(patrol));
            Assert.AreEqual(summoner, em.GetComponentData<SummonedBy>(patrol).owner);
            Assert.IsFalse(em.HasBuffer<DcTriggerSlot>(patrol), "dreamcatcher cards are not baked onto patrol");
            Assert.IsFalse(HasModifierOrigin(em, patrol, ModifierOrigin.Tile),
                "effect tiles are DefenderTile placement effects, not moving-patrol effects");
            Assert.IsFalse(HasModifierOrigin(em, patrol, ModifierOrigin.Dreamcatcher),
                "active dreamcatcher effects are not copied onto patrol");

            for (int i = 0; i < 4; i++) yield return null;
            Assert.IsTrue(bridge.TryGetUnitView(patrol, out var patrolView), "patrol Spine view spawned");

            // unit 10 — 소환물이 살아 있는 동안 소환사는 능력이 저작한 루프(attack2)를 돈다.
            // 소환 원샷(drop, ~2s)이 끝난 **다음**에 적용되는 것이 정상이므로 여유를 두고 기다린다.
            // 이 단언이 잡는 회귀: 오버라이드 요청이 원샷 도중에 오면 저장만 되고 영영 적용되지
            // 않아, 소환사가 쿨다운마다 소환 애니만 반복한다(2026-08-12 사용자 제보).
            Assert.IsTrue(bridge.TryGetUnitView(summoner, out var summonerView), "summoner Spine view spawned");
            string activeAnim = summonerData.GetAbility<SummonPatrolAbility>().activeAnimation;
            Assert.IsFalse(string.IsNullOrEmpty(activeAnim), "summoner ability authors an active animation");
            float animDeadline = Time.realtimeSinceStartup + 6f;
            while (summonerView.CurrentAnimationName != activeAnim && Time.realtimeSinceStartup < animDeadline)
                yield return null;
            Assert.AreEqual(activeAnim, summonerView.CurrentAnimationName,
                "소환물 생존 중 소환사가 능력 루프로 들어간다");
            // unit 6(발밑 아군 링) 단언은 그 unit 이 철회되며 함께 제거했다 — 표식이 필요했던
            // 근거("순찰병이 적과 같은 스켈레톤·같은 실루엣")를 unit 8 의 고유 리그가 없앴다.
            // 경위: docs/spec/summon-patrol-defender/6_ally_readability.md 상단.

            // 실제 지원 시스템 대상 집합에 순찰병이 들어오는지 검증한다. 배치 셀은 그대로 두고
            // 테스트에서만 캐스터의 sim 위치를 순찰병 곁으로 옮겨 range 변수를 제거한다.
            var patrolPos = em.GetComponentData<LocalTransform>(patrol).Position;
            MoveTo(em, healer, patrolPos);
            MoveTo(em, shield, patrolPos);
            var hp = em.GetComponentData<Health>(patrol);
            hp.value = math.max(1f, hp.max * 0.2f);
            em.SetComponentData(patrol, hp);
            float damagedHp = hp.value;
            ForceAttackReady(em, healer);
            var shieldCast = em.GetComponentData<ShieldCastState>(shield);
            shieldCast.cooldownRemaining = 0f;
            em.SetComponentData(shield, shieldCast);

            bool healed = false;
            bool shielded = false;
            for (int i = 0; i < 240 && (!healed || !shielded); i++)
            {
                yield return null;
                if (!em.Exists(patrol)) break;
                healed = em.GetComponentData<Health>(patrol).value > damagedHp;
                shielded = ShieldMath.Sum(em.GetBuffer<ShieldSlot>(patrol)) > 0f;
            }
            Assert.IsTrue(healed, "healer targets and heals the moving defender");
            Assert.IsTrue(shielded, "shield shuttle grants a shield to the moving defender");

            var firstPatrol = patrol;
            hp = em.GetComponentData<Health>(firstPatrol);
            hp.value = 0f;
            em.SetComponentData(firstPatrol, hp);
            for (int i = 0; i < 120 && em.Exists(firstPatrol); i++) yield return null;
            Assert.IsFalse(em.Exists(firstPatrol), "dead patrol is destroyed through the general unit path");

            ForceAttackReady(em, summoner);   // 게이트는 첫 소환에서 이미 소비됨 — 재소환은 무게이트
            Entity respawned = Entity.Null;
            for (int i = 0; i < 120 && respawned == Entity.Null; i++)
            {
                yield return null;
                var candidate = ResolveLivePatrol(em, summoner);
                if (candidate != firstPatrol) respawned = candidate;
            }
            Assert.AreNotEqual(Entity.Null, respawned, "stale current handle is replaced by a respawn");
            Assert.AreNotEqual(firstPatrol, respawned, "respawn is a new entity version");
            Assert.AreEqual(1, CountWith<PatrolAnchor>(em), "respawn keeps the one-patrol cap");

            // unit 9 — **소환사를 재배치하면 담당 구역과 집이 따라온다.**
            //
            // ⚠ 이 축은 **테스트 맨 끝**에 둔다. 재배치는 PendingDeployment 를 붙이는데
            // AttackSystem 쿼리가 `WithNone<PendingDeployment>` 라 소환사가 공격 루프에서
            // 빠진다 — 앞에 두면 뒤의 재소환 축이 «소환을 못 해서» 빨개진다(실제로 겪음).
            // 실경로(TryBeginDefenderRelocation)를 태운다 — RelocatePatrolAnchorFor 를 직접
            // 부르면 "그 함수가 옳다"만 보이고 "재배치가 그걸 부른다"는 안 보인다.
            Vector2Int relocTo = default;
            bool foundReloc = false;
            for (int x = -24; x < 48 && !foundReloc; x++)
            for (int y = -24; y < 48 && !foundReloc; y++)
            {
                var cand = new Vector2Int(x, y);
                if (cand == ownerCell) continue;
                if (!bridge.CanRelocateDefender(ownerCell, cand, out _)) continue;
                relocTo = cand; foundReloc = true;
            }
            Assert.IsTrue(foundReloc, "재배치 가능한 목적 셀이 있어야 이 축을 볼 수 있다");
            Assert.IsTrue(bridge.TryBeginDefenderRelocation(ownerCell, relocTo, out _, out _),
                "소환사 재배치가 성사된다");

            var relocCell = new int2(relocTo.x, relocTo.y);
            var afterAnchor = em.GetComponentData<PatrolAnchor>(respawned);
            Assert.AreEqual(relocCell, afterAnchor.cell,
                "박스 중심이 새 소환사 셀을 따라온다 — 배치 프리뷰가 칠하는 곳과 같아야 한다");
            Assert.AreNotEqual(relocCell, afterAnchor.homeCell, "집은 여전히 소환사 셀이 아니다");
            Assert.LessOrEqual(GridMath.ChebyshevDistance(afterAnchor.homeCell, relocCell),
                afterAnchor.tileRadius, "집이 새 구역 안으로 따라온다");


            bridge.StopBattle();
            yield return null;
            Assert.AreEqual(0, CountWith<PatrolAnchor>(em), "match boundary clears patrol entities");
            Assert.AreEqual(0, CountWith<PatrolRequestCarrier>(em), "match boundary clears staged requests");
        }

        [UnityTest]
        public IEnumerator SummonerDeath_RemovesItsPatrolAndView()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return LoadBattleScene();

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();
            var catalog = FindCatalog();
            var summonerData = catalog.ById("summoner");
            var ability = summonerData.GetAbility<SummonPatrolAbility>();

            bridge.SetDefenderPool(new[] { summonerData });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
            yield return null;

            Assert.IsTrue(FindSummonerCell(bridge, summonerData, ability.patrolUnit, out var ownerCell));
            Assert.IsTrue(bridge.PlaceDefenderAs(ownerCell.x, ownerCell.y, summonerData));
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var summoner = EntityAt(bridge, em, ownerCell);

            bridge.StartBattle();
            OpenSummonGate(em, summoner);
            ForceAttackReady(em, summoner);

            Entity patrol = Entity.Null;
            for (int i = 0; i < 120 && patrol == Entity.Null; i++)
            {
                yield return null;
                patrol = ResolveLivePatrol(em, summoner);
            }
            Assert.AreNotEqual(Entity.Null, patrol, "initial patrol spawned");
            for (int i = 0; i < 4; i++) yield return null;
            Assert.IsTrue(bridge.TryGetUnitView(patrol, out _), "patrol view exists before owner death");

            var ownerHp = em.GetComponentData<Health>(summoner);
            ownerHp.value = 0f;
            em.SetComponentData(summoner, ownerHp);
            for (int i = 0; i < 180 && (em.Exists(summoner) || em.Exists(patrol)); i++) yield return null;

            Assert.IsFalse(em.Exists(summoner), "summoner destroyed");
            Assert.IsFalse(em.Exists(patrol), "owner-linked patrol destroyed");
            for (int i = 0; i < 4; i++) yield return null;
            Assert.IsFalse(bridge.TryGetUnitView(patrol, out _), "patrol view is reclaimed");
            Assert.AreEqual(0, CountWith<PatrolAnchor>(em), "no ghost patrol remains");
        }

        // defender-clock-out unit 1 — 위 사망 판본의 **쌍둥이**. 소환사가 죽는 대신 **퇴근**해도
        // 순찰병이 따라 내려가는가.
        //
        // 이 단정만은 반드시 남긴다. 퇴근은 DeadTag 를 달지 않고 브리지가 엔티티를 직접 파괴하는데,
        // 순찰병 회수는 PatrolLifecycleSystem 이 **Exists(owner) 를 첫 검사로 쓴다**는 사실 하나에
        // 얹혀 있다. 코드만 읽어서는 자명하지 않은 유일한 cross-system 주장이라, 그 줄이 바뀌면
        // 유령 순찰병으로 나타난다. 여기가 그 경보다.
        [UnityTest]
        public IEnumerator RetiredSummoner_AlsoRemovesPatrol()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return LoadBattleScene();

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();
            var summonerData = FindCatalog().ById("summoner");
            var ability = summonerData.GetAbility<SummonPatrolAbility>();

            bridge.SetDefenderPool(new[] { summonerData });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
            yield return null;

            Assert.IsTrue(FindSummonerCell(bridge, summonerData, ability.patrolUnit, out var ownerCell));
            Assert.IsTrue(bridge.PlaceDefenderAs(ownerCell.x, ownerCell.y, summonerData));
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var summoner = EntityAt(bridge, em, ownerCell);

            bridge.StartBattle();
            OpenSummonGate(em, summoner);
            ForceAttackReady(em, summoner);

            Entity patrol = Entity.Null;
            for (int i = 0; i < 120 && patrol == Entity.Null; i++)
            {
                yield return null;
                patrol = ResolveLivePatrol(em, summoner);
            }
            Assert.AreNotEqual(Entity.Null, patrol, "initial patrol spawned");

            Assert.IsTrue(bridge.RetireDefender(ownerCell), "소환사 퇴근");
            Assert.IsFalse(em.Exists(summoner), "소환사는 즉시 파괴된다(브리지 직접 파괴)");

            // 순찰병 회수는 sim 틱을 한 번 돈다 — PatrolLifecycleSystem 이 Exists(owner)=false 를
            // 보고 DeadTag 를 붙이면 UnitLifecycleSystem 이 파괴한다. 1틱 지연은 계약대로 무해.
            for (int i = 0; i < 180 && em.Exists(patrol); i++) yield return null;
            Assert.IsFalse(em.Exists(patrol), "owner-linked patrol destroyed on retire");
            for (int i = 0; i < 4; i++) yield return null;
            Assert.AreEqual(0, CountWith<PatrolAnchor>(em), "no ghost patrol remains");
        }

        private static IEnumerator LoadBattleScene()
        {
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;
        }

        private static DefenderCatalog FindCatalog()
        {
            var all = Resources.FindObjectsOfTypeAll<DefenderCatalog>();
            return all.Length > 0 ? all[0] : null;
        }

        // unit 9 — 담당 구역은 소환사 attackRange 에서 나오고, 거점 성립 여부는 순찰병의
        // 통행 층에 달렸다. 두 값을 SO 에서 뽑아 실제 스냅 함수에 그대로 물어본다.
        private static bool FindSummonerCell(
            BattleBridge bridge,
            DefenderUnitData data,
            DefenderUnitData patrolUnit,
            out Vector2Int cell)
        {
            var method = typeof(BattleBridge).GetMethod(
                "TryGetPatrolHomeCell",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, "patrol home cell selector");

            int coverRadius = Wassup.Battle.Movement.GridMath.RangeToTiles(data.attackRange);
            byte layers = (byte)patrolUnit.EffectiveTraversalLayers;

            for (int x = -24; x < 48; x++)
            for (int y = -24; y < 48; y++)
            {
                if (!bridge.CanPlaceDefenderAt(x, y, data, out _)) continue;
                object[] args = { new int2(x, y), coverRadius, layers, default(int2) };
                if (!(bool)method.Invoke(bridge, args)) continue;
                cell = new Vector2Int(x, y);
                return true;
            }
            cell = default;
            return false;
        }

        private static bool PlaceFirstValid(BattleBridge bridge, DefenderUnitData data, out Vector2Int cell)
        {
            for (int x = -24; x < 48; x++)
            for (int y = -24; y < 48; y++)
            {
                if (!bridge.CanPlaceDefenderAt(x, y, data, out _)) continue;
                cell = new Vector2Int(x, y);
                return bridge.PlaceDefenderAs(x, y, data);
            }
            cell = default;
            return false;
        }

        private static System.Collections.IDictionary DefenderBindings(BattleBridge bridge)
        {
            var field = typeof(BattleBridge).GetField(
                "_defenderByTile",
                BindingFlags.NonPublic | BindingFlags.Instance);
            return (System.Collections.IDictionary)field.GetValue(bridge);
        }

        private static Entity EntityAt(BattleBridge bridge, EntityManager em, Vector2Int cell)
        {
            var dict = DefenderBindings(bridge);
            if (!dict.Contains(cell)) return Entity.Null;
            var tuple = dict[cell];
            var entity = (Entity)tuple.GetType().GetField("Item1").GetValue(tuple);
            return em.Exists(entity) ? entity : Entity.Null;
        }

        // 초회 소환 게이트를 연다. 이 테스트가 보는 것은 게이트가 아니라
        // AttackSystem → 캐리어 → Bridge 드레인 → CreatePatrolEntity 파이프라인이라,
        // "구역 안에 적이 있어야 첫 소환" 조건을 라이브 웨이브 타이밍에 맡기면 flaky 해진다.
        // 게이트 자체는 EditMode PatrolSystemIntegrationTests 가 5 케이스로 덮는다.
        private static void OpenSummonGate(EntityManager em, Entity summoner)
        {
            var state = em.GetComponentData<SummonerState>(summoner);
            state.hasSummonedOnce = true;
            em.SetComponentData(summoner, state);
        }

        private static void ForceAttackReady(EntityManager em, Entity entity)
        {
            var attack = em.GetComponentData<AttackState>(entity);
            attack.cooldownRemaining = 0f;
            em.SetComponentData(entity, attack);
        }

        private static Entity ResolveLivePatrol(EntityManager em, Entity summoner)
        {
            if (!em.Exists(summoner) || !em.HasComponent<SummonerState>(summoner)) return Entity.Null;
            var current = em.GetComponentData<SummonerState>(summoner).current;
            return current != Entity.Null
                   && em.Exists(current)
                   && em.HasComponent<PatrolAnchor>(current)
                   && em.HasComponent<Health>(current)
                   && em.GetComponentData<Health>(current).value > 0f
                ? current
                : Entity.Null;
        }

        private static int CountWith<T>(EntityManager em) where T : unmanaged, IComponentData
        {
            using var query = em.CreateEntityQuery(ComponentType.ReadOnly<T>());
            return query.CalculateEntityCount();
        }

        private static bool HasModifierOrigin(EntityManager em, Entity entity, ModifierOrigin origin)
        {
            if (!em.HasBuffer<StatModifierSlot>(entity)) return false;
            var slots = em.GetBuffer<StatModifierSlot>(entity);
            for (int i = 0; i < slots.Length; i++)
                if (slots[i].header.origin == origin) return true;
            return false;
        }

        private static void MoveTo(EntityManager em, Entity entity, float3 position)
        {
            var transform = em.GetComponentData<LocalTransform>(entity);
            transform.Position = position;
            em.SetComponentData(entity, transform);
        }

        private static int2 CellOf(EntityManager em, Entity entity)
        {
            var f = em.CreateEntityQuery(typeof(FlowFieldSingleton)).GetSingleton<FlowFieldSingleton>();
            return GridMath.WorldToCell(
                em.GetComponentData<LocalTransform>(entity).Position, f.tileSize, f.gridSize, origin: f.origin);
        }
    }
}
