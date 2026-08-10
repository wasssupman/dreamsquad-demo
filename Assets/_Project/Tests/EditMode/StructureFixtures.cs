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
        // 방어 마음(= 라이브 골 타워). SpawnStructureEntities 의 goals[] 분기와 같은 구성.
        public static Entity MakeGoalTower(EntityManager em, float3 pos, float hp = 1000f)
        {
            var e = em.CreateEntity();
            em.AddComponent<GoalTowerTag>(e);
            em.AddComponentData(e, new StructureTag
            {
                cell = new int2((int)pos.x, (int)pos.z),
                faction = Faction.DefenderCore,
            });
            em.AddComponentData(e, new Health { value = hp, max = hp });
            em.AddBuffer<IncomingDamage>(e);
            em.AddComponentData(e, new FactionTag { value = Faction.DefenderCore });
            em.AddComponentData(e, LocalTransform.FromPosition(pos));
            return e;
        }

        // 본능(3×3 통행 차단 포함). SpawnStructureEntities 의 doc 분기와 같은 구성.
        public static Entity MakeInstinct(EntityManager em, float3 pos, Faction faction, float hp = 500f)
        {
            var cell = new int2((int)pos.x, (int)pos.z);
            var e = em.CreateEntity();
            em.AddComponentData(e, new StructureTag { cell = cell, faction = faction });
            em.AddComponentData(e, new Health { value = hp, max = hp });
            em.AddBuffer<IncomingDamage>(e);
            em.AddComponentData(e, new FactionTag { value = faction });
            em.AddComponentData(e, LocalTransform.FromPosition(pos));
            var cells = em.AddBuffer<Wassup.Battle.Effects.BlockingHazardCellsBuffer>(e);
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    cells.Add(new Wassup.Battle.Effects.BlockingHazardCellsBuffer
                    {
                        cell = new int2(cell.x + dx, cell.y + dy),
                    });
            return e;
        }
    }
}
