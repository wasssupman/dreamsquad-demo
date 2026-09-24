using Unity.Mathematics;
using Wassup.Battle.Units;
using Wassup.BattleCore.Combat;
using Wassup.BattleCore.Map;

namespace Wassup.BattleCore.Move
{
    // **이동의 정지 조건**. 「멈춰도 되나 = 지금 쏠 수 있나」를 묻는다.
    //
    // 공격 루프(획득·유지·다중타격)는 unit 3 이다. 여기 있는 것은 그 술어의 **이동 쪽 소비**
    // 하나뿐이고, `AttackReach` 라는 **같은 진입점**을 지난다 — 그게 제약 13 의 이행 방식이다.
    //
    // ⚠ **인라인 재작성 금지.** 이동을 멈추는 근거가 사격 가능 여부인 이상, 이동·공격·감지가
    // 같은 답을 받아야 한다. 한 곳만 조였다가 「격자는 도착이라 하고 공격은 멀다고 하는」
    // 182프레임 교착이 실제로 났다.
    public static class ReachProbe
    {
        /// <summary>지금 때릴 수 있는 상대가 있나(= 「멈춰도 되나」). 후보는 id 오름차순으로 본다.</summary>
        public static bool HasFireTarget(BattleWorld world, Unit self, in EnemyDef def, float tileSize)
        {
            if (def.AttackRange <= 0f) return false;
            int mask = TargetDefaults.ResolveEnemy(def.TargetFactions);
            // ⚠ **공격의 대상 층**(`Attack.TargetLayers`)이지 이 적의 통행 층이 아니다. 공격 루프가
            // 같은 값을 쓴다 — 갈리면 「때릴 수는 있는데 멈추지 않는」 교착이다. 적은 0(무필터)이
            // 저작이다(옛 `targetTraversalLayers`) — 통행 층을 읽으면 비행 적이 지상을 걷는 순찰병을
            // 놓친다(2026-09-24 드리프트 감사 M7).
            byte targetLayers = (byte)def.Attack.TargetLayers;

            var units = world.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (u.Id == self.Id || !u.IsTargetable()) continue;
                if (((int)u.Faction & mask) == 0) continue;
                // 대상 층 × 이동체의 통행 층 교집합(0 = 무필터).
                byte theirLayers = u.Move != null ? u.Move.TraversalLayers : (byte)0;
                if (!LayerBits.CanTarget(targetLayers, theirLayers)) continue;
                if (AttackReach.InReach(self.Position, u.Position, def.AttackRange, tileSize,
                                        self.HitRadius, u.HitRadius))
                    return true;
            }
            return false;
        }

        /// <summary>끌려간 가디언이 사거리 안인가(= 대치 상태로 들어가도 되나).</summary>
        public static bool GuardianInRange(BattleWorld world, Unit self, in EnemyDef def, float tileSize)
        {
            if (self.Aggro == null || self.Aggro.Target.IsNone) return false;
            var g = world.Find(self.Aggro.Target);
            if (g == null || !g.IsTargetable()) return false;
            return AttackReach.InReach(self.Position, g.Position, def.AttackRange, tileSize,
                                       self.HitRadius, g.HitRadius);
        }

        /// <summary>감지 후보인가 — 진영·통행층 필터. 반경 판정은 호출부가 따로 묻는다(무제한은 건너뛴다).</summary>
        public static bool IsLegalDetectionTarget(Unit self, Unit candidate, in EnemyDef def)
        {
            if (candidate.Id == self.Id || !candidate.IsTargetable()) return false;
            if (((int)candidate.Faction & TargetDefaults.ResolveEnemy(def.TargetFactions)) == 0) return false;
            byte theirLayers = candidate.Move != null ? candidate.Move.TraversalLayers : (byte)0;
            // 공격과 같은 대상 층(위 `HasFireTarget` 주석). 옛 `DetectionSystem` 도 `atk.targetTraversalLayers`.
            return LayerBits.CanTarget((byte)def.Attack.TargetLayers, theirLayers);
        }

        /// <summary>동거리 동률은 **낮은 id** 가 이긴다 — 순회 순서에 기대지 않는 결정론.</summary>
        public static bool RanksBefore(float sqDist, int simId, float bestSq, int bestId)
        {
            if (sqDist < bestSq - 1e-6f) return true;
            if (sqDist > bestSq + 1e-6f) return false;
            return simId < bestId;
        }
    }
}
