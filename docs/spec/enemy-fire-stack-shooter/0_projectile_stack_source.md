# 0 — 투사체 스택 귀속을 사수로

## 목적

투사체가 부여한 스택이 **누적되지 않는** 결함을 고친다. 이 spec 전체가 여기에 걸려 있다 —
고치지 않으면 킨들러는 화염 스택을 영원히 1로만 유지하고 5스택 화상은 절대 터지지 않는다.

**결함**: `StackModifierSlot` 의 병합 키는 `(header.source, kind)` 다
(`ModifierApplySystem.cs:135`). 그런데 두 producer 의 `source` 의미가 다르다.

| 경로 | `source` | 결과 |
|---|---|---|
| 근접 `AttackSystem.cs:1196` | `attackerEntity` (사수) | 같은 슬롯에 누적 ✅ |
| **투사체 `ProjectileHitSystem.cs:199`** | **`entity` (투사체)** | **발사마다 새 슬롯, `stackCount` 영원히 1** ❌ |

투사체는 발사마다 새 엔티티다. 지금까지 안 터진 이유는 `ApplyStack` outputs 를 쓰는 유일한
배포 에셋이 난도질꾼(근접)이라 **이 경로에 사용자가 0** 이었기 때문이다.

투사체는 이미 사수를 알고 있다 — `ProjectileState.owner` 를 위협 귀속(`threatOwner`)에 쓰고
있고, 모든 발사 지점이 `owner = attackerEntity` 로 채운다(`AttackSystem.cs:277,855,894,962`).

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileHitSystem.cs` — `ApplyStack` 의
  `source`
- `Assets/_Project/Tests/PlayMode/ProjectileApplyStackAccumulatesTest.cs` (신규)

## 구현

`ApplyStack` enqueue 의 `source` 를 사수로 바꾼다. `threatOwner` 지역변수가 이미 같은 값을
들고 있다(같은 루프 상단, `projectile.ValueRO.owner`).

```csharp
case AttackOutputKind.ApplyStack:
    if (hasStackQ)
        stackEvents.queue.Enqueue(new StackModifierApplyEvent
        {
            ...
            // 병합 키 = (source, kind). 투사체 엔티티를 실으면 발사마다 새 슬롯이
            // 생겨 누적이 영원히 일어나지 않는다. 근접 경로(AttackSystem)와 같은
            // 규약으로 맞춘다 — source = 사수.
            source = threatOwner != Entity.Null ? threatOwner : entity,
        });
```

`Entity.Null` 폴백을 두는 이유: bridge-cast 투사체(스킬·메테오)는 `owner` 가 `Null` 이라
그대로 실으면 서로 다른 스킬의 스택이 한 슬롯을 공유하게 된다. 현재 `ApplyStack` outputs 를
가진 bridge-cast 투사체는 없으므로 폴백은 **현행 동작 보존**이 전부다.

⚠ **outputs 처리는 `PayloadKind.SingleSplash` 분기에만 있다.** `PathHit`(방향탄)·`TileAoe`
(착탄 셀 AoE) 는 outputs 를 아예 읽지 않고 `projectile.damage` 로만 해결한다. 킨들러는
`Homing`→`SingleSplash` 라 무관하지만, 나중에 파이어볼을 방향탄/광역으로 바꾸면 **스택이
조용히 멎는다.** 이 단위에서 넓히지 않는다 — 소비자가 없는 확장이다(제약 8).

⚠ **바로 위 `ApplyStat` 은 건드리지 않는다.** 같은 결함이 있지만(`source = entity`) 고치면
`Enemy_Debuffer` 가 곱누적 → 상시 ×0.6 으로 **라이브 밸런스가 바뀐다**. 별도 결정 — README
후속 후보. 두 case 의 `source` 가 달라 보이는 이유를 코드 주석 한 줄로 남긴다.

## 완료 기준

- [x] compile 통과
- [x] 신규 PlayMode `ProjectileApplyStackAccumulatesTest` green.
      `DefenderApplyStackOutputTest` 하네스를 미러하되 **투사체 유닛**으로 구동한다:
      카탈로그 `archer` 를 복제해 `outputs = [Damage 1, ApplyStack(Bleed, +1, perApp 6, max 5)]`
      로 갈아끼우고 더미 적에게 계속 쏘게 한다.
      - **핵심 단언: `StackModifierSlot` 버퍼 길이가 1을 넘지 않는다.** 수정 전이라면 히트 수
        만큼 슬롯이 늘어난다 — 이것이 결함의 직접 지문이다.
      - 보조 단언: `stackCount` 가 2 이상에 도달한다(누적이 실제로 일어남).
      - `perAppDuration` 을 6초로 크게 잡아 폴링 중 슬롯 만료로 인한 flake 를 배제한다.
      - ⚠ `stackCount == 5` 로 단언하지 말 것 — `Consume` 이 임계에서 소모하므로 폴링
        타이밍에 따라 실패한다(`bleed-fighter-defender` 계약 1 경고).
      - 배틀 PlayMode 관례대로 `LogAssert.ignoreFailingMessages = true` + `TearDown` 복구.
      - ⚠ **`bridge.StartBattle()` 필수.** 투사체는 ECS 가 `ProjectileSpawnRequest` 를 stage
        하고 bridge 가 드레인해 스폰하는 2단계인데, 그 드레인이 `if (!_running) return;`
        뒤에 있다(`BattleBridge.cs:2261`). 없으면 요청만 쌓이고 스택이 0으로 남는다 —
        **초판이 실제로 이 함정에 걸렸다**(근접 경로는 이 게이트를 안 타서 통과한다).
        `BeamPresentationTest` 가 같은 함정을 문서화하고 있다.
- [x] **mutation 확인 1회**: `source` 를 `entity` 로 되돌리면 테스트가 실제로 실패하는지
      (검출력 증명 후 원복)
- [x] EditMode 전량 green
- [x] PlayMode 전체 = HEAD 베이스라인과 동일(사전 실패 건수 대조. 신규 회귀 0)

## 확인

- **2026-07-30** · testrig 배치 실행(에디터가 Play Mode 라 MCP 경로 불가).
  - `ProjectileApplyStackAccumulatesTest` **Passed** (8.3s).
  - **mutation 실측**: `source = entity` 로 되돌리면 `Expected: 1 / But was: 14` —
    14발이 14슬롯을 만든다. 결함 지문과 검출력이 동시에 증명됐다.
  - EditMode 전량 **1584 중 1582 pass / 0 fail** (skip 2 = 기존 Ignored).
