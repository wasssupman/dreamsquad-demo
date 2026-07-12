# 2 — Wiring + Play Validation

## 목적

`DcIconStripSpawner` 를 BattleScene 에 배선하고 Play e2e 로 검증 질문에 답한다.

## 변경 대상

- `Assets/_Project/Scenes/BattleScene.unity` — `DcIconStripSpawner` GameObject + `hand`/`bridge` SerializeField
- `Assets/_Project/Scripts/Presentation/DcIconStripSpawner.cs` — offset 기본값 Play 튜닝

## 구현

- 씬 루트에 `DcIconStripSpawner` GO 생성, `hand`(DreamcatcherHandController)·`bridge`(BattleBridge) 참조 연결. `billboardCamera` 는 Camera.main 폴백 사용(StatusFxSpawner 선례).
- YAML 검증: `hand: {fileID: 2054561897}`, `bridge: {fileID: 1939688408}` non-zero 확인.
- Play 검증은 일회용 MenuItem 드라이버(placement 페이즈 진입 → 디펜더 배치 → 게이지 강제 충전 → Unit/Squad 카드 CommitUnit/CommitSquad)로 구동 후 삭제.

## 완료 기준

- [x] 부착 → 유닛 머리 위 미니 카드 표시 (farewell/thornmail/cost1_as_5 시각 확인)
- [x] Unit/Squad 프레임 색 구분 (청록 vs 골드, 2슬롯 가로 레이아웃 동작)
- [x] 호스트 사망 → 카드 회수 + 스트립 소멸 (전투 중 디펜더 사망 후 스트립 비활성 + 같은 카드 재커밋 성공으로 확인)
- [x] Placement 리셋 시 전량 클리어 (BeginPlacementPhase → registry clear → AttachmentsChanged 경로)
- [x] 콘솔 에러/워닝 0
- [x] offset 튜닝: y 2.1 → 2.6 (머리 겹침 해소), 씬+코드 기본값 동기화

확인 2026-07-12 — Play e2e 통과 (스크린샷: 세션 기록). 주의: Sleep "Zz"(StatusFx) 와의 실제 동시 표시는 미확인 — 겹침 보이면 offset 재튜닝.
