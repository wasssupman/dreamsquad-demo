# 1 — dirty 판정 순수 함수

## 목적

"작업본이 저장본과 다른가" 를 아키텍처를 모르는 순수 함수로 결정한다. 플래그를 들고 다니지 않는 이유는 정확성이다 — 유닛을 뺐다 다시 같은 자리에 넣으면 내용이 동일하므로 dirty 가 **꺼져야** 한다. 변이마다 플래그를 세우는 방식은 이 경우 거짓말을 하고, 없어도 될 경고 팝업을 띄운다.

## 변경 대상

- 신규: `Assets/_Project/Scripts/Core/Profile/PresetDiff.cs`
- 신규: `Assets/_Project/Tests/EditMode/Profile/PresetDiffTests.cs`

## 구현

```csharp
public static class PresetDiff
{
    // 슬롯 기반 프리셋(스쿼드): 이름 + 7칸 + 4칸을 순서까지 포함해 비교.
    public static bool IsSquadDirty(
        string workingName, IReadOnlyList<string> workingUnits, IReadOnlyList<string> workingStones,
        SquadPreset stored);

    // 가변 길이 프리셋(드림캐쳐): 이름 + 카드 순서열 비교.
    public static bool IsDeckDirty(
        string workingName, IReadOnlyList<string> workingCards,
        DreamcatcherPreset stored);
}
```

규약:
- `stored == null` → 작업본이 완전히 비어 있지 않으면 dirty (신규 프리셋 편집 중)
- **빈칸 정규화**: `null` 과 `""` 를 같은 값으로 취급한다. 작업본은 `""`, 저장본은 JSON 왕복 후 `null` 일 수 있어(`JsonUtility` 가 컬렉션 항목을 null 로 남기는 경로) 정규화 없이는 로드 직후 무조건 dirty 로 뜬다
- **순서 유의**: 스쿼드 7칸은 슬롯 위치가 의미를 갖고(헤더 스트립 표시 순서), 드캐 카드열도 순서를 보존한다. 집합 비교가 아니다
- **이름은 dirty 에 포함**된다. 단 이름 입력은 `onEndEdit` 에서만 작업본에 반영된다(unit 2) — 키스트로크마다 dirty 가 뜨는 것과 한글 IME 조합 중 상태를 동시에 피한다
- **길이 차이는 즉시 dirty** (드캐 9장 vs 10장)

Unity 타입 의존 0 — 입력은 `string`/`IReadOnlyList<string>` 과 프리셋 POCO 뿐이라 EditMode 에서 직접 구동된다(제약 10).

## 완료 기준

- [ ] 컴파일 그린
- [ ] `PresetDiffTests` 그린:
  - 동일 내용 → `false`
  - 유닛 1칸 교체 → `true`
  - 유닛을 뺐다 **같은 자리에** 되넣기 → `false` (플래그 방식이면 실패하는 케이스)
  - 유닛을 슬롯 0↔3 으로 **자리만 교환** → `true` (순서 유의 증명)
  - 스톤만 변경 → `true` (통합 저장 증명)
  - 이름만 변경 → `true`
  - `""` vs `null` 혼재 → `false` (빈칸 정규화 증명)
  - `stored == null` + 빈 작업본 → `false` / 내용 있는 작업본 → `true`
  - 드캐: 같은 카드 집합이지만 순서 다름 → `true`, 길이 다름 → `true`
- [ ] `PresetDiff` 에 `UnityEngine` using 없음

---

**검증 기록 2026-07-30 · `5592b676`** — 컴파일 errors=0 · `PresetDiffTests` 18건 그린(뺐다 되넣기·자리 교환·스톤만·이름만·null≡""·길이차·덱 순서) · `PresetDiff` 에 `UnityEngine` using 없음 확인.
