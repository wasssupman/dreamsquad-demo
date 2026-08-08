# 0 — 골 타워 = 건물형 유닛 (rev 2)

> rev 1 은 전용 `Faction` 비트 + 공유 체력 싱글턴 + 전용 피해 시스템으로 만들었다가
> 코드리뷰에서 CRITICAL 2건을 받고 되돌렸다. 아래 "rev 1 에서 지운 것" 참조.

## 목적

골 셀마다 **때릴 수 있는 건물**을 세운다. 표준 피해 경로를 그대로 타므로 이 spec 이
새로 만드는 시스템은 **0개**다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Battle/Units/GoalTowerTag.cs` (식별용 태그 하나뿐)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 타워 생성/정리, 안정도 미러, 패배 판정

## 구현

**아키타입**

```
GoalTowerTag + FactionTag{Faction.Defender} + Health + IncomingDamage + LocalTransform
```

**진영은 `Faction.Defender` 다.** 적의 base `targetMask` 가 이미
`Defender | BlockingHazard`(`BattleBridge` 적 스폰)라서 **타겟팅 코드가 한 줄도 필요 없다** —
전용 Faction 비트도, 골 도달 시 마스크를 열어주는 브리지 훅도, 도발 시스템 패치도 없다.

**`DefenderUnitTag` 는 붙이지 않는다.** 그건 "플레이어가 놓은 유닛" 축이고, 붙이는 순간
배치·코스트·카드 부착·시너지·피로도/열기·픽업·실드가 전부 딸려온다. 진영(`FactionTag`)과
유닛 태그를 분리해 쓰는 것은 Blocking 해저드의 선례와 같다.

**피해는 표준 경로다.** 공격자가 `IncomingDamage` 에 append → `DamageApplicationSystem` 이
`Health` 를 깎고 0 이면 `DeadTag` → `UnitLifecycleSystem` 이 파괴. 전용 시스템도, 공유 풀도,
미러도 없다. **타워가 사라진 것이 곧 패배 신호**다(`_goalTowerCount` 와 살아있는 수 비교).

**체력은 타워마다 자기 것**이다(2026-08-08 사용자 결정, 구 "공유 1풀" 대체). 골이 2개면
**하나라도 부서지면 패배**이고, 화면에는 가장 위험한 골(최소 체력)을 보여준다.

**생성/정리** — `StartBattle` 에서 골 셀마다 1기(`EnsureGoalTowers`), `BeginPlacement` ·
`StopBattle` · `DestroyBattleEntities`(티어다운) 3곳에서 정리. 티어다운 누락은 이 파일이
이미 세 번 겪은 사고다(`Resignation`·`AllyBuffField`·`BattleTimeScale`).

## rev 1 에서 지운 것 (과설계)

| 지운 것 | 왜 만들었었나 | 왜 필요 없나 |
|---|---|---|
| `Faction.GoalTower` 비트 + 골 도달 시 mask 부여 | 원거리 적이 골 앞에서 멈추는 것을 막으려고 | 진영을 `Defender` 로 두면 base mask 가 이미 포함 |
| `GoalTowerHealth` 싱글턴 + 공유 풀 + 미러 | "체력은 골마다가 아니라 한 풀" 결정 | 타워마다 `Health` 를 갖는 게 표준 경로 |
| `GoalTowerDamageSystem` + `[UpdateBefore]` | 미러 역산 버그를 피하려고 | 미러가 없으면 회피할 것도 없다 |
| `TauntAttackGrantSystem` 패치 | GoalTower 비트를 도발이 덮어써서 | 비트가 없다 |

리뷰의 CRITICAL 2건(생산자 대비 정렬 미선언 · `DeadTag` 경로 없다는 거짓 불변식)은 전부
이 세 축에서만 나왔다. 표준 경로를 우회하려고 만든 것들이었다.

## 완료 기준

- [ ] 컴파일 통과(테스트 어셈블리 포함), 콘솔 에러/경고 0
- [ ] Play: 판 시작 시 골 셀마다 타워가 서고, `BeginPlacement`·로비 왕복 후 남지 않는다
- [ ] Play: 적이 타워를 때리면 안정도가 줄고 0 이면 패배한다
- [ ] Play: 골 2개 맵에서 한쪽만 부서져도 패배하고, 표시는 낮은 쪽을 따라간다
