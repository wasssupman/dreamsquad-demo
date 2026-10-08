// salvaged from Assets/_Project/Scripts/Battle/Combat/{FrontmostTargeting, LowestHealthTargeting,
//   NearestTargeting}.cs (battle-core-rebuild unit 3)
// 이식 시 바뀐 것: `NativeArray` → 배열 + count(계약 4) · `Unity.Burst` 제거 · 세 형제를 한 파일로
//   모았다. 형제인 것이 계약이기 때문이다 — 셋 다 **랭킹만** 하고 후보 필터는 호출부가 한다.
//   `NearestTargeting` 만 반경 필터를 안에 들고 있던 예외도 그대로 옮겼다(그 0 의 뜻이 호출처마다
//   갈리면 조용히 엉뚱한 대상이 뽑힌다는 이유가 여전히 유효하다).

namespace Somnia.Battle.BattleCore.Combat
{
    // 「골에 가장 가까운 적을 먼저 친다」.
    //
    // 순위(전부 결정론):
    //   1. `FlowDist` 오름차순 — 골까지 남은 BFS 비용이 작을수록 «더 앞»
    //   2. `SqDist` 오름차순 — 공격자→후보 XZ 제곱 거리
    //   3. `SimId` 오름차순 — 전순서 tie-break(스폰 순서, 판 안정)
    //
    // ⚠ **골을 지난 적도 후보다**(C14). 그 표시는 「유출 대기」가 아니라 「골에 붙어 타워를
    // 때리는 중」이고, 경로상 가장 앞선 적이라 최전방 정의에 정확히 부합한다.
    public static class FrontmostTargeting
    {
        /// <summary>흐름장의 «못 간다» 센티널. 이 후보는 순위에 들지 않는다.</summary>
        public const int UnreachableDist = int.MaxValue;

        public struct Candidate
        {
            public int FlowDist;
            public float SqDist;
            public int SimId;
        }

        public static bool RanksBefore(in Candidate a, in Candidate b)
        {
            if (a.FlowDist != b.FlowDist) return a.FlowDist < b.FlowDist;
            if (a.SqDist != b.SqDist) return a.SqDist < b.SqDist;
            return a.SimId < b.SimId;
        }

        /// <summary>도달 가능한 최전방 후보의 인덱스. 없으면 -1.</summary>
        public static int SelectFrontmost(Candidate[] cands, int count)
        {
            int best = -1;
            for (int i = 0; i < count; i++)
            {
                if (cands[i].FlowDist == UnreachableDist) continue;
                if (best < 0 || RanksBefore(in cands[i], in cands[best])) best = i;
            }
            return best;
        }
    }

    // 「가장 다친 아군」. 힐러의 정체성이라 지속 락 제외 4종 중 하나다 —
    // 락이 걸리면 재랭킹이 죽고 힐러가 이미 다 찬 아군을 계속 본다.
    public static class LowestHealthTargeting
    {
        public struct Candidate
        {
            public float HpRatio;
            public float SqDist;
            public int SimId;
        }

        public static bool RanksBefore(in Candidate a, in Candidate b)
        {
            if (a.HpRatio != b.HpRatio) return a.HpRatio < b.HpRatio;
            if (a.SqDist != b.SqDist) return a.SqDist < b.SqDist;
            return a.SimId < b.SimId;
        }

        public static int SelectLowest(Candidate[] cands, int count)
        {
            int best = -1;
            for (int i = 0; i < count; i++)
                if (best < 0 || RanksBefore(in cands[i], in cands[best])) best = i;
            return best;
        }
    }

    // 반경 안 **최근접**. 폭탄맨의 착지 칸 선정이 쓰는 자다.
    //
    // ⚠ **사각 자(체비셰프)가 남은 유일한 전투 판정이다**(C20 — 현행 그대로 보류).
    // 이름은 폴백인데 그 아키타입의 **유일 경로**다(폭탄맨은 RESOLVE 를 안 탄다).
    // 2026-09-06 사용자 지시로 보류된 상태이고, 몸도 양쪽 0 이다. 플레이 후 재결정한다.
    // 여기를 원 자로 바꾸면 폭탄 착지 칸이 조용히 바뀐다 — 그것은 밸런스 변경이다.
    public static class NearestTargeting
    {
        public struct Candidate
        {
            /// <summary>호출부의 진영·상태 필터 통과 여부.</summary>
            public bool Eligible;
            /// <summary>체비셰프 타일 거리(반경 판정용).</summary>
            public int TileDist;
            /// <summary>XZ 제곱 거리(랭킹용).</summary>
            public float SqDist;
            public int SimId;
        }

        /// <summary>최근접 우선, 동거리는 낮은 `SimId`(= 먼저 스폰된 쪽).</summary>
        public static bool RanksBefore(in Candidate a, in Candidate b)
        {
            if (a.SqDist != b.SqDist) return a.SqDist < b.SqDist;
            return a.SimId < b.SimId;
        }

        /// <summary>
        /// 반경 안에서 가장 앞선 후보의 인덱스. 없으면 -1.
        /// 형제들과 달리 반경 필터를 **안에** 둔다 — `tileRange &lt;= 0 = 선정 없음` 이 이 함수의
        /// 계약이고, 그 해석이 호출처마다 갈리면 안 되기 때문이다.
        /// </summary>
        public static int SelectNearest(Candidate[] cands, int count, int tileRange)
        {
            if (tileRange <= 0) return -1;
            int best = -1;
            for (int i = 0; i < count; i++)
            {
                if (!cands[i].Eligible) continue;
                if (cands[i].TileDist > tileRange) continue;
                if (best < 0 || RanksBefore(in cands[i], in cands[best])) best = i;
            }
            return best;
        }
    }
}
