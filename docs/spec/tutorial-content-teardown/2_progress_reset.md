# unit 2 — 진행 저장 초기화

## 목적

스텝별 진행 저장을 걷는다. 재설계가 자기 스키마를 새로 잡는다(사용자 결정: "일단 프로그레스 관련은 초기화하고 추후에 새로 제작").

## 변경 대상

- `Assets/_Project/Scripts/Core/Profile/TutorialProgress.cs` — 스텝 버전 상수 10여개(`CoreVersion` · `LobbyIntroVersion` · `EffectTileHintVersion` · `GimmickRevealHintVersion` …)와 `ShouldRunXxx` / `IsXxxPending` / `CompleteXxx` / `ResetAll` / `ResetAllInJson`
- `Assets/_Project/Scripts/Core/Profile/PlayerProfile.cs` — 튜토리얼 진행 필드 **12개**

```
firstBattleTutorialVersion   awakeningHintVersion        awakeningTapAttachHintVersion
giftTutorialVersion          lobbyIntroVersion           lobbyLoadoutHintVersion
lobbyDeckHintVersion         lobbyKeyringHintVersion     lobbyStartHintVersion
gimmickRevealHintVersion     lobbyHistoryHintVersion     effectTileHintVersion
```

정본은 `TutorialProgress.ResetAll` 본문(모든 토큰을 나열한다). ⚠ **`lobbyIntroVersion` 은 이름에 `Hint` 도 `Tutorial` 도 없다** — `grep "HintVersion\|TutorialVersion"` 같은 관용구로 좁히면 조용히 누락된다. `giftTutorialVersion` 은 `gift-phase-removal` 이후 이미 리더가 없는 죽은 필드다(`ResetAll` 만 건드림).
**`matchesPlayed` 는 대상이 아니다** — 튜토리얼 진행이 아니라 매치 이력이고, 계약 5가 이 필드를 「첫 판」 판정의 새 소유자로 쓴다.
- `Assets/_Project/Scripts/Core/Profile/ProfileStore.cs` — `ResetTutorialProgressAt`
- `Assets/_Project/Tests/EditMode/TutorialProgressTests.cs` — 사라진 정책의 케이스

## 구현

unit 0·1 이 호출자를 모두 걷어낸 뒤라 이 파일들은 호출자 0 이다. 지우면 그만이지만 두 가지를 지킨다:

- **저장 안전 게이트는 남긴다.** `PlayerProfileSO.IsLoadedThisSession`(BattleScene 직접 Play 가 `profile.json` 을 덮어쓰는 것을 막는다)은 튜토리얼 전용이 아니다 — 프로필을 쓰는 모든 주체의 가드다.
- **마이그레이션하지 않는다.** 제거된 키는 Newtonsoft 로드에서 무시되고 다음 저장에서 사라진다. `schemaVersion` 은 올리지 않는다(읽는 쪽이 없어지는 것이지 남은 필드의 의미가 바뀌지 않는다).

**계약 5 의 신호 교체를 여기서 완결한다.** unit 1 이 `OutgameMenuController` 의 튜토리얼 참조를 걷을 때 `OnStartGame` 의 토너먼트 우회 판정은 `TutorialProgress.ShouldRunCore(profileSO)` → `profileSO.profile.matchesPlayed == 0` 으로 바뀌어 있어야 한다. 이 유닛이 `ShouldRunCore` 를 지우는 시점에 그 호출부가 남아 있으면 컴파일이 깨지고, **그 에러를 «if 블록 삭제»로 고치면 첫 판이 다시 토너먼트에 올라가 서버 `complete` 500 을 맞는다**. 순서를 지킬 것.

`TutorialProgress` 가 스텝 API 만 갖고 있으면 **파일째 삭제**한다. 게이트성 헬퍼가 남으면 그것만 남긴 채 축소한다 — 어느 쪽인지는 unit 0·1 이후의 실제 잔여로 판단한다.

⚠ 안드로이드 세이브는 앱을 지워도 살아남는다(공유저장소+자동백업). 그래서 «구버전 JSON 이 계속 돌아온다»가 정상이고, 이 유닛이 그 값을 **읽지 않게** 만드는 것이 초기화의 실현이다.

## 완료 기준

- 컴파일 통과. EditMode 두 lane 그린.
- 코드베이스에 **12개 필드명 각각**의 참조가 0건. 패턴 grep 이 아니라 **이름을 하나씩** 확인한다(`lobbyIntroVersion` 함정).
- **첫 판 토너먼트 우회가 살아 있다** — 신규 프로필로 START → 콘솔에 참가 신청 생략 로그, 두 번째 판부터는 정상 신청. 이게 이 유닛의 최우선 회귀 확인이다.
- 기존 `profile.json`(진행값이 채워진 세이브)으로 부팅 → 예외 없음, 안내 없음, 저장 1회 후 해당 키가 파일에서 사라진다.
- 신규 프로필 부팅 → 예외 없음, 안내 없음.
