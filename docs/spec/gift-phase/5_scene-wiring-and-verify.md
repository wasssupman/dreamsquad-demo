# 5 — 씬 배선 & Play 검증

## 목적

`GiftPhaseView` 를 BattleScene 에 배선하고, 네 진입 경로(Draft/Squad/Test/Restart)에서 Gift→Placement 전이가 정상 동작함을 Play 로 검증한다. handoff 작성.

## 변경 대상

- `Assets/_Project/Scenes/BattleScene.unity` — `GiftPhaseView` GameObject 신설 + SerializeField 배선.
- `docs/spec/gift-phase/6_handoff_summary.md`(신규).

## 구현

1. BattleScene 에 `GiftPhaseView` GameObject 추가(기존 `PlacementPhaseView`/`AwakeningGaugeView` 와 형제). `[DefaultExecutionOrder]` 로 `PlacementPhaseView` 보다 먼저 진입 신호를 잡도록(unit 0 라우팅 계약) 필요 시 실행순서 지정.
2. SerializeField 배선: `GameManager`, `DreamcatcherHandController`, `PlacementPhaseView`, `AwakeningGaugeView`, `GiftConfig_Default`.
3. unit 0 에서 `PlacementPhaseView` 의 진입 신호 직접 구독을 끊었으므로, 씬에서 그 참조 정리 확인.
4. **씬 저장 위생**(lessons): dirty 씬/사용자 카메라·in-memory WIP 오염 주의. 배선은 in-memory 검증 후, 저장이 필요하면 스냅샷→checkout HEAD→delta 재적용 패턴. `SaveScene` 이 WIP 를 베이크하지 않도록.
5. UnityMCP 로 자동 배선 후 Play 검증(수작업 배선 금지 원칙).

## 완료 기준

- [ ] BattleScene 에 `GiftPhaseView` 배선 완료, 참조 누락 0.
- [ ] Play e2e: **Draft** 경로 — 픽 확정 후 선물 페이즈 연출 완주 → 배치 진입.
- [ ] Play e2e: **Squad/Test** 경로 진입에서도 Gift 정상(Test 는 fast-forward 확인).
- [ ] Play e2e: **Restart** — 재시작마다 선물 페이즈 재생·이벤트 재추첨 확인.
- [ ] 인게임 사이클 덱(핸드에 뜨는 카드)이 연출에서 확정한 12장과 일치.
- [ ] `read_console` 런타임 에러 0.
- [ ] 리뷰(two-track: 일반 code-review + 필요 시 ecs-review는 해당 없음 — ECS 변경 0) 반영.
- [ ] `6_handoff_summary.md` 작성(Commit/Implemented/Key Files/Verified/Notes/Follow-up).
