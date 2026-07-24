# 5 — 선택 액션 플립북 + 이동모드 진입 전환

## 목적

이동모드 진입을 **"1초 홀드" → "탭 선택 → 좌측 플립북의 이동모드 버튼"** 으로 전환한다.
선택(탭)하면 유닛 우측엔 기존 부착 드림캐쳐 카드 패널, 좌측엔 3버튼 부채꼴 플립북
(이동모드 + 더미2)이 flip 등장한다. 이동모드 버튼 → 선택 해제 → 이동모드 진입.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DcActionFlipbookView.cs` (신규 — 좌측 부채꼴 버튼 뷰)
- `Assets/_Project/Scripts/UI/Dreamcatcher/DcInspectController.cs` (플립북 표시 + 이동모드 콜백 핸드오프)
- `Assets/_Project/Scripts/UI/DefenderRelocationController.cs` (홀드 제거 + `BeginMoveModeFor`)
- `Assets/_Project/Scripts/Bridge/BattleBridge.Relocation.cs` (entity→cell 역참조)
- 씬 배선: `DcInspect.actionFlipbook`
- 테스트: `RelocationMoveModeTest`/`RelocationPlacementSessionTest` 진입 헬퍼를 `BeginMoveModeFor` 로 교체

## 구현

1. **홀드 제거**: `DefenderRelocationController` 의 `TryBeginHold`/`TickHolding`/`_holding`/`_holdElapsed`/
   `_downScreen` 및 `Update`→hold 분기 삭제. `IsOverUi`/`BoardDragThreshold` 홀드 의존도 제거(단
   목적지 드래그 판정용 임계는 유지).
2. **`public bool BeginMoveModeFor(Entity entity, Vector2Int cell)`**: 가드(bridge/settings, 활성 비행
   없음, 쿨다운 경과, Battle 페이즈, 유닛 존재·비-busy) 통과 시 `_sourceCell/_unit/_entity` 세팅 후
   `EnterMoveMode()`. 버튼 진입이라 carried-press 없음 → `EnterMoveMode` 는 `_targetPressActive=false`
   로 시작(목적지 탭/드래그를 이후 새 press 로 받음).
3. **`DcActionFlipbookView`**: `Show(Transform anchor, Camera, bool moveEnabled, Action onMove)` /
   `Hide()`. 좌측 부채꼴 3버튼(이동=활성, 더미2=비활성 시각). flip 등장(스케일+회전 stagger).
   유닛 앵커 좌측 배치 + LateUpdate Follow(DcInspectPanelView 선례). 버튼만 `raycastTarget=true`.
   절차적 아트(`UiRoundedSprite`/원형). Entity/Bridge 모름 — 컨트롤러가 앵커·콜백 주입.
4. **`DcInspectController.Select`**: 카드 패널 표시에 이어 `actionFlipbook.Show(anchor, cam, moveEnabled:true, OnMovePressed)`.
   `Close`/`Hide` 에 플립북 Hide 동반. `Blocked()`/재탭/사망 정리 경로 모두 커버.
5. **`OnMovePressed`**: `var e=_selected; TryResolveCell(e, out cell); Close(); relocation.BeginMoveModeFor(e, cell);`
   순서(Close 가 select 슬로모/줌 해제 → relocation 이 자기 것 새로 잡음).
6. **`BattleBridge.TryGetDefenderCell(Entity, out Vector2Int)`**: `_defenderByTile` 역스캔(소규모 그리드,
   비용 무시). relocate seam 과 같은 파일.

## 완료 기준

- [x] 컴파일 클린 + 씬 배선(`DcActionFlipbook` GO + `DcInspect.actionFlipbook` fileID 비영, labelFont 한글폰트 배선)
- [x] 홀드 제거: `BeginMoveModeFor` 가 유일 진입, 홀드 코드/필드/`holdSeconds` 삭제
- [x] PlayMode: `BeginMoveModeFor` → `InMoveMode`(+쿨다운/타임아웃) `RelocationMoveModeTest`, 목적지 탭/드래그
      커밋·본인 취소·점유 reject·비행/재전개 `RelocationPlacementSessionTest`, 활성비행 중 재진입 거부 회귀.
      EditMode 7 + PlayMode relocation 5 전부 통과.
- [ ] **사용자 Play 시각 확인 (수용 게이트)** — 원격이라 보류. 확인: 유닛 탭 → 우측 카드 패널 +
      좌측 플립북(이동+더미2) flip 등장 / 이동모드 버튼 탭 → 선택 해제 후 이동모드 진입 / 더미 무반응.
      *(플립북 버튼의 실제 탭 수신·등장연출·부채꼴 배치는 EventSystem 클릭이라 자동 재현 불가 — BeginMoveModeFor
      로직만 자동 검증됨. 버튼→콜백 wiring 은 코드 리뷰 수준.)*

2026-07-24 자동 검증 통과 (EditMode 7 + PlayMode 5). 사용자 시각 확인만 남음.

## 후속 후보

- 더미 버튼 실제 액션(판매/방향 재지정/승급 등)
- 플립북 아트 정식화(절차적 → 스프라이트)
- 등장연출 사운드
