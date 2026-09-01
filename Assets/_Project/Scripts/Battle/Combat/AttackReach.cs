using Unity.Mathematics;

namespace Wassup.Battle.Combat
{
    // 사거리 판정의 **단일 술어**. 아키텍처 중립이라 순수 함수로 둔다
    // (제약 10 — 타겟팅은 sim-critical 이라 단위 테스트를 유지한다).
    //
    // ── 자 하나, 몸 하나 (distance-based-range unit 4a) ──
    //
    // 전에는 두 단계였다 — 셀 체비셰프(1차) + 「양쪽이 연속 이동일 때만」 월드 체비셰프(2차).
    // 그 구조가 만든 문제가 이 spec 의 출발점이다:
    //   ① 「사거리 안」의 뜻이 **누가 묻느냐에 따라 달랐다.** 타일 고정 유닛은 셀만, 연속
    //      유닛은 셀+월드. 같은 두 유닛의 같은 거리가 경로에 따라 다르게 판정됐다.
    //   ② 몸이 없었다 — 전부 중심점 대 중심점이라 스프라이트가 1.89배인 보스가 몸통을
    //      관통당해도 무판정이었다.
    //   ③ 셀 체비셰프는 **칸 경계에서 튄다.** 반 칸 움직였을 뿐인데 판정이 뒤집힌다.
    //
    // 지금은 `SkillMath.InBodyReach` 하나다. `bothContinuous` 인자가 사라졌다 —
    // **그 인자의 존재 자체가 ①이었다.**
    //
    // ⚠ **본체가 여기 없다.** `Wassup.Skills`(엔진 무참조 asmdef)에 있고 이 파일은
    // `int2`/`float3` ↔ 타일 단위 변환만 한다(계약 8). M1 에서 sim 을 엔진 밖으로 들어낼 때
    // 술어가 **이미 저쪽에 있어야** 그 이전이 「옮기기」가 아니라 「호출부 정리」로 끝난다.
    //
    // ── 소비처 열하나 · 전부 같은 답을 받아야 한다 ──
    //   ── 공격(Combat) ──
    //   1) AttackSystem 타겟 선정            — «때릴 수 있나»                    [획득]
    //   2) AttackSystem 적 focus 락 유지                                        [유지]
    //   3) AttackSystem 어그로 sticky 오버라이드                                 [획득]
    //   4) AttackSystem frontmost 락 유지                                       [유지]
    //   5) AttackSystem 방어유닛 focus 락 유지                                   [유지]
    //   6) AttackSystem committedTarget 재판정 — RESOLVE 시 이탈 판정             [유지]
    //   7) AttackSystem 다중타격 2번째 이후 대상 — 첫 대상과 같은 정의여야 한다      [획득]
    //   ── 정지(Combat) ──
    //   8) EnemyAiStateSystem guardianInRange — 어그로된 적이 멈춰도 되나          [획득]
    //   9) EnemyAiStateSystem.HasFireTarget   — «멈춰도 되나»                    [획득/유지]
    //   ── 시전(Effects) ──
    //  10) HazardCastSystem 캐스트 사거리                                        [획득]
    //   ── 이동(Effects) ──
    //  11) PatrolAreaMath.StepDir/CloseInDir — «더 다가가야 하나»
    //      ⚠ 이 하나만 합본이 아니라 `InCellRange`·`InReach` 를 **분해해서** 쓴다
    //      (셀 통과 AND 몸 거리 실패 = 한 칸 더 밀어 준다).
    //
    // ⚠ **인라인 재작성은 리뷰 거절 사유다.** 2026-08-12 에 한 곳만 조였다가 182프레임 교착이
    // 났다: (11)이 「격자상 사격 칸에 도착했으니 멈춰」라고 하고 (1)이 「물리적으로 머니 못 쏴」
    // 라고 해서 순찰병이 적 옆에 붙어 선 채 아무것도 안 했다. **이동을 멈추는 근거가 사격 가능
    // 여부인 이상, 셋이 같은 답을 받아야 한다.**
    //
    // ⚠ 스냅샷 어긋남: 이 술어는 **그 프레임의 위치**를 본다. `MovementSystem` 뒤에 도는
    // 시스템(`AttackSystem` · `EnemyAiStateSystem` · `HazardCastSystem`)은 이동 후 위치를,
    // 앞에 도는 것은 이동 전 위치를 본다 — 한 스텝만큼 어긋날 수 있다. 오늘 허용 범위다.
    public static class AttackReach
    {
        // 사거리(타일) 안인가. `targetBodyRadiusTiles` = 대상의 몸 반경(타일, 0 = 점).
        //
        // ⚠ **`tileSize` 로 나눠 타일 단위로 넘긴다.** 술어가 월드 단위를 모르는 이유는
        // 「사거리 3」이 저작에서 타일 수이기 때문이다 — 월드로 환산하는 지점이 하나여야 한다.
        // ⚠ `tileRange` 가 **실수**인 이유: 유지 판정이 히스테리시스 폭 `h` 를 더해 부른다
        // (`TargetPersistence.KeepsLock`). 저작은 정수지만 술어는 그걸 알 필요가 없다.
        // ⚠ `selfBodyRadiusTiles` 에 **기본값을 주지 않는다**(unit 9). 기본값을 주면
        // 새 호출부가 몸을 안 넘기고도 컴파일되고, 그 순간 「소비처 열하나가 같은 답을
        // 받는다」는 이 파일 헤더의 계약이 조용히 깨진다. 상수 시절엔 술어가 자기 몸을
        // 스스로 알았지만 이제 **저작에서 온다** — 호출부가 그것을 나를 책임을 진다.
        // 1×1 특수화(몸이 원). 다칸이 끼면 아래 `BodyShape` 오버로드를 쓴다.
        public static bool InReach(float3 atkPos, float3 tgtPos, float tileRange, float tileSize,
                                   float selfBodyRadiusTiles, float targetBodyRadiusTiles = 0f)
            => InReach(atkPos, BodyShape.Round(selfBodyRadiusTiles),
                       tgtPos, BodyShape.Round(targetBodyRadiusTiles), tileRange, tileSize);

        // 유닛의 몸 = **사각(반폭) ⊕ 원(반지름)**, 그리고 위치 → 몸 중심 보정.
        // 1×1 이면 반폭·보정이 0 이라 순수 원이다.
        public readonly struct BodyShape
        {
            public readonly float RadiusTiles;
            public readonly float2 HalfExtentTiles;
            public readonly float2 CenterOffsetTiles;

            public BodyShape(float radiusTiles, float2 halfExtentTiles, float2 centerOffsetTiles)
            {
                RadiusTiles = radiusTiles;
                HalfExtentTiles = halfExtentTiles;
                CenterOffsetTiles = centerOffsetTiles;
            }

            public static BodyShape Round(float radiusTiles)
                => new BodyShape(radiusTiles, float2.zero, float2.zero);
        }

        // **정본 진입점.** 두 몸의 민코프스키 합 — 반폭은 합산, 중심 보정은 각자 위치에 더한다.
        // 게이트(여기)와 랭킹(`AttackSystem.DistanceSqToTarget`)이 **같은 몸**을 보게 하는 것이
        // 이 오버로드의 존재 이유다(README:155 가 자인한 갈림).
        public static bool InReach(float3 atkPos, BodyShape self,
                                   float3 tgtPos, BodyShape target,
                                   float tileRange, float tileSize)
        {
            float inv = tileSize > 1e-6f ? 1f / tileSize : 1f;
            float dx = (tgtPos.x - atkPos.x) * inv
                     + target.CenterOffsetTiles.x - self.CenterOffsetTiles.x;
            float dz = (tgtPos.z - atkPos.z) * inv
                     + target.CenterOffsetTiles.y - self.CenterOffsetTiles.y;
            return Wassup.Skills.SkillMath.InBodyReachWithHalfExtent(
                dx, dz,
                self.HalfExtentTiles.x + target.HalfExtentTiles.x,
                self.HalfExtentTiles.y + target.HalfExtentTiles.y,
                tileRange, self.RadiusTiles, target.RadiusTiles);
        }

        // 셀 좌표로 묻는 사거리 — 두 몸이 각자 칸 중앙에 설 때의 답이다.
        //
        // **표기(배치 프리뷰)가 이걸 쓴다.** 프리뷰가 `dx,dz` 이중 루프로 모양을 다시 그리면
        // 「밝은 칸인데 안 때린다」가 되고, 그게 가장 나쁜 종류의 버그다 —
        // 화면이 규칙을 **틀리게** 가르친다. 위 `InReach` 와 **같은 본체**를 지난다.
        public static bool InCellReach(int2 atkCell, int2 tgtCell, float tileRange,
                                       float selfBodyRadiusTiles, float targetBodyRadiusTiles = 0f)
            => Wassup.Skills.SkillMath.InBodyReach(
                   tgtCell.x - atkCell.x, tgtCell.y - atkCell.y,
                   tileRange, selfBodyRadiusTiles, targetBodyRadiusTiles);

        // 격자 계층의 자. **사거리 판정에 쓰지 말 것** — 그 용도의 정본은 위 `InReach` 하나다.
        // 이 함수가 남은 이유는 순찰 이동뿐이다: 추격 필드 소스 수집이 셀 디스크라
        // (`FlowFieldBuilder.CollectDefenderSources`, 결정 4) 「필드가 세운 사격 칸」을
        // 판정하려면 그와 **같은 자**여야 한다.
        public static bool InCellRange(int2 atkCell, int2 tgtCell, int tileRange)
            => math.max(math.abs(tgtCell.x - atkCell.x), math.abs(tgtCell.y - atkCell.y)) <= tileRange;
    }
}
