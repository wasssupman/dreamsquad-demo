# 5b — 입력 · HUD · 맵 오버레이 · 예고선 (조각 B · 2/3)

## 목적

5a 위에서 **유닛을 드래그로 놓고, 퇴근시키고, 제출하고**, 점수·타이머·코스트·트레이·배치 페이즈가 HUD 에 보인다. 판정은 한 줄도 없다 — 입력은 커맨드를 보내고 receipt 를 표시할 뿐(계약 7).

## 변경 대상

| 항목 | 경로 |
|---|---|
| 입력 | `BattleCoreUnity/Input/`: `DragPlacementInput`(← `DefenderDragPlacementController` 2,144줄의 드래그·스냅·프리뷰 — **판정은 없음**, 커맨드 `PlaceDefender` → receipt 로 성공/복귀) · `RetireInput` · `SubmitInput`. 이름에 Controller 금지(계약 12). `PlacementCellSnap`·`PlacementPointerOffset`·`PlacementSnapDebounce` 는 순수라 재사용 |
| HUD | `ScoreHudView`·`CostDisplay`·`DefenderSelector`(트레이)·`PlacementPhaseView`·`MenuPopup` 을 **읽기 모델 구독형 복사본**(`Core*`)으로. 정본 값은 `ScoreLedger`·`MatchClock`·`CostLedger`·`PlacementService` 읽기 모델. 마음 스트레스 바(`HeartStressPulse` 재사용)는 `HeartMeter` 읽기 모델 |
| 맵 오버레이 | `TilemapMapView`(1,608줄) 복사본 `CoreMapOverlay` — 격자·사거리 링·배치 가이드. 판정 호출은 `AttackReach.InReach` 코어 버전 **호출만**(제약 13) |
| 예고선 | `CoreSpawnAlertPresenter`(← `SpawnAlertPresenter` 553줄). **거점 예고선은 `AiMovePhase` 가 노출하는 생존·방패 반영 진영 배열 + `StructureChoice.NearestIndex/IsBefore` 를 호출만 한다**(M18 — unit 2 계약이 뷰로 내려온 자리). 자기 자를 만들면 「가이드 ≠ 실제 이동선」이 돌아온다 |
| 카메라 | `CameraDirector` 재사용 — 상태별 레시피 무변, 페이즈 이벤트 구독(`GamePhase` 값 append-only, X16). `MarkerPropInstaller` 재사용 |
| 씬 | `BattleCoreScene.unity` 에 입력·HUD 캔버스·오버레이·예고선 배선 |
| 테스트 | 새 PlayMode lane 에 드래그 배치 e2e(커맨드 → receipt → 뷰 스폰) · 거절 사유 표시(`RejectReason` ↔ 트레이 「소진 > 쿨타임 > 코스트」 순서 동일) |

## 구현

1. **입력 → 커맨드.** 드래그 종료 = `PlaceDefender{defIndex, anchorCell, facing}` → receipt 거절이면 트레이로 복귀 애니 + 사유 표시. 프리뷰 고스트 색은 `PlacementService.Preview(cell, def)` 읽기 전용 질의(재판정 없음 — 같은 함수).
2. **HUD.** 점수·타이머·코스트·트레이·배치 카운트다운은 읽기 모델 폴링(프레임) + 사건은 이벤트(킬 버스트·코스트 변화).
3. **감지 표식(M9).** 표식 쿨 > 억제 관계는 코어 값이고 뷰는 `Detected` 사건만 그린다. 페이로드의 대상 id 는 트레이스 전용 — 화면이 가리키지 않는다.

## 이식 제외

| 안 옮긴 것 | 이유 | 등급 |
|---|---|---|
| `_defenderByTile` 등 브리지 등록부 11 | 코어 담당자 소유(`PlacementService` 등). 뷰는 `SimEntityId → 뷰` 사전만 | 필수 규칙의 소유 이전 |
| `Controller` 이름 3종 | `*Input` 으로 | 계약 12 |
| 드래그 컨트롤러 안의 배치 판정 복제 | 커맨드 receipt 하나 | 제거 |

## 완료 기준

- [ ] 드래그 배치 e2e 초록 · 거절 사유 순서 동일.
- [ ] 예고선이 `StructureChoice` 만 호출(grep: 뷰에 최근접 거점 계산 0).
- [ ] `bridge-methods.md` 미정 중 입력·HUD·오버레이·예고선 호출 몫 닫힘, 잔량을 상태 라인에.
- [ ] 뷰·입력 코드에 판정 0 · `Unity.Entities` 0 · Controller 이름 0.
- [ ] `core-reviewer` APPROVE.
