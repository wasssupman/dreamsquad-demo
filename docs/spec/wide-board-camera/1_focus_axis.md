# unit 1 — Director 에 관심점 축 도입 (화면 무변)

## 목적

`CameraDirector` 가 포즈를 풀 때 쓰는 **대상**을 「보드 중심」에서 「관심점」으로 바꾼다.
**이 unit 에서 관심점의 값은 여전히 보드 중심**이라 화면은 오늘과 픽셀 단위로 같다.

되돌리기가 싼 커밋을 먼저 세우는 것이 목적이다. 축만 먼저 통과시켜 두면, 실제로 그림이 뒤집히는
unit 2 가 «값 하나 바꾸는 커밋»이 된다.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/CameraDirector.cs`

## 구현

`CameraFramingMath.SolveStatePose` 는 **이미 `target` 인자를 받는다** — 시그니처 변경이 없다.
바꿀 것은 그 인자를 만드는 쪽이다. 오늘 `_boardBounds.center` 를 넘기는 곳은 **넷**이다 —
상태 포즈 2곳(`CameraDirector.cs:227`, `:237`) · DoF 의 `camDepth`(`:342`) · **배치 드래그 포커스의
`boardDepth`(`:707`)**. **앞의 셋을 `_focus` 로 바꾼다.**

넷째(`:707`)는 `CameraComposeMath.PanDelta` 의 깊이 인자이고 그 `localPos` 는 depth 에 정비례한다.
unit 4 가 `placementFocusLead` 를 0 으로 은퇴시킬 채널이므로 **여기서는 손대지 않는다** — 그때까지
보드 중심 기준으로 남아도 화면이 안 바뀐다(관심점 = 보드 중심).

- `_focus` 는 **보드 평면 위 월드 좌표 하나**이고 초기값은 `_boardBounds.center` 다.
- `SetBoardBounds` 가 불릴 때 `_focus` 를 **보드 중심으로 리셋**한다(맵 교체 = 새 판).
- 외부 진입점 `SetPanDelta(Vector2 viewportDelta)` 를 연다. **화면 델타만 받는다** — 월드 변환과
  클램프는 Director 안에서 끝난다(feature 계약 1). 이 unit 에서는 호출처가 없다.
- 관성은 **기존 스프링 스텝을 재사용**한다(제약 8 — 적분기를 새로 만들지 않는다). 계수는
  `CameraDirectionConfig` 에 `panSpring`/`panDamping`/`panSensitivity` 로 저작하고,
  **관성 0 = 관성 끔**이 성립하게 한다(전역 토글을 만들지 않는다).

**HUD 인셋은 저작값이다.** 클램프에 넘길 상·하단 인셋은 `CameraDirectionConfig` 에
`hudInsetTop`/`hudInsetBottom`(뷰포트 비율 0~1)으로 저작하고 Director 가 그 값을 읽는다.
**트레이·손패 RectTransform 을 Director 가 참조하지 않는다** — 계약 1 과 같은 이유다(런타임
레이아웃을 당겨오는 순간 카메라가 UI 계층을 알게 된다). 저작 초기값은 오늘의 실측(트레이 상단
190px + 판정 포인터 오프셋 ≈ 화면 높이의 0.1)에서 잡고, 최종 값은 README 스파이크 2 로 확정한다.

**클램프는 이 unit 부터 항상 돈다.** `fitToBoard` 상태에서는 보드가 화면보다 작으므로 unit 0 의
「작은 축은 중앙 고정」이 걸려 결과가 보드 중심 그대로다 — 즉 클램프가 켜져 있어도 화면이 안 변한다.
이게 unit 0 의 그 분기를 라이브에서 처음 검증하는 자리다.

## 완료 기준

- **화면이 안 바뀐다.** 기존 카메라 테스트 전건 초록 — 특히
  `Tests/EditMode/CameraStatePoseTests.cs` 의 `AuthoredPlacementLead_KeepsBoardOnScreen_AtFullPan`
  (에셋의 배치 레시피를 로드해 **팬 최대에서 보드 4코너의 가로 좌표**가 뷰포트 안인지 단언)이
  **손대지 않고 통과**해야 한다.
  ⚠ **단 이 테스트는 `SolveStatePose`/`PanDelta` 를 직접 부르므로 Director 배선을 검증하지 않는다**
  (가로 x 만, 2% 오차). 관심점 배선의 회귀 가드는 아래 Play 스모크가 담당한다 — 이 테스트가 초록인
  것만으로 배선이 맞다고 판정하지 말 것.
- Play 스모크: 배치↔전투 전환 비행, 드래그 포커스, 킥/셰이크가 오늘과 같이 보인다.
- `SetPanDelta` 는 호출처 0 인 채로 커밋된다(다음 unit 이 붙인다).
