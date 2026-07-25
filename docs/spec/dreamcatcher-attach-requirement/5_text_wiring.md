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

확인 2026-07-25 — 컴파일 에러 0 · EditMode 1339건(1337 pass / 0 fail / 2 기존 Ignore) · PlayMode 전체 53건 중 실패 6건 = **baseline 과 동일**(사전 실패분).

구현 노트:
- 해석기 소스는 `DefenderCatalog.DisplayNameOf(id)` 를 신설해 4곳이 공유(같은 람다 반복 회피 — 4 호출처로 추출 기준 충족). 없는 id 는 null → 포매터 id 폴백.
- 씬 와이어 4곳(MCP 할당 + 저장): `DreamcatcherHandView`·`DcInspectPanelView`(BattleScene), `DreamcatcherDeckBuilderView`·`DreamcatcherDeckPage`(OutgameScene). `DreamcatcherCardDetailView` 는 런타임 `AddComponent` 생성이라 씬 와이어가 불가 — DeckPage 가 기존 `SetField`(artImage/font) 경로로 주입한다.
- BattleScene 저장 시 battle-audio 머지분 필드 2개(`attackSfxVolume`·`attackSfxMinInterval`)가 기본값으로 함께 기록됐다. 회귀 아님(직렬화되지 않았던 신규 필드의 기본값 flush).

> **검증을 PlayMode → EditMode 로 교체한 이유 (실제로 겪은 회귀)**
> 처음엔 씬을 런타임 로드해 참조 non-null 을 확인하는 PlayMode 테스트를 썼다. 그 결과 전체 PlayMode 에서 `DreamcatcherCombatDamage` 2건과 `DreamcatcherGateE2E.ExecutionStrike` 1건이 **새로** 실패했다. 세 건 모두 **단독 실행하면 통과**(3/3) → 로직 회귀가 아니라 상태 오염. 원인은 OutgameScene 런타임 로드가 아웃게임 부트스트랩(프로필/로드아웃 로드)을 돌려 뒤따르는 전투 테스트의 장착 상태를 바꾸는 것.
> 배선은 **정적 사실**이므로 씬 에셋 텍스트에서 해당 스크립트 블록의 `defenderCatalog` 참조를 직접 확인하는 EditMode 테스트로 교체했다(부작용 0·결정론). 실데이터 해석(`{표시명} 전용` vs 미주입 시 `{id} 전용`)도 같은 EditMode 테스트가 실카탈로그로 검증한다. 교체 후 PlayMode 전체는 baseline 6건으로 복귀.
> **교훈**: 씬 와이어 검증 목적으로 PlayMode 에서 OutgameScene 을 로드하지 말 것.
