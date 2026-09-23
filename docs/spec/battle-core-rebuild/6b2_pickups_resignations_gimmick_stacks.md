# 6b2 — 픽업 · 사직서 · 열기/피로 · 기믹 정의표 (조각 C · 3/4)

> 6b 와 한 unit 이었는데 둘이 합쳐 실측 2,696줄이라 나눴다. 6b 가 **판 위에 깔리는 것**이라면 여기는 **시즌 기믹의 셈판**이다 — 주워 먹는 것, 떨어지는 것, 시간으로 쌓이는 것.

## 목적

기믹 4종의 셈판이 돈다: 레드불 픽업과 그 뒤의 라스트런, 사직서 누적과 임계, 온천의 열기, 번아웃의 피로. 그리고 **그 수치 전량이 정의표에서 온다**(제약 6) — 옛 config 싱글턴 4개는 Burst 우회라 기계가 통째로 사라진다.

조각 C 의 **마지막 코어 변경**이라 골든 재굽기가 여기 붙는다.

## 경계 — 6b2 와 unit 7 의 분담

| 축 | 6b2(이 unit) | unit 7 |
|---|---|---|
| 픽업 | 개체·수명·소비 판정·소비 효과(라스트런) | 스폰 **주기**(Match 호스트 주기 바인딩) |
| 사직서 | 개체·누적 수·임계 도달 사건 | 드랍 **계기**(사망 seam) · 임계 뒤의 **운석 barrage** |
| 열기/피로 | 스택 축·임계 파생·`HeatMath` 반전 | 누적 **주기와 대상 필터**(유닛 호스트 바인딩) |

⚠ 이 분담 때문에 **6b2 끝에는 픽업·사직서·열기/피로가 라이브에서 저절로 나타나지 않는다.** 그것이 정상이고, 대신 **디버그 커맨드**로 전부 세울 수 있어야 한다(tools.md 9).

## 변경 대상

| 항목 | 경로 |
|---|---|
| 픽업·사직서 | `BattleCore/World/Pickup.cs` · `World/Resignation.cs` + `BattleWorld` 목록 2 · `Phases/TickProjectilePhase.cs` 끝(수명 · 소비 · 누적 — 옛 캡처 22~25 가 **이동 뒤**다) |
| 라스트런 | `World/CombatParts.cs` 의 **`ProgressiveStates.LastRun`** — 별도 타이머 타입을 만들지 않는다. UML §2 가 이미 `+LastRun?` 로 그 집을 지정했고(`class-diagram.md:92`), 중단 정책 표(사망·퇴근·CC)를 이행하는 함수가 거기 하나다 |
| 열기·피로 | `Effects/HeatMath.cs`(salvage) · 스택 축은 6a 의 `StackSet`/`StackRuleDef` 를 그대로 탄다 |
| 정의표 | **`Match/CardDef.cs` 의 `GimmickDef` 확장** — `GimmickKind` + 종류별 중첩 구조체(`RedBull`·`Onsen`·`Burnout`·`ClockOut`) + canonicalize |
| seam | `Phases/SeamHooks.cs` 에 `Seam.Periodic` **append**(`_Count` 앞) **+ 호출부** — `FieldPrepPhase` 끝에서 `ctx.Seams.Run(Seam.Periodic, ctx)` |
| 커맨드 | `Match/Command.cs` 에 `DebugSpawnPickup`·`DebugDropResignation`·`DebugSetStack` |
| 사건 | `CoreEvent` 45~48: `PickupSpawned`·`PickupTaken`·`ResignationDropped`·`ResignationThreshold` |
| 테스트 | `Tests/EditModeCore/`: `PickupTests`·`ResignationTests`·`HeatFatigueTests`·`SeamHookTests` 확장 |

## 구현

1. **픽업 소비는 칸 일치가 아니라 도달 판정이다**(제약 13 — 「자를 새로 만들지 않는다」). 옛 전투는 같은 셀 폴링이었다. 새 코어는 「자리에 떨어지는 것」(칸 반폭) + 소비자 몸이다. ⚠ **이것은 플레이어가 겪는 규칙을 넓힌다**(스치듯 지나가도 먹는다). 제약 13 이 예외를 배치 판정 하나로 못박았으므로 그대로 따르고, **체감 확인 대상**으로 6c 의 플레이 항목에 올린다. 재소비 락(이미 라스트런 중인 유닛은 또 못 먹는다)은 대상 필터다.
2. **라스트런은 진행형 상태다.** 픽업을 먹으면 공속 버프가 6a 의 스탯 슬롯으로 즉시 걸리고(자체 만료), **지연 crash 타이머만** `ProgressiveStates.LastRun` 이 든다 — 만료 시 최대체력의 저작 비율만큼 피해를 인박스에 넣는다. 중단 정책은 그 표를 따른다: 사망·퇴근이면 같이 사라지고, 군중 제어로는 안 멈춘다.
3. **사직서는 유닛이 줍지 않는다.** 판 위에 쌓이고 **전역 누적 수 / 임계**로 소모된다 — level 폴링이라 한 틱에 여러 번 넘을 수 있고 그것이 사양이다(rev 3 §2). 드랍과 임계의 **상대 순서는 현행 캡처 순서 그대로**(드랍은 사망·소멸 사이 = unit 7 의 사망 seam, 임계는 스택 틱 앞). 임계 도달은 사건만 내고 **운석은 unit 7** 이다.
4. **열기와 피로는 스택이다.** 둘 다 lazy-attach(스폰 경로 무수정)이고 임계 파생은 6a 의 `StackRuleDef` 를 탄다(피로 → `ModifierOrigin.Burnout`). 열기의 반전은 `HeatMath.Delta` 를 그대로 salvage — **회복은 넘치는 만큼 잘라내고**(만피 유닛 VFX 스팸 방지) **과열은 체력 1을 바닥으로 남긴다**(F10 — 열기로는 죽지 않는다). 부호만 보고 회복/피해 인박스로 라우팅한다.
5. **피로 누적은 스탯 적용 «뒤» 단계다 — 1틱 지연을 박제한다.** 옛 `FatigueAccrualSystem` 은 `[UpdateAfter(ModifierApplySystem)]`(캡처 30)이라 여기서 쌓은 피로가 **다음 프레임**에 반영됐고, 그 1프레임이 현행 동작이다(`FatigueAccrualSystem.cs:18` 이 「그대로 박제한다」고 적어 뒀다). rev 3 §4 가 이 축을 「변경 없음」으로 닫았으므로 **단계 위치를 앞으로 당기지 않는다** — 당기면 그것이 곧 밸런스 변경이다.
6. **기믹 수치는 정의표에서 온다**(제약 6). 옛 4개 config 싱글턴(`RedBullGimmickConfig`·`Onsen`·`Burnout`·`ClockOut`)은 「존재 = 이 기믹 활성」이라는 Burst 우회였고 기계는 안 옮긴다 — 값만 `GimmickDef` 로 들어온다. 모양은 `AttackState` 가 `BombSpec`/`SummonSpec` 을 드는 것과 같다: `GimmickKind` + 종류별 중첩 구조체, **읽는 쪽이 종류로 고른다**. 닫힌 집합이라 이 형이 제약 8 에 맞는다. 활성 게이트는 「그 기믹이 뽑혔나」(`GimmickHost.Index`) 하나다.
7. **`[Periodic]` seam 을 연다 — enum 과 호출부 둘 다.** 「주기마다 무슨 일이 일어난다」의 자리이고 unit 7 의 주기 바인딩(레드불 cadence · 온천 열기 · 번아웃 피로)이 여기 붙는다. **`FieldPrepPhase` 끝에서 `ctx.Seams.Run(Seam.Periodic, ctx)` 를 실제로 부른다** — enum 값만 더하고 호출부를 안 만들면 unit 7 이 「자리가 있는 줄 알고」 등록했다가 아무 일도 안 일어난다. 핸들러 0 인 채로 호출부가 존재하는 것이 이 unit 의 산출이다(unit 3 이 seam 4개를 그렇게 뚫은 것과 같다).
8. **`[Periodic]` seam 이 rules.md E6 을 닫는다.** 배치 엣지 표식(`JustDeployed`)은 **안 옮긴다** — 표식은 `RequireForUpdate` 게이트 산물이고 정본은 `DefenderActivated` 사건이다(남기면 다음 배치 사건과 섞인다).
9. **디버그는 커맨드로 넣는다.** 도구 자체(메뉴 UI)는 6c.
10. **결정론.** 픽업·사직서 목록 순회는 `SimEntityId` 오름차순. 픽업 스폰 자리의 난수는 `RngStreams.Pickup`, 운석 자리는 `RngStreams.Meteor` — 계열을 나누는 이유는 한쪽의 호출 횟수가 바뀔 때 다른 쪽이 통째로 밀리기 때문이다.

## 파이프라인 커버리지

| 정거장 | 픽업 | 사직서 |
|---|---|---|
| 저작 | 기믹 SO(무변) | 기믹 SO(무변) |
| 정의표 | `GimmickDef.RedBull` | `GimmickDef.ClockOut` |
| 생성 | 주기 바인딩(unit 7) → `PickupSpawned` | 사망 seam(unit 7) → `ResignationDropped` |
| 매 프레임 | 수명 · 소비 판정(제약 13 자) | 누적 수 판정 |
| 소멸 | `PickupTaken` / 수명 만료 | 임계 소모 → `ResignationThreshold` |
| 뷰 | 6c | 6c |

열기·피로는 개체가 없다(유닛의 스택이다) — 파이프라인 행이 성립하지 않는다.

## 이식 제외

| 안 옮긴 것 | 이유 | 등급 |
|---|---|---|
| 4개 기믹 config 싱글턴 + `RequireForUpdate` self-gate | Burst 우회. 값은 `GimmickDef`, 게이트는 「그 기믹이 뽑혔나」 하나 | 제거 · 계약 1 |
| 픽업 스폰 **주기** · 사직서 **드랍 계기** · 열기/피로 **누적 주기와 대상 필터** | rev 3 이 셋 다 바인딩으로 환원했다(Match 호스트 주기 · 사망 seam · 유닛 호스트 per-unit 타이머 + 기믹마다 다른 필터). 여기서 또 세우면 unit 7 이 그것을 걷어내야 한다 | 보류 · unit 7 |
| 사직서 **임계 뒤의 운석 barrage** | `MeteorBarrageRequests` 채널은 안 옮긴다. 임계 도달은 사건이고 실행은 unit 7 | 보류 · unit 7 |
| 호접몽(`DreamCocoon`) | 「끝까지 자면 영구 버프, 중간에 맞으면 파탄」은 **카드의 규칙**이라 바인딩이다. 상태 자리는 unit 3 의 `ProgressiveStates`(`CombatParts.cs`)에 이미 예약돼 있다(UML §2 의 `+Cocoon?`) | 보류 · unit 7 |
| 별도 `LastRunTimer` 타입 | 집이 하나여야 중단 정책이 하나다 — `ProgressiveStates.LastRun`(UML §2) | 제거 |
| `ClockOutRefundEvents`(퇴근 코스트 환급) | `season-gimmick-clockout` unit 8 재설계로 이미 은퇴(강제 퇴근 제거 → 사망 시 사직서 드랍) | 제거(선행) |

## 고친 것 (기존 코어·Unity 층 변경)

*(구현 중 채운다.)*

## 완료 기준

- [ ] **EditMode 코어 lane 초록** + 새 테스트 3묶음 + 1확장: `PickupTests`(수명 만료 · 재소비 락 · **제약 13 자로 소비**) · `ResignationTests`(누적 · 한 틱 다중 임계) · `HeatFatigueTests`(오버힐 없음 · 체력 1 바닥 · 피로 임계 → `Burnout` 출처 · **피로 누적이 스탯 적용 뒤라 1틱 뒤에 든다**) · `SeamHookTests`(`Periodic` append 로 `Seam._Count` 앞 번호가 **안 밀렸다** + **핸들러 0 인 채로 호출부가 매 틱 실행된다**).
- [ ] 디버그 커맨드 3종이 헤드리스 하네스에서 동작(`CommandSchedule` 로 예약 → 개체·스택이 선다). **메뉴 UI 없이** 커맨드만으로 검증된다.
- [ ] 기믹 수치 **하드코딩 0** — 4종 전량이 `GimmickDef` 에서 오고, 그 값이 canonical text 에 실린다.
- [ ] **골든 11종 Unity 재굽기** — 조각 C 의 **마지막 코어 변경**이라 여기서 한 번만 굽는다. **정본 런타임은 Unity EditMode 다**(계약 5 — 헤드리스 lane 은 골든 제외. Unity Mono 가 float 를 확장 정밀도로 평가해 약 300틱부터 갈린다). 6a·6b·6b2 를 전부 구현한 뒤 한 번에 굽고, 6a 가 기록해 둔 **「값이 실제로 바뀐 시나리오 / 해시만 바뀐 시나리오」 구분**과 대조한다.
- [ ] `ledgers/rules.md` **E6**(표식 없음 · `DefenderActivated` 가 정본) + **M3**(완료 — unit 2 가 `MapRuntime`/`FlowFieldSet` 을 이미 나눴다) + **F10** 이 코드 포인터로 매핑.
- [ ] `ledgers/bridge-methods.md` **잔량 변화 없음(46)** — 픽업·사직서·기믹 관련 브리지 행은 이미 `GimmickHost`·「디버그/로그」로 배정돼 있어 **이 unit 이 닫을 미정 행이 0** 이다. 잔량을 안 줄이는 unit 이라는 사실을 상태 라인에 명시한다(누락으로 읽히지 않게).
- [ ] `core-reviewer` APPROVE — **매니저 0** · **틱 phase 수 무변** · `Unity.Entities` 0.
