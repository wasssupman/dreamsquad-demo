# 2 — world 캐릭터 + 리액션 전역 잠금

## 목적

둘째 캐릭터(중앙 우측 150px, idle 루프 전용)를 추가하고, 캐릭터 간 터치 리액션이
동시에 실행되지 않도록 전역 잠금을 도입한다.

## 변경 대상

- `Assets/_Project/Sprites/world/` — idle 49 / interaction 81 프레임,
  `world_idle.anim`(루프) · `world_interaction.anim`(3.4s 원샷) · `world.controller`
- `Assets/_Project/Prefabs/Outgame/World.prefab` — Image(640x360) + Animator + 컴포넌트
- `Assets/_Project/Scripts/UI/Outgame/WorldLobbyCharacter.cs`
- `Assets/_Project/Scripts/UI/Outgame/LobbyReactionLock.cs`

## 구현

- `WorldLobbyCharacter`: 로밍 없음. idle 기본 루프 + 클릭 시 `world_interaction` 원샷.
- `LobbyReactionLock`(정적 클래스 — Manager 싱글톤 아님): 소유자 기반
  `TryAcquire`/`Release`. hello/world 모두 리액션 진입 시 획득, 종료/파괴 시 해제 —
  하나가 재생 중이면 모든 캐릭터의 새 리액션 무시. 획득 성공 시
  `ReactionStarted(Component owner)` 발화(공용 연출 훅). Play 시작 시 정적 상태 리셋
  (`RuntimeInitializeOnLoadMethod`, 도메인 리로드 꺼짐 대비).
- 배치 y=-295 는 hello(-280)와 지면선 정합 보정 (world 프레임의 발 위치가 더 높음,
  알파 bbox 실측 기반).

## 완료 기준

- Play: world idle 진행 / 클릭 리액션 / **재생 중 hello 클릭 블록** / 종료 후 잠금
  해제되어 hello 리액션 허용. (2026-07-07 Play 실측 로그로 확인)
