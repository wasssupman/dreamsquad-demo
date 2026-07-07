# 5 — Handoff Summary

## Commit

- `57899844` Complete outgame lobby presentation polish — 본 스펙의 코드/에셋/씬 전체
  (outgame-lobby-layout 스펙 산출물과 함께 커밋됨, origin/main 푸시 완료)
- `b30c6f63` Merge origin/main — spine 4.2/파츠 작업과 무충돌 병합

## Implemented

- hello(로밍+걷기/대기+터치 리액션), world(idle+터치 리액션) — UI Image+Animator 캐릭터 2종
- 프레임 전처리 파이프라인(soft matting): 흰 배경 영상 추출 프레임 → 투명 스프라이트
- `LobbyReactionLock` 전역 잠금: 리액션 동시 실행 차단 + `ReactionStarted` 공용 이벤트
- 배경 낮/밤 디졸브 전환: UI 셰이더 4스타일, 방향별 틴트(새벽 금빛/초저녁 남보라),
  원형 확산 중심 = 트리거 캐릭터 위치
- 로그인 게이트에 `LobbyCharacters` 그룹 포함 — 로그인 전 캐릭터 비노출

## Key Files

- `Assets/_Project/Scripts/UI/Outgame/{HelloLobbyRoamer,WorldLobbyCharacter,LobbyReactionLock,LobbyBackgroundDissolve}.cs`
- `Assets/_Project/Shaders/Background_Dissolve_UI.shader` + `Art/LobbyBackgroundDissolve.mat`
- `Assets/_Project/Sprites/{hello,world}/` (클립/컨트롤러 포함), `Prefabs/Outgame/{Hello,World}.prefab`
- 씬 노드: `MenuCanvas/{LobbyBackgroundUnder,LobbyBackground,LobbyCharacters/{Hello,World}}`

## Verified

- 전 단위 Play 실측(에디터 비포커스 대비 `Tick(dt)` 주입 방식) + 스크린샷, 콘솔 에러 0
- 로밍/플립/리액션 잠금/배경 전환 왕복/게이트 토글 각각 로그로 확인 (2026-07-07)

## Notes

- 캐릭터는 SpriteRenderer 가 아니라 **UI Image** — Overlay 캔버스+전체화면 배경 구조 때문.
  되돌리지 말 것.
- `hello_attack` 은 리액션 풀에서 제외(사용자 결정)됐지만 에셋/컨트롤러 상태는 보존 —
  `HelloLobbyRoamer.ReactionStates` 에 한 줄 추가로 복귀 가능.
- `execute_code` 불가 머신: 에디터 조작은 임시 MenuItem 툴 방식(작업 후 삭제됨).
  에디터 컴파일 파이프라인이 교착되면(리프레시 무반응) 에디터 재시작이 정답.
- PlayerPrefs 인증 키(`Wassup.Auth.*`)는 2026-07-07 리셋됨 — 첫 로그인부터 테스트 가능.

## Follow-up

- 캐릭터 프레임 SpriteAtlas 화(현재 비압축 낱장 277장), 리액션 로직 공통화(3번째 캐릭터 시)
- README "후속 후보" 참조 (전환 트리거 확장, world 전용 연출 등)
