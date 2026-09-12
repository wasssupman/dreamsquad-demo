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

- ~~**모양**: Sector 양쪽 = 나비넥타이 ∩ 원 · Band 양쪽 = 가로 띠 ∩ 원.~~ (rev 2 — **폐기**, 링은 원. 아래 rev 3 절이 정본) 원 링 스프라이트는 못 그리므로 도형 유닛은
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

- ~~`TilemapMapView.SetPlacementRange(anchor, tileRange, shape, …)` — 도형 인자 필수. `shape.IsOmni` 일 때만 링.~~ (rev 3 에서 되돌림 — 링은 항상 원, 도형 인자 없음)
- `BattleBridge.SetPlacementRange`·`RefreshRangeTargetMarks` 가 `BakeAttackShape(unit.attackShape)` 를 넘긴다.
- `UnitKitSummary` — bake 를 지나 문안을 정한다(sim 과 같은 폴백: reflex 는 Omni 문안).

---

### rev 3 (2026-09-12) — 되돌림 + VFX 원점

- `TilemapMapView.SetPlacementRange` 도형 인자 삭제 · **링은 항상 원**. `BattleBridge` 마크도 원. `_placementMarkShape` 삭제.
- **`DefenderUnitData.attackVfxAtAttacker`** 신설 — 히트 VFX 를 공격자 자리에 찍는다(브리지 드레인 원점 분기, 지연 경로 포함).
  도형 유닛은 이걸 켜고 `attackVfxFacesTarget` 과 함께 저작한다 — 회전하는 부가 타격 도형의 **유일한 시각 보증자**.
  ✅ **참격 자국 저작·통합 완료 2026-09-12** — `VFX/SlashMark_SKELETON.prefab` + `.mat`(URP Particles/Unlit 가산).
  **rev 2026-09-12 (Play 「거의 안 보인다」 → 재작업)**: 파편 팬(30입자 가산)은 라이브 스크린샷에서 발밑 작은 둥근 빛으로만
  보였다 — 카메라 pitch 50° 에 눌리고 가산 주황이 타일에 묻히며 방향이 안 읽힘. 그래서 **부채꼴 모양 자체를 그린 쿼드 1장**으로
  바꿨다: 절차 생성 텍스처 `SlashMark_Sector.png`(꼭짓점 아래 중앙 · 반각 30° · 채움 α0.42 + 밝은 테두리 α0.95) 를
  `SlashMark_SectorQuad.asset`(꼭짓점 원점 · +Y 1유닛) 에 입혀 **메시 파티클 1개**(`startRotation3D x=90°` 로 눕힘 ·
  `alignment=Local` 이라 `PlayHit` 의 LookRotation 을 따라 +Z=타겟 방향) + 자식 잔불 20개(가산). 알파 블렌드라 밝은 타일
  위에서도 선다. 0.5s one-shot, 0.45s 홀드 후 페이드. pitch 50° 렌더에서 +Z·+X·대각 세 방향 부채꼴 확인(`slash_sheet4.png`).
  ⚠ 텍스처의 반각 30° 는 저작 60° 와 **손으로 맞춘 것** — 각도를 바꾸면 텍스처를 다시 굽는다(후속: 각도 → 셰이더 파라미터).
  브루저·말파이트: `attackVfxPrefab = SlashMark_SKELETON · attackVfxAtAttacker · attackVfxFacesTarget · scale 1.6`(사거리 1 + 몸).
  ⚠ 두 유닛의 **기존 히트 VFX(브루저 FireBlast · 말파이트 흙 폭발)는 슬롯이 하나라 대체됐다** — 유닛별 톤(카탈로그 팔레트)과
  「타격점 히트 + 공격자 참격」 2슬롯은 후속 후보. 필수 오버라이드: Duration 0.3 · StartColor (1,0.8,0.45,0.9) · MaxParticles 30 · Loop false.
- 문안: 「휘두르는 쪽 A° 안 최대 N체 동시 타격」(N > 1 일 때만 · N = 1 은 도형 문안 없음 — 효과 0) · Band 「찌르는 방향 일직선…」.
- `OnValidate` 경고 복귀(rev 1 계약 9): 도형 × `attackTargetCount ≤ 1` = 효과 0.

## 리뷰 반영 (2026-09-12) — 참격 방향은 재생 시점에 다시 잰다
- START 이벤트의 `targetWorld` 로 잰 방향을 `hitDelaySec`(파이터 0.3s) 뒤에 그대로 쓰면 sim 의 RESOLVE 방향(`hitDir`)과 갈린다
  (속도 1.3 적이 0.39칸 이동 → 1.5칸 거리에서 ~15°). `PendingHitVfx` 가 `attacker`·`target` 엔티티를 싣고 재생 순간 둘의 현재
  위치로 방향을 재계산한다(대상이 죽었으면 스냅샷 유지). 잔여 오차 = RESOLVE 에서 주 대상이 **다른 적으로 바뀐** 경우뿐.

