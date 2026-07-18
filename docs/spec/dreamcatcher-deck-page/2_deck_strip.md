# 2 — 덱 스트립 (DreamcatcherDeckStrip)

## 목적

우 상단 덱 트레이 — `EffectiveDeckSize` 슬롯(카드 art/빈 "+") + 유효성 상태 라벨 + **Save 버튼**(Validate 게이트). 슬롯 탭 → `SlotTapped(index)`(상세 제거 모드). 기존 "MY DECK" 프레임 + statusText + save-only-when-valid 패리티.

## 변경 대상

- 신규 `Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckStrip.cs` (`Wassup.UI`)

## 구현

- `Refresh(List<string> cardIds)`: 슬롯 도장 + status `{count}/{deckSize} · squad {n}/{max} · {reason}`(`DeckRules.Validate`/`SquadCount`) + Save `interactable=valid`(색 전환).
- `event SlotTapped(int)` / `event SaveClicked`. HorizontalLayoutGroup: 슬롯들 + flexible status + Save 버튼.

## 완료 기준

- [x] 컴파일 클린. `Refresh`가 슬롯·상태·Save 게이트 반영.
- [x] Play: 덱 슬롯 art + 상태 라벨 + [저장] 렌더. 무효 덱(10/8)일 때 Save 비활성 확인.

> 구현 2026-07-18 · 커밋 `30d882cf`. deckSize 는 ruleConfig(현재 8) — 슬롯 수 동적.
