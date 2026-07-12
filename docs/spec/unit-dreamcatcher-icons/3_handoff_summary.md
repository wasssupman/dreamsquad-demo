# 3 — Handoff Summary

## Commit

- `19adbe75` — unit 0: `DreamcatcherHandController` 부착 목록 읽기 API + `AttachmentsChanged`
- `ece5f267` — unit 1: `DcIconStripView`/`DcIconStripSpawner` + `BattleBridge.TryGetUnitViewAnchor`
- unit 2 커밋 (이 문서와 동일 커밋): BattleScene 배선 + offset 튜닝(y 2.6) + 리스트 풀 제거(아키텍트 리뷰 반영)

## Implemented

- 배치 유닛에 부착된 드림캐쳐 카드(Unit + Squad hosted)를 머리 위 미니 타로 카드 스트립으로 표시
- 아이콘 = `card.art` 재사용(신규 에셋 0), 프레임 = `UiRoundedSprite` — Squad 골드 / Unit 청록 테두리
- `AttachmentsChanged` 이벤트 구동 전체 리빌드(부착/사망 회수/Placement 리셋 3지점) — per-frame 은 앵커 추종/빌보드만
- 호스트 사망 → 카드 회수 + 스트립 소멸, Placement 재진입 → 전량 클리어
- 순수 프레젠테이션: ECS 컴포넌트/시스템/채널 변경 0, bridge 추가는 read-only 앵커 wrapper 1개

## Key Files

- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherHandController.cs` — 부착 registry(source of truth) + 이벤트/읽기 API
- `Assets/_Project/Scripts/Presentation/DcIconStripSpawner.cs` — 이벤트 리빌드 + 뷰 풀 + 프레임 스프라이트 캐시
- `Assets/_Project/Scripts/Presentation/DcIconStripView.cs` — 슬롯 렌더 + 앵커 추종/빌보드
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `TryGetUnitViewAnchor` (pull 모델 앵커 조회)
- `Assets/_Project/Scenes/BattleScene.unity` — `DcIconStripSpawner` GO (hand/bridge 배선)

## Verified

- compile 0 에러 · Play e2e: 부착→표시(farewell/thornmail/cost1_as_5), Unit/Squad 프레임 색 구분, 2슬롯 레이아웃, 사망 회수→소멸, 콘솔 클린
- 씬 YAML `hand`/`bridge` non-zero fileID 확인
- 아키텍트 리뷰(정당 5/과잉 1/위반 0) — 과잉 판정된 `_listPool` 제거 반영

## Notes

- **StatusFx(push) 와 달리 pull 모델** — bridge 가 밀어주지 않고 스포너가 `TryGetUnitViewAnchor` 로 당겨간다. 데이터 소스가 ECS 가 아니라 Mono registry 라서다. 되돌리지 말 것.
- 뷰는 ECS `DcTriggerSlot` 을 읽지 않는다 — 부착 사실의 source of truth 는 HandController registry (spec 계약 1).
- 스트립 순서 = entryId 오름차순 (리빌드 간 결정론). 부착 시각순이 필요하면 registry 구조 변경 필요.
- 리빌드 시 앵커 미해석 host 는 스킵 — 다음 이벤트에서 재시도. 디펜더는 정적이라 실질 문제 없음.

## Follow-up

- Sleep "Zz" 동시 표시 실확인(미재현) — 겹치면 offset 재튜닝. 그 외 후속 후보는 README 참조(트리거 진행도 뱃지 / 전용 icon 필드 / 부착·회수 연출 / 탭 상세).
