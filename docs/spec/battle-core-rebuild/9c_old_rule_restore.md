# 9c — 옛 규칙 복원 4건

상태: **구현 2026-09-25** — `27297a0cb` · `125d002b0` · `356596355` · `ea73d1ddd`. core-reviewer · 플레이 4차 대기.

## 목적

unit 9 의 옛 테스트 이식이 드러낸 「옛 규칙 vs 코어」 차이 6건 중 **전투 규칙 4건**을 옛 규칙으로 되돌린다(결정 ⑥ — 「기획 그대로」 기본값. 사용자가 뒤집으면 되돌린다). 표현 2건(같은 입구 종별 예고선 · 예보 경로 해석)은 이 unit 밖이다 — 사용자 결정 대기, `RetiredWaveForecastPortTests` 의 `[Ignore]` 2 유지.

## 변경 대상

`Scripts/BattleCore/Phases/TickProjectilePhase.cs` · `Phases/AiMovePhase.cs` · `Phases/CombatPhase.cs` · `Move/ReachProbe.cs` · 신설 `Combat/ClassFilter.cs` · 테스트 `EditModeCore/RetiredCombatRulePortTests.cs` · `RetiredDetectionMovePortTests.cs` · `DetectionRulesTests.cs` · `ProjectileBehaviorTests.cs`.

## 구현 (옛 줄은 `7f9b496e1` 기준)

| # | 규칙(게임 언어) | 옛 줄 | 코어 줄 | 테스트 |
|---|---|---|---|---|
| 1 | 방향탄이 관통을 다 쓴 틱(또는 사거리 끝)에 튕김이 남았고 그 틱에 맞힌 적이 있으면, 마지막 피해자 자리에서 다음 적에게 **호밍·단일 착탄으로 바꿔** 튕긴다(홉 1 소비 · 감쇠) | `Battle/Combat/Projectile/ProjectileHitSystem.cs:627`·`:648-676` | `TickProjectilePhase.cs:989`(`SweepPath` 꼬리) · `:1001` `TryBounceFrom` | `RetiredCombatRulePortTests::방향탄은_관통을_다_쓰면_호밍으로_바꿔_다음_적에게_튕긴다`(`[Ignore]` 해제) |
| 2 | 적이 못 때리는 직업의 방어유닛은 **감지 후보가 아니다** | `Battle/Combat/DetectionSystem.cs:241-242`·`:313` | `Move/ReachProbe.cs:67` → `Combat/ClassFilter.cs`(공격 `CombatPhase.cs:1627` 과 같은 술어) | `RetiredDetectionMovePortTests::직업_필터_밖의_방어유닛은_발견하지_않는다`(`[Ignore]` 해제) |
| 3 | 같은 적에게 도발을 다시 걸면 남은 시간은 **긴 쪽** | `Battle/Effects/AggroStateSystem.cs:289-290` | `Phases/AiMovePhase.cs:239` | 신설 `DetectionRulesTests::도발을_다시_걸면_남은_시간은_긴_쪽이_남는다` |
| 4 | 길막(방벽)은 **어느 쪽 광역에도** 안 맞는다 — 칸 광역·스플래시·경로 스윕·튕김/재조준. 방벽을 **겨눈 직격**은 맞는다 | 칸 광역 풀 `ProjectileHitSystem.cs` `AnyDefender/AnyEnemy` · 스플래시·튕김·스윕 `:330`·`:384`·`:504`·`:651`(`OpponentUnitsOf`) · 재조준 `ProjectileMoveSystem.cs:78`(적 유닛 풀) · 옛 테스트 `GoalProjectileTests::TileAoe_BlockingHazard_IsVictimOfNeitherPool` | `TickProjectilePhase.cs:1067` `IsAreaLegal` → `:867`·`:908`·`:948`·`:1118` | 신설 `ProjectileBehaviorTests` 3 — `적의_칸_광역은_길막을_치지_않고_옆의_방어유닛은_친다` · `방어유닛의_칸_광역도_길막을_치지_않는다` · `적_탄의_스플래시는_길막을_치지_않지만_길막을_겨눈_직격은_맞는다` |

이식 제외: ① 1번의 옛 「전환 때 산출물 표 떼기」 — 옛 스윕은 피해만 냈기에 홉에만 상태이상이 걸리는 비대칭을 막으려던 것이다. 코어는 스윕 피격도 같은 `Deal` 로 산출물을 얹어 그 비대칭이 없다. ② 4번 방어유닛 쪽은 원래 방벽을 안 쳤다(`DefenderMask = AnyEnemy`) — 무변.

**보고만 하고 고치지 않은 차이 2**(범위 밖 · 사용자 결정 필요):
- 옛 스윕은 한 틱에 가로지른 적을 **앞에서부터** 맞혔다(`:536-545` front-most). 코어는 `SimEntityId` 순이다 — 관통 예산보다 많은 적을 한 틱에 가로지를 때만 누가 맞는지 갈린다. 1번의 튕김 기준점은 옛 정의(맞힌 적 중 최전방)로 맞췄다.
- 옛 스플래시·스윕·튕김 풀은 유닛만이었다(`OpponentUnitsOf`). 코어는 공격 마스크라 적의 스플래시가 **골(방어 마음)** 을 친다(9b 표 행 103 「광역 풀별 거점 포함/제외」 과 같은 축).

## 완료 기준

- [x] 건마다 빨강 확인 → 수정 → 초록(헤드리스): 1 `null` · 2 `True` · 3 `2.0` · 4 `75`(2건, 방어 쪽은 원래 초록).
- [x] 헤드리스 클린 export(`ea73d1ddd`): build 0 · test **882/884**(건너뜀 2 = 표현 2건) · Check 0 · `check_ledgers.py` 기본 통과.
- [x] Unity EditMode: .Core **893/895** · .Assets + EditMode 1607 중 실패 2(선행 bomb_man·boomerang 문안) — 합계 2502(기준 2498 + 신설 4).
- [x] PlayMode.Core **95/95** · 아웃게임 `PresetBarPopupLayerTest` 2/2.
- [x] 골든 Verify **11 일치** — 재굽기 없음.
- [ ] core-reviewer · 사용자 플레이 4차.
