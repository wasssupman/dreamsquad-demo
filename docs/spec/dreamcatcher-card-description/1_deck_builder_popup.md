# 1 — 덱빌더 팝업에 description 렌더

## 목적

덱빌더 상세 팝업 본문에 공용 카드 formatter를 표시한다. 구조화 수치/메커니즘 라인이 우선하고,
구조화 요약이 없는 카드만 authored `description` fallback을 추가한다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckBuilderView.cs`
  - `PopupBody(DreamcatcherCard card)` (static, line ~476)

## 구현

`PopupBody()` 는 공용 formatter를 호출한다. 현재 구조:

```
<size=22>[축 · 타입]</size>

항상 → 레인저 아군 공격 속도 +10%   ← 구조화 formatter 라인
```

구조화 summary가 없고 description 이 있으면 fallback 본문을 덧붙인다:

```
... (구조화 summary가 없는 카드의 헤더) ...

<색상/구분>
{card.description}
```

- **graceful 빈 값**(계약 4): `string.IsNullOrEmpty(card.description)` 이면 아무것도 덧붙이지
  않는다 → 기존 레이아웃 그대로.
- 구조화 데이터가 없고 description 만 있는 카드(Unit 카드 등)는 description만 본문에 뜬다.
- description fallback은 별도 블록으로 구분한다. 새 SerializeField/에셋 참조 없이 rich-text로 처리.
- 팝업 텍스트 영역(`_popupEffect`) 은 이미 존재(sizeDelta 620x150) — 긴 텍스트는
  `enableWordWrapping`(TMP 기본) 로 래핑. 넘치면 폰트/영역은 이번 범위 밖(후속에서 조정).
- **axis 칩은 Squad 전용**: 헤더의 `[AXIS · TYPE]` 에서 축 부분은 `card.type == Squad`
  일 때만 표시하고, Unit(및 그 외) 은 타입 라벨만 표시한다. 근거 — `axis`(CardTargetAxis)
  는 축 스탯 버프의 대상 필터(`BattleBridge.MatchesDcAxis`)라 개별 부착 경로
  (`ApplyDreamcatcherCardToUnit`, axis 미소비)를 타는 Unit 카드에는 의미가 없다.
  Unit 에셋의 `axis=All(3)` 은 inert 기본값 → 칩으로 노출하면 "전체 버프" 오해를 준다.

## 완료 기준

- [ ] 컴파일 성공.
- [ ] 덱빌더에서 스탯/메커니즘 카드 탭 → 현재 SO 값으로 구조화 summary가 보인다.
- [ ] 구조화 데이터가 없는 카드만 description fallback이 보인다.
- [ ] description이 있어도 구조화 summary에 같은 문장을 중복하지 않는다.
