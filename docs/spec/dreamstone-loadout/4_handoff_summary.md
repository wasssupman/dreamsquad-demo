# 4 — Handoff Summary

dreamstone-loadout 구현 종료. 최신 계약은 README + 번호 문서 우선.

## Commit

- 스펙 `bd9cd16` + 시그널 프로토콜 `2a97a57`
- 0 `5a47adf` — DreamstoneData/Grade/Catalog SO + 에셋 16종 + 캡 validator (Codex 구현)
- 1 `926aea6` — SquadSave.stoneIds 4슬롯 + SetStoneSlot (Sonnet 에이전트, 이하 동일)
- 2 `5d6ef22` — SquadBuilderView 슬롯 탭→피커 모달 재편
- 3 `6d28b74` — 전투 반입 set-then-apply + axis All + PlayMode smoke
- 배선 `2289362` — stoneCatalog 씬 배선(2줄) + stoneSlotsContainer 런타임 fallback + StartSquadMatch e2e
- 육안 반영 `a691144`(렌더링 3건: 피커 캔버스 rect 0×0 / 두부 라벨 ASCII화 / TestModeButton 관통) · `d93fa82`(피커 레이캐스트 관통 — 루트 캔버스 last-sibling 재구성) · `7ab66b0`(장착 항목 딤드 + 스톤 displayName 2줄)
- REDRAFT 누수 `f4bfa09` — Codex 외부 리뷰 HIGH, 드래프트 진입점 pending 클리어 + 회귀 테스트
- 확장 5 `f23eb8f` — 개별 아이템 64종(순차 id stone_001~064) + 캐파 내 소수1자리 [상,중,중,하] 티어 (유니크 7.5/6/6/4.5 등)
- 확장 6 `140d8f6` — MOVE 폐기 → 코스트 생산속도(CostRate) 스톤 + CostRuntime.RegenRateMultiplier 배선 (배율은 매치 진입 결정 지점만, 드래프트 확정=1.0). 실측 검증: 재생률 1.24/s vs 1.00/s (비율 정확 1.24)
- 확장 7 `c51338c` — 스탯별 아이콘 4종 + ScrollRect 아이콘 피커 (타 세션 구현, 리뷰 APPROVE + 아이콘 maxTextureSize 256 조정)
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
- 리뷰: 설계 크리틱 1회(ecs-reviewer, CRITICAL set-then-apply 사전 적발) + unit별 리뷰 + unit 3 투트랙(양측 코드 APPROVE) + Codex 외부 리뷰(HIGH 1건 → `f4bfa09` 수정)
- 육안: 사용자 확인 OK (2026-07-04, UI 픽스 3라운드 반영 후). 피커 상호작용은 리그 물리클릭·상호작용 진단(레이캐스트 차단/딤드 계약 어설션)으로 게이트화.
- 스탯 검증: 혼합 등급 +17.5% 를 모디파이어 원장(슬롯 덤프)으로 확인. 라이브 "20→22" 관찰은 가디언 배치버프(+30%, 6초) + 가산(additive) 정책 + 팝업 반올림 조합으로 정합 — 스톤 정상 (기본 15 × 1.475 = 22.125).
- 코스트 생산속도 실측: 프레임 단위 누적 측정(이산 점프 필터)으로 스톤 +24% → 재생률 비율 정확히 1.2400 (리그 진단 CostRegenMeasurementDiagnostic, 리그 전용).
- 주의: 리그 픽커 진단은 실사용자 디스크 프로필을 읽으므로 시작 시 스톤 슬롯을 비우는 격리 필수 (unit 7 게이트 false-fail 교훈).

## Notes

- **되돌리지 말 것**: set-then-apply(배치 전 직접 등록은 BeginPlacement 클리어에 지워짐) · MatchesDcAxis의 명시적 `All` 분기(빠지면 조용한 no-op) · 스톤 중복 장착 허용(캡 산식 전제) · CardTargetAxis.All은 enum 끝(직렬화 보존)
- REDRAFT 스톤 누수 수정(Codex 외부 리뷰 HIGH, 2026-07-04): `OnRedraftRequested`/`TryConfirm` 두 드래프트 진입점에서만 pending 스톤 클리어. **teardown 일괄 클리어는 금지** — RESTART 의 스톤 재적용 계약과 충돌한다.
- 등급 캡 표는 validator 상수(런타임 소비자 없음). 런타임 표시 필요 시 SO 승격
- 씬 배선은 YAML 정밀 삽입 2줄(에디터 잠금 상태에서 진행) — 에디터가 씬을 다시 저장해도 커밋돼 있어 안전. stoneSlotsContainer는 fallback이라 씬 authoring 선택사항

## Follow-up

- 버프 스택 표시 UX: 유닛 표시 데미지에 자체 배치버프가 섞여 스톤 효과 체감이 어려움(이번 스탯 검증에서 사용자 혼동 발생) — 필요 시 스탯 툴팁/버프 아이콘 후속
- README 후속 후보 참조: 획득/인벤토리, 강화/세트, headless auto-pick 기존 버그, EHP 표기 vs 실효 노출 등
