# 2 — 하단 조작성 계측 + 오프셋 값 확정

## 목적

오프셋의 **대가를 측정**하고 그 안에서 값을 확정한다.

**초판의 전제는 틀렸다(critic C2, 코드로 확인).** "오프셋이 과하면 최하단 행이 도달 불가가 된다"고
적었지만, `PlacementCellSnap.Resolve` 가 결과 셀을 **그리드로 clamp** 한다:

```csharp
// Assets/_Project/Scripts/UI/PlacementCellSnap.cs:39-41
return new Vector2Int(Mathf.Clamp(cx, 0, gridSize.x - 1), Mathf.Clamp(cy, 0, gridSize.y - 1));
```

그리고 `_onBoard` 는 발점 평면해(`s > 0`)만 요구하므로(`DefenderDragPlacementController.cs:360-363`)
손가락이 그리드 **밖**의 보드 평면을 때려도 판정이 성립한다. 즉 손가락을 아래로 내리면 언제나 row 0
으로 접힌다 — **도달 불가는 발생하지 않는다.** 상단 3행 회귀와 대칭이라던 서술은 clamp 를 빼먹은
것이었다(그 회귀는 발점이 `totalDrop` 만큼 밀려 *히스테리시스 밴드 자체*가 판 밖으로 나간 경우다).

그래서 실제로 나빠지는 것은 **"그 행을 노리려면 손가락이 화면 어디까지 내려가야 하나"** 이고,
위험은 그 위치가 **방어 유닛 트레이 UI 아래로 밀려나** 손가락 대신 트레이가 하이라이트를 가리는 것이다
(가림이 없어지는 게 아니라 오클루더가 바뀌는 것). 이 단위는 그 높이를 잰다.

선행: unit 0, unit 1.

## 변경 대상

- `Assets/_Project/Tests/PlayMode/DragPlacementReachTest.cs` — 단언 형태 교정 + 하단 계측 신규
- `Assets/_Project/Data/Config/DragSwaySettings.asset` — 확정값 반영

## 구현

### 기존 상단 단언: 등식 → 부등식

`:91` 은 현재 `Assert.AreEqual(topPlaceableRow, maxRow)` 다. 지키려는 계약은 "**도달 가능**"인데
등식은 그보다 넓게 못박아 clamp 와 맵 placeability 분포에 결합된다. 오프셋은 상단 도달을 *늘리므로*
`maxRow > topPlaceableRow` 가 되면 등식이 깨진다(오늘 통과하는 이유는 clamp 가 이미 천장을 치고
있기 때문일 가능성이 높다 — 미확인). `Assert.GreaterOrEqual(maxRow, topPlaceableRow)` 로 바꾼다.

### 하단: 도달 tripwire + 조작성 계측

- 스윕 하한을 `1px` 까지 내린다(기존 5%). 오프셋이 붙으면 하단 밴드가 5% 아래로 내려간다.
  1% 스텝은 유지 — 근거는 기존 주석(`placementStickMargin` 이 크면 이웃 도달 창이 `1 - margin`
  타일로 좁아져 거친 스텝은 행을 건너뛴다). **오프셋은 상수 평행이동이라 margin 과 곱셈적으로
  얽히지 않는다**(`Resolve` 는 frac 만 본다 — 확인). 다음 사람이 재검토하지 않게 이 사실을 주석에 남긴다.
- `BottomPlaceableRow(bridge, unit, grid)` — `y = 0` 부터 위로 스캔한 첫 배치가능 행.
- **단언 A (tripwire, 약함)**: `Assert.LessOrEqual(minObservedRow, bottomPlaceableRow)`.
  clamp 덕에 거의 항상 성립한다 — 이건 가드가 아니라 판정이 통째로 망가졌을 때만 울리는 회귀선이다.
  **약한 단언임을 주석에 명시**한다(다음 사람이 이걸 안전망으로 오해하면 안 된다).
- **단언 B (진짜 가드)**: `bottomPlaceableRow` 를 hover 로 잡은 샘플들의 **최대 raw screen.y** =
  그 행을 노릴 수 있는 손가락의 **가장 높은** 위치다. 이 값이 화면 하단 안전선보다 위여야 한다:

  ```csharp
  Assert.Greater(maxScreenYTargetingBottomRow, Screen.height * BottomSafeRatio,
      "최하단 배치가능 행을 노리려면 손가락이 화면 최하단(트레이 영역)까지 내려가야 한다. "
      + "오프셋을 낮추거나 하단 행을 포기할지 결정하라.");
  ```

  `BottomSafeRatio` 는 테스트 로컬 상수(초기 `0.12f`). 테스트는 UI 를 모르므로 이건 트레이 높이의
  **근사**다 — 실측 대조는 아래 완료 기준의 실기기 항목이 담당한다. 근사임을 주석에 적는다.

### 값 확정

1. 기본값 `0.06`(1080 세로 랜드스케이프 ≈ 65px ≈ 0.8셀)로 테스트 실행 → A·B 통과 확인.
2. **민감도 확인**: 오프셋을 올려가며 **단언 B 가 처음 실패하는 값**을 실측해 문서에 적는다.
   그 값이 실용 구간(0.06~0.10)에서 멀면 B 는 그 구간을 못 지키므로 README 계약 5 의 서술을
   그만큼 더 하향해야 한다. **어떤 값에서도 통과하면 B 는 가드가 아니다.**
3. Android 실기기에서 엄지 드래그 체감 확인. 가려지면 한 단계 올려 1~2 반복.
4. 확정값을 `DragSwaySettings.asset` 에 반영.

> 오프셋 필드는 **unit 0 커밋 시점부터 이미 라이브**다(Unity 는 필드 이니셜라이저 실행 후 YAML 을
> 덮으므로 파일에 키가 없는 신설 필드는 클래스 기본값을 유지한다). 이 단위의 에셋 편집은 회귀 게이트가
> 아니라 튜닝값 고정이다.

## 완료 기준

- `DragPlacementReachTest` 의 상단 부등식 + 하단 A·B 통과.
- 단언 B 의 **첫 실패 오프셋 값이 실측되어 문서에 기록**됨(민감도 증명). 기록 없으면 미완료.
- 확정값이 `DragSwaySettings.asset` 에 반영됨.
- **실기기 항목 (사용자 확인)**: ① 드래그 중 손가락이 포커스 칸을 가리지 않는다. ② **최하단 배치가능
  행에 실제로 유닛을 놓아 보고**, 그때 하이라이트가 **트레이 패널에 가리지 않는다**. ②가 실패하면
  오프셋이 가림을 없앤 게 아니라 오클루더를 손가락→트레이로 옮긴 것이므로 값을 낮춘다.
