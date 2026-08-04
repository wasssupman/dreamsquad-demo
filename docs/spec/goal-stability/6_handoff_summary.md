# Goal Stability — Handoff Summary

## Commit

- `453ec744` unit 0 — per-goal 안정도 authoring (+ spec 폴더 신설, ecs 설계 critic 1회 반영)
- `d3b672b9` unit 1 — `Faction.Goal` + 골 엔티티 스폰/teardown (행동 변화 0)
- `297ff7ea` unit 2 — 공성 게이트 + 근접 개통 + goal 최후순위
- `f30cd71e` unit 3 — 원거리 TileAoe 골 포함 + walk-only grant + 도발 병존
- `e1216f38` unit 4 — `GoalCollapsedEventsSingleton` (28번째 채널) + CLAUDE.md 채널 목록
- `4db42f17` unit 5 — 유닛식 오버헤드 안정도 체력바 + 붕괴 VFX + 씬 wiring

## Implemented

- `MapDocument.goalMaxStability[]` per-goal authoring — 부재/길이 불일치 = 전 골 0 = 현행 무변화(무형 롤아웃, 기존 5맵 무마이그레이션)
- M>0 골 = blocking hazard 동형 전투 엔티티(`GoalPoint`+`FactionTag{Goal}`+`Health`+`IncomingDamage`)
- 공성: 살아있는 골 셀에서 유출(PastGoalTag) 봉인, 모든 적이 골을 **최후순위** 타겟으로 공격
- walk-only(Runner/Swift) 스폰 grant(mask=Goal) + 도발 `previousTargetMask` 원복 병존
- 원거리: 직격 호밍은 타겟 직결이라 무변경 개통, Defender 풀 TileAoe 에 골 포함
- 붕괴(안정도 0) = 엔티티 파괴 = 즉시 현행 유출 지점 전환. 이벤트 채널은 연출/로그 전용
- 유닛식 오버헤드 안정도 바(anchor 직접 투영) + 붕괴 록버스트 VFX(`SpawnGoalCollapse`)

## Key Files

- `Assets/_Project/Scripts/Data/MapGrid/MapDocument.cs` · `MapDocumentBuilder.cs` · `GeneratedMap.cs`
- `Assets/_Project/Scripts/Battle/Units/GoalPoint.cs` · `GoalCollapsedEvent(sSingleton).cs` · `UnitLifecycleSystem.cs`
- `Assets/_Project/Scripts/Battle/Movement/MovementSystem.cs` — 공성 게이트
- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — 최후순위 · `TauntAttackGrantSystem.cs`
- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileHitSystem.cs` — TileAoe 골 풀
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — SpawnGoalEntities·walk-only grant·드레인·게이지 sync
- `Assets/_Project/Scripts/Presentation/VfxSpawner.cs` — SpawnGoalCollapse
- `Assets/_Project/Editor/MapPainterWindow.cs` — per-goal M 입력

## Verified

- EditMode: 골 전용 7클래스(왕복 4·브리지 4·게이트 4·최후순위 3·도발 3·투사체 3·붕괴 2) + 인접 스위트(AttackSystem/Movement/Patrol/Aggro/투사체/MapGrid) 무회귀 — unit 별 28~96 규모 전부 green
- 사용자 Play 확인(unit 별): 공성·유출 0·붕괴→유출 전환·도발 병존·오버헤드 바·붕괴 VFX
- 씬 wiring: BattleScene 미로드 YAML 직접 배선(`goalCollapsePrefab` non-zero fileID), SaveScene 미사용

## Notes (되돌리면 안 되는 의도)

- **붕괴 신호 = 골 엔티티 부재.** 별도 플래그/동기화 금지 — 게이트·게이지·유출 전환이 전부 이 하나에 걸린다.
- 골은 최후순위 + **FocusUntilDead 잠금 금지**(리뷰 M3). general-dead 루프의 `WithNone<GoalPoint>` 제거 금지(리뷰 M1 — 이벤트 유실/이중 파괴).
- `TauntAttackGranted.previousTargetMask` 0/비0 의미 구분(리뷰 M2): 0 = 통째 부여·제거(무공격 적 현행 경로).
- 골 엔티티에 `CcEffect`/`StatModifierSlot` 버퍼를 부여하지 않는다(`CcApplySystem` 버퍼 부재 crash 전제 — README 계약).
- **실맵 M 값은 검증용 임시(전 골 300)가 미커밋 dirty 상태** — 콘텐츠 값은 밸런싱 결정 후 별도 커밋. 이 dirty 를 무관 커밋에 딸려 보내지 말 것.

## Follow-up

→ `docs/spec/README.md` Follow-up Backlog **"목표지점 안정도"** 그룹 참조 (콘텐츠 M 밸런싱·점수 재균형·데미지 넘버·붕괴 아트·골 힐·예고선).
