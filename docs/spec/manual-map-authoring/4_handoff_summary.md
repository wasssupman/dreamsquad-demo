# 4. Handoff Summary

## Commit

- `acff0abc` feat(map): 명일방주식 수동 authoring 맵 + 고정 맵 시드 기본화
- `ba4ed7e3` fix(map): 리뷰 후속 3건 — document 연결성 가드·로거 실시드·패널 무푸시 hydrate
- `12a9518d` tune(map): 15x10 스위치백 리레이아웃 + 원경 확장 + 유닛 스케일/드래그 줌아웃 튜닝

## Implemented

- 맵 소스 우선순위 확립: mapDocument > fixedMapSeed > matchSeed 랜덤 (README 계약 참조)
- `MapDocument_ArkFunnel` 15×10 실전 배선 — 현행 레이아웃은 unit 5 (3스폰 · 골 (0,0) 모서리 · 스폰별 25/33/20칸)
- 패널 초기화 = bridge 값 hydrate 만 (push 는 사용자 조작 시에만)
- 수동 document 런타임 연결성 가드 + 로그 mapSeed 실시드 기록
- 튜닝: ringRadius 10 · tilemapCharacterScale 0.504 · focusFovDelta +4°

## Key Files

- `Assets/_Project/Scripts/Data/MapGrid/MapGridBattleAdapter.cs` — document 소비 분기 + `IsUsableDocument`
- `Assets/_Project/Scripts/Data/MapGrid/MapDocument.cs` / `MapDocumentBuilder.cs` — 데이터 shape / 양방향 변환
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `BuildMapForBattle` 시드 결정·가드·`SetActualMapSeed` 호출
- `Assets/_Project/Scripts/UI/Draft/MapSettingsPanelView.cs` + `Core/DraftController.cs` — hydrate 경로

## Verified

- compile 0 err · EditMode 영향 없음 · Play e2e (적 976/976 샘플 도로 준수, 유출 완주) · 사용자 Play 확인
- 로그 실시드(-1) 실측 · 재베이크 위험 해소 확인

## Notes (되돌리면 안 되는 것)

- document 배선 중 fixedMapSeed 는 무효 — "시드 바꿨는데 맵 안 변함"은 버그가 아니라 우선순위다.
- 패널 init 에 push 를 되살리면 씬 authoring 덮어쓰기 버그가 재발한다 (unit 0/2 의 존재 이유).
- 씬 YAML 외부 수정은 에셋 임포트 전엔 무효, **사용자 씬 저장이 에디트 모드의 옛 값을 재베이크**할 수 있다 — 외부 수정 시 에디트 모드 인스턴스 동기화 필수 (0.42 재유입 실사례).
- authoring 시 BFS+2×2 자가 검증은 생략 금지 — 수동 맵의 유일한 사전 검증선이다.

## Follow-up

`docs/spec/README.md` Follow-up Backlog `(manual-map-authoring)` 항목 참조.
