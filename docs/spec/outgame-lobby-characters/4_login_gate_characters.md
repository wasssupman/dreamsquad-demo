# 4 — 로그인 게이트에 캐릭터 포함

## 목적

로그인(SIGN IN) 단계에서는 로비 캐릭터가 노출되지 않고, 로그인 완료 후에만 나타난다.

## 변경 대상

- 씬: `MenuCanvas/LobbyCharacters` 그룹(빈 RectTransform) 신설 — Hello/World 를 하위로
  이동 (sibling 순서·anchoredPosition 보존)
- `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs` —
  `lobbyCharactersRoot` SerializeField 추가, `ApplyAuthGate()` 에서 menuRoot 와 함께 토글

## 구현

- outgame-login-gate 스펙의 기존 게이트 경로를 그대로 탄다: Awake/`onSignedIn` 에서
  `ApplyAuthGate()` 재적용 → 별도 이벤트 배선 없음. RESET ACCOUNT 시 캐릭터도 숨김.
- 배경(및 배경 전환)은 게이트 밖 유지 — 로그인 화면에서도 배경은 보인다
  (outgame-lobby-layout 계약과 일치).

## 완료 기준

- Play(로그아웃 상태): 캐릭터 비노출 + SIGN IN 패널만 표시. 로그인(세션 주입) 후
  캐릭터 그룹 활성. (2026-07-07 Play 실측 + 캡처로 확인)
