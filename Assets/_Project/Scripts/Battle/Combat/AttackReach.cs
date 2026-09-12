using AttackShapeBaked = Wassup.Data.AttackShapeBaked;
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
    // 지금은 `SkillMath.ReachFromUnit` 하나다(unit 23a — 본문은 private, 진입점이 형을 선언한다).
    // `bothContinuous` 인자가 사라졌다 —
    // **그 인자의 존재 자체가 ①이었다.**
    //
    // ⚠ **본체가 여기 없다.** `Wassup.Skills`(엔진 무참조 asmdef)에 있고 이 파일은
    // `int2`/`float3` ↔ 타일 단위 변환만 한다(계약 8). M1 에서 sim 을 엔진 밖으로 들어낼 때
    // 술어가 **이미 저쪽에 있어야** 그 이전이 「옮기기」가 아니라 「호출부 정리」로 끝난다.
    //
    // ── 소비처 열둘 · 전부 같은 답을 받아야 한다 ──
    //   ── 공격(Combat) ──
    //   1) AttackSystem 타겟 선정            — «때릴 수 있나»                    [획득]
    //   2) AttackSystem 적 focus 락 유지                                        [유지]
    //   3) AttackSystem 어그로 sticky 오버라이드                                 [획득]
    //   4) AttackSystem frontmost 락 유지                                       [유지]
    //   5) AttackSystem 방어유닛 focus 락 유지                                   [유지]
    //   6) AttackSystem committedTarget 재판정 — RESOLVE 시 이탈 판정             [유지]
    //   7) AttackSystem 다중타격 2번째 이후 대상 — 첫 대상과 같은 정의여야 한다      [획득]
    //      ⚠ directional-attack-shape rev 3 — 이 하나만 **도형 항**이 추가로 곱해진다(`InReachShaped`).
    //        원 안이면서 「주 대상 방향 도형 안」이어야 한다. 원 항은 (1)과 같다.
    //   ── 정지(Combat) ──
    //   8) EnemyAiStateSystem guardianInRange — 어그로된 적이 멈춰도 되나          [획득]
    //   9) EnemyAiStateSystem.HasFireTarget   — «멈춰도 되나»                    [획득/유지]
    //   ── 시전(Effects) ──
    //  10) HazardCastSystem 캐스트 사거리                                        [획득]
    //   ── 이동(Effects) ──
    //  11) PatrolAreaMath.StepDir/CloseInDir — «더 다가가야 하나»
    //      ⚠ 이 하나만 합본이 아니라 `InCellRange`·`InReach` 를 **분해해서** 쓴다
    //      (셀 통과 AND 몸 거리 실패 = 한 칸 더 밀어 준다).
    //   ── 감지(Combat) ──
    //  12) DetectionSystem — «저 놈을 발견했나 · 이미 문 것을 놓나»            [획득/유지]
    //      ⚠ 이 하나만 `tileRange` 자리에 **`detectionRange`** 를 넣는다(사거리가 아니다).
    //      술어·몸·필터는 (1)과 같고 **반경만 다르다** — 그게 「탐색 반경이 넓어졌다」의 구현이다.
    //      무제한(`< 0`)은 이 호출을 건너뛰지만 legal 필터는 그대로 지난다
    //      (enemy-detection-range 계약 12).
    //
    // ⚠ **인라인 재작성은 리뷰 거절 사유다.** 2026-08-12 에 한 곳만 조였다가 182프레임 교착이
    // 났다: (11)이 「격자상 사격 칸에 도착했으니 멈춰」라고 하고 (1)이 「물리적으로 머니 못 쏴」
    // 라고 해서 순찰병이 적 옆에 붙어 선 채 아무것도 안 했다. **이동을 멈추는 근거가 사격 가능
    // 여부인 이상, 셋이 같은 답을 받아야 한다.**
    //
    // ⚠ 스냅샷 어긋남: 이 술어는 **그 프레임의 위치**를 본다. `MovementSystem` 뒤에 도는
    // 시스템(`AttackSystem` · `EnemyAiStateSystem` · `HazardCastSystem`)은 이동 후 위치를,
    // 앞에 도는 것은 이동 전 위치를 본다 — 한 스텝만큼 어긋날 수 있다. 오늘 허용 범위다.
    //
    // ── 도형 (directional-attack-shape rev 3, 2026-09-12 사용자 결정 「안 1」) ──
    // **획득·유지·정지는 원이다** — 사거리 안이면 반드시 반응한다(주류 TD 의 계약, 캐주얼 안전판).
    // 도형(부채꼴/띠)은 **부가 타격에만** 곱해진다: 주 대상이 정해진 뒤 «그 대상을 향한 실제 방향»을
    // 축으로 도형을 세우고, 원 안 후보 중 그 안의 것만 같이 때린다. 캐릭터의 좌/우 반전은 연출이다
    // (타겟 쪽으로 가장 가까운 면). rev 2(좌/우 축 · 획득부터 도형 · 나비넥타이 표기)는 «사거리 안인데
    // 가만히 선 유닛»이 자동전투에선 교정 수단 없이 반복 노출된다는 이유로 폐기 — README 리뷰 이력.
    // 그래서 표기는 원 링 그대로이고, 도형은 **공격 순간 VFX** 로만 그린다(정적 가이드는 절반의 시간 거짓말).
    public static class AttackReach
    {
        // **정본 진입점.** 사거리(타일) 안인가 — rev 3(2026-09-01 외부 세션): 몸 = 원 하나,
        // `d² ≤ (사거리 + selfR + targetR)²` edge-to-edge. `targetBodyRadiusTiles` 0 = 점.
        //
        // ⚠ **`tileSize` 로 나눠 타일 단위로 넘긴다.** 술어가 월드 단위를 모르는 이유는
        // 「사거리 3」이 저작에서 타일 수이기 때문이다 — 월드로 환산하는 지점이 하나여야 한다.
        // ⚠ `tileRange` 가 **실수**인 이유: 유지 판정이 히스테리시스 폭 `h` 를 더해 부른다
        // (`TargetPersistence.KeepsLock`). 저작은 정수지만 술어는 그걸 알 필요가 없다.
        // ⚠ `selfBodyRadiusTiles` 에 **기본값을 주지 않는다**(unit 9). 기본값을 주면
        // 새 호출부가 몸을 안 넘기고도 컴파일되고, 그 순간 「소비처 열하나가 같은 답을
        // 받는다」는 이 파일 헤더의 계약이 조용히 깨진다. 반경은 저작(적 티어)·파생
        // (방어유닛 가로/2 파생 — rev 2026-09-04, 구 내접원)에서 온다 — 호출부가 그것을 나를 책임을 진다.
        // ⚠ rev 2 의 `BodyShape`(사각 반폭 ⊕ 원)는 은퇴했다 — 사유는 `SkillMath` 술어 헤더.
        public static bool InReach(float3 atkPos, float3 tgtPos, float tileRange, float tileSize,
                                   float selfBodyRadiusTiles, float targetBodyRadiusTiles = 0f)
        {
            float inv = tileSize > 1e-6f ? 1f / tileSize : 1f;
            return Wassup.Skills.SkillMath.ReachFromUnit(
                (tgtPos.x - atkPos.x) * inv, (tgtPos.z - atkPos.z) * inv,
                tileRange, selfBodyRadiusTiles, targetBodyRadiusTiles);
        }

        // 셀 좌표로 묻는 사거리 — 두 몸이 각자 칸 중앙에 설 때의 답이다.
        //
        // **표기(배치 프리뷰)가 이걸 쓴다.** 프리뷰가 `dx,dz` 이중 루프로 모양을 다시 그리면
        // 「밝은 칸인데 안 때린다」가 되고, 그게 가장 나쁜 종류의 버그다 —
        // 화면이 규칙을 **틀리게** 가르친다. 위 `InReach` 와 **같은 본체**를 지난다.
        public static bool InCellReach(int2 atkCell, int2 tgtCell, float tileRange,
                                       float selfBodyRadiusTiles, float targetBodyRadiusTiles = 0f)
            => Wassup.Skills.SkillMath.ReachFromUnit(
                   tgtCell.x - atkCell.x, tgtCell.y - atkCell.y,
                   tileRange, selfBodyRadiusTiles, targetBodyRadiusTiles);

        // **부가 타격 전용 진입점** (directional-attack-shape rev 3). 원 항(위 `InReach` 와 같은 본체) AND
        // 도형 항. `dirToPrimary` = 공격자 → 주 대상 **sim XZ** 벡터(정규화 불필요, 월드/타일 어느 단위든
        // 방향만 쓴다). 도형은 그 방향을 +X 로 회전한 프레임에서 판정한다(`SkillMath` 게이트는 +X 고정).
        //
        // ⚠ `shape` 에 기본값을 주지 않는다 — 소비처가 「내 도형」을 선언한다(제약 13 「원점 선언」과 같은 형태).
        // ⚠ 방향이 정의되지 않으면(주 대상이 같은 자리) 도형 항은 **통과** — `SkillCone.SameSpotEpsSq` 선례.
        //   Omni 면 도형 항을 건너뛴다(오늘 경로와 명령 수 동일).
        public static bool InReachShaped(float3 atkPos, float3 tgtPos, float tileRange, float tileSize,
                                         float selfBodyRadiusTiles, float targetBodyRadiusTiles,
                                         in AttackShapeBaked shape, float2 dirToPrimary)
        {
            float inv = tileSize > 1e-6f ? 1f / tileSize : 1f;
            float dx = (tgtPos.x - atkPos.x) * inv, dz = (tgtPos.z - atkPos.z) * inv;
            if (!Wassup.Skills.SkillMath.ReachFromUnit(dx, dz, tileRange, selfBodyRadiusTiles, targetBodyRadiusTiles))
                return false;
            if (shape.kind == AttackShapeBaked.OmniKind) return true;
            float len2 = math.lengthsq(dirToPrimary);
            if (len2 <= Wassup.Skills.SkillCone.SameSpotEpsSq) return true;
            float2 u = dirToPrimary * math.rsqrt(len2);
            float along  = u.x * dx + u.y * dz;        // 주 대상 방향 성분
            float across = u.x * dz - u.y * dx;        // 그 수직 성분(부호는 게이트가 |·| 로 접는다)
            return shape.kind == AttackShapeBaked.SectorKind
                ? Wassup.Skills.SkillMath.SectorGateX(along, across, shape.sinHalf, shape.cosHalf, targetBodyRadiusTiles)
                : Wassup.Skills.SkillMath.BandGateX(along, across, shape.halfWidth,
                                                     tileRange + selfBodyRadiusTiles, targetBodyRadiusTiles);
        }

        // 격자 계층의 자. **사거리 판정에 쓰지 말 것** — 그 용도의 정본은 위 `InReach` 하나다.
        // 이 함수가 남은 이유는 순찰 이동뿐이다: 추격 필드 소스 수집이 셀 디스크라
        // (`FlowFieldBuilder.CollectDefenderSources`, 결정 4) 「필드가 세운 사격 칸」을
        // 판정하려면 그와 **같은 자**여야 한다.
        public static bool InCellRange(int2 atkCell, int2 tgtCell, int tileRange)
            => math.max(math.abs(tgtCell.x - atkCell.x), math.abs(tgtCell.y - atkCell.y)) <= tileRange;
    }
}
