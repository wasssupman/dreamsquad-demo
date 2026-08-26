using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Wassup.Core;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.Battle.Units;
using Wassup.Battle.Effects;

namespace Wassup.Tests.PlayMode
{
    // dreamcatcher-kill-and-threshold unit 3 — Spec B 두 능력의 실전투 통합 검증.
    // last_stand: HP 임계 돌파 시 자기 공격력 버프가 실제 ModifierStats 에 올라오는가.
    // devouring_craving: 적 처치 시 killer(=공격자) 에게 공속 버프가 붙는가(OnKill+킬귀속).
    // 코어 IncomingDamage.source 수술이 기존 데미지 경로를 깨지 않는지도 여기서 커버.
    public class DreamcatcherKillThresholdTest
    {
        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        // last_stand — HealthThreshold(fraction=0.7 → HP 30% 이하) × SelfStatBuff(공격력 +30%, 영구).
        [UnityTest]
        public IEnumerator LastStand_BelowHpThreshold_BuffsAttackDamage()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleBridge present");
            var gm = Object.FindObjectOfType<GameManager>();
            var cat = FindDefenderCatalog();
            var guardian = cat.ById("guardian");

            bridge.SetDefenderPool(new[] { guardian });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
            yield return null;
            Assert.IsTrue(PlaceFirstValid(bridge, guardian), "place guardian");
            yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var defender = FindDefender(bridge, em);
            Assert.AreNotEqual(Entity.Null, defender, "defender resolved");

            int handle = bridge.ApplyDreamcatcherCardToUnit(defender, MakeLastStandCard());
            Assert.GreaterOrEqual(handle, 0, "last_stand attached (bake ok, maxHp snapshot)");

            // on-place 버프 감쇠 → damageMul baseline 안정화.
            yield return RunSeconds(8f);
            float preMul = em.GetComponentData<ModifierStats>(defender).damageMul;

            // 30% 경계 돌파(생존 유지) — HP 를 25% 로. 적이 없어 HP 는 고정.
            var hp = em.GetComponentData<Health>(defender);
            em.SetComponentData(defender, new Health { value = hp.max * 0.25f, max = hp.max });
            yield return RunSeconds(1.5f); // HealthThresholdSystem fire → ModifierApply → Aggregate
            float postMul = em.GetComponentData<ModifierStats>(defender).damageMul;

            Assert.Greater(postMul, preMul * 1.2f,
                $"last_stand: HP<30% 돌파 후 damageMul 상승 예상 ({preMul:0.00}->{postMul:0.00})");
        }

        // devouring_craving — OnKill × SelfStatBuff(공속 +8%, 4s). 처치 시 killer 에 부착.
        [UnityTest]
        public IEnumerator DevouringCraving_OnKill_BuffsAttackSpeed()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();
            var cat = FindDefenderCatalog();
            var guardian = cat.ById("guardian"); // melee → 직접 IncomingDamage(source=attacker)

            bridge.SetDefenderPool(new[] { guardian });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
            yield return null;
            Assert.IsTrue(PlaceFirstValid(bridge, guardian), "place guardian");
            yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var defender = FindDefender(bridge, em);
            Assert.AreNotEqual(Entity.Null, defender, "defender resolved");

            int handle = bridge.ApplyDreamcatcherCardToUnit(defender, MakeDevouringCard());
            Assert.GreaterOrEqual(handle, 0, "devouring attached");

            yield return RunSeconds(8f); // on-place 감쇠
            float preAs = em.GetComponentData<ModifierStats>(defender).attackSpeedMul;

            // 약한 적(HP 1) 을 사거리 안에 → guardian 이 한 방에 처치 → OnKill 발동.
            var defPos = em.GetComponentData<LocalTransform>(defender).Position;
            var enemy = em.CreateEntity();
            em.AddComponentData(enemy, LocalTransform.FromPosition(defPos + new float3(0.05f, 0f, 0f)));
            em.AddComponentData(enemy, new Health { value = 1f, max = 1f });
            em.AddComponentData(enemy, new FactionTag { value = Faction.EnemyUnit });
            em.AddBuffer<IncomingDamage>(enemy);
            // 스킬 레이어는 두 풀 안의 엔티티만 다룬다(unit 3a 함정) — 실적 아키타입 모사.
            em.AddComponent<Wassup.Battle.Units.AttackUnitTag>(enemy);
            BattleBridgeTestAccess.AttachSimEntityId(bridge, enemy);

            // ⚠ **죽음 seam 의 첫 증인.** 이 단언이 없으면 라우팅이 끊기고 legacy arm 이
            // 대신 처리해도 아래 결과 단언이 초록이다.
            Wassup.Battle.Skills.SkillDispatchSystemBase.ResetExecutedCount();

            float t = 0f;
            while (t < 5f && em.Exists(enemy) && em.GetComponentData<Health>(enemy).value > 0f)
            { t += Time.deltaTime; yield return null; }
            Assert.IsTrue(!em.Exists(enemy) || em.GetComponentData<Health>(enemy).value <= 0f,
                "guardian 이 약한 적을 처치");

            for (int i = 0; i < 4; i++) yield return null; // ModifierApply/Aggregate 정착
            float postAs = em.GetComponentData<ModifierStats>(defender).attackSpeedMul;

            Assert.GreaterOrEqual(
                Wassup.Battle.Skills.SkillDispatchSystemBase.ExecutedCountOf(
                    Wassup.Battle.Skills.SkillSeam.Death), 1,
                "죽음 seam 이 concrete 를 안 거쳤다 — legacy arm 이 대신 처리했다면 아래 단언은 "
                + "라우팅이 끊겨도 초록이 된다");
            Assert.Greater(postAs, preAs * 1.03f,
                $"devouring: 처치 후 attackSpeedMul 상승 예상 ({preAs:0.000}->{postAs:0.000})");
        }

        private static DreamcatcherCard MakeLastStandCard()
        {
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.axis = CardTargetAxis.All;
            card.type = CardType.Unit;
            card.effects = new CardEffect[0];
            card.attackMods = new DcAttackModSpec[0];
            card.mechanics = new[] { new DcMechanic {
                trigger = new DcTriggerSpec { kind = DcTriggerKind.HealthThreshold, fraction = 0.7f },
                payload = new DcPayloadSpec { kind = DcPayloadKind.SelfStatBuff, buffStat = CardBuffKind.AttackDamage, magnitude = 30f, duration = 0f },
            }};
            return card;
        }

        // skill-layer-migration unit 3d — **시체폭발의 첫 행동 그물.**
        //
        // 이 payload 는 여태 「붙는다」만 검증됐고 **어디서 터지나**를 아무도 안 쟀다.
        // 그게 이 스킬의 전부인데도 그랬다 — 자기 자리 폭발과 코드가 거의 같아 보이지만
        // 게임에서는 「내가 맞은 자리」와 「내가 죽인 자리」로 완전히 다른 그림이다.
        //
        // 가르는 기하: 방어유닛에서 1칸에 처치 대상 A, 2칸에 구경꾼 B, 반경 1.
        //   · 폭발이 **A 자리**면 B 는 1칸이라 맞는다   ← 사양
        //   · 폭발이 **방어유닛 자리**면 B 는 2칸이라 안 맞는다
        // 그래서 B 의 피해 유무 하나로 자리가 갈린다.
        [UnityTest]
        public IEnumerator CorpseBurst_ExplodesAtTheVictimsSpot_NotTheCasters()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();
            var guardian = FindDefenderCatalog().ById("guardian");
            bridge.SetDefenderPool(new[] { guardian });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
            yield return null;
            Assert.IsTrue(PlaceFirstValid(bridge, guardian), "place guardian");
            // ⚠ **투사체 요청 드레인은 `_running` 아래다.** 배치 페이즈에 머물면 폭발
            // 캐리어가 만들어지고 영원히 안 풀린다(unit 2a 에서 프레임 계측으로 확인).
            // 형제 테스트(포식)는 스탯 모디파이어라 이 경로가 필요 없었다.
            bridge.StartBattle();
            BattleBridgeTestAccess.SetField(bridge, "_usingGeneratedWaves", false);
            ((System.Collections.IList)BattleBridgeTestAccess.Field(bridge, "_pending")).Clear();
            yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var defender = FindDefender(bridge, em);
            Assert.AreNotEqual(Entity.Null, defender, "defender resolved");
            Assert.GreaterOrEqual(bridge.ApplyDreamcatcherCardToUnit(defender, MakeCorpseBurstCard()), 0,
                "corpse burst attached");

            float tile = (bridge.GridToWorldCenterVector(new Vector2Int(1, 0))
                          - bridge.GridToWorldCenterVector(new Vector2Int(0, 0))).magnitude;
            var defPos = em.GetComponentData<LocalTransform>(defender).Position;

            // A: 곧 죽을 적(HP 1) — 방어유닛에서 1칸.
            var victim = SpawnBystander(em, bridge, defPos + new float3(tile, 0f, 0f), hp: 1f);
            // B: 구경꾼 — A 에서 1칸, 방어유닛에서 2칸.
            const float ByHp = 100000f;
            var bystander = SpawnBystander(em, bridge, defPos + new float3(tile * 2f, 0f, 0f), ByHp);

            Wassup.Battle.Skills.SkillDispatchSystemBase.ResetExecutedCount();

            float t = 0f;
            while (t < 6f && em.Exists(victim) && em.GetComponentData<Health>(victim).value > 0f)
            { t += Time.deltaTime; yield return null; }
            Assert.IsTrue(!em.Exists(victim) || em.GetComponentData<Health>(victim).value <= 0f,
                "전제: 방어유닛이 A 를 처치해야 이 그물이 측정이 된다");

            for (int i = 0; i < 30; i++) yield return null;   // 캐리어 → 탄 → 피해
            float dealt = ByHp - em.GetComponentData<Health>(bystander).value;
            if (em.Exists(victim)) em.DestroyEntity(victim);
            em.DestroyEntity(bystander);

            Assert.GreaterOrEqual(
                Wassup.Battle.Skills.SkillDispatchSystemBase.ExecutedCountOf(
                    Wassup.Battle.Skills.SkillSeam.Death), 1,
                "죽음 seam 이 concrete 를 안 거쳤다");
            Assert.Greater(dealt, 0f,
                "구경꾼이 안 맞았다 — 폭발이 죽은 자리가 아니라 시전자 자리에서 터졌다(2칸은 반경 밖)");
        }

        // skill-layer-migration unit 3d′ — **잿불(죽은 자리 장판)의 첫 행동 그물.**
        //
        // 시체폭발과 자리는 같고 하는 일이 다르다 — 즉발 폭발이 아니라 **남는 장판**이다.
        // 여기서 재는 것은 「깔렸나 · 죽은 자리에」 둘뿐이고, 모양·지속·틱은 해저드 저작
        // 소유라 이 그물의 축이 아니다.
        [UnityTest]
        public IEnumerator EmberField_LaysAZoneAtTheVictimsCell()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();
            var guardian = FindDefenderCatalog().ById("guardian");
            bridge.SetDefenderPool(new[] { guardian });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
            yield return null;
            Assert.IsTrue(PlaceFirstValid(bridge, guardian), "place guardian");
            bridge.StartBattle();
            BattleBridgeTestAccess.SetField(bridge, "_usingGeneratedWaves", false);
            ((System.Collections.IList)BattleBridgeTestAccess.Field(bridge, "_pending")).Clear();
            yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var defender = FindDefender(bridge, em);
            var card = MakeEmberFieldCard();
            if (card == null) Assert.Ignore("존 해저드 저작이 없다 — 이 그물의 전제가 성립하지 않는다");
            Assert.GreaterOrEqual(bridge.ApplyDreamcatcherCardToUnit(defender, card), 0, "ember attached");

            using var zoneQ = em.CreateEntityQuery(
                ComponentType.ReadOnly<Wassup.Battle.Effects.Hazard>());
            int before = zoneQ.CalculateEntityCount();

            float tile = (bridge.GridToWorldCenterVector(new Vector2Int(1, 0))
                          - bridge.GridToWorldCenterVector(new Vector2Int(0, 0))).magnitude;
            var defPos = em.GetComponentData<LocalTransform>(defender).Position;
            var victim = SpawnBystander(em, bridge, defPos + new float3(tile, 0f, 0f), hp: 1f);
            // ⚠ 죽으면 사라진다 — **지금** 자리를 적어 둔다. 이 그물의 요점이 그 자리다.
            var victimCell = bridge.DebugWorldToCell(
                new Vector3(defPos.x + tile, defPos.y, defPos.z));
            var casterCell = bridge.DebugWorldToCell(new Vector3(defPos.x, defPos.y, defPos.z));
            Assert.IsTrue(!casterCell.Equals(victimCell),
                "전제: 두 자리가 달라야 「누구 자리인가」를 물을 수 있다");
            var beforeZones = new System.Collections.Generic.HashSet<Entity>(
                zoneQ.ToEntityArray(Unity.Collections.Allocator.Temp).ToArray());

            Wassup.Battle.Skills.SkillDispatchSystemBase.ResetExecutedCount();

            float t = 0f;
            while (t < 6f && em.Exists(victim) && em.GetComponentData<Health>(victim).value > 0f)
            { t += Time.deltaTime; yield return null; }
            Assert.IsTrue(!em.Exists(victim) || em.GetComponentData<Health>(victim).value <= 0f,
                "전제: 방어유닛이 적을 처치해야 이 그물이 측정이 된다");

            for (int i = 0; i < 20; i++) yield return null;   // 요청 → 존 생성
            int after = zoneQ.CalculateEntityCount();
            if (em.Exists(victim)) em.DestroyEntity(victim);

            Assert.GreaterOrEqual(
                Wassup.Battle.Skills.SkillDispatchSystemBase.ExecutedCountOf(
                    Wassup.Battle.Skills.SkillSeam.Death), 1,
                "죽음 seam 이 concrete 를 안 거쳤다");
            Assert.Greater(after, before,
                "처치 후 장판이 안 깔렸다 — 「불씨가 안 깔린다」는 육안 추적이 어려운 증상이라 여기서 잡는다");

            // ⚠ **개수만으로는 이 concrete 를 증명 못 한다**(투트랙 리뷰 M-3). 시전자
            // 발밑에 깔려도 개수는 똑같이 는다. 잿불의 요점은 「죽은 자리」라, 새로 생긴
            // 존이 **피해자 칸**을 덮는지를 묻는다.
            // ⚠ **덮는지**가 아니라 **중심이 어디인지**를 묻는다. 장판이 3×3 이면 시전자
            // 발밑에 깔려도 한 칸 옆 피해자 칸을 덮어서, 「덮었나」 단언은 공허해진다.
            // 대칭 장판의 칸 평균 = 중심이라, 그게 곧 「누구 자리인가」다.
            bool sawNewZone = false;
            var centroid = new float2(0f, 0f);
            foreach (var z in zoneQ.ToEntityArray(Unity.Collections.Allocator.Temp))
            {
                if (beforeZones.Contains(z)) continue;
                if (!em.HasBuffer<Wassup.Battle.Effects.HazardCellsBuffer>(z)) continue;
                var cells = em.GetBuffer<Wassup.Battle.Effects.HazardCellsBuffer>(z);
                if (cells.Length == 0) continue;
                var sum = new float2(0f, 0f);
                foreach (var c in cells) sum += new float2(c.cell.x, c.cell.y);
                centroid = sum / cells.Length;
                sawNewZone = true;
            }
            Assert.IsTrue(sawNewZone, "새 장판의 칸 버퍼를 못 읽었다");
            Assert.AreEqual(victimCell.x, Mathf.RoundToInt(centroid.x), 
                $"장판 중심 x 가 피해자 칸이 아니다(중심 {centroid}, 피해자 {victimCell}, 시전자 {casterCell})");
            Assert.AreEqual(victimCell.y, Mathf.RoundToInt(centroid.y),
                $"장판 중심 y 가 피해자 칸이 아니다(중심 {centroid}, 피해자 {victimCell}, 시전자 {casterCell})");
        }

        // 존 해저드 저작 하나를 찾아 잿불 카드를 만든다. 저작이 없으면 null(위에서 Ignore).
        private static DreamcatcherCard MakeEmberFieldCard()
        {
            HazardSO zone = null;
            foreach (var z in Resources.FindObjectsOfTypeAll<HazardSO>())
                if (z != null) { zone = z; break; }
            if (zone == null) return null;

            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.axis = CardTargetAxis.All;
            card.type = CardType.Unit;
            card.effects = new CardEffect[0];
            card.attackMods = new DcAttackModSpec[0];
            card.mechanics = new[] { new DcMechanic {
                trigger = new DcTriggerSpec { kind = DcTriggerKind.OnKill },
                payload = new DcPayloadSpec { kind = DcPayloadKind.SpawnHazard, hazard = zone },
            }};
            return card;
        }

        private static DreamcatcherCard MakeCorpseBurstCard()
        {
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.axis = CardTargetAxis.All;
            card.type = CardType.Unit;
            card.effects = new CardEffect[0];
            card.attackMods = new DcAttackModSpec[0];
            card.mechanics = new[] { new DcMechanic {
                trigger = new DcTriggerSpec { kind = DcTriggerKind.OnKill },
                // 반경 1 이 이 그물의 기하 전제다 — 키우면 시전자 자리에서도 B 가 맞아
                // 단언이 자리를 못 가른다.
                payload = new DcPayloadSpec {
                    kind = DcPayloadKind.SelfTileAoe, magnitude = 50f, tileRange = 1,
                    projectile = FindAnyAoeProjectile(),
                },
            }};
            return card;
        }

        // `SelfTileAoe` 는 ProjectileData 가 없으면 폭발 요청이 통째로 드롭된다(피해까지).
        private static ProjectileData FindAnyAoeProjectile()
        {
            foreach (var p in Resources.FindObjectsOfTypeAll<ProjectileData>())
                if (p != null && p.id == "jjangssen_quake") return p;
            return Resources.FindObjectsOfTypeAll<ProjectileData>()[0];
        }

        // skill-layer-migration unit 3d″ — **작별 선물.** 방어유닛이 쓰러진 자리에서 터진다.
        //
        // ⚠ 이 그물이 없으면 라우팅이 조용히 죽는다 — 슬롯은 붙고, 브리지 로그도 뜨고,
        // 아무 데서도 안 터진다. 여기 seam(파괴 뒤)은 드레인 시점에 **시전자가 이미 없어서**
        // 값 스냅샷이 하나라도 새면 폭발이 월드 원점으로 간다. 그래서 「피해가 들어갔나」가
        // 아니라 **「내가 쓰러진 자리 옆의 적이 맞았나」**를 묻는다.
        [UnityTest]
        public IEnumerator FarewellGift_ExplodesWhereIFell_AfterIAmDestroyed()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();
            var guardian = FindDefenderCatalog().ById("guardian");
            bridge.SetDefenderPool(new[] { guardian });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
            yield return null;
            Assert.IsTrue(PlaceFirstValid(bridge, guardian), "place guardian");
            bridge.StartBattle();
            BattleBridgeTestAccess.SetField(bridge, "_usingGeneratedWaves", false);
            ((System.Collections.IList)BattleBridgeTestAccess.Field(bridge, "_pending")).Clear();
            yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var defender = FindDefender(bridge, em);
            Assert.AreNotEqual(Entity.Null, defender, "defender resolved");
            var giftCard = MakeFarewellGiftCard();
            if (giftCard == null)
                Assert.Ignore("AOE 뷰 저작이 없다 — 이 그물의 전제가 성립하지 않는다");
            Assert.GreaterOrEqual(
                bridge.ApplyDreamcatcherCardToUnit(defender, giftCard), 0, "작별 선물 부착");

            var defPos = em.GetComponentData<LocalTransform>(defender).Position;
            float tile = (bridge.GridToWorldCenterVector(new Vector2Int(1, 0))
                          - bridge.GridToWorldCenterVector(new Vector2Int(0, 0))).magnitude;
            // 한 칸 옆(반경 2 안) 과 아주 멀리. 먼 쪽이 안 맞아야 「어디서」가 증명된다.
            var near = SpawnBystander(em, bridge, defPos + new float3(tile, 0f, 0f), hp: 9999f);
            var far = SpawnBystander(em, bridge, defPos + new float3(tile * 30f, 0f, 0f), hp: 9999f);
            yield return null;
            float nearBefore = em.GetComponentData<Health>(near).value;
            float farBefore = em.GetComponentData<Health>(far).value;

            // 경계 ①: bake 가 이 슬롯을 스킬 레이어로 보냈나(라우팅 키).
            int routedId = int.MinValue;
            foreach (var sl in em.GetBuffer<Wassup.Battle.Combat.DcTriggerSlot>(defender))
                if (sl.trigger == DcTriggerKind.OnDeath) routedId = sl.skillId;
            Assert.AreEqual(Wassup.Skills.Concrete.DeathSiteBlastSkill.Id, routedId,
                "bake 가 OnDeath×SelfTileAoe 를 스킬 레이어로 안 보냈다");

            Wassup.Battle.Skills.SkillDispatchSystemBase.ResetExecutedCount();

            // 방어유닛을 쓰러뜨린다. 파괴는 UnitLifecycleSystem 이 하고, seam 은 그 뒤다.
            em.SetComponentData(defender, new Health { value = 0f, max = 100f });
            for (int i = 0; i < 30; i++) yield return null;
            Assert.IsFalse(em.Exists(defender), "전제: 방어유닛이 파괴돼야 이 seam 의 조건이 성립한다");

            float nearAfter = em.Exists(near) ? em.GetComponentData<Health>(near).value : 0f;
            float farAfter = em.Exists(far) ? em.GetComponentData<Health>(far).value : farBefore;
            if (em.Exists(near)) em.DestroyEntity(near);
            if (em.Exists(far)) em.DestroyEntity(far);

            Assert.GreaterOrEqual(
                Wassup.Battle.Skills.SkillDispatchSystemBase.ExecutedCountOf(
                    Wassup.Battle.Skills.SkillSeam.Lifecycle), 1,
                "자기 죽음 seam 이 concrete 를 안 거쳤다 — 라우팅이 조용히 죽었다");
            Assert.Less(nearAfter, nearBefore,
                "쓰러진 자리 옆의 적이 안 맞았다 — 작별 선물이 안 터졌거나 엉뚱한 자리에서 터졌다");
            Assert.AreEqual(farBefore, farAfter, 0.01f,
                "30칸 밖의 적이 맞았다 — 폭발이 월드 원점 같은 엉뚱한 자리로 갔다는 뜻이다");
        }

        // ⚠ `SelfTileAoe` bake 는 **AOE 뷰(ProjectileData)를 요구한다** — 없으면 조용히
        // 건너뛰고 부착이 -1 이 된다. 저작이 없는 환경에서는 이 그물의 전제가 없다.
        private static DreamcatcherCard MakeFarewellGiftCard()
        {
            var vfx = FindAoeProjectileData();
            if (vfx == null) return null;
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.axis = CardTargetAxis.All;
            card.type = CardType.Unit;
            card.effects = new CardEffect[0];
            card.attackMods = new DcAttackModSpec[0];
            card.mechanics = new[] { new DcMechanic {
                trigger = new DcTriggerSpec { kind = DcTriggerKind.OnDeath },
                payload = new DcPayloadSpec
                {
                    kind = DcPayloadKind.SelfTileAoe, magnitude = 400f, tileRange = 2,
                    projectile = vfx,
                },
            }};
            return card;
        }

        private static ProjectileData FindAoeProjectileData()
        {
            foreach (var pd in Resources.FindObjectsOfTypeAll<ProjectileData>())
                if (pd != null && pd.name.Length > 0) return pd;
            return null;
        }

        private static Entity SpawnBystander(EntityManager em, BattleBridge bridge, float3 pos, float hp)
        {
            var e = em.CreateEntity();
            em.AddComponentData(e, LocalTransform.FromPosition(pos));
            em.AddComponentData(e, new Health { value = hp, max = hp });
            em.AddComponentData(e, new FactionTag { value = Faction.EnemyUnit });
            em.AddBuffer<IncomingDamage>(e);
            em.AddBuffer<CcEffect>(e);
            em.AddComponent<AttackUnitTag>(e);
            em.AddComponentData(e, new Wassup.Battle.Movement.PathFollowState
            {
                speed = 0f, traversalLayers = (byte)PlacementLayer.Path,
            });
            BattleBridgeTestAccess.AttachSimEntityId(bridge, e);
            return e;
        }

        private static DreamcatcherCard MakeDevouringCard()
        {
            var card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            card.axis = CardTargetAxis.All;
            card.type = CardType.Unit;
            card.effects = new CardEffect[0];
            card.attackMods = new DcAttackModSpec[0];
            card.mechanics = new[] { new DcMechanic {
                trigger = new DcTriggerSpec { kind = DcTriggerKind.OnKill },
                payload = new DcPayloadSpec { kind = DcPayloadKind.SelfStatBuff, buffStat = CardBuffKind.AttackSpeed, magnitude = 8f, duration = 4f },
            }};
            return card;
        }

        private static IEnumerator RunSeconds(float seconds)
        {
            float t = 0f;
            while (t < seconds) { t += Time.deltaTime; yield return null; }
        }

        private static DefenderCatalog FindDefenderCatalog()
        {
            var all = Resources.FindObjectsOfTypeAll<DefenderCatalog>();
            return all.Length > 0 ? all[0] : null;
        }

        private static bool PlaceFirstValid(BattleBridge bridge, DefenderUnitData u)
        {
            for (int x = -24; x < 48; x++)
                for (int y = -24; y < 48; y++)
                    if (bridge.CanPlaceDefenderAt(x, y, u, out _))
                        return bridge.PlaceDefenderAs(x, y, u);
            return false;
        }

        private static Entity FindDefender(BattleBridge bridge, EntityManager em)
        {
            var f = typeof(BattleBridge).GetField("_defenderByTile", BindingFlags.NonPublic | BindingFlags.Instance);
            var dict = (System.Collections.IDictionary)f.GetValue(bridge);
            foreach (System.Collections.DictionaryEntry de in dict)
            {
                var val = de.Value;
                var entity = (Entity)val.GetType().GetField("Item1").GetValue(val);
                if (em.Exists(entity)) return entity;
            }
            return Entity.Null;
        }
    }
}
