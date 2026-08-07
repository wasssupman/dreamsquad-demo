# 1 — 안정도 체력바

## 목적

골의 안정도를 **유닛 체력바와 같은 언어**로 골 위에 그린다. 유닛 바보다 크고 확실히
구별되며, 바 위에 현재 안정도 수치를 띄운다. 선행: unit 0.

## 변경 대상

- `Assets/_Project/Scripts/Data/UnitOverheadUiStyle.cs` — `BarSkin` 3번째 직렬화 필드 + 접근자,
  수치 라벨 스타일, 높이 오프셋
- `Assets/_Project/Data/.../UnitOverheadUiStyle.asset` — 신규 스킨 값 저작
- `Assets/_Project/Scripts/Presentation/UnitOverheadView.cs` — 스킨 선택 일반화 + 라벨 슬롯
- `Assets/_Project/Scripts/Presentation/UnitOverheadUiLayer.cs` — 안정도 전용 진입점
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 골 앵커 → 스크린 좌표 피드
- `Assets/_Project/Tests/PlayMode/UnitOverheadUiLifecycleTest.cs` — 시그니처 변경 반영

## 구현

**1. 스킨 일반화는 뷰까지만** — `UnitOverheadView.Show/Rebuild` 의 `bool defender` 를
`OverheadBarSkin`(Defender/Enemy/GoalStability) enum 으로 바꾼다. 스킨이 3종이 되는 순간
bool 은 거짓말을 시작한다. 반면 레이어의 `SetUnit(Entity, bool defender, …)` 는 **그대로
둔다** — 거기서의 bool 은 스킨 수가 아니라 *유닛의 진영*을 뜻하고, 안정도는 애초에 엔티티가
없어 자기 진입점(`SetStability`)으로 들어온다. 레이어가 bool→enum 을 번역한다. 덕분에
프로덕션 호출처 2곳(`BattleBridge`)과 테스트 4곳(`UnitOverheadUiLifecycleTest`)이 불변이다.

**2. 타워 스킨은 신규 직렬화 필드다** — 기존 SO 는 `defender`/`enemy` 두 `BarSkin` 만 갖는다
(`UnitOverheadUiStyle.cs:66,79`). 3번째 `BarSkin` + 접근자를 추가하고 라이브 asset 을 저작한다.
`fullHealthAlpha` 는 `BarSkin` 에 이미 있어 재사용하지만(만피에도 1.0 으로 상시 노출), **수치
라벨 스타일은 새 필드**다. 값 방향: 더 큰 `height`·`maxWidth`, 구별되는 `frame`/`track`.
타일 게이지의 `hideWhenFull` 규칙은 적용하지 않는다 — 안정도는 판의 상태판이라 항상 읽혀야 한다.

**3. 안정도 전용 진입점** — 안정도는 ECS 엔티티가 아니므로 `SetUnit(Entity, ...)` 을 쓸 수
없다(`Entity.Null` 을 즉시 거절한다, `:57`). 레이어에 **골 인덱스로 키잉하는** 슬롯을 따로
둔다: `SetStability(int goalIndex, float ratio, string valueText, Vector2 screenAnchor,
float tileScreenWidth)` + `HideStability()`. 유닛 풀의 `_seen`/`EndFrame` 수명주기와 섞지
않는다 — 안정도 바는 전투 중 상시 존재하고 페이즈 경계에서만 사라진다. (레이어가 "ECS 를
모른다"는 기존 주석과도 이쪽이 정합이다.)

**4. 앵커** — 골 셀마다 바 1개. 월드 좌표는 primary 골은
`TilemapMapView.TryGetGoalVisualAnchor`(구조물 기준), 나머지 골은 `CellCenterToWorld` 로 얻고
스타일의 높이 오프셋을 더한다. 사막 테마는 `goalStructureProp` 이 비어 있어 앵커가 셀 중심으로
폴백한다 — 크래시가 아니라 위치가 낮아지는 것뿐이다. 스크린 변환은 기존 유닛 경로와 같은
카메라 규약을 따른다(`TryGetUnitScreenAnchor` 는 엔티티 기반이므로 월드 좌표용 변환이 필요).

**5. 표시값** — 골이 2개면 두 바가 **같은 값**을 밀살 표시한다(안정도는 판 전체 공유 1값).
Battle 페이즈에서만 보이고 Placement/Tally/Result 에서는 숨는다.

## 완료 기준

- [ ] 컴파일 통과(테스트 어셈블리 포함), 콘솔 에러/경고 0
- [ ] Play: 만피에서도 바가 보이고 수치가 바 위에 뜬다
- [ ] Play: 유출로 안정도가 깎이면 바와 수치가 같이 줄고, 유닛 바와 한눈에 구별된다
- [ ] Play: 골 2개 맵에서 두 바가 같은 값을 보여준다
- [ ] Play: Placement/Result 에서 바가 보이지 않는다
- [ ] PlayMode `UnitOverheadUiLifecycleTest` 통과 — 유닛 바의 기존 룩·수명주기 무회귀
- [ ] 스크린샷 1장 육안 확인
