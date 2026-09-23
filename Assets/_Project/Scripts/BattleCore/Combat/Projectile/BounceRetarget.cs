// salvaged from Assets/_Project/Scripts/Battle/Combat/Projectile/BounceRetarget.cs
//   (battle-core-rebuild unit 3)
// 이식 시 바뀐 것: `NativeArray` 오버로드 3개 → **배열 인자 하나**. 옛 오버로드 사슬(층 → 진영 →
//   몸)은 기존 producer 를 안 깨려고 층층이 쌓인 것이라 옮길 이유가 없다 — 새 코어에는
//   producer 가 하나뿐이고 그 하나가 셋을 전부 넘긴다. 은퇴한 `gridSize`/`origin` 인자도 뺐다
//   (unit 18 에서 위치 기반이 된 뒤로 읽히지 않았다).
using Unity.Mathematics;

namespace Wassup.BattleCore.Combat.Projectile
{
    // 튕기는 탄의 **재조준 결정**: 방금 때린 대상을 빼고 착탄 지점이 닿는 가장 가까운 생존자.
    //
    // 순수 기하다 — 판도 틱도 모른다. 호출부가 후보 배열(위치·층·진영·몸)을 만들어 넘긴다.
    public struct BounceCandidate
    {
        public float3 Pos;
        /// <summary>이 후보의 통행 층. 0 = 무필터.</summary>
        public byte TraversalLayers;
        /// <summary>이 후보의 진영 비트.</summary>
        public int Faction;
        /// <summary>이 후보의 몸(타일). 큰 몸은 더 멀리서 걸린다 — 다른 자들과 같은 규칙.</summary>
        public float BodyRadius;
    }

    public static class BounceRetarget
    {
        /// <summary>
        /// 다음 튕김 대상의 인덱스. 없으면 -1.
        /// `excludeIndex`(직전 대상) 제외 · 층·진영 게이트 · `hitPos` 에서 `tileRange` 안
        /// (**자리형** 도달 — 착탄점은 누군가의 몸이 아니다) · XZ 제곱 거리 최소.
        /// 동률은 낮은 인덱스(스냅샷 순서 = 결정론). `tileRange &lt;= 0` → -1.
        ///
        /// ⚠ 원점이 다르다: 카드의 `tileRange` 는 **시전자 기준** 사거리인데 이 값은 **탄의 현재
        /// 위치 기준**으로 재해석된다. 같은 숫자라도 날아간 만큼 실효 도달이 늘어난다.
        /// </summary>
        public static int FindNext(float3 hitPos, int excludeIndex,
                                   BounceCandidate[] cands, int count,
                                   byte attackTargetLayers, int wantedFactionMask,
                                   int tileRange, float tileSize)
        {
            if (tileRange <= 0) return -1;
            float invT = tileSize > 1e-6f ? 1f / tileSize : 1f;
            int best = -1;
            float bestSq = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (i == excludeIndex) continue;
                var c = cands[i];
                if (!Map.LayerBits.CanTarget(attackTargetLayers, c.TraversalLayers)) continue;
                if (wantedFactionMask != 0 && (c.Faction & wantedFactionMask) == 0) continue;
                if (!Wassup.Skills.SkillMath.ReachFromCell(
                        (c.Pos.x - hitPos.x) * invT, (c.Pos.z - hitPos.z) * invT,
                        tileRange, c.BodyRadius)) continue;
                float dx = c.Pos.x - hitPos.x;
                float dz = c.Pos.z - hitPos.z;
                float d2 = dx * dx + dz * dz;
                if (d2 < bestSq)    // strict < → 동률은 낮은 인덱스 승리(결정론)
                {
                    bestSq = d2;
                    best = i;
                }
            }
            return best;
        }
    }
}
