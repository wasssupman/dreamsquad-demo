# 장부 — 「새 주인」 별칭 (unit 8c · owner-aliases.md)

> 생성 2026-09-25(8c 구현 5). 장부의 「새 주인」 칸 문법: **첫 백틱 토큰 = 코드 심볼**(`파일명.cs` 또는 `타입.멤버`), 백틱이 없으면 맨 앞 식별자(`PlacementService` 등)를 심볼로 본다. 「삭제 …」는 심볼이 필요 없다(근거를 같이 적는다). 「미실현 …」은 **실패**다 — 배정만 되고 실체가 없다는 뜻이다. 자유 범주어는 **아래 별칭일 때만** 허용되고, 별칭의 심볼 목록은 전부 남는 코드에 있어야 한다(`*` = 타입 이름 와일드카드). 검사: `python3 tools/battle-core-rebuild/check_ledgers.py --owners`.
>
> 8c 뒤 bridge-methods · bridge-fields · rule-holders 의 모든 「삭제」 아닌 행은 **심볼로 시작한다** — 별칭으로 해석되는 행은 0 이다. 별칭은 범주어의 뜻을 고정하는 어휘표로 남긴다(다음 장부가 범주어를 쓰면 여기서 해석된다). 8a 가 드러낸 「실체 없는 배정」(bridge-fields 1·31·55 — 초판 「unit 6 의 보너스 뷰」·「5b」)은 범주어가 심볼 없이 통과하던 모양이다. 그래서 별칭은 **목록이 가리키는 실체가 있을 때만** 통과한다.

| 별칭 | 심볼 | 뜻 |
|---|---|---|
| 뷰 풀 | `Core*ViewPool` · `Core*Presenter` · `Core*Spawner` | 코어 사건을 구독해 그리는 새 층 컴포넌트(5a~8a) |
| 담당자 구독 | `PlacementService` · `WaveScheduler` · `CostLedger` · `HeartMeter` · `ScoreLedger` · `HandDeck` · `GimmickHost` · `MatchClock` | 담당자 8 — 담당자 간 순서는 사건 구독 순서(절대 제약 1) |
| MapRuntime (코어) | `MapRuntime` | 판의 칸·흐름장 상태(코어) |
| 입력 | `DragPlacementInput` · `SelectionInput` · `CardInput` · `SubmitInput` | 입력 → 커맨드(절대 제약 4) |
| 뷰 | `Core*` · `CorePhaseFeed` · `AppBootstrap` | 프레젠테이션 · 앱 셸(판 밖) |
| MatchDefinitionBuilder | `MatchDefinitionBuilder` · `*DefinitionBuilder` · `MatchEntry` | SO → 정의표(판 밖) |
| 디버그/로그 | `Core*DebugMenu` · `CoreTrace` | 도구 처분표(`tools.md`)의 코어판 · 트레이스 |
| 유지(예외) | `TimeManager` | 추가 제약의 의도된 예외 |
