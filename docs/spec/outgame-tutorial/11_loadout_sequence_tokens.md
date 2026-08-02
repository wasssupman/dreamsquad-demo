# 11 — 로드아웃 시퀀스 진행 토큰

## 목적

챕터 B(스쿼드+드림캐쳐 한 덩어리)를 스텝 4개의 시퀀스로 쪼개기 위한 토대.
**순수 가산 unit 이다** — 신규 토큰과 그 판정 함수만 추가하고, 기존 이름·기존 체인·컨트롤러는
일절 건드리지 않는다. 그래서 이 커밋만으로는 게임 동작이 정확히 그대로다.

개명(`...LoadoutHint` → `...SquadHint`)과 체인 재배열은 **unit 12 가 컨트롤러와 함께** 한다.
여기서 미리 하면 컨트롤러가 옛 이름을 부르고 있어 컴파일이 깨지고
(`OutgameTutorialController.cs:137`·`:389`), 체인만 먼저 바꾸면 덱 토큰을 채우는 코드가 아직
없어 **챕터 C 가 영원히 발화하지 않는다**.

## 변경 대상

- `Assets/_Project/Scripts/Core/Profile/PlayerProfile.cs`
- `Assets/_Project/Scripts/Core/Profile/TutorialProgress.cs`
- `Assets/_Project/Tests/EditMode/TutorialProgressTests.cs`

## 구현

**신규 필드 2개** (additive · 0 = pending): `lobbyDeckHintVersion`(스텝 B2 드림캐쳐) ·
`lobbyStartHintVersion`(스텝 E 재출발). 버전 const 는 각각 1.

**기존 `lobbyLoadoutHintVersion` 은 스텝 B1(스쿼드)의 저장소로 재사용한다.** JSON 필드명을
좁히지 않는 이유는 호환이다 — 바꾸면 기존 진행이 0 으로 읽혀 온보딩이 되살아난다
(`awakeningHintVersion` 선례). const 이름도 필드명과 짝이므로 유지하고, **의미는 API 이름이
나른다**(unit 12 의 개명).

**신규 판정 함수** — 이번 unit 에선 호출처가 없다(unit 12·13 이 붙인다):

```
ShouldRunLobbyDeckHint  = 로드된 세션 && !IsLobbyLoadoutHintPending && IsLobbyDeckHintPending
ShouldRunLobbyStartHint = 로드된 세션 && !IsLobbyKeyringHintPending && IsLobbyStartHintPending
```

**레거시 계정 가드**(리뷰 M1). 옛 B·C 를 이미 마친 계정은 신규 토큰이 0 이라 그대로 두면
`이번엔 드림캐쳐 덱 차례!` 와 재출발 안내를 **맥락 없이 다시 본다**(B1 은 완료라 안 뜨므로
"이번엔" 이 가리킬 앞 단계가 없다). 상태를 새로 저장하지 않고 **파생**으로 막는다
(`ShouldRunAwakeningIntro` 가 세운 "이중 상태 방지" 선례):

```
IsLegacyLobbySequenceDone(p) = p.lobbyKeyringHintVersion >= LobbyKeyringHintVersion
                            && p.lobbyDeckHintVersion == 0
                            && p.lobbyStartHintVersion == 0
```

이 조합은 **레거시 계정만** 만족한다 — 새 순서에서는 덱(B2)이 키링(C)보다 먼저 완료되므로
`키링 완료 && 덱 미완료` 가 성립할 수 없다. `IsLobbyDeckHintPending`·`IsLobbyStartHintPending`
둘 다 이 가드를 `&& !IsLegacyLobbySequenceDone(p)` 로 물린다. 레거시 계정이 사라지면 삭제 가능한
코드라는 사실을 주석에 남긴다.

**리셋 등록**: 신규 토큰 2개를 `ResetAll` 과 `ResetAllInJson` **양쪽**의 `changed` 표현식과
대입부에 넣는다. `changed` 에서 빠지면 그 토큰만 다를 때 파일 교체를 건너뛰어 리셋이 디스크에
닿지 않는다. (리셋 후에는 키링 토큰도 0 이 되므로 레거시 가드가 자동으로 풀린다.)

## 완료 기준

- 컴파일 0 (Runtime · Tests.EditMode) — 컨트롤러 무변경이므로 옛 API 가 그대로 있어야 한다
- `TutorialProgressTests` 신규 케이스:
  - B1 완료만으로 `ShouldRunLobbyDeckHint` 가 true, 덱 완료 후 false
  - C 완료 후 `ShouldRunLobbyStartHint` 가 true, 스타트 완료 후 false
  - **레거시 프로필**(로드아웃·키링 완료 + 신규 토큰 0)에서 덱·스타트가 **pending 이 아니다**
  - **신규 순서 프로필**(덱 완료 → 키링 완료 → 스타트 0)에서 스타트가 **pending 이다**
    (레거시 가드가 정상 진행을 삼키지 않는지 — 이 둘이 짝이다)
  - `ResetAll`·`ResetAllInJson` 이 신규 토큰 2개를 0 으로 되돌리고, **그 둘만 비0일 때도**
    `changed == true`
- 기존 EditMode 스위트 실패 0 — 특히 `LobbyKeyringHint_RunsOnlyAfterLoadoutHintComplete`
  (`TutorialProgressTests.cs`)는 이 unit 에서 **그대로 통과해야 한다**. 깨진다면 체인을 건드린 것이다
- Play 동작 변화 0: 로비 안내 흐름이 커밋 전과 동일

확인 완료 2026-08-02 · `10fad0c2` (EditMode 신규 6건 포함 회귀 통과).
