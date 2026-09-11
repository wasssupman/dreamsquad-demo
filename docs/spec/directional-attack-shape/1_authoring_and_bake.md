# 1 — 저작 스키마와 bake

## 목적

두 SO(`DefenderUnitData` · `AttackUnitData`)에 공통 저작 struct `AttackShape` 를 얹고,
`AttackState` 에 bake 된 값을 싣는다. **이 unit 이 끝나도 라이브 동작은 무변**(게이트 소비처는 unit 2).

## 스키마 탐색 결과 (2026-09-11 · 사용자 확정)

리포에 「kind + 파라미터」 저작이 4가지 있고 둘은 실패 사례로 남아 있다:

| 선례 | 방식 | 결과 |
|---|---|---|
| `AttackOutput` | enum `kind` + 이름 붙은 flat 필드, 필드 주석이 「어느 kind 전용」 명시 | ✅ 4 kind × 7 필드가 안 꼬임 |
| `ProjectilePatternData` | `[Header]` 그룹 + flat + `[Tooltip]` | ✅ 인스펙터에서 읽힘 |
| `DcTriggerSlot.tileRange` | **스칼라 하나가 13가지 뜻 겸직** | ❌ `SkillParams` 헤더가 「못 읽게 만든 원인」으로 기록 |
| `HazardShape` | 도형을 **사전 정의 enum**(`Square3x3`/`RadiusSquare`) | ❌ 같은 도형의 두 표현 · 수치가 enum 이름에(제약 6) |

`[SerializeReference]` 는 리포 0건(타입 리네임 시 데이터 소실). 별도 SO 는 3개 숫자에 에셋 층(제약 8).

**채택 = `AttackOutput` 형태.** 형마다 파라미터가 **하나**라 겸직 압력이 없다 — `angleDeg`(도, 거리에
비례해 벌어짐)와 `width`(타일, 평행)는 둘 다 «측면 한계» 지만 **단위와 기하가 달라** 한 필드로 접으면
`tileRange` 의 재현이다.

## 변경 대상

- `Data/AttackShape.cs` 신규 — 저작 struct + enum
- `Data/DefenderUnitData.cs` · `Data/AttackUnitData.cs` — 필드 1개씩 + `OnValidate` 경고
- `Combat/AttackState.cs` — bake 필드 4개
- `Combat/AttackShapeBake.cs` 신규 — 저작 → bake 순수 변환 + 정의역 검증
- `Bridge/BattleBridge.cs` — `AttackState` 생성 3곳(방어유닛 `:8325` · 순찰 아군 `:8611` · 적 `:10717`)
- `Tests/EditMode/AttackShapeBakeTests.cs` 신규

## 구현

```csharp
public enum AttackShapeKind : byte { Circle = 0, Rect = 1 }

[Serializable]
public struct AttackShape
{
    // ⚠ 도형은 「얼마나 멀리」를 정하지 않는다 — 반경도 길이도 attackRange 가 소유한다.
    public AttackShapeKind kind;
    [Range(15f, 360f)] public float angleDeg;   // Circle 전용. 360 = 전방위 = 오늘. 0 = 미저작 → 360
    [Min(0f)]          public float width;      // Rect 전용. 타겟 방향 기준 폭(타일)
    public static AttackShape Omni => new AttackShape { kind = AttackShapeKind.Circle, angleDeg = 360f };
}
```

- SO 필드: `public AttackShape attackShape = AttackShape.Omni;` — Unity 는 YAML 에 키가 없으면 C# 초기값을
  유지한다(`visible = 1` 선례). **그러나 정본은 bake 폴백이다**(아래) — 신규 에셋 생성·시트 경로에서 0 이
  샐 수 있다.
- 저작 enum 에 `None` 을 **두지 않는다** — `Circle/360` 이 항등원이라 `None` 은 같은 것의 두 번째 표현이다
  (`HazardShape` 가 그 중복으로 실패). 「또 다른 X」는 kind 하나 + 전용 필드 하나로 자란다.

### bake — `AttackState` 에 4필드

```csharp
public byte  shapeKind;       // 0 = Omni · 1 = Sector · 2 = Rect   ⚠ 저작 enum 과 번호가 다르다
public float shapeSinHalf;    // Sector 전용
public float shapeCosHalf;
public float shapeHalfWidth;  // Rect 전용
```

- **bake 쪽에만 `Omni = 0` 이 있는 이유**: `default(AttackState)` 가 안전해야 한다. 코드가 만드는
  `AttackState`(`TauntAttackGrantSystem:47` 도발 공격 · `BattleBridge:6510` v1 투사체)는 도형을 모르고
  0 으로 남는다 — 그게 곧 오늘 동작이어야 한다. 저작 쪽은 항등원이 이미 있어 `None` 이 중복이고,
  bake 쪽은 0 이 안전값이어야 해서 sentinel 이 필요하다. **둘의 번호를 맞추려 들지 말 것.**
- `AttackShapeBake.From(AttackShape, string ownerName)` 순수 static:
  - `Circle`: `angleDeg <= 0 || angleDeg >= 360` → Omni. `(180, 360)` → **에러 로그 + Omni**
    (README 계약 5 — `SkillCone` bake 가 반각 ≥ 90 을 거절하는 규율). `(0, 180]` → Sector,
    `sin/cos(angleDeg/2)`.
  - `Rect`: `shapeHalfWidth = width * 0.5` → Rect. `width < 0` 은 `[Min]` 이 막는다.
  - 로그는 bake 호출부(브리지)가 찍는다 — 순수 함수는 `bool ok` 만 돌려준다(제약 10).
- `OnValidate`(두 SO): 도형이 Omni 가 아닌데 `attackTargetCount <= 1` → 경고 「도형을 저작했는데
  동시 타격 1 — 효과 0」(README 계약 9).
- 시트 연동 안 함 — `UnitStatImportDto` 에 없으면 로그인 임포트가 덮지 않는다. 후속 후보.

## 완료 기준

- [ ] bake 테스트: `0 → Omni` · `360 → Omni` · `120 → Sector(sin60, cos60)` · `180 → Sector(1, 0)` ·
      `270 → Omni + ok=false` · `Rect(1.0) → halfWidth 0.5` · `default(AttackShape) → Omni`.
- [ ] 27 방어유닛 + 적 SO 전건이 bake 에서 Omni(저작 0) — 에셋 lane 단언 1건(드리프트 그물).
- [ ] 브리지 3곳이 같은 bake 함수를 지난다(grep 으로 `AttackShapeBake.From` 3건).
- [ ] 라이브 sim 무변: 골든 코퍼스 전건 초록 · EditMode 코어+에셋 lane 초록.
