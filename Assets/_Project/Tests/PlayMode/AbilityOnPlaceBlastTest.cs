using System.Collections;
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
using Wassup.Battle.Movement;

namespace Wassup.Tests.PlayMode
{
    // skill-layer-foundation unit 1 — EmitProjectilePattern arm 특성화
    // (샷건맨 배치 스킬 = OnPlace × 방향 부채꼴 5연발).
    //
    // 이 arm 의 계약(BossPeriodicTriggerSystem + OnPlaceFireAim):
    //  · 배치 순간 payload.tileRange 안 **최근접 적**을 향해 조준을 확정하고
    //    (샷건맨은 DeployedFacing 이 없어 «조준 없음 → 최근접» 폴백을 탄다),
    //  · 패턴이 저작한 부채꼴(min~maxAngleDeg, shots)로 pellets 를 쏜다.
    //  · 사거리 = payload.tileRange × tileSize. 피해 = **패턴 SO 의 damage**
    //    (평타 볼리와 달리 유닛 output 으로 덮지 않는다 — arm 주석 "damage 는 채우지 않는다").
    //  · 후보가 하나도 없으면 **발사하지 않는다**(방향 (0,0) 탄 금지).
    //
    // ⚠ 단언은 «pellets 가 실제로 적을 때렸나»다. EmitterInstance 가 생겼다/탄이 떴다로
    // 끝내면 조준·사거리·부채꼴이 죽어도 초록이 난다. 방향성은 뒤/옆 적의 무피해로,
    // 사거리는 축 위 원거리 적의 무피해로 잰다 — 서로가 서로를 가려주지 못하는 4개 축이다.
    public class AbilityOnPlaceBlastTest
    {
        private const float Hp = 100000f;

        private int _savedMap;
        [SetUp]
        public void PinMap() => _savedMap = BattleBridgeTestAccess.PinMap();

        [TearDown]
        public void TearDown()
        {
            LogAssert.ignoreFailingMessages = false;
            BattleBridgeTestAccess.RestoreMap(_savedMap);
        }

        [UnityTest]
        public IEnumerator Blast_HitsTowardNearest_InAuthoredFanAndRange_Only()
        {
            yield return LoadBattle();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var blaster = MakeBlaster("test_blast_fan");
            var pattern = blaster.GetAbility<UnitSkillAbility>().mechanics[0].payload.pattern;
            float pellet = pattern.damage;           // 저작 하드코딩 금지 — SO 가 권위
            int aimRange = blaster.GetAbility<UnitSkillAbility>().mechanics[0].payload.tileRange;
            Assert.Greater(pellet, 0f, "블라스트 피해가 저작돼 있어야 한다");
            Assert.Greater(aimRange, 0, "사거리(payload.tileRange)가 저작돼 있어야 한다");
            Assert.Greater(pattern.shots.Length, 1, "부채꼴 연발 사양 — shot 이 여러 발이어야 한다");

            Prepare(bridge, gm, blaster);
            var cell = FindCellWithMargin(bridge, em, blaster, up: aimRange + 2, down: 3, side: 3);
            float tile = TileSize(bridge);

            // 기하 전제 — 뒤(3타일)·옆(3타일)·축상 사거리 밖(+2타일)의 이격이 pellets 의
            // 스침 반경(hitThreshold)을 넉넉히 넘어야 아래 무피해 단언이 «측정»이 된다.
            float thr = pattern.barrel.hitThreshold;
            Assert.Greater(3f * tile, thr + 0.5f, "전제: 3타일 이격 > 스침 반경 — 깨지면 배치 거리 재계산");
            Assert.Greater(2f * tile, thr + 0.5f, "전제: 사거리 끝→원거리 적 2타일 > 스침 반경");

            // 조준 앵커(최근접, 북쪽 1칸)가 축을 정한다. pellets 는 pierce 저작이라
            // 앞의 적이 뒤 판정을 가리지 않는다.
            var anchor = SpawnDummy(em, bridge, new Vector2Int(cell.x, cell.y + 1));
            var behind = SpawnDummy(em, bridge, new Vector2Int(cell.x, cell.y - 3));
            var side = SpawnDummy(em, bridge, new Vector2Int(cell.x + 3, cell.y));
            var farOnAxis = SpawnDummy(em, bridge, new Vector2Int(cell.x, cell.y + aimRange + 2));

            Assert.IsTrue(bridge.PlaceDefenderAs(cell.x, cell.y, blaster), "배치");

            // OnPlace 1회 발사 — 반복이 없으므로 pellets 가 사거리 끝까지 갈 시간만 준다.
            yield return Seconds(2f);

            float dAnchor = Hp - em.GetComponentData<Health>(anchor).value;
            float dBehind = Hp - em.GetComponentData<Health>(behind).value;
            float dSide = Hp - em.GetComponentData<Health>(side).value;
            float dFar = Hp - em.GetComponentData<Health>(farOnAxis).value;
            foreach (var e in new[] { anchor, behind, side, farOnAxis }) em.DestroyEntity(e);
            Object.Destroy(blaster);

            Assert.Greater(dAnchor, 0f,
                "조준 앵커(최근접)가 안 맞았다 — «조준 없음 → 최근접» 폴백이나 발사 자체가 죽었다");
            // 피해는 pellet 의 정수배여야 한다 — 다른 피해원이 섞이면(웨이브·구조물 소거 실패)
            // 여기가 깨져서 오염을 드러낸다.
            float k = dAnchor / pellet;
            Assert.AreEqual(Mathf.Round(k), k, 0.001f,
                $"앵커 피해({dAnchor})가 pellet({pellet})의 정수배가 아니다 — 다른 피해원이 섞였다");
            Assert.AreEqual(0f, dBehind, 0.001f,
                "등 뒤 적이 맞았다 — 부채꼴이 방향을 잃었다(전방 반각 저작이 죽었다)");
            Assert.AreEqual(0f, dSide, 0.001f,
                "측면 90° 적이 맞았다 — 부채꼴 반각이 저작(min~maxAngleDeg)을 넘었다");
            Assert.AreEqual(0f, dFar, 0.001f,
                $"사거리({aimRange}타일) 밖 축상 적이 맞았다 — maxDistance = tileRange × tileSize 계약이 죽었다");
        }

        // 후보 0 → 발사 0. arm 은 «조준도 합법 후보도 없으면 발사하지 않는다»(방향 (0,0)
        // 탄 금지)를 계약으로 갖는다. SkyStrike 의 동종 테스트는 Entity 조준 축이고,
        // 이쪽은 Direction 조준(OnPlaceFireAim) 축이라 코드 경로가 다르다.
        [UnityTest]
        public IEnumerator NoCandidateInAimRange_FiresNothing()
        {
            yield return LoadBattle();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var blaster = MakeBlaster("test_blast_nocand");
            int aimRange = blaster.GetAbility<UnitSkillAbility>().mechanics[0].payload.tileRange;
            Prepare(bridge, gm, blaster);
            var cell = FindCellWithMargin(bridge, em, blaster, up: aimRange + 2, down: 0, side: 0);

            // 조준 풀(반경 aimRange) 밖 — 유일한 적이 후보가 못 된다.
            var far = SpawnDummy(em, bridge, new Vector2Int(cell.x, cell.y + aimRange + 2));

            Assert.IsTrue(bridge.PlaceDefenderAs(cell.x, cell.y, blaster), "배치");
            yield return Seconds(2f);

            float d = Hp - em.GetComponentData<Health>(far).value;
            em.DestroyEntity(far);
            Object.Destroy(blaster);

            Assert.AreEqual(0f, d, 0.001f,
                "조준 후보가 없는데 적이 맞았다 — «후보 0 = 무발사» 대신 엉뚱한 방향으로 쐈다");
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static float TileSize(BattleBridge bridge)
            => (bridge.GridToWorldCenterVector(new Vector2Int(1, 0))
                - bridge.GridToWorldCenterVector(new Vector2Int(0, 0))).magnitude;

        private static IEnumerator LoadBattle()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;
        }

        private static IEnumerator Seconds(float sec)
        {
            float t = 0f;
            while (t < sec) { t += Time.deltaTime; yield return null; }
        }

        private static DefenderUnitData MakeBlaster(string testId)
        {
            var catalog = Resources.FindObjectsOfTypeAll<DefenderCatalog>()[0];
            var unit = Object.Instantiate(catalog.ById("shotgunner"));
            unit.id = testId;
            unit.attackRange = 0f;   // 평타 볼리가 섞이면 배치 블라스트분을 분리 측정할 수 없다
            unit.cost = 0;
            unit.maxOnBoard = 100;
            var skill = unit.GetAbility<UnitSkillAbility>();
            Assert.IsNotNull(skill, "샷건맨에 UnitSkillAbility(배치 블라스트)가 배선돼야 한다");
            Assert.AreEqual(DcTriggerKind.OnPlace, skill.mechanics[0].trigger.kind, "트리거 = 배치");
            Assert.AreEqual(DcPayloadKind.EmitProjectilePattern, skill.mechanics[0].payload.kind,
                "페이로드 = 발사 명세 트리거");
            Assert.IsNotNull(skill.mechanics[0].payload.pattern, "payload.pattern 미배선");
            Assert.IsNotNull(skill.mechanics[0].payload.pattern.barrel, "pattern.barrel 미배선");
            return unit;
        }

        // 이 파일의 단언은 «블라스트가 얼마나/어디를 때렸나»다. 그러려면 판 위의 피해원과
        // 적이 그것뿐이어야 한다:
        //  · 구조물(본능)의 AttackState 를 소거한다 — SkyStrike 와 같은 이유(조용한 오염).
        //  · **웨이브 스폰을 원천 차단한다** — SkyStrike 는 임자(target) 태그로 남의 탄을
        //    구분했지만, 방향 pellets 에는 임자가 없고 조준(최근접)마저 웨이브 적이 훔칠 수
        //    있다. 스폰을 막는 것이 유일하게 정확한 격리다.
        private static void Prepare(BattleBridge bridge, GameManager gm, DefenderUnitData unit)
        {
            bridge.SetDefenderPool(new[] { unit });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
            bridge.StartBattle();   // 투사체 드레인은 _running 아래다(SkyStrike 계측 주석)
            MuteWaves(bridge);
            SilenceOtherAttackers();
        }

        private static void MuteWaves(BattleBridge bridge)
        {
            BattleBridgeTestAccess.SetField(bridge, "_usingGeneratedWaves", false);
            ((System.Collections.IList)BattleBridgeTestAccess.Field(bridge, "_pending")).Clear();
        }

        // 배치 전이라 지금 AttackState 를 가진 것은 구조물뿐이다(SkyStrike 선례).
        private static void SilenceOtherAttackers()
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            using (var attackers = em.CreateEntityQuery(
                       ComponentType.ReadOnly<Wassup.Battle.Combat.AttackState>()))
            {
                if (!attackers.IsEmpty)
                    em.RemoveComponent<Wassup.Battle.Combat.AttackState>(attackers);
            }
        }

        private static Entity SpawnDummy(EntityManager em, BattleBridge bridge, Vector2Int cell)
        {
            var w = bridge.GridToWorldCenterVector(cell);
            var e = em.CreateEntity();
            em.AddComponentData(e, LocalTransform.FromPosition(new float3(w.x, w.y, w.z)));
            em.AddComponentData(e, new Health { value = Hp, max = Hp });
            em.AddComponentData(e, new FactionTag { value = Faction.EnemyUnit });
            em.AddBuffer<IncomingDamage>(e);
            em.AddBuffer<CcEffect>(e);
            em.AddComponent<AttackUnitTag>(e);
            return e;
        }

        // 위(+y)로 up, 아래로 down, 옆(+x)으로 side 만큼의 셀이 전부 그리드 안인 배치 칸.
        // 더미가 그리드 밖에 서면 후보 셀 계산(WorldToCell)이 왜곡될 수 있어 안에 가둔다.
        private static Vector2Int FindCellWithMargin(
            BattleBridge bridge, EntityManager em, DefenderUnitData u, int up, int down, int side)
        {
            using var q = em.CreateEntityQuery(ComponentType.ReadOnly<FlowFieldSingleton>());
            Assert.AreEqual(1, q.CalculateEntityCount(), "flow field 싱글턴");
            var ff = q.GetSingleton<FlowFieldSingleton>();

            for (int x = 0; x < ff.gridSize.x; x++)
                for (int y = 0; y < ff.gridSize.y; y++)
                {
                    if (x + side >= ff.gridSize.x || y + up >= ff.gridSize.y || y - down < 0) continue;
                    if (bridge.CanPlaceDefenderAt(x, y, u, out _)) return new Vector2Int(x, y);
                }
            Assert.Fail("여유 공간을 가진 배치 칸이 없다");
            return default;
        }
    }
}
