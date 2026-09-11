# 1 — 저작 스키마와 bake

## 목적

두 SO(`DefenderUnitData` · `AttackUnitData`)에 공통 저작 struct `AttackShape` 를 얹고 `AttackState` 에 bake 된
`AttackShapeBaked` 를 싣는다. **이 unit 이 끝나도 라이브 동작은 무변**(소비처는 아직 Omni 를 넘긴다, unit 2).

## 스키마 (rev 1 탐색 결과 · 사용자 확정)

리포 선례 4건 — `AttackOutput`(kind + 이름 붙은 flat 필드) ✅ · `ProjectilePatternData`(Header+Tooltip) ✅ ·
`DcTriggerSlot.tileRange`(스칼라 하나가 13가지 뜻 겸직) ❌ `SkillParams` 헤더가 실패로 기록 · `HazardShape`
(사전 정의 도형 enum, `Square3x3` = `RadiusSquare(1)` 중복) ❌. `[SerializeReference]` 는 리포 0건.

**채택 = `AttackOutput` 형태. 형마다 파라미터 하나.** `angleDeg`(도)와 `width`(타일)는 둘 다 «세로 허용»이지만
단위·기하가 달라 한 필드로 접으면 `tileRange` 의 재현이다.

```csharp
public enum AttackShapeKind : byte { Circle = 0, Rect = 1 }

[Serializable]
public struct AttackShape
{
    // ⚠ 도형은 「얼마나 멀리」를 정하지 않는다 — 길이·반경은 attackRange 가 소유한다.
    //    여기 있는 것은 「보는 쪽으로 얼마나 좁게」뿐이다. 방향은 항상 캐릭터가 보는 좌/우.
    public AttackShapeKind kind;
    [Range(15f, 360f)] public float angleDeg;   // Circle 전용. 보는 쪽 부채꼴 전체각. 360 = 전방위 = 오늘. 0 = 미저작 → 360
    [Min(0f)]          public float width;      // Rect 전용. 띠의 세로 폭(타일). 찌르는 방향에 수직
    public static AttackShape Omni => new AttackShape { kind = AttackShapeKind.Circle, angleDeg = 360f };
}
```

- SO 필드 `public AttackShape attackShape = AttackShape.Omni;` — YAML 무키면 C# 초기값 유지(`visible = 1` 선례).
  **정본은 bake 폴백**(신규 에셋·시트 경로에서 0 이 샌다).
- 저작 enum 에 `None` 없음 — `Circle/360` 이 항등원이라 `None` 은 같은 것의 두 번째 표현(`HazardShape` 실패).

## 변경 대상

- `Data/AttackShape.cs` 신규 · 두 SO 에 필드 1개씩
- `Combat/AttackState.cs` — `public AttackShapeBaked shape;`
- `Combat/AttackShapeBake.cs` 신규 — 저작 → bake 순수 변환 + 정의역 검증(`bool ok`)
- `Bridge/BattleBridge.cs` — `AttackState` 생성 3곳(방어유닛 `:8325` · 순찰 아군 `:8611` · 적 `:10717`)
- `Tests/EditMode/AttackShapeBakeTests.cs` 신규

## bake

`AttackShapeBaked { byte kind; float sinHalf, cosHalf, halfWidth; }` — **`kind 0 = Omni`** (저작 enum 과 번호가
다르다: bake 는 `default(AttackState)` 가 안전해야 한다 — 도발 공격 `TauntAttackGrantSystem:47` · v1 투사체
`BattleBridge:6510` 은 도형을 모르고 0 으로 남는다. 저작 쪽은 항등원이 있어 `None` 이 중복. **둘의 번호를
맞추려 들지 말 것.**) `1 = Sector` · `2 = Band`.

- `Circle`: `angleDeg <= 0 || >= 360` → Omni. `(180, 360)` → **에러 로그 + Omni**(계약 7 · `SkillCone` bake 규율).
  `(0, 180]` → Sector, `sin/cos(angleDeg/2)`.
- `Rect`: `halfWidth = width/2` → Band.
- 로그는 브리지가 찍고 순수 함수는 `ok` 만 돌려준다(제약 10).
- `OnValidate`: 경고 없음(rev 1 계약 9 폐기 — 획득도 자르므로 단일 타겟 도형이 유효하다).
- 시트 연동 안 함.

## 완료 기준

- [ ] bake: `0→Omni` · `360→Omni` · `120→Sector(sin60,cos60)` · `180→Sector(1,0)` · `270→Omni+ok=false` ·
      `Rect(1.0)→halfWidth 0.5` · `default(AttackShape)→Omni`.
- [ ] 27 방어유닛 + 적 SO 전건 Omni — 에셋 lane 단언 1건(드리프트 그물).
- [ ] 브리지 3곳이 같은 bake 를 지난다(grep `AttackShapeBake.From` 3건).
- [ ] 라이브 무변: 골든 전건 초록 · EditMode 코어+에셋 lane 초록.
