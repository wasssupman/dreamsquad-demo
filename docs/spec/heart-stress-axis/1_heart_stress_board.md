# 1 — 마음 스트레스를 보드에 그린다 (rev — 바 → 잠식)

## 목적

**스트레스가 보드를 먹는다.** 마음 주변이 스트레스만큼 붉게 번지고, 수위가 높을수록 강하고
빠르게 맥동한다.

> **rev 사유(2026-08-23, 사용자 지적).** 처음엔 머리 위 «차오르는 바»로 만들었는데, 본능·적
> 마음·유닛과 **같은 문법**이라 「색만 다른 4번째 바」로 읽혔다. 판을 끝내는 유일한 축인데
> 화면 점유가 잡몹 체력바와 같은 급인 것이 문제였다. **임팩트는 면적에서 온다** —
> 바는 화면의 15px 지만 잠식은 보드의 9칸이다.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/HeartStressStainMath.cs` **(신설)** — 링 채움·맥박 순수 함수
- `Assets/_Project/Scripts/Core/TilemapMapView.cs` — `SetHeartStress` / `ClearHeartStress` / `StainSprite`
- `Assets/_Project/Scripts/Presentation/BoardSortOrder.cs` — `HeartStressStainOrder`
- `Assets/_Project/Scripts/Data/TileSetData.cs` — 잠식 저작 파라미터 6종
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `SyncGoalOverheadGauges` 마음 분기 · `ResetGoalStability` 정리
- `Assets/_Project/Tests/EditMode/HeartStressStainMathTests.cs` **(신설)**

## 구현

**1. 3×3 로 번진다.** 링 0 = 마음 셀 · 1 = 직교 4칸 · 2 = 대각 4칸. 링별 구간을 **겹치게**
잡아(0~30% / 25~65% / 55~100%) 「한 칸씩 툭툭 켜지는」 계단이 아니라 번지는 그림이 되게 한다.
규칙은 순수 함수(`HeartStressStainMath`)가 갖고 EditMode 가 **단조성**과 **안→밖 순서**를 고정한다.

**2. ⚠ 배치 하이라이트와 확실히 가른다.** 보드 액체 어휘(`PlacementLiquidTile`)가 이미 배치
포커스 셀에 쓰인다. 같아 보이면 붉은 얼룩이 「배치 가능/불가 칸」으로 오독된다. 그래서
- **모양**: 배치는 둥근사각 **테두리**, 잠식은 **경계 없는 소프트 원**(절차적 텍스처, 가장자리 완전 감쇠)
- **색**: 배치는 시안/그린, 잠식은 붉은 계열
- **정렬**: `HeartStressStainOrder = −14` — 「보드 레이어 < 유닛 레이어」 규칙의 **최하단**.
  배치 하이라이트가 이 위를 덮는 것은 의도다(배치 중엔 배치가 주인공).

**3. 저작 파라미터는 `TileSetData`.** 이미 보드 시각 파라미터의 집이다(`rangeColor`·
`rangePulseSpeed`·`placementLiquidMaterial`). 씬 컴포넌트에 `[SerializeField]` 로 두면 값 하나
바꾸는 데 씬 저장이 필요하고, 그건 **남의 미저장 WIP 까지 베이크**한다.

**4. 맥박의 계산 주체는 하나다.** `SetHeartStress` 가 이번 프레임의 맥박 배율을 **반환**하고
브리지가 그 값을 화면 림(unit 3)에 넘긴다. 각자 계산하면 파라미터가 갈리는 순간 위상이
어긋나 「화면과 보드가 따로 뛴다」.

**5. 남긴 것.** `OverheadBarSkin.Stress` 와 `BarSkin.fadeAtEmpty` 는 **지우지 않는다** —
되돌리기가 두 줄이고, 차오르는 바가 필요한 다음 소비자가 그대로 쓴다.
프랍 균열 틴트(`SetGoalCrack`)도 유지 — 잠식은 «주변», 균열은 «본체» 라 겹치지 않는다.

## 완료 기준

- [x] 컴파일 0 에러 · 콘솔 에러 0
- [x] `HeartStressStainMathTests` — 단조성 · 안→밖 순서 · 맥박이 밝기 배율(0.7~1.0) 범위 유지
- [x] 대상 EditMode 46/46 통과
- [ ] Play: 마음이 맞을수록 주변이 **붉게 번진다**(9칸까지)
- [ ] Play: 악몽을 잡으면 **물러난다**
- [ ] Play: 스트레스가 높을수록 **빠르고 강하게** 맥동한다
- [ ] Play: 배치 하이라이트와 **혼동되지 않는다**
- [ ] Play: 마음 머리 위에 바가 **없다**. 본능·적 마음의 바는 그대로다
