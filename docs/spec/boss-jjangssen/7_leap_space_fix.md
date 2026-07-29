# 7 — 도약 아치의 좌표계 수정 (sim → view)

## 목적

unit 6 의 아치가 **sim 좌표**에 얹혀 있었다. `BoardSpace.ToView` 는 `simWorld.x`·`.z` 로만 셀
좌표를 만들고 **`.y` 를 읽지 않으므로**(함수 주석: "평면 뷰에서 높이는 화면 위치가 아니다"),
`camUp * arcHeight` 의 세로 성분이 통째로 버려지고 보드 평면 성분만 **높이가 아니라 보드 변위**로
남았다. 보스가 뜨는 게 아니라 카메라 쪽으로 미끄러지고 있었다.

`/simplify` 5각도 리뷰 중 **3개 렌즈가 독립으로** 같은 결함을 찍었다(reuse: 선례와 다른 seam /
motion-sim: 성분 분해 + 정렬 오독 / altitude: 튜닝 수치가 잃은 성분의 보상). `SpineUnitView` 의
넉업 hop 주석이 이미 같은 함정을 경고하고 있었다 — *"⚠ sim-Y 에 넣으면 안 된다 … ToView **뒤에**
view 공간 Y 로 더한다."*

## 변경 대상

- `Assets/_Project/Scripts/Presentation/SpineUnitView.cs` — `SetFlightHeight` + 렌더 가산
- `Assets/_Project/Scripts/Presentation/QuadUnitView.cs` — 동일 seam(적 fallback 뷰)
- `Assets/_Project/Scripts/Bridge/BattleBridge.BossLeap.cs` — 궤적 축 분리 + 오버라이드 2축
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 적 피드가 수평/높이를 각자 소비

## 구현

**축 분리.** `DismountPoint` 에 앵커의 y 를 0 으로 눕히고 기저축을 **순수 `Vector3.up`** 으로 준다.
그러면 반환점이 정확히 둘로 갈린다 — `xz` = 보드 평면 수평 경로, `y` = **순수 아치 높이**.

- 수평(`xz`)은 sim 좌표로 오버라이드 → `ToView` 가 셀 정합을 잡는다(원래 이 함수의 일).
- 높이(`y`)는 뷰가 **`ToView` 뒤에** 더한다 — 넉업 hop 과 같은 seam, 독립 슬롯이라 도약 중
  넉업을 맞아도 합산된다.
- sim y 는 지면 높이라 양 끝을 `Lerp` 로 보간한다(아치와 무관).
- **카메라 참조가 사라졌다.** `camUp` 이 필요 없으므로 `Camera.main` 조회와 부재 시 조기 이탈
  분기가 함께 없어졌다.

**자기 해제.** 피드가 매 프레임 `SetFlightHeight(비행 아니면 0)` 을 쓴다 → 별도 clear 경로 불필요.
비행이 비정상 종료해도 다음 프레임에 0이 된다.

**튜닝 단위가 바뀌었다.** 이전 값(factor 0.95 / minHeight 8.5 / launch.y 1.25)은 버려지는 성분을
메우려 부풀린 것이었다. 정직해진 지금은 같은 궤적을 view 공간에서 쓰는 드롭 하마
(`DragSwaySettings` ⑩: factor 0.5 / minHeight 3.5)와 같은 대역이어야 한다 →
**factor 0.55 / minHeight 4.5 / launch (0.25, 1.0)** 로 복귀.

## 부수 효과 — 정렬 행 오독 해소

`SpineUnitView.UpdateSortingOrder` 는 `_simWorld` 로 행을 역산한다. 이전에는 오버라이드가
`camUp` 성분에 오염돼 **공중의 보스가 지나가는 행 뒤로 정렬**됐다. 이제 sim 좌표가 순수 보드
평면이라 행 정렬이 실제 보드 위치를 따른다. (방어유닛 비행은 `SetFlightView` 가
`DragPreviewOrder` 를 강제해 원래 무해했다 — 그래서 이 결함은 적 경로에만 있었다.)

**적 피드의 틴트·오버헤드 UI 는 건드리지 않았다.** 방어유닛 분기는 `continue` 로 그것들을
건너뛰는데(배치 대기 중 비전투라 정당) 도약 중 보스는 HP 바가 계속 보여야 한다. 위치 write 만 교체했다.

## 완료 기준

- compile 클린 · 기존 EditMode 무회귀
- **Play 육안**: HP 50%·10% 에서 보스가 **화면 세로로** 솟았다 내리찍는다. 옆으로 미끄러지지 않는다
- 비행 중 보스가 지나가는 행 **뒤로 숨지 않는다**(정렬 오독 해소 확인)
- 도약 중 넉업을 맞으면 두 높이가 합산된다(독립 슬롯 확인) — 단 보스는 넉업 면역이라
  일반 적으로 확인해야 하며, 일반 적은 도약하지 않으므로 **실질 검증 불가**(설계상 안전)
- 아치 높이 재튜닝 — 값 의미가 바뀌었으므로 눈으로 다시 잡는다

- 확인: (대기) — EditMode 1566 중 1565 통과. 실패 1건은 `UnitKitSummaryTests` 로 무관
  (커밋 `616e3584` 의 방어유닛 한글 인코딩 손상, 별도 처리 중).
