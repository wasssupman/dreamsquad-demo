# 6 — 선물 튜토리얼 진행 상태

## 목적

선물 단계 튜토리얼을 "두 번째 판"(핵심 안내 완료 후 첫 선물 노출)에 한 번만 노출하고, 첫 판(핵심
안내 pending)에는 선물 연출 자체를 억제하는 판정을 기존 버전 정수 패턴으로 추가한다.

## 변경 대상

- `Assets/_Project/Scripts/Core/Profile/PlayerProfile.cs`
- `Assets/_Project/Scripts/Core/Profile/TutorialProgress.cs`
- `Assets/_Project/Tests/EditMode/TutorialProgressTests.cs`

## 구현

`PlayerProfile`에 additive 필드 `giftTutorialVersion`을 추가한다. 구 JSON은 기본값 0 = 미완료로
해석되며 `schemaVersion`은 올리지 않는다(unit 0과 동일 규약).

`TutorialProgress`에 추가한다.

- `GiftTutorialVersion = 1` 상수
- `IsGiftTutorialPending(profile)` — `giftTutorialVersion < GiftTutorialVersion`
- `ShouldRunGiftTutorial(holder)` — `IsLoadedThisSession` && 핵심 안내 **완료**(`!IsCorePending`)
  && gift pending. 첫 판(core pending)과 상호배타 — 같은 holder 에서 `ShouldRunCore`와
  `ShouldRunGiftTutorial`이 동시에 true 가 될 수 없다.
- `CompleteGiftTutorial(profile)` — 해당 버전만 갱신. 다른 상태 불변.
- `ResetAll` / `ResetAllInJson` — 세 번째 필드 포함으로 확장. 리셋 도구(로비 `RESET TUTORIAL`,
  에디터 `Wassup > Tutorial > Reset First Session Tutorial`)는 `ProfileStore` 경유로 이 두 함수만
  호출하므로 도구 측 코드 변경은 없다.

첫 판의 선물 연출 억제 판정은 새 predicate 없이 기존 `ShouldRunCore`를 그대로 쓴다(소비처는 unit 7).

## 완료 기준

- [x] compile clean.
- [x] EditMode: 신규 프로필은 gift tutorial pending, core pending 상태에선 `ShouldRunGiftTutorial=false`.
- [x] core 완료 + gift pending + loaded 세션에서만 `ShouldRunGiftTutorial=true`.
- [x] `CompleteGiftTutorial`은 gift 버전만 갱신하고 core/awakening 을 건드리지 않는다.
- [x] `ResetAll`/`ResetAllInJson`이 세 필드를 모두 0으로 만들고 다른 토큰은 보존한다.
- [x] JSON round-trip 후 세 버전 값이 유지되고, 필드 없는 구 JSON 은 0으로 로드된다.

구현: 2026-07-20 · 커밋 `9e75c0ae` · 런타임 로그로 판정 확정(run1 ShouldRunGift=False / run2 True).
