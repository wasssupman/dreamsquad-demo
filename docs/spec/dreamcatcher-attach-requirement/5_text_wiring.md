# 5 — 문안 resolver 배선 (화면 노출)

## 목적

unit 4 의 접두를 **실제 화면에 보이게** 한다. 이 unit 이 없으면 사용자 결정 ③(문안 자동 표기)이 달성되지 않는다 — 포매터는 접두를 만들 수 있지만 아무도 resolver 를 넘기지 않아 UnitId 제한이 id 문자열로만 보인다.

## 변경 대상

문안 소비처 4곳:
- `Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckBuilderView.cs:334`
- `Assets/_Project/Scripts/UI/Outgame/DreamcatcherCardDetailView.cs:55`
- `Assets/_Project/Scripts/UI/Dreamcatcher/DcInspectPanelView.cs:128`
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherHandView.cs:809`

## 구현

1. 각 뷰에 유닛 표시명 소스를 주입한다. 소스는 `DefenderCatalog`(`Assets/_Project/Scripts/Data/DefenderCatalog.cs` — `units` 배열) — 신규 레지스트리를 만들지 않는다.
   - 이미 defender 데이터를 들고 있는 뷰가 있으면 그것을 쓰고, 없는 뷰에만 `[SerializeField] DefenderCatalog` 를 추가한다. 4곳 모두에 기계적으로 필드를 달지 말고 실제로 없는 곳만.
2. `id → displayName` 조회를 `Body(card, resolver)` 에 넘긴다. 조회 실패는 unit 4 의 id 폴백이 흡수하므로 뷰 쪽에 예외 처리 불요.
3. **씬 wiring 은 이 unit 안에서 끝낸다** — UnityMCP 로 인스펙터 참조를 할당하고 Play 로 확인한다. 사용자 수작업으로 미루지 않는다(CLAUDE.md 금지 행동).

## 완료 기준

- compile 통과, 콘솔 에러 0.
- 씬의 4개 뷰에 소스 참조가 실제로 할당됨(빈 참조 0).
- Play 육안: 제한 카드 1장을 손패/덱빌더 상세에 노출시켜 접두가 보인다 — Class 제한은 "가디언 전용", UnitId 제한은 유닛 **표시명**(id 아님)으로 표기.
- 무제한 카드 문안 무회귀.
