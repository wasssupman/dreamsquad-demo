# 4 — Handoff Summary

dreamstone-loadout 구현 종료. 최신 계약은 README + 번호 문서 우선.

## Commit

- 스펙 `bd9cd16` + 시그널 프로토콜 `2a97a57`
- 0 `5a47adf` — DreamstoneData/Grade/Catalog SO + 에셋 16종 + 캡 validator (Codex 구현)
- 1 `926aea6` — SquadSave.stoneIds 4슬롯 + SetStoneSlot (Sonnet 에이전트, 이하 동일)
- 2 `5d6ef22` — SquadBuilderView 슬롯 탭→피커 모달 재편
- 3 `6d28b74` — 전투 반입 set-then-apply + axis All + PlayMode smoke
- 배선 `2289362` — stoneCatalog 씬 배선(2줄) + stoneSlotsContainer 런타임 fallback + StartSquadMatch e2e
- 부수 `a21d91e` — UiLayer.cs 누락 커밋 보충(HEAD 컴파일 복구, spec2 세션 몫 대리)

## Implemented

- 스톤 16종(4등급×4스탯, 수치=캡÷4) 카탈로그 + EditMode validator가 등급 예산 강제
- 스쿼드별 4슬롯 장착(중복 허용, 구버전 JSON 호환), 피커 모달 UI(유닛/스톤 동일 인터랙션)
- 게임 시작 시 아군 전체 매치 상시 버프: SetDreamstones(pending) → BeginPlacement 클리어 직후 적용(set-then-apply). ECS 변경 0
- 유니크 ATK 4개 = 정확히 +30% (additive), 드림캐쳐 카드와 공존, 재시작 정확 1회 재적용
- StartSquadMatch/StartTestModeMatch 양 경로 반입, 드래프트 폴백 미적용

## Key Files

- `Assets/_Project/Scripts/Data/Dreamstone/{DreamstoneData,DreamstoneCatalog}.cs` + `Data/Dreamstones/*.asset`
- `Assets/_Project/Scripts/Core/Profile/PlayerProfile.cs` (SquadSave.stoneIds/SetStoneSlot)
- `Assets/_Project/Scripts/UI/Outgame/SquadBuilderView.cs` (피커 모달 + 컨테이너 fallback)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (806~810 set-then-apply, 2440~ SetDreamstones), `Core/GameManager.cs` (ResolveEquippedStones)
- 테스트: `Tests/EditMode/DreamstoneCatalogTests.cs`, `Tests/PlayMode/DreamstoneCarryInSmokeTest.cs` (bridge 직접 + StartSquadMatch e2e 2종)

## Verified

- 리그(worktree + APFS 클론 Library + Unity 배치): compile clean, EditMode 12/12, PlayMode 4/4 — e2e가 프로필 장착→씬 배선 카탈로그→배치 유닛 1.30 전 구간 관통
- 리뷰: 설계 크리틱 1회(ecs-reviewer, CRITICAL set-then-apply 사전 적발) + unit별 리뷰 + unit 3 투트랙(양측 코드 APPROVE)
- **잔여(육안만)**: 스쿼드 페이지 Play 스크린샷 — 레이아웃/피커 모달 렌더(overrideSorting)/등급 색, 구 ownedContainer GameObject 정리 여부

## Notes

- **되돌리지 말 것**: set-then-apply(배치 전 직접 등록은 BeginPlacement 클리어에 지워짐) · MatchesDcAxis의 명시적 `All` 분기(빠지면 조용한 no-op) · 스톤 중복 장착 허용(캡 산식 전제) · CardTargetAxis.All은 enum 끝(직렬화 보존)
- REDRAFT 스톤 누수 수정(Codex 외부 리뷰 HIGH, 2026-07-04): `OnRedraftRequested`/`TryConfirm` 두 드래프트 진입점에서만 pending 스톤 클리어. **teardown 일괄 클리어는 금지** — RESTART 의 스톤 재적용 계약과 충돌한다.
- 등급 캡 표는 validator 상수(런타임 소비자 없음). 런타임 표시 필요 시 SO 승격
- 씬 배선은 YAML 정밀 삽입 2줄(에디터 잠금 상태에서 진행) — 에디터가 씬을 다시 저장해도 커밋돼 있어 안전. stoneSlotsContainer는 fallback이라 씬 authoring 선택사항

## Follow-up

- 스쿼드 페이지 Play 육안/스크린샷 (위 잔여 — 에디터 열리면 1분)
- README 후속 후보 참조: 획득/인벤토리, 강화/세트, headless auto-pick 기존 버그, EHP 표기 vs 실효 노출 등
