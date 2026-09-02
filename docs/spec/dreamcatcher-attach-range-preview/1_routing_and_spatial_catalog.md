# 1 — 라우팅 추출 + 공간성 카탈로그 (순수 함수)

> 사용자 결정 2026-09-02 Q1: `SkillIdForMechanic` 을 추출해 bake 와 프리뷰가 같은 함수를 부른다.
> 선행: unit 0 (사각이 사라져 카탈로그의 도형은 `None | Circle` 둘이다).

## 목적

「이 카드는 이 host 에서 어떤 도형·어떤 반경으로 작용하나」를 **ECS 무참조 plain 값**으로 답한다.
표기가 판정과 갈릴 수 있는 지점은 ① 트리거×페이로드→concrete 라우팅 ② concrete→반경 식,
둘이다. ①은 bake 와 **같은 함수**를 부르게 해서, ②는 EditMode 로 고정해서 닫는다.

## 변경 대상

- **신설** `Scripts/Core/Dreamcatcher/DcSkillRouting.cs` —
  `public static int SkillIdFor(DcTriggerKind trigger, DcPayloadKind kind)`.
  `BattleBridge.cs:9918 SkillIdForMechanic` + `SkillIdForPayload` 본문을 **그대로 이동**(주석 포함).
  브리지의 두 private 은 이 함수로 위임(호출처 무변). 같은 어셈블리(`Wassup.Runtime`)라 asmdef 무변.
- **신설** `Scripts/Core/Dreamcatcher/DcRangeCatalog.cs`
  ```csharp
  public enum DcRangeShape : byte { None = 0, Circle = 1 }
  public readonly struct DcRangeSpec { public readonly DcRangeShape shape; public readonly float radiusTiles; }
  public static class DcRangeCatalog
  {
      public static DcRangeSpec Resolve(int skillId, int tileRange);           // concrete → 도형
      public static DcRangeSpec ResolveCard(DreamcatcherCard card);            // 카드 단위(단일 도형 불변식)
  }
  ```
- **신설** `Tests/EditMode/DcSkillRoutingTests.cs` · `Tests/EditMode/DcRangeCatalogTests.cs`(코어 lane)
  · `Tests/EditModeAssets/DcCardRangeInvariantTests.cs`(에셋 lane — `Card_*.asset` 46장 훑음).

## 구현

**`Resolve(skillId, tileRange)` 표** — README 분류표의 코드 형태. 기본값은 **`None`(fail-closed)**:
새 concrete 가 배선 없이 들어오면 「범위를 지어내지 않는다」.

| skillId | 결과 |
|---|---|
| `SelfAreaBlastSkill.Id` · `AreaSleepSkill` · `AreaCcSkill` · `AreaDotSkill` · `AreaStackSkill` · `AreaTauntSkill` · `AllySpeedAuraSkill` · `AllyStatAuraSkill` · `OpponentStatAuraSkill` | `tileRange > 0 ? Circle(tileRange + CellHalfWidthTiles) : None` |
| `GrantShieldSkill.Id` | 같은 식 — `tileRange == 0` 은 자기만이라 `None` |
| `EmitPatternSkill.Id` | `tileRange > 0 ? Circle(tileRange) : None` — 칸 반폭 없음(사거리 자) |
| `DeathSiteBlastSkill` · `DeathSiteHazardSkill` · `ConeBreathSkill` · 그 외 전부 | `None` |

- 반경 상수는 `Wassup.Skills.SkillMath.CellHalfWidthTiles` 를 **참조**한다(리터럴 0.5 금지 —
  판정 상수가 바뀌면 표기가 따라간다).
- `ResolveCard(card)`: `card.mechanics` 를 돌며 `SkillIdFor(m.trigger.kind, m.payload.kind)` →
  `Resolve(id, m.payload.tileRange)`. 공간 결과가 **2개 이상이고 서로 다르면** 첫 것을 쓰고
  `Debug.LogWarning` 1회(라이브 안전망). 정식 방어는 에셋 lane 테스트(계약 6).
- `tileRange` 는 **런타임 payload 값**을 받는다(시트가 덮은 뒤). 에셋 값을 캐시하지 않는다
  (메모리 「스킬 밸런스 값은 시트가 정본」).
- bake/UI 시점 전용 — per-frame 호출 금지(managed SO 읽기). unit 3 이 드래그 시작에 1회 호출.

## 완료 기준

- [ ] `DcSkillRoutingTests`: 트리거 분기 핀 — `SelfTileAoe` × {OnKill, OnDeath, OnRetire} →
      `DeathSiteBlastSkill.Id` / × {OnDamagedN, OnShieldBreak, HealthThreshold} → `SelfAreaBlastSkill.Id`
      · `None` × {SelfBuffLethal, DreamCocoon, BountyMark} · `HealthThreshold × SelfStatBuff` →
      `ThresholdSelfBuffSkill.Id` · 그 외는 `SkillIdForPayload` 와 동일.
- [ ] `DcRangeCatalogTests`: ① 표의 각 행 ② **겸직 6종은 값 무관** — `BountyMark`·`SelfStatBuff`·
      `ApplyStackToTarget`·`ProjectileToTarget`·`SelfOrbitProjectile` 에 `tileRange ∈ {0, 1, 5, 30}` 전부
      `None`, `GrantShield` 0 → `None` ③ 미배선 skillId(예: 9999) → `None` ④ 반경이
      `SkillMath.CellHalfWidthTiles` 를 참조(상수 바꿔도 초록).
- [ ] `DcCardRangeInvariantTests`(에셋 lane): 전 카드 `ResolveCard` 예외 0 · 카드당 공간 spec ≤ 1 ·
      오늘 공간 카드 = {궁지폭발, 실드폭발, 진동갑주, 자장가} 4장 **로그 출력**(리터럴 단언은 하지 않는다 —
      카드 추가로 빨개지지 않게).
- [ ] 브리지 bake 경로 무변(골든 바이트 무변 — unit 0 재베이크 직후 기준).
- [ ] sim 파일(`Scripts/Battle/**`) 변경 0.
