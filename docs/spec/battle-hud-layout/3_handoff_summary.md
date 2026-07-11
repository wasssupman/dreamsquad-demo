# 3 — Handoff Summary

## Commit

- `9e6895fd` feat(ui): battle-hud-layout — 유닛 스트립·코스트 하단 중앙 통일 (unit 0~2)

## Implemented

- 유닛 배치 스트립 bottom-left(40,40) → bottom-center(0,32) — 드림캐쳐 핸드와 동일 footprint, 스트립↔핸드 플립이 좌표 점프 없는 제자리 플립이 됨
- 코스트 배지 (40,184) → (0,164) 중앙 동반 이동 (코스트-스트립 한 클러스터)
- Battle 페이즈 스트립 슬림 912×88 / Placement 풀 912×120 (`PhaseChanged` 구독, SerializeField 튜닝 가능)
- 핸드 오픈 시 배지 동반 퇴장: `CostDisplay.SetSuppressed()` — 표시 결정은 CostDisplay 가 페이즈×억제 결합으로 단독 소유, HandView 는 Open/Close/ForceClose 에서 신호만
- 씬 배선: BattleScene 의 `DreamcatcherHandView.costDisplay` ← CostDisplay (+4줄)

## Key Files

- `Assets/_Project/Scripts/UI/DefenderSelector.cs` (unit 0·2)
- `Assets/_Project/Scripts/UI/CostDisplay.cs` (unit 1 + 억제)
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs` (억제 신호)
- `Assets/_Project/Scenes/BattleScene.unity` (배선)

## Verified

- 컴파일 클린, 콘솔 에러 0
- Play 스크린샷 4상태: Placement(풀+배지) / 핸드 오픈(스트립·배지 퇴장) / Battle(슬림+배지) / Result(배지 숨김, 스크림이 스트립 덮음)
- 사용자 확인 2026-07-11 (드래그 감각 포함)

## Notes

- 배지-핸드 겹침 해법은 y 조정이 아니라 **억제**다 — y 를 올리면 평상시 코스트-스트립 클러스터가 찢어진다. 되돌리지 말 것.
- 표시 결정을 CostDisplay 내부(`_phaseVisible && !_suppressed`)에 둔 것은 PhaseChanged 구독 순서 경합 회피가 목적. HandView 에서 SetActive 를 직접 만지면 Battle→Placement 전환(핸드 열린 채) 때 배지가 새어나온다.
- 좌우 배치 결정 근거(전문가 2-패널 교차 검증)는 README "배경" 섹션 참조. 우측안이 아니라 중앙안이 채택된 이유가 기록돼 있다.
- 검증에 쓴 일회용 `HudLayoutVerifyOneShot.cs` 는 삭제됨 (드래프트 자동확정·핸드 토글·START 리플렉션 — 필요 시 git history 에서 복구).

## Follow-up

- 긴 유닛명 3연속(포이즌캐스터/파이어캐스터/아이스캐스터) 슬롯 이름 겹침 — 폰트 오버플로우, README 후속 후보에 추가됨
- 스트립 배경 플레이트 (투명 슬롯이 맵 길 위에 얹힘 — 중앙 이동으로 노출 빈도 증가)
- 7슬롯 첫 세션 게이팅 / 슬롯 시각 변별 (전문가 패널 공통 권고)
