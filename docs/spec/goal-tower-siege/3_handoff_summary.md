# 3 — Handoff (units 0~2)

## Commit

- `43f85107` unit 0 (rev 1) · `15fc28f1` unit 1 (rev 1) · `b01c748b` unit 2 (rev 1)
- **rev 2 재설계** — 이 문서와 같은 커밋. 코드리뷰(REQUEST CHANGES) + 사용자 판단으로
  전용 Faction 비트·공유 체력 싱글턴·전용 피해 시스템을 걷어내고 **건물형 유닛**으로 교체.

**푸시 안 함**(승인제). **Unity 실행 검증 없음** — dotnet 컴파일만 통과.

## Implemented (rev 2 기준 = 현재 코드)

- 골 셀마다 **건물형 유닛**: `GoalTowerTag` + `FactionTag{Faction.Defender}` + `Health` +
  `IncomingDamage` + `LocalTransform`. `DefenderUnitTag` 는 안 붙인다(플레이어 유닛 축).
- **신규 ISystem 0개.** 피해·사망이 표준 경로(`DamageApplicationSystem` → `DeadTag` →
  `UnitLifecycleSystem`)를 그대로 탄다. 타워가 사라진 것이 곧 패배 신호.
- 체력은 타워마다 자기 것, **하나라도 부서지면 패배**, 표시는 최소 체력.
- 골 도달이 죽음이 아니다: `GoalReachedMarker` 로 이벤트 1회 고정(싱크가 있을 때만 부착 —
  fail-open 유지), 엔티티 존속.
- **돌격형 예외**: `AttackState` 없는 적(Runner·Swift)은 자폭하고 `stabilityDamage` 를
  **부딪힌 골**의 타워 버퍼로(이벤트에 도달 위치 동승).
- `PastGoalTag` 타겟팅 배제 5곳 해제.
- `ProjectileHitSystem` 의 TileAoe 피해자 풀에 타워 포함 — 보스 AreaBarrage 가 타워를 못
  때리던 침묵 결함 수정(`DefenderUnitTag` 쿼리라 진영만으로는 안 걸린다).
- 코드리뷰 반영: `ResultScreen` 리더보드 디코딩 누락, 안정도 바 폴백의 sim→view 변환,
  티어다운 대칭(`DestroyBattleEntities`), `HasLiveEntityManager()` 가드, `GoalStabilityTest`
  계약 갱신.

## rev 1 에서 삭제한 것

`Faction.GoalTower` · `GoalTowerHealth`(싱글턴+순수함수) · `GoalTowerDamageSystem` ·
`GoalTowerPoolTests` · `GrantGoalTowerTarget` · `TauntAttackGrantSystem` 패치.
리뷰의 CRITICAL 2건(생산자 대비 정렬 미선언 · `DeadTag` 경로 없다는 거짓 불변식)과
HIGH 1건(브리지가 Combat 컴포넌트를 씀)이 **전부 이 축들에서만** 나왔다.

## Key Files

- `Battle/Units/GoalTowerTag.cs`(태그 하나) · `GoalReachedMarker.cs` ·
  `GoalReachedEvent.cs`(canSiege + position) · `UnitLifecycleSystem.cs`
- `Battle/Combat/AttackSystem.cs` · `Projectile/ProjectileHitSystem.cs` ·
  `Projectile/Emission/ProjectileEmitterSystem.cs` · `Projectile/ProjectileMoveSystem.cs`
- `Bridge/BattleBridge.cs` — `EnsureGoalTowers`/`DestroyGoalTowers`/`EnqueueGoalTowerDamage`/
  `SyncGoalStability`, `DrainGoalEvents` 2갈래
- `Tests/EditMode/UnitLifecycleSystemTests.cs` · `FrontmostAttackLockTests.cs` ·
  `Tests/PlayMode/GoalStabilityTest.cs`

## Verified

- `dotnet build` Wassup.Runtime / Tests.EditMode / Tests.PlayMode **오류 0**.
- 맵 실측: 6개 맵 9개 골 **전부** 인접 거리 1 에 배치칸 존재 → 공성 적을 못 잡는 맵은 없다.
- 적 12종 `attackMethod` 실측 → Runner·Swift 만 공격 수단 없음(돌격형 예외의 근거).

## Notes (되돌리면 안 되는 의도)

- **타워에 `DefenderUnitTag` 를 붙이지 말 것.** 진영(`FactionTag`)만으로 "적이 때린다" 가
  성립한다. 유닛 태그를 붙이면 배치·코스트·카드·시너지·기믹 스택이 전부 딸려온다.
- **전용 피해 시스템을 다시 만들지 말 것.** rev 1 이 그렇게 했다가 정렬 미선언(공성 피해
  무음 소실 가능)과 거짓 불변식으로 CRITICAL 2건을 받았다.
- 안정도 표시는 `CeilToInt`, 패배 판정은 원본 float. 바꾸면 "0 인데 안 죽음" 이 보인다.
- 마커는 **이벤트를 실제로 보낸 경우에만** 붙인다. 아니면 싱크 부재 창에서 유령 적이 남아
  웨이브 전멸 판정을 그 판 내내 막는다.
- 원거리 적이 골 셀에 안 들어오는 것은 **수용된 대가**다(스트레스 카운터 손실).

## 검증 체크리스트

**`docs/spec/three-minute-survival/5_verification_checklist.md` 하나로 통합했다.**
두 spec 은 하나의 룰 개편이고 코드가 맞물려 있어 따로 검증할 수 없다 — 여기에 사본을 두면
금세 갈린다. 공성 관련 항목(§3 "골 타워 · 공성", "rev 2 로 새로 생긴 상호작용")과
미처리 리뷰 지적(§7)이 거기 있다.

## Follow-up

- **밸런스**: 공성 DPS 가 붙으면서 안정도 20 이 순식간에 녹을 수 있다. `goalStabilityMax` 와
  적 공격력의 관계를 실측해 재조정할 것(three-minute-survival `5_verification_checklist.md` §4).
- **공성 연출**: 타워 피격에 데미지 폰트·히트 플래시가 없다(유닛이 아니라 의도). 타워가
  맞고 있다는 시각 신호가 필요한지는 Play 후 판단.
- **타워 파괴 연출**: `goalStructureProp` 은 정적 prop 이라 엔티티를 추적하지 않는다.
- **테스트 공백**: 공성 지속 피해를 고정하는 EditMode 테스트가 없다(표준 경로라 전용 테스트
  없이도 회귀 검출이 되지만, "타워가 적의 유효 타겟인가" 는 한 번 잠가둘 가치가 있다).
- 힐러의 골 수리·보스의 골 사냥은 진영 변경으로 생긴 **새 상호작용**이다. Play 후 유지/차단 결정.
