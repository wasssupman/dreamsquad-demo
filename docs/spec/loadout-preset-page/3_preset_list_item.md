# 3 — 프리셋 리스트 아이템 (PresetListItemView)

## 목적

프리셋 하나를 표현하는 목록 아이템: 이름 + 유닛 셀 7 + 드림캐쳐 아트 10 + 적용 버튼. 스크롤 목록의
반복 단위.

## 변경 대상

- 신규: `Assets/_Project/Scripts/UI/Outgame/PresetListItemView.cs` (`Wassup.UI`)

## 구현

레이아웃(한 아이템):

```
┌──────────────────────────────────────────────┐
│  프리셋 이름                          [ 적용 ] │
│  [U][U][U][U][U][U][U]        ← PresetUnitCell 7 │
│  [d][d][d][d][d][d][d][d][d][d]  ← 카드 아트 10  │
└──────────────────────────────────────────────┘
```

```csharp
public class PresetListItemView : MonoBehaviour
{
    public event Action ApplyClicked;
    // 아이템 내부 UI 를 빌드하고 프리셋으로 채운다(런타임 빌더). 재바인드 지원.
    public void Build(SquadPreset preset, TMP_FontAsset font);
}
```

- **이름**: `preset.presetName` (빈 값이면 폴백 "프리셋"). 상단 좌측 TMP.
- **유닛 행**: `SquadSave.SlotCount`(7)개의 `PresetUnitCell` 생성. `i < preset.units.Length` 면
  `Set(preset.units[i])`, 아니면 `Set(null)`(빈 셀). `HorizontalLayoutGroup` 배치.
- **아트 행**: `preset.cards` 각 카드마다 `Image` 하나. 아트 유무 폴백은 `DreamcatcherDeckStrip.Refresh`
  규칙 재현 — `card.art != null` → 스프라이트, 아니면 `CardCategoryStyle.ArtFallback(card)` 단색.
  `preserveAspect = true`, 행 높이에 맞게 축소(`HorizontalLayoutGroup` + 셀 사이즈로 "행에 들어갈만큼").
  카드가 null 인 배열 항목은 건너뛴다.
- **적용 버튼**: 우측 상단, onClick → `ApplyClicked?.Invoke()`. `MenuPopup.MakeButton` 톤(초록 계열) 참고.
- 아이템 높이는 `LayoutElement.preferredHeight` 로 고정(unit 4 의 VerticalLayoutGroup 이 세로 배치).

원칙:

- 읽기전용 표시 + 적용 버튼 하나만 상호작용(계약 6). 유닛/카드 개별 탭 없음.
- 카탈로그 불필요 — 표시는 프리셋의 SO 참조에서 직접 읽는다(`unit.portrait`, `card.art`).
- 색/사이즈 상수는 스쿼드·덱 페이지 톤과 맞춘다.

## 완료 기준

- [ ] Unity 컴파일 무오류.
- [ ] (unit 4 통합 후) 한 프리셋이 이름·유닛 7셀·카드 10아트·적용 버튼으로 렌더.
- [ ] 적용 버튼 클릭이 `ApplyClicked` 로 전달됨(로그 또는 unit 4 팝업으로 확인).
- 확인 2026-07-20 (커밋 05c7c7b8): Play 렌더 확인 — 이름 + 유닛 셀 7 + 드캐 아트 10 + [적용] 버튼.
