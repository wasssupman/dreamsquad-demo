# 0 — 전수 조사 확정본 + 시그니처 고정

## 목적

이전 대상과 그 authoring 출처를 **확정**한다. rev 3 은 이 조사를 미룬 채 추정을 units
1~5 에 확정문으로 박아 착수 불가 판정을 받았다. 아래 표는 rev 4 작성 시 **에셋 YAML 과
코드를 직접 읽어** 채운 값이다 — 추정 없음.

## 변경 대상

이 문서만. 코드 0 · 에셋 0.

## 표 1 — 이전 대상 12행 (확정)

**유닛 베이크** (`BattleBridge.BakeNightmareMechanics`, `AttackUnitData.nightmareMechanics`
— **적 전용**. 시트 비구동):

| 유닛 | 발동 조건 | 효과 | 값 | 이전 unit |
|---|---|---|---|---|
| 마메모 | 주기 3.5s | AreaSleep(16) | 3명 / 반경 4 / 2.5s | 2 |
| 마메모 | 경계 0.34 | GrantShield(19) | 350 / self | 4 |
| 마메모 | 주기 2.5s | GrantShield(19) | 60 / 반경 4 | 3 |
| 나이트메어 | 주기 10s | EmitProjectilePattern(17) | 패턴 참조 | 3 |
| 나이트메어 | 주기 0.5s | AllyMoveSpeedAura(9) | +20% / 반경 3 / 0.6s | 3 |
| 나이트메어 | 주기 0.1s | EmitProjectilePattern(17) | 패턴 참조 | 3 |
| 짱쎈놈 | 경계 0.2 | SelfTileAoe(2) | 60 / 반경 2 | 4 |
| 짱쎈놈 | 경계 0.5 | SelfBlink(6) | 밀집 2 / 링 6 | 4 |
| 짱쎈놈 | 경계 0.9 | SelfBlink(6) | 밀집 2 / 링 6 | 4 |
| 짱쎈놈 | 경계 0.8 | UltimateLeap(18) | 밀집 2 / 링 6 / 예고 2s | 4 |

**카드 부여** (`BattleBridge.Dreamcatcher.ApplyDreamcatcherCardToUnit` — 방어유닛.
**시트 구동** → authoring 무변경, 어댑터로 수렴):

| 카드 | 발동 조건 | 효과 | 값 | 이전 unit |
|---|---|---|---|---|
| 빈사폭주 `Card_LastStand` | 경계 0.7 | SelfStatBuff(12) | +30% | 5 |
| 진동갑주 `Card_TremorPlate` | 경계 0.7 | SelfTileAoe(2) | 15 / 반경 1 | 5 |

**카드 주기 트리거 = 0장**(전수). 카드 `EmitProjectilePattern` 은 bake 가 loud 거절하고
그 거절을 EditMode 테스트가 고정한다 — 열지 않는다.

## 표 2 — 시트 왕복 (확정)

`DcSheetApplier.OverlayMechanics` 가 덮는 것은 **`DreamcatcherCard.mechanics` 뿐**이다.
`AttackUnitData.nightmareMechanics` 를 만지는 임포터는 리포에 없다.
→ **보스 10행 = 시트 무관(이전 자유)** / **카드 2행 = 시트 구동(계약 7 로 authoring 보존)**
→ **이 spec 의 시트 손실 0.** 계약 9 확정.

## 표 3 — 로직 파일 (감시 분기와 1:1)

주기 4분기 · 경계 5분기 = 9분기 → 로직 8개(GrantShield 가 self·반경 겸직):
`AreaSleepSkill` · `GrantShieldSkill` · `AllyMoveSpeedAuraSkill` ·
`EmitProjectilePatternSkill` · `SelfStatBuffSkill` · `SelfBlinkSkill` ·
`SelfTileAoeSkill` · `UltimateLeapSkill`. **누락·잉여 0**(critic 검산).

## 시그니처 고정

```csharp
// 저작 — Assets/_Project/Scripts/Data/UnitSkills/  (UnityEngine O / Battle X)
public abstract class UnitSkillDef : ScriptableObject
{
    public string skillName;      // 콘텐츠 이름("자장가") — 로그·후속 툴팁
    public DcTriggerSpec when;    // 발동 조건 = 데이터 (계약 2)
    public abstract SkillKind Kind { get; }
    public virtual bool Validate(out string reason);   // bake 가드 문자열의 이사처
}
// 로직 — Assets/_Project/Scripts/Battle/Combat/Skills/  (UnityEngine X)
readonly struct AreaSleepParams { /* in DcTriggerSlot 위의 이름 붙은 뷰 */ }
static class AreaSleepSkill { static void Execute(in AreaSleepParams p, ref SkillContext ctx, Entity executor); }
```

번역(SO 타입 필드 → `DcTriggerSlot` 스칼라)은 **브리지 bake 의 case 하나**가 한다 —
`DefenderAbilityData` 선례("해석은 브리지 번역자 단독")와 같은 형태.

## 완료 기준

- [ ] 위 표 3개가 이 문서에 확정 상태로 존재(rev 4 작성 시 완료 — 이 unit 은 검토·승인만)
- [ ] units 1~6 의 변경 대상이 표 1 의 unit 배정과 모순 없음
- [ ] docs 커밋 1개
