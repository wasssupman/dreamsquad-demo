# 3 — 표기: 링이 도형을 그린다 · 문안 · VFX

## 목적

「저 위 적은 안 때린다」가 **배치 전에** 읽혀야 한다(결정 rev 2-3). 링·배치 프리뷰·부착 프리뷰가 판정과 같은
술어로 도형을 그린다(계약 9). 방향의 시각 보증자는 캐릭터 반전 자체라 VFX 는 회전이 필요 없다.

## 변경 대상

- `Core/TilemapMapView.cs:1109` `SetPlacementRange` 셀 페인트 — `InCellReach(..., shape, side 0)`
- `Bridge/BattleBridge.cs:8010` 링 반경 산출 — 도형 유닛은 원 링 대신 **셀 윤곽 페인트**(또는 절차 메시)
- `Presentation/` 링 컴포넌트(distance-based-range unit 5·15) — 도형 분기
- `dreamcatcher-attach-range-preview` 링 — 같은 술어
- `Data/UnitKitSummary.cs:32` — 문안
- `Bridge/BattleBridge.cs:4813~` — `attackVfxAtAttacker`(선택)

## 구현

- **모양**: Sector 양쪽 = 나비넥타이 ∩ 원 · Band 양쪽 = 가로 띠 ∩ 원. 원 링 스프라이트는 못 그리므로 도형 유닛은
  **셀 채움**으로 떨어진다(윤곽 렌더 신설 없음 — 링이 없으면 `RangeFillAlpha` 가 채움에 풀알파를 준다) — `InCellReach` 가 이미 같은 본체를 지나므로 「밝은 칸인데 안 때린다」가
  구조적으로 불가능하다. 원 링(Omni)은 무변. 정렬 3티어·다크 라이너 규칙은 unit 5 그대로.
- **부착 프리뷰·선택 링**(판정 캐리어 3종 중 링) — 같은 분기. 그림자·대상 마크 무변.
- **문안**(`UnitKitSummary`) — 기존 어휘를 키운다:
  - Omni: 무변. Sector: 「**보는 쪽 A°** 안의 적만 공격」(+ N체면 「최대 N체 동시 타격」 병기).
    Band: 「**보는 쪽 일직선(세로 폭 W)** 최대 N체 동시 타격」 / 1체면 「…의 적만 공격」. ⚠ 「관통」을 쓰지 않는다 —
    잿불 관통탄(하나가 뚫고 지나감)과 다른 일이다(리뷰 LOW). ⚠ 「전방」은 이제 **참말**이지만(캐릭터가 보는 쪽)
    「보는 쪽」이 더 직접적이다.
- **VFX**: 캐릭터가 좌/우 반전으로 방향을 이미 말한다. `attackVfxFacesTarget` 은 기존대로. 참격 자국을 공격자
  자리에 찍고 싶으면 `attackVfxAtAttacker` 옵션(원점 분기 한 줄) — 필수 아님, unit 4 육안에서 필요하면.

## 완료 기준

- [x] 도형 유닛 배치 프리뷰 = `InReach(tileSize 1, shape, 0)` 셀(`TilemapMapView:1112` — 셀 좌표를 float3 로 넘긴다;
      `InCellReach` 는 테스트 전용 동치 진입점) · 도형 유닛은 원 링을 안 그린다.
      ⚠ 표기 단언 확장은 안 했다 — `RangeDisplayContractTests` 는 이미 「칠한 칸 == InReach」를 같은 두 함수로
      대조하고, 도형은 그 두 함수에 같은 인자로 들어간다. 별도 단언은 같은 것을 두 번 쓰는 것.
- [~] Omni 유닛 링·프리뷰 픽셀 무변 — unit 4 Play 육안으로 이월(코드상 Omni 분기는 종전 경로 그대로).
- [x] 문안 테스트: `AttackShapeTextTests` 5건(Omni 무변 · Sector N체/1체 · Band · reflex→Omni 문안). 기존 문안 테스트 무변.
- [~] 부착 프리뷰 — **손대지 않았다.** `SetAttachPreview` 는 카드의 `DcRangeSpec`(스킬 반경)을 그리지 유닛 사거리가
      아니다. 유닛 사거리를 그리는 표기는 배치 프리뷰(+마크)뿐이라 그 둘만 도형을 받는다.
- [~] `attackVfxAtAttacker` 옵션 — 넣지 않았다(선택 항목 · 소비처 0 · 제약 8). unit 4 육안에서 필요하면.

---

### 진행 기록 — 구현 2026-09-12

- `TilemapMapView.SetPlacementRange(anchor, tileRange, shape, …)` — 도형 인자 필수. `shape.IsOmni` 일 때만 링.
- `BattleBridge.SetPlacementRange`·`RefreshRangeTargetMarks` 가 `BakeAttackShape(unit.attackShape)` 를 넘긴다.
- `UnitKitSummary` — bake 를 지나 문안을 정한다(sim 과 같은 폴백: reflex 는 Omni 문안).

---

### rev 3 (2026-09-12) — 되돌림 + VFX 원점

- `TilemapMapView.SetPlacementRange` 도형 인자 삭제 · **링은 항상 원**. `BattleBridge` 마크도 원. `_placementMarkShape` 삭제.
- **`DefenderUnitData.attackVfxAtAttacker`** 신설 — 히트 VFX 를 공격자 자리에 찍는다(브리지 드레인 원점 분기, 지연 경로 포함).
  도형 유닛은 이걸 켜고 `attackVfxFacesTarget` 과 함께 저작한다 — 회전하는 부가 타격 도형의 **유일한 시각 보증자**.
  ⚠ 참격 자국 프리팹 저작·슬롯 연결은 **에디터 작업**(unity-vfx-authoring → 오프스크린 렌더 육안) — 미실행.
- 문안: 「휘두르는 쪽 A° 안 최대 N체 동시 타격」(N > 1 일 때만 · N = 1 은 도형 문안 없음 — 효과 0) · Band 「찌르는 방향 일직선…」.
- `OnValidate` 경고 복귀(rev 1 계약 9): 도형 × `attackTargetCount ≤ 1` = 효과 0.
