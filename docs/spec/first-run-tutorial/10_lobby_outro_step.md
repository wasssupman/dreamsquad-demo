# 10 — 온보딩 판 이후 로비 복귀 강제 포커스

## 목적

온보딩 판을 마치고 로비로 돌아온 유저에게 **한 번 더** START 를 가리킨다. 지금은 판이
끝나는 순간 `firstRunTutorialDone` 이 켜져 `ShouldRun` 이 거짓이 되므로, 복귀 로비는
아무 안내 없이 그냥 열린다 — 「배웠으니 이제 진짜로 해봐라」를 말할 자리가 없다.

## 변경 대상

- `Assets/_Project/Scripts/Core/Profile/PlayerProfile.cs` — 필드 하나
- `Assets/_Project/Scripts/UI/Outgame/Tutorial/LobbyTutorialStep.cs` — 두 모드
- `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs` — RESET 이 둘 다 되돌림

## 구현

### 진행 필드를 나눈다

```csharp
public bool firstRunTutorialDone;      // 배틀 구간 완주 (기존)
public bool firstRunLobbyOutroDone;    // 복귀 로비 안내까지 봤다 (신규)
```

`matchesPlayed` 에 얹지 않는다 — **계약 2 그대로**. 그 필드는 「계정의 첫 판은 토너먼트에
올리지 않는다」의 유일한 신호이고, 두 규칙이 한 필드를 겸직하면 한쪽을 고칠 때 다른 쪽이
조용히 바뀐다. 「완주했나」와 「복귀 안내를 봤나」도 같은 이유로 갈라 둔다.

### 로비 스텝이 두 모드를 갖는다

구조는 그대로다 — **딤 + START 구멍 하나 + 문구 하나**. 무엇을 띄울지만 갈린다.

| 조건 | 문구 |
|---|---|
| `!firstRunTutorialDone` | `"누가 더 많은 악몽을 제거 하는지\n시작해 보시죠"` (기존) |
| `firstRunTutorialDone && !firstRunLobbyOutroDone` | `"이제 진짜 승부를 시작해 보시죠"` |
| 둘 다 참 | 뜨지 않는다 |

아웃트로를 **띄운 시점에** `firstRunLobbyOutroDone` 을 기록하고 저장한다. 배틀 구간처럼
「완주해야 기록」이 아닌 이유: 이 스텝의 내용이 곧 노출 자체다(플레이어가 해낼 행동이
START 하나뿐이고, 그건 스텝의 완료 조건이 아니라 로비를 떠나는 동작이다).

⚠ 기존 게이트 셋은 그대로 곱한다 — `IsLoadedThisSession` · `UserSession.IsSignedIn` ·
`loadoutReady`. 특히 `loadoutReady` 는 START 가 띄우는 `LoadoutGatePopup` 이 딤 아래로
깔려 로비가 잠기는 것을 막는 가드다(unit 2 주석 참조).

### RESET TUTORIAL

개발 버튼이 **두 필드를 함께** 0 으로 되돌린다. 하나만 되돌리면 재실행한 판이 끝난 뒤
아웃트로가 안 뜬다.

## 알아둘 것

- **아웃트로 시점에는 `ShouldRun` 이 이미 거짓이다.** 그래서 다음 판은 온보딩 웨이브도
  아니고 토너먼트 제출도 정상으로 돈다(계약 16 의 셋이 전부 꺼진 상태) — 문구가
  「이제 진짜 승부」라고 말하는 것이 사실과 맞는다.
- **스텝 인스턴스의 `_shown` 가드는 씬 단위다.** 로비 재진입마다 새 인스턴스라, 1회성은
  `_shown` 이 아니라 프로필 필드가 보장한다.

## 완료 기준

- [x] compile 통과
- [x] Play — 온보딩 판을 마치고 로비로 오면 START 가 딤 위로 포커스되고 문구가 뜬다
- [x] Play — 그 판을 또 끝내고 돌아오면 **뜨지 않는다**
- [x] Play — `RESET TUTORIAL` 후 로비 인트로부터 다시 시작한다

**확인**: 2026-08-20 사용자 Play 확인 — 전 구간 통과. `dotnet build Wassup.Runtime` 오류 0. 커밋 `a1a87a63`.
