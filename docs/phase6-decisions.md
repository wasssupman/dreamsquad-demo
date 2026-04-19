# Phase 6 — Decisions Log

> Superseded: 확정/구현 완료 내용은 `PHASE6.md`에 통합됨. 본 문서는 히스토리/리뷰 기록으로만 유지.

코스트 관리 시스템의 구현 결정을 누적한다. PHASE6.md 스펙에서 사용자 응답으로 확정된 13개 결정 + 구현 선택들.

---

## 사용자 결정 (Pre-implementation Q&A)

1. **A1 = (b) 유닛별 차등 비용** — 약한 유닛은 싸고 강한 유닛은 비싸다. `DefenderUnitData.cost` SO 필드로 튜닝 가능.
2. **A2 = (b) 스킬별 차등 비용** — `SkillData.cost` SO 필드.
3. **A3 = (b) maxCost 상한 존재** — 호더 방지. 초기값 15.
4. **B4 = (b) 배치 페이즈 카운트다운 후 자동 시작** — 30초 제한. 시간 만료 또는 "START BATTLE" 버튼으로 전투 시작.
5. **B5 = (a) 배치 페이즈 중 스킬 비활성** — 스킬은 전투 중에만 쓸 수 있도록 SkillBar가 `GameManager.CurrentPhase == Battle` 조건을 게이트에 포함.
6. **B6 = (a) 배치 페이즈 중 regen 정지** — 배치 페이즈는 `CostRuntime.ResetToStart()`만 호출하고 `BeginRegen()`은 전투 진입 시점에 호출.
7. **C7 = (b) 배치 실패 시 시각 피드백** — 해당 타일 0.2초 빨간 플래시. MapView가 `FlashTileReject(Vector2Int)` 공개 메서드 제공.
8. **C8 = (a) 스킬 슬롯 회색 비활성** — CanAfford==false 또는 Phase!=Battle이면 `Button.interactable = false` + tint 어둡게.
9. **D9 = (a) Restart 시 Placement 재진행** — 같은 픽 유지, teardown 후 다시 30초 배치 페이즈.
10. **D10 = (a) Redraft 시 Placement 재진행** — Draft→Placement→Battle 순으로 경유.
11. **E11 = 좌하단 CostDisplay** — DefenderSelector 위쪽.
12. **E12 = 연속 float 충전** — `_current += regenPerSec * Time.deltaTime`. Update 매 프레임. Display는 세그먼트별 `fillAmount`로 부분 채움.
13. **E13 = 로그에 cost_spent 필드 추가** — PlacementLog + SkillUsageLog 양쪽.

## 초기 밸런스 수치 (튜닝 가능)

14. **유닛 코스트**: Scout=1, Archer/Ranger/Guardian=2, Bruiser/Marksman/Piercer/Bastion=3, Cannon/Sniper=4. 체력/DPS/사거리 조합 기준.
15. **스킬 코스트**: SlowField=2, RapidFire=2, PowerSurge=3. 쿨다운과 효과 강도 고려.
16. **글로벌 파라미터**: startingCost=10, maxCost=15, regenPerSec=1.0, placementPhaseDuration=30초.
17. **모두 SO 필드**: `DefenderUnitData` / `SkillData` / `CostConfig` — 코드 수정 없이 Inspector에서 튜닝.

## 구현 선택 — 데이터 구조

18. **CostConfig 독립 SO 채택**: AttackDeck 확장 대신 별도 SO(`Assets/_Project/Data/Config/DefaultCostConfig.asset`). 이유: 덱 교체와 무관하게 경제 밸런스를 재사용/공유할 수 있음.
19. **BattleLogEntry.phase 기본값 "phase6"**: 이전 Phase에서의 로그와 구분. Phase 재편 시 이 필드만 바꾸면 된다.
20. **cost_spent int**: 정수로 저장 (float 금지). 분석 시 단순하고 SO의 int cost와 1:1 매칭.

## 구현 선택 — 상태 머신

21. **GamePhase enum (GameManager에 존재)**: Briefing/Draft/Placement/Battle/Result. 명시적 상태로 "지금 무엇이 유효한가"를 모든 시스템이 공통으로 판정.
22. **`GameManager.PhaseChanged` 이벤트**: 팩터리 대신 이벤트 드리븐. CostDisplay/PlacementPhaseView 등이 각자 구독.
23. **CurrentPhase 쓰기는 GameManager.SetPhase 한 지점만**: 다른 코드가 `CurrentPhase =` 대입 금지. 전이는 단일 주관.

## 구현 선택 — BattleBridge 이원화

24. **`BeginPlacement()` + `StartBattle()` 분리**: Phase 6 이전에는 StartBattle이 ECS 초기화 + 타이머 + 스폰 시작을 한꺼번에 했음. 배치 페이즈 중에도 `PlaceDefenderAs`가 작동해야 하므로, ECS 초기화만 하는 `BeginPlacement`를 분리.
25. **`_placementAllowed` 플래그**: `PlaceDefenderAs`가 `_running || _placementAllowed`일 때 동작. 스폰/타이머/공격은 여전히 `_running`으로만 게이트.
26. **`EnsureQueriesAndQueues()` private 헬퍼**: BeginPlacement와 (필요 시) StartBattle이 공통으로 호출. 쿼리/싱글톤/NativeQueue 초기화의 중복 제거.

## 구현 선택 — UI

27. **PlacementPhaseView 런타임 빌드 패턴**: DraftView/SkillBar와 동일. 프리팹 자산 0건 원칙 유지.
28. **PlacementPhaseView는 DraftController.DraftConfirmed 구독**: DraftController가 TryConfirm에서 StartBattle을 직접 호출하지 않고 이벤트만 발행 → PlacementPhaseView가 수신해 배치 페이즈 시작.
29. **PlacementPhaseView.FinishPlacement이 StartBattle 호출**: 카운트다운 종료 또는 START BATTLE 버튼. CostRuntime.BeginRegen도 이 지점에서.
30. **CostDisplay 세그먼트 게이지**: 초기 안은 단일 가로 바였으나 사용자 요청으로 15칸 세그먼트 + 부분 채움으로 개선. 정수 단위 가독성 ↑.
31. **CostDisplay는 PhaseChanged로 show/hide**: Briefing/Draft/Result 페이즈에서 자동 숨김.
32. **회색 스킬 슬롯 색상**: 기존 쿨다운 어두운 tint(`uiTint * 0.4f`)를 재사용. 새 색상 추가 금지.

## 구현 선택 — CostRuntime

33. **MonoBehaviour + Update 기반**: float regen이 프레임 간 연속 증가하도록. `Time.deltaTime * regenPerSec`.
34. **비싱글톤**: GameManager가 SerializeField로 참조 보유. `public static Instance` 금지 (CLAUDE.md).
35. **API 5개**: Configure / ResetToStart / BeginRegen / StopRegen / TrySpend / RefundSpend / CanAfford / Current / CurrentInt / Max.
36. **Configure는 값 저장만, ResetToStart에서 current 적용**: Awake 시점에 Configure는 저장만 하고 실제 Current는 Placement 진입 시 ResetToStart로 startingCost 부여.

## 구현 선택 — 흐름 통합

37. **Restart 경로**: `BattleBridge.OnRestartRequested`가 teardown + `_placementPhaseView.BeginPlacementPhase()`. StartBattle 직접 호출하지 않음.
38. **Redraft 경로**: teardown + `draftController.BeginDraft()` → DraftConfirmed 이벤트 체인으로 자동 Placement 진입.
39. **Teardown에서 `_placementAllowed = false`**: 다음 BeginPlacement가 다시 true로.

## 검증

40. **EditMode 테스트 35/35 pass**: CostRuntimeTests 6건 신규 (Configure/ResetToStart/TrySpend/Refund/BeginRegen flag/StopRegen) + 기존 29건 회귀 없음.
41. **컴파일 에러 0**: 모든 refactor 후 콘솔 clean.
42. **로그 샘플 확인**: phase="phase6", placements[].cost_spent, skill.usages[].cost_spent 채워짐.

## 미해결 / 후속

- **P6-11 실제 플레이어 회귀 검증**: 사용자 Play 모드에서 Restart/Redraft 플로우 직접 확인 필요.
- **자율 결정 항목들**: CostDisplay 폰트/바 색상, RefundSpend는 현 Phase 6에서 호출 경로 없음(향후 캐스트 실패 롤백 시 사용 예상).
- **Phase 7 이후 확장 훅**: CostRuntime이 "보너스 지급"(특수 유닛 처치 시 +N) 메커니즘 추가 쉬움. CostConfig SO가 덱별로 교체 가능.
