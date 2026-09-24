# 7d — 기믹 · 보스 · 분열 · 순찰 · 디버그 · 장부 마감 (조각 D · 4/4)

> 6b2 가 **셈판**을 세우고 「무엇이 언제 그것을 놓는가는 unit 7」로 남긴 구멍을 여기서 닫는다. 그리고 조각 D 가 끝나므로 **장부 미정 44 → 0** 과 도구 처분 2행도 여기서 끝난다 — 그것이 조각 E 진입 조건이다.

## 목적

시즌 기믹 4종이 **저절로 일어나고**(레드불이 떨어지고, 사직서가 쌓여 운석이 쏟아지고, 온천이 데우고, 번아웃이 지치게 한다), 보스가 위협을 기억하고 도약하고, 분열체가 갈라지고, 소환사가 순찰병을 낸다. 복사·적응 대상 실측 **약 1,900줄**(기믹 시스템·config 851 · `SplitChain` 104 + 브리지 분열 · 보스 파셜 2 443 · 재배치 파셜 390 · 순찰 소환 + 디버그 메뉴 2).

## 변경 대상

| 항목 | 경로 |
|---|---|
| 기믹 바인딩 | `BattleCore/Trigger/GimmickBindings.cs` — `GimmickHost.Begin` 뒤 판 시작 1회 부착. 유닛 호스트 **2**(번아웃 피로 · 온천 열기) + Match 호스트 1(레드불 cadence) + 사망 seam 1(사직서 드랍). ~~임계 1(운석 barrage)~~ → **7b 로 이동**(리드 배정 2026-09-24 — `Trigger/ResignationBarrage.cs`, `ResignationThreshold` 사건의 소비자) |
| 기믹 정의표 | `Match/CardDef.cs` 의 `GimmickDef` 확장(6b2 가 연 `GimmickKind` + 종류별 중첩 구조체)에 **주기·필터 값** append + canonicalize |
| 분열 | `Trigger/SpawnUnitsIntent` 소비 — `World/BattleWorld.SpawnEnemy` 재사용 · `Match/EnemyDef.SplitRange`(정의표 줄) · salvage `Data/SplitChain.cs`(**104줄** — 순환 차단, 순수) |
| 보스 | `Combat/ThreatTable.cs`(salvage **99줄** — unit 3 이월 C25) · `Combat/LandingSiteChoice.cs`(도약 착지 선정 — 옛 `DefenderDensity` 의 자) |
| 호접몽 | `World/CombatParts.cs` 의 `ProgressiveStates.Cocoon`(UML §2 예약 칸) + 완주 감시 |
| 순찰 소환 | `Match/CombatDefs.cs` 의 `SummonSpec` 에 순찰 줄 인덱스(옛 `RegisterPatrolUnitSO` 의 후계) · 이동·앵커는 `Move/PatrolAreaMath.cs`(unit 2, **233줄** 이미 코어) |
| 커맨드 | `Match/Command.cs` 에 `DebugSummonPatrol`·`DebugFireBinding` |
| 사건 | `CoreEvent` append: `GimmickTriggered`(기믹 종류 · 대상 — 트레이스·뷰) |
| 디버그 도구 | `Editor/BattleCore/CoreSummonDebugMenu.cs` · `Editor/BattleCore/CoreTriggerDebugMenu.cs` (tools.md 10·11) |
| 장부 | `ledgers/bridge-methods.md` **28 → 0** · `ledgers/tools.md` 10·11 · `ledgers/rules.md` 마감 |
| 테스트 | `Tests/EditModeCore/`: `GimmickBindingTests`·`ResignationBarrageTests`·`SplitTests`·`BossTests`·`SummonPatrolTests` |

## 구현

1. **기믹의 주기는 판이 아니라 유닛이 소유한다**(정정 2 · C4). 번아웃 피로와 온천 열기는 둘 다 **per-unit lazy-attach 타이머**이고 위상이 **부착 시점**이다 — Match 호스트 하나로 접으면 전원이 같은 프레임에 같이 쌓인다. 판 시작·스폰 시 유닛 호스트 바인딩을 붙이고, `[Periodic]` seam(6b2 가 열어 둔 호출부)이 드레인한다.
2. **대상 필터는 기믹마다 다르고 그대로 옮긴다.** 번아웃 = **defender 전용 · 사망 미제외** / 온천 = **전 유닛 · 사망·배치중 제외**. 통일하면 규칙이 바뀐다(critic 열린 질문 1). `subjectFilter` 는 **저작 노출 없음** — 코어 내부 바인딩 전용 축이다(rev 3 §1).
3. **전역 주기는 하나뿐이다** — 레드불 스폰 cadence 만 **Match 호스트** 바인딩이다(정정 2 의 반대편). 소비는 매 틱 공간 폴링(6b2 가 제약 13 자로 이미 세웠다) → `PickupTaken` 사건, 재소비 락(`hasLastRun`)은 `subjectFilter`.
4. **사직서는 드랍이 사망 seam, 임계가 level 폴링이다**(L12). 드랍 = 방어유닛 **자연 사망**(퇴근은 `Dead` 를 안 켜므로 배제 코드 0줄로 안 일어난다 — 불변식 11). 임계는 **한 틱에 여러 번 넘을 수 있고 그것이 사양**이다. 임계 뒤의 **운석 barrage**(Walk 타일 10곳 순차 낙하, 적 전용)는 **7b 가 먼저 세웠다**(`Trigger/ResignationBarrage.cs` · `ResignationBarrageTests` — 자리 난수 `RngStreams.Meteor`). 이 unit 에 남은 것은 **드랍 계기**(사망 seam)다.
5. **피로 누적은 스탯 적용 «뒤»다 — 1틱 지연을 박제한다**(6b2 구현 5 와 같은 문장). 바인딩을 `[Periodic]`(`FieldPrepPhase` 끝)에 붙이면 **앞으로 당겨져 밸런스가 바뀐다.** 누적 자리는 6b2 가 정한 단계를 그대로 쓰고, 이 unit 은 **주기와 대상만** 준다.
6. **기믹 활성 게이트는 하나다** — 「그 기믹이 뽑혔나」(`GimmickHost.Index`). 옛 4개 config 싱글턴 + `RequireForUpdate` 는 6b2 가 이미 걷었다. 바인딩 부착은 `GimmickHost.Begin` 뒤 판 시작 1회.
7. **분열은 `OnSlain` 이다**(정정 4 · H7). `OnDeath`(모든 사망 경로)로 옮기면 분열 조건이 넓어진다 — 치명 타이머·순찰 수명으로 죽어도 갈라진다. intent `SpawnUnits` 는 **부모 셀 중심 양자화 칸**(연속 좌표에 더하면 자식이 옆 칸에 태어나 골이면 「처치했는데 유출」이 난다, E1) + 배치각 `2π·c/count` **인덱스 결정론**(난수 금지) + **첫 슬롯만** + 상한 8 + `SplitChain.Validate` 순환 차단. ⚠ **바인딩은 그릇은 붙이되 항목은 건너뛴다**(S8) — 초판 설계가 전용 큐·레지스트리·슬롯·이벤트·스탬프 다섯을 만들려다 리뷰가 걷어낸 자리다.
8. **보스의 위협 귀속을 여기서 세운다**(unit 3 이월 C25). `ThreatTable`(99줄)의 소비자가 보스뿐이라 unit 3 이 「그때 세운다」로 미뤘다. `Leader` 는 **소비자 0 이라 부활시키지 않는다**.
9. **「생존당 1회」는 `fireCap 1` 이다**(정정 5 의 짝). 옛 코드는 `fraction ≥ 0.5` 일 때 둘째 경계가 음수가 되어 **우연히** 성립했고, 밸런스로 값 한 칸이 0.4 가 되면 조용히 2회가 된다. ⚠ **궁극기 바인딩에만** 준다 — 같은 트리거를 빈사폭주·진동갑주·가호가 쓰고 **그쪽은 다회 발동이 사양**이다.
10. **보스 도약의 착지 지점 선정은 형을 먼저 정한다.** `DefenderDensity` 의 **사각 자**가 unit 3 이 「보스 도약 착지 지점 선정 = unit 7. 그때 형부터 정하고 온다」로 이월한 것이다. 기본값 = **현행 사각 자 박제**(폭탄맨 폴백 C20 과 같은 처분 — 원 자로 바꾸면 착지 칸이 달라져 밸런스 변경이다). **사용자 결정 필요 ②**. 착지 슬램 자체는 **자리형**(몸 0)이고 unit 3 이 이미 그렇게 선언했다(C22).
11. **마메모의 「웨이브 회전 정지」는 바인딩이 아니다**(rev 3 §8 확인 대기 해소). 코드를 뒤지면 그 훅이 없고, 실제 기제는 **웨이브 생성기의 보스 호위 후처리**(`WaveGenerator.cs:189` 의 `BossEscortMin/Max`)다 — unit 4 가 이미 이식했다. 마메모가 판에 얹는 것은 자장가(광역 수면)와 실드뿐이고 둘 다 일반 바인딩이다. **여기서 새로 만들 것이 없다.**
12. **광역 수면은 「재우자마자 내가 깨울 자리」를 뺀다**(S9). 재우는 **수**는 그대로고(뺄 만큼 더 뽑는다) 달라지는 것은 **누가** 자느냐다. 옛 라우팅 표 주석에만 있던 규칙이라 놓치기 쉽다.
13. **호접몽은 카드의 규칙이지만 상태 자리는 이미 있다**(6b2 이월). 「끝까지 자면 영구 버프, 중간에 맞으면 파탄」 — 개시는 **잠 + 완주 감시자 원자 부착**(S19), 상태는 `ProgressiveStates.Cocoon`(UML §2 가 예약해 둔 칸).
    ⚠ **중단 사유 표에 「피격」이 없다.** 오늘 `ProgressInterrupt` 는 `Death`·`Retire`·`Cc`·`OwnerDestroyed` 넷뿐인데(`CombatParts.cs:370`) 파탄은 **맞는 순간**이다. 새 사유를 더하지 않고 **기상(wake-on-hit)이 고치를 같이 걷게** 한다 — 6a 가 「피격 기상은 피해가 든 직후 같은 틱」(F25)으로 그 자리를 이미 정해 뒀고, 잠이 깨는 것과 고치가 깨지는 것은 **한 사건**이기 때문이다. 별도 사유로 나누면 두 문이 갈려 「깼는데 고치가 남는」 상태가 난다.
14. **소환사의 순찰병.** 소환물이 살아 있어도 쿨은 돌고 **스폰만 skip** 한다(unit 3 C2 가 이미 정책 값으로 세웠다). 이 unit 은 그 스폰의 **실행**(정의표 줄 · 순찰 앵커)을 잇는다 — 이동·앵커 수학은 unit 2 가 이미 코어에 들여놨다(`Move/PatrolAreaMath.cs` 233줄).
15. **디버그 도구 2개를 코어 커맨드로 재작성한다**(tools.md 10·11). `CoreSummonDebugMenu`(순찰병 수동 스폰 + 앵커 표시) 와 `CoreTriggerDebugMenu`(바인딩 목록 · 강제 발화 · **왜 안 터졌나** 4원인 — 감지자 없음 / 조건 불통과 / `fireCap` 소진 / lifetime 만료). 도구가 없으면 「재현이 먼저다」가 이 영역에서 집행 불가다.
16. **결정론.** 기믹 타이머는 유닛별이되 **판의 시계**(틱)로 세고, 순회는 `SimEntityId` 오름차순. 픽업 자리는 `RngStreams.Pickup`, 운석 자리는 `RngStreams.Meteor`. `System.Random` 금지.

## 파이프라인 커버리지

| 정거장 | 분열 자식 | 순찰병 | 운석 barrage |
|---|---|---|---|
| 저작 | `AttackUnitData.splitUnit` · `SplitChain` | `SummonPatrolAbility` | 기믹 SO(`ClockOut`) |
| 정의표 | `EnemyDef` + 분열 바인딩 | `UnitDef` + 순찰 줄(`RegisterPatrolUnitSO` 의 후계) | `GimmickDef.ClockOut` |
| 생성 | `OnSlain` → `SpawnUnits` → `EnemySpawned` | 공격 루프 소환 정책 → `UnitSpawned` | 임계 → `ProjectileRequests`(자리형, 몸 0) |
| 매 프레임 | 일반 적과 같음 | 순찰 앵커 추종(unit 2) | 탄 비행(unit 3) |
| 소멸 | 일반 적과 같음 | 수명 만료 = 사망 경로 | 착탄 |
| 뷰 | 기존 유닛 풀 | 기존 유닛 풀 | 기존 투사체 풀 + 예고 표식(6c) |

⚠ **새 뷰 풀이 없다** — 셋 다 기존 아키타입(유닛 · 투사체)에 올라탄다. 그래서 이 unit 은 아키타입 표를 새로 만들지 않는다.

## 이식 제외

| 안 옮긴 것 | 이유 | 등급 |
|---|---|---|
| **재배치(유닛 이동)** 전량 | `defender-clock-out/0` 이 진입구를 껐고(팀 리뷰 2026-08-13) 퇴근이 그 자리를 대신한다. 기능 코드는 「판단이 뒤집힐 때를 대비해」 남겨 둔 것이지 **라이브 규칙이 아니다**. 되살리는 것은 새 기능이므로 이 spec 범위 밖 | 제거(선행 결정) |
| `RelocationDebugMenu`(tools 11) | 위와 함께 **은퇴**. 구동할 기능이 없다 | 은퇴 |
| 판 안 재시작 · `OnRestartRequested` | **재시작 없음**(사용자 확정 2026-09-23) — `BattleMatch` 를 새로 조립한다. C7·S6·K5 의 재시작 전제는 안 옮긴다 | 제거(사용자 결정) |
| 유출 한도 4함수(`EffectiveLeakLimit` 등) | 제거 확정(X17 · 계약 9) | 제거 |
| 4개 기믹 config 싱글턴 · `MeteorBarrageRequests` 채널 | 6b2 가 값을 `GimmickDef` 로 옮겼고, 채널은 계약 1(큐 미이식). 임계는 사건, 실행은 이 unit 의 직접 호출 | 제거(선행 · 계약 1) |
| `ThreatTable.Leader` | 소비자 0. 부활시키지 않는다 | 제거(C25) |
| 강제 퇴근(10초 타이머)·퇴근 코스트 환급 | `season-gimmick-clockout` unit 8 재설계로 이미 폐기(사망 시 사직서 드랍으로 대체) | 제거(선행) |
| 「자는 유닛이 주기 스킬을 계속 쓴다」 — 주기 바인딩이 행동 잠금을 안 본다 | 옛 `BossPeriodicTriggerSystem` 은 CC 를 **읽지 않는다**(실측). 스펙은 사양으로 썼고 **사용자는 버그로 읽었다**(Play 관측 2026-08-11). 고치면 주기 스킬을 가진 **전원**의 동작이 바뀐다. 기본값 = **현행 박제**. **사용자 결정 필요 ③**. ⚠ **rev 3 §5 의 「자는 캐스터」 미결과 다른 질문이다** — 그쪽은 캐스터 제거로 **소멸**했고(`HazardCastSystem`·`ShieldCastSystem` 포인터는 stale), 남은 것은 **주기 바인딩 전체**의 축이다 | 보류(현행 박제) |
| 보스 도약 착지 선정의 **원 자 전환** | 구현 10 — 기본값은 사각 자 박제. **사용자 결정 필요 ②** | 보류 |
| 브리지 수명 8함수(`Awake`·`OnValidate`·`OnDestroy`·`Destroy*`·`Dispose*`·`EnsureQueriesAndQueues`) | MonoBehaviour + ECS 월드 수명의 산물. 코어의 수명은 `BattleMatch` 이고 드라이버가 든다 | 제거(계약 1) |
| **위협 표**(`ThreatTable` 전체 · 구현 8 의 「위협 귀속」) | 구현 8 은 「소비자가 보스뿐이라 여기서 세운다」였는데 **그 소비자가 이미 없다** — 보스 순간이동이 「위협 리더 근처」에서 「상대 밀집 칸」으로 바뀌며 누적만 돌고 읽는 자가 0 이 됐다(옛 `HealthThresholdSystem.cs:28-32` 주석이 「현재 소비자가 없다」로 적어 둠). 의도를 내는 concrete 도 0. `IntentApplier` 의 `CreditThreat` 는 버리되 **말한다**(조용한 폐기 금지) | 제거(소비자 0 · `cef88d259`) |
| `LeakSiegingEnemy`(골 붕괴 셀의 공성 적 → 유출) | 옛 코드도 **도달 불가** — 첫 붕괴가 판을 끝낸다(`BattleBridge.cs:7072` `EndMatch("stress_full")`, 그 위 주석 「휴면」). 코어는 `HeartMeter.Damage` 가 첫 붕괴에 판을 닫는다 | 제거(휴면 코드) |
| 순찰 디버그 스폰의 「가까운 길 칸 스냅」(옛 `DebugSpawnPatrolAt` 의 walk tile 탐색) | 진짜 소환은 **소환사 칸**에 세운다(`CombatPhase.SpawnPatrol` — 앵커 = 소환사 칸). 도구가 다른 자리에 세우면 「도구로는 되는데 진짜 소환은 안 된다」가 생긴다. 새 도구는 소환과 같은 자리 규칙 | 제거(도구 = 진짜 경로) |
| 브레스 슬롯이 비었을 때의 **링 펄스 폴백**(옛 `VfxSpawner.cs:272-273`) | 슬롯 null = 에러 한 번(옛 VFX 규약 · `CoreVfxSpawner.MissingSlot`). 폴백 그림이 있으면 「배선 누락」이 「그림이 약하다」로 읽힌다 | 제거(규약) |

## 고친 것 (기존 코어·Unity 층 변경)

| 무엇 | 전 → 후 | 근거 |
|---|---|---|
| 짱쎈 일반 도약의 **착지 슬램 발사 자리** | 옛 = 브리지가 **뷰 도착 시각**(0.83초)에 쐈다 → 코어 비행 창(`ProgressiveStates.Hop*`)이 끝나는 틱에 코어가 낸다(창 길이 = `MovementTuningConfig.bossLeapFlightSeconds` 기본 0.83). 비행 중 사망 = 슬램 없음(옛 abandoned) | 옛 `BattleBridge.BossLeap.cs:210-222` `ResolveLanding` · 계약 1(규칙은 코어) · `cef88d259` |
| 궁극기 「생존당 1회」 | 옛 = `fraction ≥ 0.5` 에서 둘째 경계가 음수라 **우연히** 1회 → bake 가 궁극기에만 `FireCap 1`(경계 규칙 다회 유지) | 구현 9 · `BindingDefinitionBuilder` · `BossTests` |
| 회오리·포탈 그림의 **수명** | 옛 = 뷰가 `Destroy(go, 지속 + 0.1)` 제 시계 → 소멸 사건(`FieldDespawned`)에 걷는다 | 옛 `VfxSpawner.cs:145`·`:181` · 계약 7 · `f45a9ae7e` |
| 착탄 예고 링 **반납** | 옛 = 그 탄의 착탄 사건에만 → 착탄 **또는 소멸** 사건(착탄 없이 사라진 탄이 링을 남기지 않는다) | 옛 `BattleBridge.cs:5131-5135` · `3cf864ad5` |
| `FieldSpawned` 페이로드 | 출구·반경이 없었다 → 포탈 출구 `SiteTarget` · 반경(칸) `AreaTiles`(값 스냅샷 · 트레이스 무변 — a·b·i·f 만 기록) | 계약 4·7(뷰가 되묻지 않는다) · `f45a9ae7e` |
| 넘겨받은 WIP(커맨드 2종) | 진영 타입 이름 미해석으로 **컴파일이 안 됐다** → `Wassup.Battle.Units.Faction` 한정(규칙 무변) | `ff3094ac4` |
| `TriggerDispatcher` 진단 칸 | 없음 → 종류별 **감지된 사실 수**(`SensedCount`) — 판정에 안 쓰는 관측값. 「감지자 없음」을 코드 목록이 아니라 관측으로 답한다 | `BindingDiagnosis` · `ff3094ac4` |

### 미배선 — 씬(Unity MCP 세션 끊김 · 열린 씬 YAML 외부 편집 금지)

`BattleCoreScene` 에 아래를 더하면 끝난다. 값은 옛 `BattleScene.unity` 의 `VfxSpawner` 블록(4439~4452줄) 그대로다. 비면 컴포넌트가 **에러 한 번**을 찍는다(조용한 무동작 없음). `_overlay` 는 같은 씬에서 찾아 쓴다.

| 오브젝트 | 컴포넌트 · 칸 | 값 |
|---|---|---|
| `Vfx`(또는 새 `Fields`) | **`CoreFieldPresenter` 추가**: `_driver` · `_tornadoPrefab` · `_portalPrefab` | 씬 `BattleDriver` · 옛 `tornadoPrefab`(guid `ae85f8af7e5c6c44c91e2f6966a4354b`, fileID 207745087118991948) · 옛 `portalPrefab`(guid `8a018bf2f3cb64a26b5a382a6bfc6d73`, fileID 6817788139784616374) |
| `Vfx` | `CoreVfxSpawner._areaBreathPrefab` · `_areaBreathScalePerTile` · `_areaBreathScaleMax` · `_areaBreathForwardFactor` · `_areaBreathAngleOffset` · `_overlay` | 옛 `areaBreathPrefab`(guid `5a2d78f3c61b49e8b4137ac9e0d562b1`, fileID 7251879486380165639) · 0.55 · 2.4 · 0.45 · 90(코드 기본값과 같다) · 씬 `CoreMapOverlay` |

### 장부 마감 증거(2026-09-24)

```
$ python3 tools/battle-core-rebuild/check_ledgers.py
[bridge-methods] OK (367)
[bridge-fields] OK (91, 씬 키 91)
[bridge-methods] 미정 0 (조각 E 진입 조건: 0)
```

28행 처분: 새 주인 6(`CoreUnitView.PlayLandingSquash` · `EnemySpawn` · `CombatDefinitionBuilder` ×2 · `WaveScheduler.FieldClear` · `EnemySplit`) · 삭제 22(재배치 7 · 수명·ECS 정리 9 · 유출 한도 3 · 휴면 1 · 재시작 1 · 환경 게이트 1). 각 행의 비고에 이유 한 줄.

## 완료 기준

- [ ] **헤드리스 초록** · **EditMode 코어 lane 초록** + 새 테스트 5묶음: `GimmickBindingTests`(per-unit 타이머 위상이 부착 시점 · 필터가 기믹마다 다르다 · 레드불만 Match 주기) · `ResignationBarrageTests`(사망 드랍 · 퇴근은 안 드랍 · 한 틱 다중 임계 · 운석 10발이 **자리형**) · `SplitTests`(`OnSlain` 에서만 · 부모 칸 중심 양자화 · 인덱스 배치각 · 상한 8 · 순환 차단) · `BossTests`(위협 귀속 · `fireCap 1` 은 궁극기만 · 빈사폭주는 다회) · `SummonPatrolTests`(소환물 생존 중 쿨은 돌고 스폰만 skip). — **2026-09-24 상태**: 헤드리스 초록(test 667 · build 0 · Check 0) · 새 묶음 5 + `TriggerForceFireTests` 존재. ⚠ **Unity EditMode 코어 lane 미실행**(MCP 끊김) — 그래서 칸을 비워 둔다. ⚠ 분열 **상한 8** 은 빌더(`MatchDefinitionBuilder.MaxSplitChildren`)가 자르므로 코어 단언이 없다 — Assets lane 몫(미작성).
- [x] **증상 단언 4건**: ⑴ 레드불을 먹은 방어유닛이 빨라지고 **시간이 지나면 쓰러진다** ⑵ 방어유닛 5기가 죽으면 **운석이 쏟아진다** ⑶ 슬라임을 잡으면 **그 칸에서** 자식이 퍼진다 ⑷ 보스가 밀집한 곳으로 **도약한다**. — 헤드리스 초록: `GimmickBindingTests.증상_레드불을_먹은_방어유닛이_빨라지고_시간이_지나면_쓰러진다` · `ResignationBarrageTests`(5기 사망 → 운석 10 자리형) · `SplitTests.증상_슬라임을_잡으면_그_칸에서_자식이_퍼진다` · `BossTests.증상_보스가_밀집한_곳으로_도약하고_비행이_끝나는_틱에_그_자리에_슬램이_떨어진다`.
- [x] 디버그 커맨드·메뉴 2종(`CoreSummonDebugMenu` · `CoreTriggerDebugMenu`) — 코어 커맨드 `DebugSummonPatrol`(24) · `DebugFireBinding`(25) + 판정 `BindingDiagnosis` · 헤드리스 `SummonPatrolTests` 5 · `TriggerForceFireTests` 6. 메뉴는 스크래치 csproj 로 컴파일 0 오류 확인 — **에디터 메뉴 실행은 Unity 연결 뒤**. `ledgers/tools.md` **10·11행 닫힘**(11 **은퇴**) — 도구 11행 전건 처분 완료(`ff3094ac4`).
- [ ] **골든 재굽기(Unity)** — 조각 D 가 정의표(`BindingDef[]`·`CardDef` 확장·`GimmickDef` 확장)를 바꿨으므로 `configHash` 가 전부 움직인다. 6a 의 규율대로 **「값이 실제로 바뀐 시나리오」와 「해시만 바뀐 시나리오」를 먼저 구분해 기록**한 뒤 굽는다. 정본 런타임은 **Unity EditMode**(계약 5). — ⚠ **미실행**(MCP 끊김). 이 세션의 커밋 3개(`ff3094ac4`·`f45a9ae7e`·`3cf864ad5`)는 정의표·`configHash` 를 건드리지 않았다(커맨드·진단·사건 페이로드·뷰뿐 — 트레이스는 a·b·i·f 만 기록). 앞선 7d 커밋 3개의 정의표 변경은 기본값이면 canonical 줄을 생략한다고 적었지만 **Unity 에서 대조하기 전까지 「해시 무변」은 주장하지 않는다**.
- [x] `ledgers/rules.md` **E1 완료(7d) · E2·E4 결정(7a·7b) · S1·S8·S9·C5 완료(7a)** · F35 **결정**(감속장 스냅샷 현행 박제) · F19 7d 확인. 보류 **31**, 기계 점검으로 **unit 번호·후속 후보 없는 보류 0**.
- [x] `ledgers/bridge-methods.md` 미정 **28 → 0**(위 「장부 마감 증거」): Relocation 8행(`RelocationCheck/7`·`TryGetDefenderAt/4`·`RelocatePatrolAnchorFor/2`·`PlayLandingSquash/3`·`TryGetRelocationAnchors/5`·`ActivateRelocatedDefender/3`·`ApplyRefitHeal/2`·`FinishDefenderRelocation/2`) + `BattleBridge.cs` 20행(수명 8 · 유출 4 · `ComputeSpawnLateralOffset/1` · `FindSummonPatrolAbility/1` · `RegisterPatrolUnitSO/1` · `NoQueuedAttackersRemain/0` · `ApplyEnvironmentGating/0` · `SpawnSplitChildren/2` · `DestroyEntitiesByType/0` · `OnRestartRequested/0`).
- [x] `python3 Tools/battle-core-rebuild/check_ledgers.py` **exit 0** · `bridge-methods` 미정 **0** 을 README 상태 라인에 숫자로 적는다(**조각 E 진입 조건**).
- [ ] `core-reviewer` APPROVE — 매니저 0 · 하드코딩 0(기믹 수치 전량 `GimmickDef`) · `Unity.Entities` 0 · 틱 phase 수 무변.
- [ ] **사용자 플레이 — 조각 D 의 질문**: *「카드를 쓰는 맛과 판이 얹는 변수가 옛 게임과 같은가」*. 구체 확인 6: ⑴ 손패를 끌어 유닛에 붙이고 **범위 링이 유닛마다 다르게** 보인다 ⑵ 액티브를 칸에 쏜다 ⑶ 붙인 유닛이 죽거나 퇴근하면 카드가 **돌아온다** ⑷ 이번 판의 기믹이 실제로 **일어난다** ⑸ 보스가 도약하고 분열체가 갈라진다 ⑹ 각성이 차고 손패가 열린다. ⚠ 같이 고지할 것: **재배치(유닛 이동)는 없다**(라이브도 그렇다) · **사망 시 작별 선물이 여러 장이면 전부 터진다**(사용자 결정 ① 의 기본값) · 「자는 가디언」은 **현행 그대로**(결정 ③).
- [x] 6c 「아직 안 보이는 것」 → 전 행 해소(회오리·포탈 장 · 픽업/사직서 · 열기/피로 · 호접몽/운석 · 착탄 예고 · 브레스 — 뒤의 넷은 이 unit, 셋은 씬 미배선). 「아직 안 보이는 것」 표를 unit 8 에 넘긴다: 전투 BGM · 제출 payload 의 덱 스냅샷 · 결과 화면 실제 랭킹(로비 진입).
