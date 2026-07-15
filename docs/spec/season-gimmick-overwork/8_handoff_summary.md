# 8. Handoff Summary — season-gimmick-overwork

세션 인계 지도. 최신 계약은 README + 번호 문서가 우선.

## Commit

- `dfae1cd2` unit 0 — Fatigue 스택 + 번아웃 임계 룰 SO
- `c465c6a7` unit 1 — MaxHealthMul 모디파이어 + 번아웃 3룰
- `de6068e5` unit 2 — 기믹 프레임 (SO + SeasonData.gimmick + 주입 seam)
- `4ded63e1` unit 3 — 피로도 누적 시스템 (룰 1)
- `a5d89682` unit 4 — 레드불 픽업 아키타입 + 주기 스폰
- `30d70b4e` unit 5 — 픽업 소비 → 라스트런 (룰 2)
- `c5040abc` unit 6 — 레드불 픽업 뷰
- `529b9d09` unit 7 — 야근 시즌 정식화 + 픽업 동시개수 상한

## Implemented

- 시즌→기믹 프레임: `SeasonData.gimmick`(null=무변화), `OverworkGimmickData`(SO) → `OverworkGimmickConfig`(blittable) 를 BattleBridge 가 주입. 룰 시스템은 `RequireForUpdate` self-gate.
- 룰 1 (피로도→번아웃): 배치 유닛 10초마다 `StackKind.Fatigue` +1 → 5스택 시 기존 StackModifier 임계 파이프라인이 공속/공격력/최대체력 ×0.8 (15s) 발동, Consume 로 스택 리셋 후 재누적.
- 룰 2 (레드불→라스트런): 5초마다 Walk∪Place 셀에 레드불 스폰(결정론 rng, 동시상한 6·수명 20s), 유닛(적 통과/defender 배치) co-location 소비 → 공속 ×1.5(5s) → 5초 후 최대체력 ×0.1(영구).
- `StatKind.MaxHealthMul` 신설: Effects 가 배율 결정, Units 의 `MaxHealthScaleSystem` 이 Health.max 소비/클램프 (맥락 경계 유지).
- 야근 시즌(`season_overwork`, forest 테마) = 정식 defaultSeason. forest 시즌은 클린 baseline.
- 레드불 뷰: BattleBridge poll-reconcile + `PickupPresenter` 절차적 플레이스홀더.

## Key Files

- 데이터: `Data/Gimmick/{GimmickData,OverworkGimmickData}.cs`, `Data/Season/SeasonData.cs`, SO: `Data/Gimmick/{Gimmick_Overwork,StackModifier_Fatigue}.asset`, `Data/Season/season_overwork.asset`
- Effects: `Battle/Effects/{OverworkGimmickConfig,FatigueAccrual,FatigueAccrualSystem,Pickup,PickupSpawnState,PickupSpawnSystem,PickupConsumeSystem,LastRun,LastRunSystem,PickupPresenter}.cs`, `Modifiers/{ModifierTypes,ModifierStats,ModifierStatsAggregateSystem}.cs`
- Units: `Battle/Units/{Health,MaxHealthScaleState,MaxHealthScaleSystem}.cs`
- Bridge: `Bridge/BattleBridge.cs` (CreateGimmickConfigIfActive, BuildPickupSpawnState, ReconcilePickupViews, DebugLog*)

## Verified

- compile 클린, EditMode 775 pass (ModifierMath + HealthScaleMax).
- Play 실측: 번아웃 3스탯 ×0.8 + HP 클램프 → 15s 해제 재누적. 레드불 소비→(5s)→crash ×0.10 (Editor.log). 픽업 4개 안정, 스폰 셀 전부 Walk/Place(off-tile 0), 뷰 렌더(스크린샷). `season=S_Overwork` 주입. 에러 0.

## Notes

- **소비/crash telemetry 로그** 위해 `PickupConsumeSystem`/`LastRunSystem` 은 non-Burst. 저빈도라 무해하나 PickupConsumeSystem 은 매 프레임 유닛 순회 → 후속서 Burst화 + 로그 gate 검토.
- 라스트런 StatModifier `origin=Unspecified` — `ModifierOrigin` enum 은 unit-buff-debuff-aura 세션 소유라 전용 `Gimmick` 값은 조율 후 추가.
- 번아웃/라스트런 상태 아이콘은 별도 제작 안 함 — 임시 버프/디버프라 그 세션의 Buffed/Debuffed 오라가 자동 분류.
- 피로도/픽업은 placement 페이즈에도 누적/스폰(sim 진행 시). running-only 게이팅은 후속 튜닝.
- BattleBridge 는 다른 세션과 공유 — 커밋 시 hunk 선별 스테이징 필요. season_S1_forest 는 클린(기믹 null).

## Follow-up

- 감정효과(희·노·애·락) 상태별 구현체 설계 — 별도 spec (본 spec 범위 밖, 분류만 인지).
- PickupConsume/LastRun Burst화 + telemetry 로그를 에디터 gate/이벤트로 분리.
- 전용 `ModifierOrigin.Gimmick` (그 enum 소유 세션과 조율).
- 피로도/픽업 placement-phase 게이팅 (running-only) 튜닝.
- 레드불 정식 아트 + 소비/스폰 VFX + 뷰 지면 grounding(원근 부유 완화).
- 피격 시 피로도 누적 소스 추가 (야근 변형 룰).
- 매치 시작 UI 기믹 배지/설명.
