# 1 — 라우팅 추출 + 공간성 카탈로그 (순수 함수)

> 결정 Q1. 선행: 0a(도형은 `None | Circle` 둘).

## 목적

「이 카드는 이 host 에서 어떤 도형·어떤 반경으로 작용하나」를 **ECS 무참조 plain 값**으로 답한다.
표기가 판정과 갈릴 수 있는 지점은 ① 트리거×페이로드→concrete 라우팅 ② concrete→반경 식, 둘이다.
①은 bake 와 **같은 함수**를 부르게 해서, ②는 EditMode 로 고정해서 닫는다.

## 변경 대상

- **신설** `Scripts/Core/Dreamcatcher/DcSkillRouting.cs` —
  `public static int SkillIdFor(DcTriggerKind trigger, DcPayloadKind kind)`.
  `BattleBridge.cs:9918 SkillIdForMechanic` + `SkillIdForPayload` 본문을 **그대로 이동**(주석 포함), 브리지의
  두 private 은 위임. 자리가 `Core/`(= `Wassup.Runtime`)인 이유: `Wassup.Skills` 는 별개 어셈블리이고
  `noEngineReferences: true` 라 `Wassup.Data` enum 도 `Debug` 도 못 쓴다. `Runtime` 이 `Skills` 를 이미 참조하므로
  concrete `Id` 상수는 여기서 읽힌다. asmdef 무변.
- **신설** `Scripts/Core/Dreamcatcher/DcRangeCatalog.cs`
  ```csharp
  public enum DcRangeShape : byte { None = 0, Circle = 1 }
  public readonly struct DcRangeSpec { public readonly DcRangeShape shape; public readonly float radiusTiles; }
  public static DcRangeSpec Resolve(int skillId, int tileRange);     // concrete → 도형 (fail-closed: None)
  public static DcRangeSpec ResolveCard(DreamcatcherCard card);      // mechanics 순회 · 단일 도형 불변식
  ```
- **신설 테스트** `Tests/EditMode/DcSkillRoutingTests.cs` · `Tests/EditMode/DcRangeCatalogTests.cs`(코어 lane) ·
  `Tests/EditModeAssets/DcCardRangeInvariantTests.cs`(에셋 lane — `Card_*.asset` 전 장).

## 구현

| skillId | 결과 |
|---|---|
| `SelfAreaBlastSkill` · `AreaSleepSkill` · `AreaCcSkill` · `AreaDotSkill` · `AreaStackSkill` · `AreaTauntSkill` · `AllySpeedAuraSkill` · `AllyStatAuraSkill` · `OpponentStatAuraSkill` · `GrantShieldSkill` | `tileRange > 0 ? Circle(tileRange + SkillMath.CellHalfWidthTiles) : None` |
| `EmitPatternSkill` | `tileRange > 0 ? Circle(tileRange) : None` — 칸 반폭 없음(사거리 자) |
| `DeathSiteBlastSkill` · `DeathSiteHazardSkill` · `ConeBreathSkill` · 그 외 전부 | `None` |

- 상수는 `SkillMath.CellHalfWidthTiles` **참조**(리터럴 0.5 금지).
- `ResolveCard` 는 `card.mechanics` 만 본다 — `attackMods[].tileRange`(팅김 반경) 는 host 중심이 아니라 카탈로그
  대상이 아니다. 공간 결과가 2개 이상 서로 다르면 첫 것 + `LogWarning` 1회(라이브 안전망).
- `tileRange` 는 **런타임 payload 값**(시트 적용 뒤). bake/UI 시점 전용, per-frame 호출 금지.
- 신규 `.cs` 뒤 `refresh_unity(scope=all)`; `dotnet build` 검증 시 `.csproj` 가 파일을 명시 나열함을 기억.

## 완료 기준

- [ ] `DcSkillRoutingTests`: `SelfTileAoe` × {OnKill, OnDeath, OnRetire} → `DeathSiteBlastSkill.Id` /
      × {OnDamagedN, OnShieldBreak, HealthThreshold} → `SelfAreaBlastSkill.Id` · `None` × {SelfBuffLethal,
      DreamCocoon, BountyMark} · `HealthThreshold × SelfStatBuff` → `ThresholdSelfBuffSkill.Id` · 그 외 =
      `SkillIdForPayload` 와 동일.
- [ ] `DcRangeCatalogTests`: 표의 각 행 · 겸직 kind 는 **값 무관**(`BountyMark`·`SelfStatBuff`·`ApplyStackToTarget`·
      `ProjectileToTarget`·`SelfOrbitProjectile` 에 `tileRange ∈ {0, 1, 5, 30}` 전부 `None`, `GrantShield` 0 → `None`) ·
      미배선 skillId → `None` · 반경이 `CellHalfWidthTiles` 를 따라간다.
- [ ] `DcCardRangeInvariantTests`(에셋 lane): 전 카드 `ResolveCard` 예외 0 · **`mechanics` 와 `attackMods` 양쪽**을
      훑어 카드당 공간 spec ≤ 1 · 오늘 공간 카드 = `cornered_burst` · `shield_burst` · `tremor_plate` · `shield_lull`
      **로그 출력**(리터럴 단언 X — 카드 추가로 빨개지지 않게).
- [ ] 브리지 bake 무변(골든 바이트 무변 — 0a 재베이크 기준) · `Scripts/Battle/**` 변경 0.
