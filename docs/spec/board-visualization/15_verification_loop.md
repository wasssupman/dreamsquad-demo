# 15. Verification Loop

## 목적

rev2 시각 목표를 확인할 수 있는 반복 가능한 체크리스트를 고정한다. 각 구현 커밋 후 이 체크리스트를 돌려 회귀를 잡는다.

## 변경 대상

- `docs/spec/board-visualization/15_verification_loop.md` (본 문서가 checklist source of truth)
- 필요 시 `docs/spec/board-visualization/verification/` 에 스크립트/스크린샷 보관
- PlayMode test 1 개: `BoardVisualPlan` 기반 smoke

## 체크리스트

### Static Contract

- [ ] `grep "map.TileAt"` in `Assets/_Project/Scripts/Core/MapView.cs` → 0
- [ ] `grep "MapTileType"` in `BackgroundPropPlacer.cs` → 0
- [ ] `grep "FloodFillRegion"` in `BackgroundPropPlacer.cs` → 0
- [ ] `BoardShapeType` 열거 항목 수 = 16 (`None` 제외, inner corner enum 없음)
- [ ] `BoardDecorAnchorType` 열거 항목 수 = 5
- [ ] EditMode 테스트 전원 통과

### Deterministic

- [ ] 동일 seed × 동일 map × 2 회 `BoardVisualPlan` 생성 → 셀 mask/anchor 비트 동일
- [ ] 동일 plan × 2 회 `BackgroundPropPlacer.Generate` → placement 시퀀스 비트 동일

### Runtime

- [ ] `BattleBridge.StartBattle` 100 회 반복: Persistent leak 경고 0
- [ ] 동일 seed 2 회 재생성 시 placement diff 없음

### Visual (screenshot review)

- [ ] Place L자 경계에 inner corner sprite 가 보인다
- [ ] Env region 내부에 2 종 이상 surface variation 이 관찰된다
- [ ] 인접 Env region 경계가 hard cut 이 아니고 1 셀 폭 blend 가 있다
- [ ] 같은 prop family 가 cluster 또는 scatter 분포로 보인다 (복붙 인상 없음)
- [ ] rotation/scale jitter 로 같은 prop 의 반복감이 해소된다

### Occupancy

- [ ] 모든 placement footprint 가 Env 셀만 점유
- [ ] placement 간 footprint overlap 0
- [ ] footprint 가 map bounds 초과 0

### Theme Swap Smoke

- [ ] forest → volcano 로 교체 후 렌더 에러 0
- [ ] 각 theme 에서 필수 카테고리 (14 번) 빠짐 0

## 재현 가이드

1. Clean game view 캡처
   - Play 진입, UI canvas 임시 비활성화, 동일 seed
   - 해상도 고정 (예: 1920×1080)
   - `Assets/Screenshots/board_visual_<topic>_<rev>.png` 저장
2. Scene view 캡처
   - 같은 씬 뷰 프레이밍으로 비교
3. 자동 체크
   - EditMode 테스트 단일 실행
   - `grep` 기반 static contract 검사 스크립트 (예: `tools/check_board_contract.sh`, optional)
4. Leak 검증
   - `NativeLeakDetection.Mode = Full` 로 100 회 반복

## 완료 기준

- 본 문서가 체크리스트 source of truth 로 참조됨.
- 체크리스트의 각 항목이 커밋 전에 최소 한 번 돌아갈 수 있는 상태.
- PlayMode smoke test 1 개가 `Assets/_Project/Tests/PlayMode/` 에 추가됨 (optional, 시각 관련 결정론만 검사).

## 주의

- screenshot 은 spec 완료 시점에 소량만 커밋 (저장소 용량). rev2 마감 전 과잉 스크린샷은 삭제 정리.
- visual review 는 주관적이지만, 위 항목이 한 문장 이상으로 관찰 가능해야 "통과" 로 기록한다.

확인 일자: 2026-04-24 / 커밋 해시: 813f6d2
