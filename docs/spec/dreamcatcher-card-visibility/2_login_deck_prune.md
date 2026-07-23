# 2. 로그인 시 저장 덱에서 숨김 카드 장착 해제

## 목적

이미 편성돼 있던 카드가 숨김으로 바뀌면, 로그인 시 저장 덱에서 자동으로 빠지게 한다.

## 변경 대상

- `Assets/_Project/Scripts/Core/Profile/DeckPrune.cs` — **신규**. 순수 정리 함수
- `Assets/_Project/Scripts/UI/Outgame/HiddenCardDeckPruner.cs` — **신규**. `onSignedIn` 훅
- `Assets/_Project/Tests/EditMode/Profile/DeckPruneTests.cs` — **신규**
- `Assets/_Project/Scenes/OutgameScene.unity` — 컴포넌트 배선

## 구현

**순수 코어** (`Wassup.Core.DeckPrune`): 프로필과 카탈로그를 받아 모든 `dreamcatcherDecks` 의 `cardIds` 에서 숨김 카드를 제거하고, 제거한 개수를 돌려준다. 아키텍처 타입에 의존하지 않는 값 판정이라 static 순수 함수로 두고 EditMode 로 검증한다(제약 10).

판정 규칙:
- `catalog.ById(id)` 가 **null 이면 남긴다**. 미해결 id 는 이 기능의 관심사가 아니다 — 카탈로그 결측/오타를 숨김으로 오인해 조용히 지우면 `LoadoutGate` 의 "catalog does not know this id" 진단이 사라진다.
- `card.visible == 0` 이면 제거한다.
- 카탈로그가 null 이면 아무것도 하지 않는다(배선 오류로 덱을 훼손하지 않는다).

**훅** (`HiddenCardDeckPruner`): `LoginPanelView.onSignedIn` 을 구독한다 — `LoginAutoImport` 가 "모든 진입 경로가 지나는 단일 seam" 으로 이미 쓰는 지점이다. 제거가 1건이라도 있을 때만 `ProfileStore.Save(profile)` 을 호출한다.

주의:
- **`LoginAutoImport` 와 달리 dev 게이트를 걸지 않는다.** 자동 import 는 dev API 호출이라 릴리즈에서 꺼야 하지만, 이 정리는 빌드에 박힌 `visible` 값만 읽는 로컬 연산이라 릴리즈에서도 돌아야 한다.
- **살아 있는 프로필 인스턴스를 그대로 수정한 뒤 저장한다.** 새 `PlayerProfile` 을 만들어 저장하면 스쿼드 등 다른 섹션이 날아간다.
- dev 환경에서 시트 import 는 비동기라 이번 로그인의 정리는 **직전 빌드/에셋 값** 기준이다. 방금 시트에서 숨긴 카드는 다음 진입에 정리된다 — `LoginAutoImport` 의 non-blocking 설계와 같은 성질이다.
- 덱이 정원 미달이 되는 것은 의도된 결과다. `LoadoutGate` 가 START 에서 막고 덱 페이지로 유도한다.

## 완료 기준

- [x] 컴파일 통과 · EditMode 통과 (2026-07-23 리그 배치: 1263개 중 실패 0, 신규 `DeckPruneTests` 7개 포함)
- [x] 제거가 없으면 `0` 반환 → 호출처가 저장을 건너뛴다 (`RemoveHiddenCards_NoHidden_ChangesNothing`)
- [x] 카탈로그가 모르는 id 는 남는다 (`RemoveHiddenCards_UnknownId_IsKept` + 실 카탈로그로 재현 확인)
- [x] 모든 덱에 적용 · 중복 숨김 카드 전부 제거 · null 카탈로그/프로필 무해
- [x] OutgameScene 배선 완료 — `UnitStatRefresher` 호스트(`LoginAutoImport` 와 같은 GO)에 `loginPanel`/`profileSO`/`cardCatalog` 연결
- [ ] 실제 로그인 왕복: `visible=0` 카드가 든 덱으로 로그인 → 저장 덱에서 사라지고 재실행해도 유지 (Play 필요)
- [ ] 정리 후 덱이 9/10 이면 START 가 막히고 덱 부족 안내가 뜬다 (Play 필요)
