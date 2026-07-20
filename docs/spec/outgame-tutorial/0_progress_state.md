# 0 — 로비 온보딩 진행 상태

## 목적

챕터 A/B 각각을 한 번만 노출하기 위한 영속 플래그를 추가한다. 기존 튜토리얼 3플래그와 동형으로
만들어 dev 트레이 `RESET TUTORIAL` 버튼과 에디터 메뉴가 자동으로 함께 리셋하도록 한다.

## 변경 대상

- `Assets/_Project/Scripts/Core/Profile/PlayerProfile.cs`
- `Assets/_Project/Scripts/Core/Profile/TutorialProgress.cs`
- `Assets/_Project/Tests/EditMode/TutorialProgressTests.cs`

## 구현

`PlayerProfile`에 필드 2개를 추가한다. `schemaVersion`은 올리지 않는다 — 기존 프로필에서 누락된
필드는 `0`으로 역직렬화되어 자연히 pending 상태가 된다(기존 3플래그와 같은 additive 규약).

```csharp
// outgame-tutorial unit 0 — 로비 차단형 온보딩. A는 로비 최초 노출,
// B는 인게임 core 완료 이후 로비 복귀에서 각각 1회.
public int lobbyIntroVersion;
public int lobbyLoadoutHintVersion;
```

`TutorialProgress`에 상수 2개와 판정/완료 메서드를 기존 형태 그대로 추가한다.

```csharp
public const int LobbyIntroVersion = 1;
public const int LobbyLoadoutHintVersion = 1;

public static bool ShouldRunLobbyIntro(PlayerProfileSO holder) =>
    holder != null && holder.IsLoadedThisSession && IsLobbyIntroPending(holder.profile);

// 챕터 B는 인게임 core 튜토리얼 완료를 전제로 한다. 따라서 A/B가 동시에
// pending 될 수 없고 순서가 플래그만으로 보장된다 (ShouldRunGiftTutorial 선례).
public static bool ShouldRunLobbyLoadoutHint(PlayerProfileSO holder) =>
    holder != null && holder.IsLoadedThisSession && holder.profile != null &&
    !IsCorePending(holder.profile) && IsLobbyLoadoutHintPending(holder.profile);
```

`IsLobbyIntroPending` / `IsLobbyLoadoutHintPending` / `CompleteLobbyIntro` /
`CompleteLobbyLoadoutHint`도 기존 `IsCorePending` / `CompleteCore` 시그니처와 동형으로 만든다.

`ResetAll`과 `ResetAllInJson`에 두 토큰을 편입한다. **`changed` 식과 토큰 쓰기를 둘 다 확장해야 한다** —
`ResetAllInJson`의 `changed`는 `ProfileStore.ResetTutorialProgressAt`(`ProfileStore.cs:80-84`)에서
백업 생성과 파일 치환 **전체를 게이트**한다. `root[...]` 쓰기만 추가하고 `changed` 식(`cs:81`)을
빠뜨리면 신규 두 토큰만 1인 상태에서 디스크가 고쳐지지 않은 채 "이미 리셋됨" 로그가 뜬다.
in-memory `ResetAll`의 `changed`도 동일하다.

`ResetAllInJson`은 **`JObject` 부분 패치 원칙을 유지**한다 — 전체 재직렬화하면 이 클라이언트 모델이
모르는 계정 필드가 유실되기 때문이다 (`TutorialProgress.cs:71-74` 주석 참조).

## 완료 기준

- [ ] 컴파일 통과
- [ ] EditMode `TutorialProgressTests` 확장 및 전체 통과
  - `lobbyIntroVersion` 누락된 레거시 JSON → pending 판정
  - `ShouldRunLobbyLoadoutHint`가 core pending 상태에서 false
  - `ResetAllInJson`이 두 토큰을 0으로 되돌리고 **미지의 필드를 보존**
  - `ResetAllInJson`의 `changed` 가 **두 신규 토큰만 0이 아닐 때도 true** (H3 회귀 가드)
  - `IsLoadedThisSession == false` → `ShouldRun*` 전부 false
- [ ] **두 필드가 `1`인 상태에서** 로비 dev 트레이 `RESET TUTORIAL` 실행 → `profile.json`에서 `0`으로 바뀜
      (사전 조건 없이 확인하면 `ResetAllInJson`이 없던 키를 새로 써넣는 것과 구분되지 않아 항상 통과한다)

> 검증 2026-07-21 · 커밋 `251705d8` — EditMode `TutorialProgressTests` 16/16, 전체 1133 중 0 실패.
