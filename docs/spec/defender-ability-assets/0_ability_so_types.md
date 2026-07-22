# 0 — 능력 SO 타입 계층 (additive)

## 목적

`DefenderAbilityData` base + 캐스트 4종 구체 SO + `DefenderUnitData.abilities` 리스트/헬퍼를
추가한다. **기존 flat 필드 무변경**(additive) — 이 단위만으로 동작 변화 0, compile green.

## 변경 대상

- `Assets/_Project/Scripts/Data/Abilities/DefenderAbilityData.cs` (신규) — 추상 base
- `Assets/_Project/Scripts/Data/Abilities/DirectionalVolleyAbility.cs` (신규)
- `Assets/_Project/Scripts/Data/Abilities/HazardCastAbility.cs` (신규)
- `Assets/_Project/Scripts/Data/Abilities/ShieldCastAbility.cs` (신규)
- `Assets/_Project/Scripts/Data/Abilities/BombThrowAbility.cs` (신규)
- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` — `abilities` 리스트 + 헬퍼 (필드 삭제는 unit 2)

## 구현

- **base**: `public abstract class DefenderAbilityData : ScriptableObject { public string id; public virtual bool RequiresFacing => false; }`
  — `id` = 시트 매칭키 예약(계약 5). Battle/Entities 참조 금지(계약 2).
- **구체 4종** (필드 = 기존 flat 필드의 의미명 이동, 기본값 동일):
  - `DirectionalVolleyAbility`: `shotCount=1`·`shotIntervalSec`·`spreadAngleDeg`. `RequiresFacing=>true` (단발 방향 유닛 = shotCount 1). `CreateAssetMenu`.
  - `HazardCastAbility`: `castRange`·`cooldown`·`HazardCastKind kind`·`HazardSO zoneHazard`·`BlockingHazardSO blockingHazard`·`footprintWidth=1`·`footprintHeight=1`.
  - `ShieldCastAbility`: `cooldown`·`amount`·`targetCount=1`·`ShieldTargetFilter filter`.
  - `BombThrowAbility`: `landingTiles`·`travelSec`·`fuseSec`·`aoeTileRange=1`·`aoeTargetCap`·`arcHeight`·`damage`·`sleepSec`·`stunSec`. `RequiresFacing=>true`.
- **DefenderUnitData**: `[Header("Abilities")] public List<DefenderAbilityData> abilities = new();`
  + `public T GetAbility<T>() where T : DefenderAbilityData`(첫 매치 or null, null 원소 스킵)
  + `public bool RequiresFacing`(abilities 중 하나라도 true).
- 네이밍/메뉴: `CreateAssetMenu(menuName = "Wassup/Ability/{Kind}")`.

## 완료 기준

- [x] compile 0 (신규 .cs — `refresh_unity(scope=all)`).
- [x] 기존 전 유닛 동작 불변(코드 소비처 무변경 — additive 확인은 diff 로 자명).

확인 2026-07-22 · compile 0. additive — 소비처/flat 필드 무변경.
