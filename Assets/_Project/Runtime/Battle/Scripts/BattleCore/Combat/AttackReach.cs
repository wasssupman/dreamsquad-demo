// salvaged from Assets/_Project/Scripts/Battle/Combat/AttackReach.cs (battle-core-rebuild unit 2)
// 이식 시 바뀐 것: `AttackShapeBaked` 를 코어 사본으로 가리킨다. 술어 본체는 `Somnia.Battle.Skills.SkillMath`
//   **그대로**다(코어가 이미 참조하는 엔진 무관 어셈블리 — 계약 4). 소비처 목록 주석은 옛 시스템
//   이름 대신 새 코어의 자리로 다시 적었다.
using Unity.Mathematics;

namespace Somnia.Battle.BattleCore.Combat
{
    // 사거리 판정의 **단일 술어**.
    //
    // ── 자 하나, 몸 하나 ──
    //
    // 전에는 두 단계였다 — 칸 체비셰프(1차) + 「양쪽이 연속 이동일 때만」 월드 체비셰프(2차).
    // 그 구조가 만든 문제 셋:
    //   ① 「사거리 안」의 뜻이 **누가 묻느냐에 따라 달랐다.**
    //   ② 몸이 없었다 — 전부 중심점 대 중심점이라 스프라이트가 큰 보스가 몸통을 관통당해도 무판정.
    //   ③ 칸 체비셰프는 **칸 경계에서 튄다.** 반 칸 움직였을 뿐인데 판정이 뒤집힌다.
    //
    // 지금은 `SkillMath.ReachFromUnit` 하나다. `bothContinuous` 인자가 사라진 이유는
    // **그 인자의 존재 자체가 ①이었기** 때문이다.
    //
    // ⚠ **본체가 여기 없다.** `Somnia.Battle.Skills`(엔진 무참조)에 있고 이 파일은 `float3` ↔ 타일 단위
    // 변환만 한다. 코어가 그 어셈블리를 참조하므로 술어는 **이미 저쪽에** 있다.
    //
    // ── 이 unit 에서의 소비처 ──
    //   · 「멈춰도 되나」 — `ReachProbe.HasFireTarget` / `GuardianInRange`(이동의 정지 조건)
    //   · 「붙으러 가야 하나」 — 순찰 이동 보정(`PatrolAreaMath.CloseInDir`)
    //   · 「저 놈을 발견했나 · 이미 문 것을 놓나」 — 감지(`tileRange` 자리에 **감지 반경**을 넣는다.
    //     술어·몸·필터는 같고 **반경만 다르다** — 그게 「탐색 반경이 넓어졌다」의 구현이다)
    //   공격 루프(획득·유지·다중타격)와 시전은 unit 3 이 같은 진입점에 합류한다.
    //
    // ⚠ **인라인 재작성은 리뷰 거절 사유다.** 한 곳만 조였다가 182프레임 교착이 났다:
    // 이동이 「격자상 사격 칸에 도착했으니 멈춰」라고 하고 공격이 「물리적으로 머니 못 쏴」라고
    // 해서 순찰병이 적 옆에 붙어 선 채 아무것도 안 했다. **이동을 멈추는 근거가 사격 가능
    // 여부인 이상, 셋이 같은 답을 받아야 한다.**
    public static class AttackReach
    {
        // **정본 진입점.** 몸 = 원, `d² ≤ (사거리 + selfR + targetR)²` edge-to-edge.
        //
        // ⚠ **`tileSize` 로 나눠 타일 단위로 넘긴다.** 술어가 월드 단위를 모르는 이유는
        // 「사거리 3」이 저작에서 칸 수이기 때문이다 — 월드로 환산하는 지점이 하나여야 한다.
        // ⚠ `tileRange` 가 **실수**인 이유: 유지 판정이 히스테리시스 폭을 더해 부른다.
        // ⚠ `selfBodyRadiusTiles` 에 **기본값을 주지 않는다.** 기본값을 주면 새 호출부가 몸을
        // 안 넘기고도 컴파일되고, 그 순간 「소비처가 전부 같은 답을 받는다」는 계약이 조용히 깨진다.
        public static bool InReach(float3 atkPos, float3 tgtPos, float tileRange, float tileSize,
                                   float selfBodyRadiusTiles, float targetBodyRadiusTiles = 0f)
        {
            float inv = tileSize > 1e-6f ? 1f / tileSize : 1f;
            return Somnia.Battle.Skills.SkillMath.ReachFromUnit(
                (tgtPos.x - atkPos.x) * inv, (tgtPos.z - atkPos.z) * inv,
                tileRange, selfBodyRadiusTiles, targetBodyRadiusTiles);
        }

        // 칸 좌표로 묻는 사거리 — 두 몸이 각자 칸 중앙에 설 때의 답이다.
        //
        // **표기(배치 프리뷰)가 이걸 쓴다.** 프리뷰가 이중 루프로 모양을 다시 그리면 「밝은
        // 칸인데 안 때린다」가 되고, 그게 가장 나쁜 종류의 버그다 — 화면이 규칙을 **틀리게**
        // 가르친다. 위 `InReach` 와 **같은 본체**를 지난다.
        public static bool InCellReach(int2 atkCell, int2 tgtCell, float tileRange,
                                       float selfBodyRadiusTiles, float targetBodyRadiusTiles = 0f)
            => Somnia.Battle.Skills.SkillMath.ReachFromUnit(
                   tgtCell.x - atkCell.x, tgtCell.y - atkCell.y,
                   tileRange, selfBodyRadiusTiles, targetBodyRadiusTiles);

        // «같은 자리» 임계(월드 거리²). 0.01 월드 유닛 = 타일 1개 기준 1% — 셀 판정을 흔들지 않으면서 방향 계산이
        // 의미를 잃는 구간만 잡는다. (옛 `Somnia.Battle.Skills.SkillCone` 에 있던 값 — 소비처가 여기 하나라 옮겼다.)
        private const float SameSpotEpsSq = 1e-4f;

        // **부가 타격 전용 진입점.** 원 항(위와 같은 본체) AND 도형 항.
        // `dirToPrimary` = 공격자 → 주 대상 XZ 벡터(정규화 불필요 — 방향만 쓴다).
        //
        // **획득·유지·정지는 원이다** — 사거리 안이면 반드시 반응한다. 도형(부채꼴·띠)은
        // **부가 타격에만** 곱해진다: 주 대상이 정해진 뒤 «그 대상을 향한 실제 방향»을 축으로
        // 도형을 세우고, 원 안 후보 중 그 안의 것만 같이 때린다.
        //
        // ⚠ `shape` 에 기본값을 주지 않는다 — 소비처가 「내 도형」을 선언한다.
        // ⚠ 방향이 정의되지 않으면(주 대상이 같은 자리) 도형 항은 **통과**한다.
        public static bool InReachShaped(float3 atkPos, float3 tgtPos, float tileRange, float tileSize,
                                         float selfBodyRadiusTiles, float targetBodyRadiusTiles,
                                         in AttackShapeBaked shape, float2 dirToPrimary)
        {
            float inv = tileSize > 1e-6f ? 1f / tileSize : 1f;
            float dx = (tgtPos.x - atkPos.x) * inv, dz = (tgtPos.z - atkPos.z) * inv;
            if (!Somnia.Battle.Skills.SkillMath.ReachFromUnit(dx, dz, tileRange, selfBodyRadiusTiles, targetBodyRadiusTiles))
                return false;
            if (shape.kind == AttackShapeBaked.OmniKind) return true;
            float len2 = math.lengthsq(dirToPrimary);
            if (len2 <= SameSpotEpsSq) return true;
            float2 u = dirToPrimary * math.rsqrt(len2);
            float along = u.x * dx + u.y * dz;        // 주 대상 방향 성분
            float across = u.x * dz - u.y * dx;       // 그 수직 성분(부호는 게이트가 접는다)
            if (shape.kind == AttackShapeBaked.SectorKind)
                return Somnia.Battle.Skills.SkillMath.SectorGate(along, across, shape.sinHalf, shape.cosHalf, targetBodyRadiusTiles);
            return Somnia.Battle.Skills.SkillMath.BandGate(along, across, shape.halfWidth,
                                                    tileRange + selfBodyRadiusTiles, targetBodyRadiusTiles);
        }

        // 격자 계층의 자. **사거리 판정에 쓰지 말 것** — 그 용도의 정본은 위 `InReach` 하나다.
        // 이 함수가 남은 이유는 순찰 이동뿐이다: 추격 필드 소스 수집이 칸 디스크라
        // 「필드가 세운 사격 칸」을 판정하려면 그와 **같은 자**여야 한다.
        public static bool InCellRange(int2 atkCell, int2 tgtCell, int tileRange)
            => math.max(math.abs(tgtCell.x - atkCell.x), math.abs(tgtCell.y - atkCell.y)) <= tileRange;
    }
}
