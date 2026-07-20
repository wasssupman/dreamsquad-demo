# 3 — 오케스트레이터 (DreamcatcherDeckPageController)

## 목적

상세뷰·브라우저·덱스트립을 소유하고 working 덱을 구동. 추가(캡)/제거/중복/무의식 풀 제외/명시적 저장(Validate 게이트). 카드 상세는 좌측 패널(모달 폐기).

## 변경 대상

- 신규 `Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckPageController.cs` (`Wassup.UI`)

## 구현

- `OnEnable`: 배선 1회 + 풀 빌드(무의식 제외) + `SelectedDeck()` 로드 + `browser.ShowCards(pool)` + 첫 카드 선택 + refresh.
- 브라우저 `CardSelected` → 컬렉션 모드 상세(canAdd/hint 계산). 덱스트립 `SlotTapped` → 제거 모드 상세. 상세 `ActionClicked` → `_selectedDeckIndex>=0` ? RemoveAt : AddCard.
- `AddCard`: `EffectiveDeckSize` 상한 + `EffectiveMax(type)` 캡 준수. **편집은 in-memory** — Save 버튼만 영속(`Validate` 통과 시, deck_1 없으면 생성, `ProfileStore.Save`). auto-save 아님(패리티, 정확히-N 덱).
- `SetBadged`(편성중 집합) / `SetSelected`. (rev 2026-07-18: 유니크 전제 — 카운트/중복 제거, dedup 추가)

## 완료 기준

- [x] 컴파일 클린. 추가/제거/캡/중복/무의식필터/저장 로직 완성.
- [x] Play 실화면: 브라우즈→상세 비파괴 확인('guardian_fortress'→가디언 풀존버). 무효 덱 Save 비활성.

> 구현 2026-07-18 · 커밋 `30d882cf`. 편집 non-persist(Save 게이트) = 기존 뷰 패리티, 스쿼드(auto-save)와 의도적 상이.
