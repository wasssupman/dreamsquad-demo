# 5 — 시드 · 프루너 · 폴백 정리

## 목적

프리셋이 1개에서 30개로 늘어나면서 "엔트리가 하나뿐" 을 암묵 전제하던 세 곳을 맞춘다. 요청에 없던 부수 작업이지만 **하지 않으면 조용히 깨진다.**

## 변경 대상

- `Assets/_Project/Scripts/Core/Profile/ProfileStore.cs` — `EnsureDefaultSquad` · `EnsureDefaultStones` · `EnsureDefaultDeck`
- `Assets/_Project/Scripts/Core/Profile/DeckPrune.cs` (+ `HiddenCardDeckPruner` 호출부)
- `Assets/_Project/Scripts/Core/GameManager.cs:226~233` — 빈-스쿼드 draft 폴백 (검토)
- `Assets/_Project/Scripts/UI/Outgame/DefaultLoadoutButton.cs` — 로그 문구

## 구현

**1. 시드 (`ProfileStore`)** — 구조는 거의 그대로다. 기존 코드가 이미 "리스트가 비면 하나 만들고, 확정 포인터가 깨졌으면 교정하고, **비어 있을 때만** 내용을 시드" 라는 옳은 정책을 갖고 있다. 바꿀 것:
- `EnsureDefaultSquad` 가 만드는 첫 엔트리 이름을 `"Squad 1"` → `"스쿼드 1"` (unit 3 의 기본 이름 규칙과 일치)
- `EnsureDefaultDeck` 의 `"Deck 1"` → `"덱 1"`
- `NormalizePresets()` 를 **`EnsureNonNull` 과 `CreateDefault` 각각의 말미 1곳**에서 호출한다(unit 0 의 "로드" 호출 지점). 세 `EnsureDefault*` 안에 각각 넣지 않는다 — 3중 호출이 되고 불변식의 소유자가 흐려진다. 기존 `EnsureDefaultSquad` 안에 있던 확정 포인터 교정(`:159~160`)은 `NormalizePresets` 로 흡수되므로 중복 로직을 남기지 않는다
- **시드가 여러 프리셋을 만들지 않는다** — 신규 유저는 프리셋 1개로 시작하고 나머지는 `[+]` 로 만든다

`EnsureDefaultStones` 는 `CommittedSquad()` 의 4칸이 전부 비었을 때만 시드하는 현 정책을 유지한다. 프리셋이 여러 개여도 **확정 프리셋에만** 시드한다 — 다른 프리셋을 의도적으로 비워둔 것을 로드마다 되살리면 해제가 불가능해진다(기존 주석의 논리 그대로).

**2. 프루너 (`DeckPrune`)** — 시트에서 `visible=0` 으로 숨긴 카드를 저장 덱에서 떼어내는 로그인 시점 정리다. 현재 `profile.dreamcatcherDecks` 를 순회하므로 **리스트가 커지면 자동으로 30개 전체를 훑는다** — 이미 옳다. 확인할 것:
- 순회가 `SelectedDeck()` 하나로 좁혀진 곳이 없는지 재확인
- 프루닝이 프리셋 내용을 바꾸면 그건 **시스템 주도 변경**이라 즉시 저장이 맞다(플레이어의 [저장] 대상이 아니다). 현 동작 유지
- 프루닝이 페이지가 열린 상태에서 일어날 수 있는가 — 로그인 시점이라 페이지 진입 전이다. 작업본과 충돌하지 않음을 확인하고 주석에 남긴다

**3. 빈-스쿼드 draft 폴백 (`GameManager`)** — 계약 8 로 빈 확정 프리셋이 허용되므로 이 분기의 도달 조건을 재검토한다:
```csharp
if (squad != null && !squad.IsEmpty() && ...) { StartSquadMatch(squad); return; }
if (draftController != null) { /* 레거시 draft */ }
```
`LoadoutGate` 가 START 를 막으므로 로비 경로에서는 도달 불가다. 남은 도달 경로는 **게이트를 우회하는 것들** — 테스트 모드, 에디터에서 BattleScene 직접 Play. 이번 spec 의 판단:
- **동작을 바꾸지 않는다.** 폴백은 프리셋보다 오래된 안전망이고, 제거는 이 spec 의 검증 질문과 무관하다
- 대신 폴백 진입 시 로그 한 줄을 추가해 "확정 프리셋이 비어서 draft 로 떨어졌다" 를 명시한다. 지금은 조용히 다른 모드로 갈아타서 원인 추적이 어렵다
- draft 경로 자체의 은퇴는 README 후속 후보가 아니라 **별도 spec 후보**로 남긴다(범위 밖)

**4. `DefaultLoadoutButton`** — 접근자 개명은 unit 0 에서 이미 끝나 있다. 여기서는 **로그 문구만** 보강한다 — `squad=`/`deck=`/`stones=` 가 "프리셋 N개 중 확정분" 임이 드러나게(현재는 편성이 하나뿐인 것처럼 읽힌다).

## 완료 기준

- [ ] 컴파일 그린
- [ ] `ProfileStoreTests` · `ProfileStoreDefaultDeckTests` 그린. 이름 기본값 변경(`"스쿼드 1"`/`"덱 1"`)을 어서션에 반영
- [ ] **신규 프로필**: 파일 없는 상태에서 로드 → 프리셋 1개, 확정됨, 스타터 유닛 7 + 스톤 4 + 기본 덱이 그 프리셋에 들어감
- [ ] **기존 프로필**: 실기기/에디터의 현재 `profile.json` 로드 → 편성 무변경, 프리셋 1개로 보임, 확정됨
- [ ] `DeckPruneTests` 그린 + **프리셋 여러 개 케이스 추가**: 30개 중 3개에 숨김 카드가 있으면 3개 모두 정리된다
- [ ] 확정 아닌 프리셋의 빈 스톤 4칸이 로드마다 되살아나지 **않음** (의도적 해제 보존)
- [ ] 빈 확정 프리셋으로 START → 게이트 팝업 `0/7`. BattleScene 직접 Play → draft 폴백 + 신규 로그 1줄
- [ ] `DefaultLoadoutButton` 클릭 → 로그 정상, 프로필 재생성 후 페이지 재진입 시 프리셋 1개
