# 8 — 대가와 보상 (코스트 · 스킬 재발동 · 체력 회복)

## 목적

재배치를 **공짜 이동**에서 **코스트로 사는 재정비**로 바꾼다. 확정 프레임에 배치 코스트를 내고,
착지해 복귀하는 순간 배치 스킬이 다시 터지고 체력이 찬다. 상한 1 이 만든 "남는 코스트"의
소비처이자 "한 기를 계속 굴린다"는 축이다 (README 계약 1·4·12).

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.Relocation.cs` — 코스트 게이트/차감, on-place 재무장,
  활성화 꼬리 `ActivateRelocatedDefender`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 효과 타일 가드 분리. 호출처가 **둘**이다:
  `TriggerDeploymentOnPlaceSkill`(드래그 배치) · `TriggerOnPlaceAndSynergy`(즉시 배치). 한쪽만 고치면
  경로에 따라 규칙이 갈린다.
- `Assets/_Project/Scripts/Data/RelocationSettings.cs` — `refitHealRatio`
- `Assets/_Project/Scripts/UI/DefenderRelocationController.cs` — 활성화 호출 2곳을 새 꼬리로 교체
- **`Assets/_Project/Tests/PlayMode/RelocationPlacementSessionTest.cs`** — 옛 계약을 명시적으로
  단정하는 줄이 있다. 이 unit 이 들어가면 **반드시 빨개진다**:
  `Assert.AreEqual(costBefore, ..., "relocation costs no cost (계약 1)")` → 코스트가 유닛 코스트만큼
  **줄었음**을 단정하도록 뒤집는다.
- `Assets/_Project/Tests/PlayMode/RelocationSmokeTest.cs` — "on-place 는 가드로 재발화 없음" 주석 갱신.
- 테스트 신규: `Tests/EditMode/RelocationCheckTests.cs` 확장

## 구현

**코스트** — 판정은 `CanRelocateDefender` 안, `RelocationCheck`(공간 판정) **뒤**에 둔다.
공간 사유가 자원 사유를 이긴다 (구조 > 자원 — defender-board-limit 계약 4 와 같은 순서).
차감은 `TryBeginDefenderRelocation` 안에서 **스왑 직전** 1회.

```csharp
// 판정 → 차감 → 스왑 순서를 지킨다. 차감 뒤에 실패 경로가 있으면 코스트가 유실된다.
if (costRuntime != null && !costRuntime.TrySpend(binding.data.cost)) { ... InsufficientCost }
```

**스킬 재발동** — 스왑 직후 한 줄.

```csharp
_onPlaceTriggeredEntities.Remove(entity); // 재무장 — 활성화가 부르는 TriggerDeploymentOnPlaceSkill 이 다시 돈다
```

**효과 타일 분리** — `ApplyEffectTileIfAny` 호출 **2곳 모두**를 신규 `_effectTileAppliedEntities`
가드로 감싼다(엔티티당 영구 1회). 두 가드는 `_onPlaceTriggeredEntities` 를 비우는 같은 리셋 지점에서
함께 Clear.

⚠ **이유를 정확히 적는다** — 틀린 이유를 남기면 다음 사람이 "덮어쓰기니까 괜찮네" 하고 가드를 푼다.
효과 타일은 병합키(같은 유닛 · `stackId=2` · 같은 stat)라 **같은 stat 은 refresh 로 덮어써서 겹치지
않는다**. 문제는 `duration=∞` 인데 **회수 경로가 없다**는 것이다: 공속 타일 → 공격력 타일로 옮기면
공격력이 붙고 **공속이 영원히 남는다**(stat 이 다르면 슬롯이 갈리므로). `ApplyEffectTileIfAny` 위의
"유닛 제거/재배치 기능이 없어 revocation 불요" 주석은 재배치가 생긴 지금 **이미 stale** 하다 —
같이 고친다.

**회복 + 밀치기 + 활성화 = 한 꼬리** — 컨트롤러가 `ActivateDeployedDefender` 를 부르던 2곳
(정상 착지 / 즉시 완결)을 하나로 교체한다.

```csharp
// 회복 비율은 인자로 받는다 — 노브는 컨트롤러(RelocationSettings)가 소유, 브리지는 값만 쓴다.
public void ActivateRelocatedDefender(Vector2Int cell, Entity entity, float healRatio)
```

⚠ **꼬리 맨 앞에서 바인딩을 확인하고 통째로 물러난다.** `ActivateDeployedDefender` 는 바인딩이
안 맞으면 **조용히 리턴**하므로, 순서대로 늘어놓기만 하면 **활성화가 실패해도 회복은 들어간다**.

순서: `ApplyOnPlacePush` → `ActivateDeployedDefender`(스킬 재발동 포함) → `IncomingHeal` append
(`Health.max * healRatio`). 밀치기를 확정이 아닌 **여기서** 부르는 이유는 확정 시점엔 유닛이 아직
비행 중이라 **빈 칸을 밀게** 되기 때문이다(즉시 배치 경로가 on-place 와 밀치기를 한 묶음으로 부르는
것과 같은 모양).

## 완료 기준

- 컴파일 통과.
- **EditMode 는 신규 테스트가 없다.** 순수 판정(`RelocationCheck`)은 unit 8 이 건드리지 않고(그건
  unit 9), 코스트·회복은 둘 다 런타임 상태(`CostRuntime`/`Health`)를 필요로 해 EditMode 에서
  관측되지 않는다. 회복량은 `max * ratio` 한 줄이라 호출처 하나뿐인 순수 함수로 빼지 않는다
  (CLAUDE.md 제약 10 의 과잉 추상화 단서). 기준은 **기존 EditMode 스위트가 계속 초록**인 것.
- **PlayMode**: 재배치 1회에 ⑴ 코스트가 유닛 코스트만큼 줄고 ⑵ 활성화 후 HP 가 `max*0.5` 만큼 오르며
  ⑶ 배치 스킬 로그가 다시 찍힌다. 회귀로 **기존 재배치 3스위트가 계속 통과**해야 한다(코스트 시딩은
  이미 `AddCost(1000)` 로 돼 있어 깨지지 않는다 — 깨지는 건 위에 적은 "코스트 0" 단정 한 줄뿐).
- **효과 타일 미재적용**: 효과 타일은 맵 저작물(`_effectTilesByCell`)이라 라이브 맵에 타일이 없으면
  PlayMode 로 관측되지 않는다. 검증은 **가드가 두 호출처 모두에서 분리돼 있다는 코드 수준 확인**까지가
  기준이고, 실측은 효과 타일이 저작된 맵이 생기면 그때 얹는다.
- **코스트 무한 엔진 회귀**: 스카우트(배치 스킬 = 코스트 획득 1, 코스트 3)를 재배치하면 순손실 2 —
  재배치가 코스트를 만들어내지 않는다.
- 코스트 부족 상태로 확정을 시도하면 유닛이 제자리에 남고 코스트가 줄지 않는다(유실 없음).

> **확인 2026-08-13** · 커밋 `568d2f9f` — 사용자 Play 확인 완료.
> 자동 검증: EditMode 2344 중 4 실패(전부 타 세션 `map-rework` 통로 폭 계약, 신규 회귀 0) ·
> PlayMode 재배치 9/9. 로그 실측 `Refit heal 68 ... ratio 0.50` = 레인저 최대 체력 136 의 절반.
