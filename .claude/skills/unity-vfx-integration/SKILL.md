---
name: unity-vfx-integration
description: Use when wiring `_SKELETON` VFX prefabs into battle view pools/presenters (`BattleCoreUnity/View/Core*` — e.g. `CoreVfxSpawner`), assigning renderer slots, adding SerializeFields, or making a VFX fire on a battle-core event (core event → view pool subscribing via `BattleDriver.Subscribe` + `ViewOrder`).
---
# Unity VFX Integration
## Overview
This skill consumes authored `_SKELETON.prefab` assets and connects them to the battle view layer (`Assets/_Project/Scripts/BattleCoreUnity/View/`). It does not design the visual itself; it wires ownership, timing, prefab slots, and fallbacks.

전투는 순수 C# 코어(`Assets/_Project/Scripts/BattleCore/`, `noEngineReferences`)가 판정하고, 코어가 낸 **값 스냅샷 사건**(`CoreEvent`)을 Unity 층이 받아 그린다. 사건을 뷰로 옮겨 적는 중개자(브리지·매니저)는 없다 — **뷰 풀마다 자기 사건을 직접 구독한다**(battle-core-rebuild README 계약 12).

## The Iron Law
"뷰는 판정하지 않는다. 사건이 나른 값으로만 그리고, 코어에 상태를 되묻지 않는다."

- 반경·자리·몸은 사건이 나른 짝(`SiteFired`/`SiteTarget` 의 `Pos`·`OriginBody`, `AreaTiles`)으로만 그린다(`docs/reference/battle-core-architecture.md` §8-7 — 판정 산식 하나). 뷰가 반경을 다시 계산하면 화면이 규칙을 틀리게 가르친다.
- 뷰가 코어 개체를 고치지 않는다. `BattleDriver.Units`·`Find` 는 읽기 전용이다.

## When to Use
- `_SKELETON.prefab` 을 `CoreVfxSpawner` 나 다른 `Core*` 뷰 풀·프리젠터에 연결할 때
- 인스펙터 `SerializeField` 슬롯을 추가하거나 정리할 때
- 입력 즉시 보이는 효과(프리뷰·조준)와 코어 사건 시점 효과를 분기할 때
- 빈 슬롯 로그·폴백 규약을 맞출 때
- 새 코어 사건(또는 기존 사건)에 VFX 를 새로 붙일 때

## 사건 → 뷰 풀 구독 (정본 경로)
코어 쪽(`Wassup.BattleCore`):
1. 사건 종류 = `CoreEventKind`(`BattleCore/Match/CoreEvent.cs`). **append-only** — 새 종류는 `_Count` 앞에 넣는다.
2. 사건을 낼 곳 = 그 일의 **담당자**. 담당자가 `EventBus.Publish` 로 쌓고(`BattleCore/Match/EventBus.cs`) 틱 끝에 배달된다.
3. 페이로드는 값 스냅샷이다 — 발화 시점의 자리·몸 반경·정의표 인덱스(`DefIndex`)·수치를 싣는다. 소멸·사망처럼 드레인 시점에 주체가 없는 사건이 있으므로 id 로 되물을 값을 싣지 않는 것이 아니라 **값 자체를** 싣는다.
4. 새 종류를 열면 관측 정거장도 같이 연다 — `BattleCore/Harness/CoreTrace.cs` 의 `CoreTraceChannel`(append-only) + `TryChannel` 매핑.

Unity 층(`Wassup.BattleCoreUnity`):
1. `BattleDriver` 가 틱 뒤·커맨드 적용 직후에 `DrainEvents()` 로 코어 사건을 구독자에게 다시 방출한다. 커맨드(배치 등)가 만든 사건은 `Apply` 안에서 즉시 배달된다 — 정지 중에도 배치가 보이는 이유.
2. 뷰 풀은 `[SerializeField] private BattleDriver _driver;` 를 들고 `OnEnable` 에서 `_driver.Subscribe(ViewOrder.X, OnCoreEvent)`, `OnDisable` 에서 `_driver.Unsubscribe(OnCoreEvent)` 한다. 핸들러는 `switch (e.Kind)` 로 자기 사건만 고른다. 모범: `View/CoreVfxSpawner.cs`.
3. 순서는 `ViewOrder` 상수(`BattleCoreUnity/ViewOrder.cs`)가 말한다 — `Leap → Board → Unit → Projectile → Effect → Damage → Status → Overhead → Hand → Audio → Outcome`. C# 이벤트 등록 순서(= 씬 나열 순서)에 기대지 않는다. 몸에 붙는 VFX 는 유닛 뷰가 선 뒤(`Effect` 이상)라야 앵커가 있다.
4. `CoreEventKind.MatchStarted` 에서 판별 상태(대기 목록·카운터)를 비운다.
5. 앵커가 필요하면 다른 풀이 노출한 읽기 창을 쓴다(예: `CoreUnitViewPool.TryResolveViewPosition`). sim→view 좌표는 `Wassup.Core.BoardSpace.ToView`.

## Decision Tree
```text
+----------------------------------------+-------------------------------------------+
| 질문                                   | 경로                                      |
+----------------------------------------+-------------------------------------------+
| 규칙이 아직 안 일어났나? (드래그 프리뷰 | 입력 층이 뷰에 직접 민다                  |
|  · 카드 조준 · 부착 범위 링)           | (예: CoreMapOverlay.ShowPlacement/        |
|                                        |  ShowAimRing, CoreDragPreviewPresenter)   |
| 코어가 판정한 결과인가? (타격·착탄·    | 코어 사건 → 뷰 풀 구독                    |
|  회복·배치 성사·처치)                  | (BattleDriver.Subscribe + ViewOrder)      |
| 한 효과가 둘 다 필요한가?              | 두 경로 병행 허용                         |
+----------------------------------------+-------------------------------------------+
```

혼합 소유권 주석:
"한 효과가 예고 + 판정 시점 둘 다 필요하면 두 경로 병행 사용 가능. 예: 착탄 예고(`ProjectileSpawned` 에서 `CoreMapOverlay.ShowTelegraph` → `ProjectileHit`/`ProjectileDespawned` 에서 해제) + 착탄 버스트(`ProjectileHit`)"

## Red Flags
- 뷰가 `BattleDriver.Match`/`World` 를 통해 코어 상태를 **고치는** 경우, 또는 사건 처리 중 코어에 값을 되묻는 경우
- 뷰 쪽에서 반경·사거리·몸을 다시 계산하는 경우(사건 값의 짝으로 그릴 것)
- 여러 풀의 사건을 받아 뿌리는 「통합 뷰」·중개 매니저를 새로 만드는 경우
- prefab slot 이 비었는데 로그 없이 조용히 리턴하는 경우
- authoring 단계 책임인 shader/material 설계를 여기서 다시 하는 경우
- 코어 사건 페이로드에 managed reference 나 scene object 를 넣는 경우(코어는 엔진을 모른다 — 저작 자산은 `DefIndex` 로 `BattleDriver.DefenderAssets`/`EnemyAssets`/`ViewAssets` 에서 되찾는다)

## Rationalization Table
| Topic | Default | Why |
| --- | --- | --- |
| 사건 수신 | 풀마다 `BattleDriver.Subscribe` | 중개자 없음(계약 12) |
| 순서 | `ViewOrder` 상수 | 씬 나열 순서에 흔들리지 않게 |
| 입력 피드백 | 입력 층 → 뷰 직접 | 규칙이 아직 없으니 사건도 없다 |
| 판정 결과 VFX | 코어 사건 | 판정 틱과 동기화 |
| Missing prefab | 슬롯당 1회 로그 | 「사건은 나는데 화면만 조용한」 상태를 기능 사망과 구분 |
| Validation | Play Mode visual check | 타이밍과 연결을 가장 빨리 확인 가능 |

## Common Mistakes
- 핸들러를 만들었지만 `OnEnable` 구독 / `OnDisable` 해제 중 하나를 빠뜨린다.
- `_driver` SerializeField 를 씬에서 배선하지 않는다(구독이 조용히 0 — `unity-feature-wiring` 스킬).
- 새 `CoreEventKind` 를 열고 `CoreTraceChannel` 매핑을 안 연다.
- prefab slot 이 비었을 때 조용히 리턴해서 효과가 사라진다.
- 예고와 판정 시점 폭발을 한 경로로 억지 통합한다.
- 옛 전투의 「START 사건 + `hitDelaySec` 지연 큐」를 재현한다 — `AttackResolved` 는 그 자체가 RESOLVE 라 미루면 타격보다 늦게 터진다.

## Quick Reference Checklist
- `_SKELETON.prefab` handoff 를 받았는가
- `SerializeField` 슬롯이 있는가
- 슬롯이 비면 슬롯당 1회 로그를 남기는가 — `CoreVfxSpawner.MissingSlot`: `Debug.LogError($"[CoreVfxSpawner] {slot} 미할당 — 인스펙터에서 프리팹을 연결할 것.")`. 폴백이 있는 슬롯은 `LogWarning` + 폴백(예: `_goalCollapsePrefab` → 배치 링 펄스)
- 사건 경로면: 담당자의 `Publish` + (새 종류면 `CoreEventKind` + `CoreTraceChannel`) + 뷰 풀 `Subscribe(ViewOrder.X)`/`Unsubscribe` + `switch` 분기가 모두 있는가
- 뷰가 코어에 쓰거나 판정을 재계산하지 않는가
- 씬(`BattleCoreScene`)에서 풀의 `_driver` 와 새 슬롯이 배선됐는가

## Handoff
authoring/integration 경계:
"종료 산출물 = _SKELETON.prefab + .mat 저장까지. Renderer 슬롯 연결 및 SerializeField 추가는 integration 스킬 호출"

경로 적용 기준:
- 규칙이 일어나기 전의 배치 프리뷰/조준/부착 범위는 입력 층 → 뷰 직접
- 코어가 확정한 피해/처치/착탄/배치 성사는 코어 사건
- 사건 payload 는 위치, 반경, 정의표 인덱스 같은 값 타입 위주로 유지

## 공격 VFX 슬롯 규약 (rev 2026-09-12 — directional-attack-shape 에서 정착, 2026-09-25 새 코어 기준 갱신)
- `DefenderUnitData.attackVfxPrefab` 하나에 `attackVfxScale`(프리팹 스케일을 **덮는다** — 크기는 이 인자로) · `attackVfxFacesTarget`(방향)
  · `attackVfxAtAttacker`(원점 = 공격자 발밑, 아니면 대상 자리) · `attackVfxEulerOffset`(계산 회전 **뒤에** 곱하는 자세 knob).
- 소비처 = `CoreVfxSpawner` 의 `AttackResolved` 처리. 사건이 곧 RESOLVE 라 지연 큐가 없다. 원점 = `atAttacker ? SiteFired.Pos : SiteTarget.Pos`.
  방향: 발밑 참격은 사건이 나른 공격 축(`AttackDir`), 타격점 VFX 는 공격자 뷰 위치(`CoreUnitViewPool.TryResolveViewPosition`)에서 대상 자리로 잰다.
  참격 자국 길이 = 사건의 `AttackRange + SiteFired.OriginBody`(공격자를 되묻지 않는다).
- 방향 회전은 `CoreProjectileViewPool.PlayHit` 이 up 축을 고정하고 yaw 만 준다 — 방향을 그대로 forward 로 주면 기운다.
- 세션형(빔)은 `CoreBeamPresenter` 가 공격 실주기(`AttackResolved.Amount`) × 여유 배수로 TTL 을 잡는다 — 상수 TTL 금지(공속 버프에서 깜빡인다).
- 지연 재생 콜백·풀 재사용: `TrailRenderer.autodestruct=false`, 풀 GO 는 `CoreProjectileViewPool` 의 `ResetVfx` 로 재생 신선도 확보.
