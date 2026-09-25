# 9c — 옛 규칙 복원 6건

상태: **구현 2026-09-25** — `27297a0cb` · `125d002b0` · `356596355` · `ea73d1ddd` · 행 5 `0cbb0cd31`(리드 추가 2026-09-25 → **사용자 결정 ⑦-2 로 철회** `fb0c9952c`) · 행 6 `484e950b4`(unit 9 감사 A 2026-09-25). core-reviewer **APPROVE**(2026-09-25 — 행 1~4 MEDIUM 1 = 행 5 로 해소 · LOW 1 `ClassFilter` Role 미설정 함정(옛과 같은 함정, 후속 후보) · 행 5 부록 finding 0). 플레이 4차 대기.

## 목적

unit 9 의 옛 테스트 이식이 드러낸 「옛 규칙 vs 코어」 차이 6건 중 **전투 규칙 4건**을 옛 규칙으로 되돌린다. 행 5·6 은 원본 9c 에서 **「보고만 하고 고치지 않은 차이 2 — 사용자 결정 필요」**였던 것을 리드가 닫았다 — 리드 판단은 옛 규칙 복원 방향이었고, **사용자 재확인(결정 ⑦, 2026-09-25)**: 행 5 는 **철회**(⑦-2 부가 피해는 거점도 친다 — 되돌림 `fb0c9952c`) · 행 6 은 **확정**(⑦-3). 행 5 는 9c 구현 중, 행 6 은 unit 9 감사(2026-09-25)에서 복원했다. 표현 2건(같은 입구 종별 예고선 · 예보 경로 해석)은 이 unit 밖이다 — 사용자 결정 대기, `RetiredWaveForecastPortTests` 의 `[Ignore]` 2 유지.

## 변경 대상

`Scripts/BattleCore/Phases/TickProjectilePhase.cs` · `Phases/AiMovePhase.cs` · `Phases/CombatPhase.cs` · `Move/ReachProbe.cs` · 신설 `Combat/ClassFilter.cs` · 테스트 `EditModeCore/RetiredCombatRulePortTests.cs` · `RetiredDetectionMovePortTests.cs` · `DetectionRulesTests.cs` · `ProjectileBehaviorTests.cs`.

## 구현 (옛 줄은 `7f9b496e1` 기준)

| # | 규칙(게임 언어) | 옛 줄 | 코어 줄 | 테스트 |
|---|---|---|---|---|
| 1 | 방향탄이 관통을 다 쓴 틱(또는 사거리 끝)에 튕김이 남았고 그 틱에 맞힌 적이 있으면, 마지막 피해자 자리에서 다음 적에게 **호밍·단일 착탄으로 바꿔** 튕긴다(홉 1 소비 · 감쇠) | `Battle/Combat/Projectile/ProjectileHitSystem.cs:627`·`:648-676` | `TickProjectilePhase.cs:989`(`SweepPath` 꼬리) · `:1001` `TryBounceFrom` | `RetiredCombatRulePortTests::방향탄은_관통을_다_쓰면_호밍으로_바꿔_다음_적에게_튕긴다`(`[Ignore]` 해제) |
| 2 | 적이 못 때리는 직업의 방어유닛은 **감지 후보가 아니다** | `Battle/Combat/DetectionSystem.cs:241-242`·`:313` | `Move/ReachProbe.cs:67` → `Combat/ClassFilter.cs`(공격 `CombatPhase.cs:1627` 과 같은 술어) | `RetiredDetectionMovePortTests::직업_필터_밖의_방어유닛은_발견하지_않는다`(`[Ignore]` 해제) |
| 3 | 같은 적에게 도발을 다시 걸면 남은 시간은 **긴 쪽** | `Battle/Effects/AggroStateSystem.cs:289-290` | `Phases/AiMovePhase.cs:239` | 신설 `DetectionRulesTests::도발을_다시_걸면_남은_시간은_긴_쪽이_남는다` |
| 4 | 길막(방벽)은 **어느 쪽 광역에도** 안 맞는다 — 칸 광역·스플래시·경로 스윕·튕김/재조준. 방벽을 **겨눈 직격**은 맞는다 | 칸 광역 풀 `ProjectileHitSystem.cs` `AnyDefender/AnyEnemy` · 스플래시·튕김·스윕 `:330`·`:384`·`:504`·`:651`(`OpponentUnitsOf`) · 재조준 `ProjectileMoveSystem.cs:78`(적 유닛 풀) · 옛 테스트 `GoalProjectileTests::TileAoe_BlockingHazard_IsVictimOfNeitherPool` | `TickProjectilePhase.cs:1070` `IsAreaLegal` → `:908`(칸 광역). 스플래시·스윕·튕김/재조준 후보도 같은 `IsAreaLegal`(행 5 철회 뒤 — 결정 ⑦-2) | 신설 `ProjectileBehaviorTests` 3 — `적의_칸_광역은_길막을_치지_않고_옆의_방어유닛은_친다` · `방어유닛의_칸_광역도_길막을_치지_않는다` · `적_탄의_스플래시는_길막을_치지_않지만_길막을_겨눈_직격은_맞는다` |
| 5 | ~~스플래시·경로 스윕·튕김·재조준은 유닛만 고른다~~ — **사용자 결정 ⑦-2 로 철회**(2026-09-25) — 부가 피해는 거점(마음·본능)도 친다(새 코어 동작 유지). 방벽 면제(행 4)는 그대로 | 옛 `ProjectileHitSystem.cs:330`·`:384`·`:504`·`:651`(`OpponentUnitsOf`) · `ProjectileMoveSystem.cs:78` — **옮기지 않는다**(이식 제외 ④) | `0cbb0cd31` 을 손으로 되돌림 — 스플래시·스윕·튕김/재조준 후보가 `IsAreaLegal`(방벽만 제외)로 복귀, `IsUnitPoolLegal` 삭제 | `ProjectileBehaviorTests` 5 를 결정 문장으로 뒤집음 — `적_탄의_스플래시는_마음도_친다` · `방어유닛_탄의_재조준은_거점을_고를_수_있다` · `방어유닛_탄의_튕김은_거점을_고를_수_있다` · `방어유닛의_경로_스윕은_적_거점도_친다` · `방어유닛의_칸_광역은_적_거점도_친다`(무변) |
| 6 | 관통탄이 한 틱에 여럿을 가로지르면 **진행 방향 앞(가까운 쪽)부터** 관통을 쓴다 — 관통 1 탄은 가로지른 적 중 가장 가까운 적에서 멈춘다. 같은 거리는 `SimEntityId` 오름차순(결정론) | `ProjectileHitSystem.cs:536-545`(`sweptDist` 최소부터 소비 · 「a 1-pierce shot must stop at the nearest enemy it crossed」) | `TickProjectilePhase.cs:938` `SweepPath` — 후보 수집 → `:965` 진행 방향 투영 거리 안정 정렬 → `:981` 앞에서부터 소비. 튕김 기준(`lastVictim` = 최전방)은 무변 | 신설 `ProjectileBehaviorTests::관통_1_탄은_한_틱에_가로지른_적_중_가까운_쪽에서_멈춘다`(먼 적이 작은 id) |

이식 제외: ① 1번의 옛 「전환 때 산출물 표 떼기」 — 옛 스윕은 피해만 냈기에 홉에만 상태이상이 걸리는 비대칭을 막으려던 것이다. 코어는 스윕 피격도 같은 `Deal` 로 산출물을 얹어 그 비대칭이 없다. ② 4번 방어유닛 쪽은 원래 방벽을 안 쳤다(`DefenderMask = AnyEnemy`) — 무변. ③ 5번 옛 재조준 풀은 주인과 무관하게 **적 유닛**이었다(적이 쏜 재조준 탄이 자기편을 고를 수 있는 모양). 옮긴 것은 「유닛만」이라는 의도이고, 진영은 공격 마스크와의 교집합이 정한다 — 방어유닛 탄은 옛과 같고, 적 탄은 자기편을 고르지 않는다. 힐러처럼 아군 유닛을 겨누는 저작도 교집합이라 그대로 따라간다. **(③ 은 행 5 철회로 무효)** ④ 5번 옛 부가 피해자 풀(`OpponentUnitsOf` · 유닛만 — 거점 제외)은 **사용자 결정 ⑦-2 로 옮기지 않는다** — 스플래시·스윕·튕김·재조준은 공격 마스크에서 방벽만 뺀 풀이라 마음·본능을 친다(새 코어 동작).

**행 6 복원**(unit 9 감사 2026-09-25 — 앞선 「유지한 차이 1」 판단을 뒤집음): 9c 리드 판단은 스윕 피격을 `SimEntityId` 순으로 **유지**했다(동률 결정론 의도). 그러나 **동률 결정론과 기하 순서 규칙은 다른 문제**다 — `SimEntityId` 는 같은 거리의 순서만 정하면 되고, 누가 앞에 있는지는 기하가 정한다. 수정 전 코어는 관통 1 탄이 가까운 적을 지나 먼 적(작은 id)을 맞혔다(테스트 빨강 = 먼 적만 피해). 1번의 튕김 기준점(맞힌 적 중 최전방)은 원래 옛 정의였다.

## 완료 기준

- [x] 건마다 빨강 확인 → 수정 → 초록(헤드리스): 1 `null` · 2 `True` · 3 `2.0` · 4 `75`(2건, 방어 쪽은 원래 초록) · 5 빨강 4(마음·본능 `195` · 튕김·재조준이 거점 id 를 고름) → 초록, 대조 1 은 원래 초록.
- [x] 행 1~4(`ea73d1ddd`): 헤드리스 882/884 · EditMode 2502(선행 2) · PlayMode.Core 95/95 · `PresetBarPopupLayerTest` 2/2 · 골든 11 일치.
- [x] 행 5(`0cbb0cd31`): 헤드리스 클린 export build 0 · test **887/889**(건너뜀 2 = 표현 2건) · Check 0 · `check_ledgers.py` 0 · EditMode 3 어셈블리 **2507** 중 실패 2(선행 bomb_man·boomerang 문안) · PlayMode.Core **95/95** · 골든 Verify **11 일치** — 재굽기 없음.
- [x] 행 6(`484e950b4`): 빨강(먼 적만 피해 · 가까운 적 100/100) → 초록 · 헤드리스 클린 export build 0 · test **891/893**(9 감사 B 3 포함 · 건너뜀 2 = 표현 2건) · Check 0 · Retire.Check 0. [ ] Unity EditMode·PlayMode 코어 · 골든 Verify(Unity 게이트 뒤).
- [x] 행 5 철회(`fb0c9952c` · 결정 ⑦-2): 테스트 뒤집기 빨강 4 → 초록 · 헤드리스 클린 export build 0 · test **891/893** · Check 0 · EditMode.Core **902/904**(건너뜀 2 = 표현 2건) · PlayMode.Core **97/97** · 골든 Verify **11 일치** — 재굽기 없음.
- [x] core-reviewer APPROVE(행 1~5) · [ ] 행 6 리뷰 · [ ] 사용자 플레이 4차 · [x] 사용자 재확인(결정 ⑦ — 행 5 철회 · 행 6 확정).

리드 재검증 2026-09-25 — HEAD `d3f8d026c` 클린 export: build 0 · test 887/889(Ignore 2 = 표현 2건) · Check 0 · 장부 기본 통과 · 골든 파일 diff 0. Unity: EditMode 3 어셈블리 2505/2507(선행 2) · PlayMode 코어 95/95 · 골든 Verify 11 일치(행 1~4 뒤 · 행 5 뒤 각 1회 — 코퍼스가 다섯 경로를 구조적으로 안 탄다: bounce·classFilter·taunt·blocker·splash 저작 0). 리드 판단: 스윕 피격 순서 `SimEntityId` 순 유지(동률 결정론 선례) — **unit 9 감사에서 뒤집힘 → 행 6 복원.**

리드 재검증 2026-09-25(감사 후속 뒤, HEAD `f86772dfd`): 헤드리스 export 891/893(Ignore 2) · Unity EditMode 3 어셈블리 2509/2511(선행 2) · PlayMode 코어 97/97(G16 2 포함) · 골든 Verify 11 일치(행 6 규칙 변경에도 무변 — 코퍼스에 관통 예산 초과 장면 없음).
