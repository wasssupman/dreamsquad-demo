# Unit 13 — Mono 소비: 어그로 아이콘 (AggroIconSpawner / View)

> 아키텍처 비의존 실증. 같은 `Aggroed` 상태를 **두 번째 아키텍처(MonoBehaviour View)가 소비** — ECS 는 이동/전투로, Mono 는 아이콘으로 각자 소비만.

## 목적

어그로된 적 머리 위에 "어그로 끌림" 아이콘을 띄운다. 상태 구동(persistent) — `Aggroed` 있는 동안 표시, 해제 시 회수.

## 변경 대상

- (신규) `Assets/_Project/Scripts/Presentation/AggroIconSpawner.cs`
- (신규) `Assets/_Project/Scripts/Presentation/AggroIconView.cs`
- (신규) `Assets/_Project/Data/.../AggroIconStyle.asset` (SO)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (reconcile 배선 + SerializeField)
- 씬: BattleBridge 에 `AggroIconSpawner` 참조 배선

## 구현

**패턴 = `EnemyHitBarSpawner` 복제**: `AggroIconSpawner` 가 `Dictionary<Entity, AggroIconView>` + `Queue` 풀 보유. `AggroIconView` 는 적 뷰 앵커 위 빌보드(카메라 대면).

**상태 구동 reconcile (히트바처럼 일회성 아님)**: BattleBridge 가 이미 매 프레임 유닛 뷰↔ECS 동기화 중 → 그 루프에서:
- `Aggroed` 보유 적 → `Spawner.Ensure(entity, anchor)` (없으면 생성/풀에서, 있으면 위치 갱신).
- `Aggroed` 잃은(맵에 있으나 상태 없는) 적 → `Spawner.Hide(entity)` (풀 반환).
- 성능: O(aggroed + dict) per frame — 체력바 reconcile 와 동일 특성, 현 규모(수십 마리)에서 무시 가능(critic L2).

**SO(`AggroIconStyle`)**: 스프라이트 · Y 오프셋 · 크기 · (선택)펄스. 하드코딩 금지(`HealthDisplayStyle` 선례). 미할당 시 1회 LogError + 스킵.

**정리**: 전투 teardown 시 `Clear()`(`EnemyHitBarSpawner.Clear` 대칭).

## 완료 기준

- [ ] 어그로된 적 머리 위 아이콘 표시(Play 스크린샷 육안).
- [ ] 해제(가디언 사망) → 아이콘 즉시 사라짐.
- [ ] 적 사망/디스폰 → 아이콘 회수(잔류 없음).
- [ ] 아이콘 파라미터가 전부 SO 에서(하드코딩 0). teardown 후 잔여 아이콘 0.

코드/에셋 완료: 2026-07-09 (Spawner/View/Style + BattleBridge reconcile 배선 + `AggroIconStyle.asset`(붉은 주황, 절차적 "!" 폴백) / 커밋 `b84b6887` + asset).
씬 배선 완료: 2026-07-09 (BattleScene 에 `AggroIconSpawner` GO + `style`=AggroIconStyle + `BattleBridge.aggroIconSpawner` 연결, reflection 검증·저장 / 커밋 `5ea07f6c`). Play 진입 에러 0. **아이콘 표시/해제 육안 스모크만 잔여**(포커스 필요, 사용자).
