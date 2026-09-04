using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Wassup.Battle.Units;

namespace Wassup.Tests.EditMode
{
    // battle-structures unit 4(리뷰 M-d) — 거점 아키타입의 **공용 픽스처 빌더**.
    //
    // 왜 필요한가: 최후순위 계약이 라이브에서 발효되지 않았던 원죄가 «테스트가 손으로 맞춘
    // 골 사본» 과 «브리지가 실제로 만드는 골» 의 아키타입 drift 였다. 손 사본은 브리지가
    // 바뀔 때 따라갈 강제가 없다. 모든 테스트가 이 빌더를 쓰고,
    // GoalTowerArchetypeTests 가 «브리지 산물의 컴포넌트 집합 == 빌더 산물» 을 단정해
    // drift 를 구조로 잡는다 — 빌더가 낡으면 그 테스트가 깨진다.
    public static class StructureFixtures
    {
        // battle-sim-extraction M0 unit 1 — 테스트 월드에도 «먼저 만든 쪽이 작은 ID» 라는
        // 라이브 불변식을 준다. 값 자체엔 의미가 없고 **상대 순서**만 의미가 있으므로
        // 프로세스 전역 단조 증가로 충분하다(테스트 간 리셋 불요 — 유일성이 그대로 유지된다).
        // 라이브 발급기는 `BattleBridge.AttachSimEntityId` 하나뿐이고 여기가 그 테스트 짝이다.
        private static int _nextSimId;

        public static SimEntityId NextSimEntityId() => new SimEntityId { value = _nextSimId++ };

        // 방어 마음(= 라이브 골 타워). SpawnStructureEntities 의 goals[] 분기와 같은 구성.
        public static Entity MakeGoalTower(EntityManager em, float3 pos, float hp = 1000f)
        {
            var e = em.CreateEntity();
            em.AddComponentData(e, NextSimEntityId());
            em.AddComponent<GoalTowerTag>(e);
            em.AddComponentData(e, new StructureTag
            {
                cell = new int2((int)pos.x, (int)pos.z),
                faction = Faction.DefenderCore,
            });
            em.AddComponentData(e, new Health { value = hp, max = hp });
            em.AddBuffer<IncomingDamage>(e);
            // distance-based-range unit 20 리뷰 M-2 — 골 타워도 몸을 갖는다(마음 1×1 → 0.5).
            // 브리지 스폰과 **같은 함수**에서 값을 읽는다 — 픽스처가 숫자를 손으로 적으면
            // 산식이 바뀌는 날 프로덕션과 조용히 갈린다.
            em.AddComponentData(e, new HitRadius
            { value = Wassup.Data.StructurePlacements.BodyRadiusOf(Faction.DefenderCore) });
            // heart-stress-axis unit 2 — 브리지 스폰과 대칭(GoalTowerArchetypeTests 가 강제한다).
            em.AddBuffer<IncomingHeal>(e);
            em.AddComponentData(e, new FactionTag { value = Faction.DefenderCore });
            em.AddComponentData(e, LocalTransform.FromPosition(pos));
            return e;
        }

        // 본능(3×3 통행 차단 포함). SpawnStructureEntities 의 doc 분기와 같은 구성.
        public static Entity MakeInstinct(EntityManager em, float3 pos, Faction faction, float hp = 500f)
        {
            var cell = new int2((int)pos.x, (int)pos.z);
            var e = em.CreateEntity();
            em.AddComponentData(e, NextSimEntityId());
            em.AddComponentData(e, new StructureTag { cell = cell, faction = faction });
            em.AddComponentData(e, new Health { value = hp, max = hp });
            em.AddBuffer<IncomingDamage>(e);
            // unit 20 리뷰 M-2 — 거점 몸(본능 3×3 → 1.5). 브리지가 붙이는데 픽스처가 빠뜨리면
            // 「점 몸 본능」이라는 **프로덕션에 없는 상태**를 테스트하게 된다(골 타워와 같은 사유).
            em.AddComponentData(e, new HitRadius
            { value = Wassup.Data.StructurePlacements.BodyRadiusOf(faction) });
            em.AddComponentData(e, new FactionTag { value = faction });
            em.AddComponentData(e, LocalTransform.FromPosition(pos));
            var cells = em.AddBuffer<Wassup.Battle.Effects.OccupiedCellsBuffer>(e);
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    cells.Add(new Wassup.Battle.Effects.OccupiedCellsBuffer
                    {
                        cell = new int2(cell.x + dx, cell.y + dy),
                    });
            return e;
        }
    }
}
