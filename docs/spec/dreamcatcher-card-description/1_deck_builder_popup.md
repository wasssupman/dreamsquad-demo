# 1 — 덱빌더 팝업에 description 렌더

## 목적

덱빌더 상세 팝업 본문에 authored `description` 을 표시한다. 기존 자동 수치라인은 유지하고
그 아래에 description 블록을 추가한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckBuilderView.cs`
  - `PopupBody(DreamcatcherCard card)` (static, line ~476)

## 구현

`PopupBody()` 의 반환 문자열 끝에 description 블록을 append. 현재 구조:

```
<size=22>[AXIS · TYPE]</size>

Attack Speed +10%   ← effects[] 자동 라인 (유지)
```

여기에 description 이 있으면 구분선 + 본문을 덧붙인다:

```
... (기존 axis 헤더 + 효과 수치 라인) ...

<색상/구분>
{card.description}
```

- **graceful 빈 값**(계약 4): `string.IsNullOrEmpty(card.description)` 이면 아무것도 덧붙이지
  않는다 → 기존 레이아웃 그대로.
- effects[] 가 비고 description 만 있는 카드(Unit 카드: 콕콕 바늘 등)는 수치 라인이 없고
  description 만 본문에 뜬다 — 현재 빈칸 문제가 이걸로 해소된다.
- description 은 muted/화이트 톤 별도 블록으로. 자동 수치 라인과 시각적으로 구분되게
  앞에 한 줄 여백 또는 옅은 구분 컬러 적용. 새 SerializeField/에셋 참조 없이 rich-text 로 처리.
- 팝업 텍스트 영역(`_popupEffect`) 은 이미 존재(sizeDelta 620x150) — 긴 텍스트는
  `enableWordWrapping`(TMP 기본) 로 래핑. 넘치면 폰트/영역은 이번 범위 밖(후속에서 조정).
- **axis 칩은 Squad 전용**: 헤더의 `[AXIS · TYPE]` 에서 축 부분은 `card.type == Squad`
  일 때만 표시하고, Unit(및 그 외) 은 타입 라벨만 표시한다. 근거 — `axis`(CardTargetAxis)
  는 축 스탯 버프의 대상 필터(`BattleBridge.MatchesDcAxis`)라 개별 부착 경로
  (`ApplyDreamcatcherCardToUnit`, axis 미소비)를 타는 Unit 카드에는 의미가 없다.
  Unit 에셋의 `axis=All(3)` 은 inert 기본값 → 칩으로 노출하면 "전체 버프" 오해를 준다.

## 완료 기준

- [ ] 컴파일 성공.
- [ ] 덱빌더에서 스탯 카드(예: Ranger ATK) 탭 → 기존 수치 라인 + (authoring 후) description.
- [ ] Unit 카드(예: 콕콕 바늘) 탭 → 본문에 description 텍스트가 보인다(더 이상 빈칸 아님).
- [ ] description 이 빈 카드는 기존과 동일하게 수치 라인만(레이아웃 깨짐 없음).
