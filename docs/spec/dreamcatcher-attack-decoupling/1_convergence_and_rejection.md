# 1 — 판정 수렴 + 무효 조합 거절

## 목적

unit 0 의 `DcApplicability` 를 **실제 소비처 두 곳에 연결**한다. 지금 두 경로는 손으로 미러링되고 있다(`DreamcatcherAttachEval.cs:18` 의 "★ 동기화 계약"):

- UI preflight — `WouldDreamcatcherCardApply` → `DreamcatcherAttachEval.WouldApply(card, bool×5)`
- 커밋 bake — `ApplyDreamcatcherCardToUnit` 의 자체 preflight 체인 + 메커닉별 host 가드

이 단위가 끝나면 **부착 가능 ⇒ 반드시 발동**이 성립한다(사건 지점이 아직 없는 조합은 거절되므로). 이 spec 에서 처음으로 **실제 동작이 바뀌는** 단위다.

## 변경 대상

- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherAttachEval.cs` — `WouldApply` 를 `DcApplicability` 위임으로 재작성
- `Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs` — `BuildHostProfile` 신설, bake 루프의 host 가드를 단일 호출로 대체
- `Assets/_Project/Tests/EditMode/DreamcatcherAttachEvalTests.cs` — 시그니처 변경 반영
- 신규 `Assets/_Project/Tests/EditMode/DcApplicabilityMatrixTests.cs` — 전수 행렬

## 구현

### host 프로필 조립 (브리지)

```csharp
private DcHostProfile BuildHostProfile(Entity defender)
```

- `archetype` — `BombLauncherState` 보유 → `BombThrow` · `HazardCastState` → `HazardCast` · `VolleyFireState`+`DeployedFacing` → `FacingVolley` · 그 외 `Standard`. **판정 순서를 이대로 고정**한다(폭탄맨도 facing 을 갖는다 — 먼저 걸러야 한다).
- `route` — `ProjectileRef.movement`(`MovementKind`)를 번역. 단 `archetype == BombThrow` 면 선언과 무관하게 `Grenade`(계약 6). `ProjectileRef` 없으면 `None`.
- 나머지는 기존 조회 재사용: `TargetsEnemies`, `HasPositiveDamageOutput`, `LethalTimer`, `DreamCocoon`.

### 소비처 수렴

`WouldApply` 시그니처를 `(DreamcatcherCard, in DcHostProfile)` 로 접는다 — bool 인자 5개가 profile 하나로 접히고, 새 host 속성이 생겨도 시그니처가 흔들리지 않는다. 내부는 "메커닉/모드 중 하나라도 `DcRejectReason.None` 이면 true"(계약 4의 카드 단위 해석).

bake 루프는 각 메커닉 진입 직후 **한 줄**로 판정한다:

```csharp
var reason = DcApplicability.EvaluateMechanic(m.payload.kind, m.trigger.kind, hostProfile);
if (reason != DcRejectReason.None) { Debug.LogWarning(...reason...); continue; }
```

이 호출이 대체하는 **기존 host 가드**(제거 대상):
- ProjectileToTarget 의 `TargetsEnemies` 게이트(`:493`, unit-trigger 계약 10)
- HeavyStrike 의 `HasPositiveDamageOutput` 게이트
- LethalTimer / DreamCocoon 이중 상태 preflight
- attackMods 의 `ProjectileRef` 유무 게이트 → `route == Homing` 으로 **강화**

**남기는 것**: `magnitude <= 0`, `projectile == null`, `duration <= 0`, gate combo, `attachType` 제한 — 전부 카드 데이터 검증이라 unit 0 범위 밖이다(계약 유지).

### 잠금 / 해제 표

| 조합 | unit 1 이후 | 해제 |
|---|---|---|
| 비수 × 폭탄맨·캐스터4 | 거절 `NoEventPoint` | **unit 3·4** |
| 빙결·밀치기·자장가·출혈·동상 × 폭탄맨·캐스터4 | 거절 `NoEventPoint` → 사건 개통 후에도 `NeedsTargetContext` | **영구**(계약 9) |
| 통통구슬 × 머신거너·아틸러리·폭탄맨 | 거절 `NeedsHomingRoute` | **별도 spec**(방향탄 bounce 개통) |
| 비수 × 힐러 | 거절 `NeedsEnemyTargeting` | 영구 |
| 그 외 현행 부착 가능 조합 | **변화 없음** | — |

unit 4 완료 기준에서 이 표를 재확인한다(잠갔다 여는 왕복을 잊지 않기 위해).

## 완료 기준

- [ ] 컴파일 클린 + EditMode 전체 그린(사전 실패 제외).
- [ ] **전수 행렬** `DcApplicabilityMatrixTests`: 카탈로그 전 `DreamcatcherCard` × 전 `DefenderUnitData` 조합에 대해, 카드의 각 메커닉/모드 판정이 위 표와 일치. `AssetDatabase.FindAssets` 로 실제 에셋을 읽어 **다음 유닛/카드 추가 시 자동으로 커버**되게 한다.
- [ ] 거절은 **무차감**: `attached == 0` → `-1` 반환 → `HandController.CommitAttach` 가 Spend 전에 반환(기존 계약 유지). 부분 무효는 부착되고 살아남은 메커닉만 동작.
- [ ] 기존 `DreamcatcherAttachEvalTests` 전 케이스가 새 시그니처로 통과 — **UI 판정 결과가 바뀌지 않았음**의 증거.
- [x] Play 확인: 배치 → `BuildHostProfile` → `Would/Apply` 실측. 전 조합이 잠금 표와 일치.

  | host | archetype/route | 비수 | 통통구슬 | 빙결 |
  |---|---|---|---|---|
  | MachineGunner | FacingVolley/Directional | 0 | **-1** | 0 |
  | BombMan | BombThrow/Grenade | **-1** | **-1** | **-1** |
  | FireCaster | HazardCast/None | **-1** | **-1** | **-1** |
  | Archer | Standard/Homing | 0 | 0 | 0 |

  Play 가 잡은 결함 1건: `FacingVolley` 판별이 `DeployedFacing`(조준 완료 여부)을 요구해 조준 전 머신거너가 `Standard` 로 판정됐다 → `VolleyFireState` 단독 판별로 수정(`12958858`). 지금은 두 아키타입의 판정이 같아 무해했지만 unit 3·4 에서 규칙이 갈리면 잠복 버그였다.

## 주의 (다음 단위로 넘기는 것)

- **코스트 상한은 10** (`CostRuntime._max`). 라이브 검증에서 `AddCost(90)` 는 클램프된다 — 여러 유닛을 배치하려면 `_max`/`_current` 를 reflection 으로 올려야 한다.
- unit 3·4 는 `DcApplicability.HasEventPoint` 의 `AttackN` 줄만 바꾸면 개통된다. `DcApplicabilityTests.AttackN_HasNoEventPoint_OnBombThrowAndHazardCast` 의 기대값이 뒤집히는 것이 곧 해제의 증거다.

---

확인 일자 / 커밋: 2026-07-27 · `81817768`(구현) + `12958858`(Play 실측 수정)
