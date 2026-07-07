# 1 — hello 캐릭터: 애니메이션 + 로밍 + 터치 리액션

## 목적

로비 첫 캐릭터. 좌우 로밍(걷기/대기)과 클릭 리액션으로 로비에 생동감을 준다.

## 변경 대상

- `Assets/_Project/Sprites/hello/` — walk 49 / attack 49 / interaction 49 프레임,
  `hello_walk.anim`(24fps 루프) · `hello_idle.anim`(0프레임 정지) ·
  `hello_attack.anim` · `hello_interaction.anim`(원샷) · `hello.controller`
- `Assets/_Project/Prefabs/Outgame/Hello.prefab` — Image(586x330) + Animator + 로머
- `Assets/_Project/Scripts/UI/Outgame/HelloLobbyRoamer.cs`

## 구현

- 컨트롤러: idle 기본 상태, `IsWalking` bool 로 idle↔walk 전환(Exit Time 없음).
  리액션은 `Animator.Play` 직접 진입 + Exit Time 1.0 으로 idle 자동 복귀.
- `HelloLobbyRoamer`: 대기(랜덤 1.5~3.5s) → 범위 내 랜덤 X 목표 → 이동(160px/s) 루프.
  진행 방향으로 `localScale.x` 플립(오른쪽=원본). `IPointerClickHandler` 로 리액션 —
  전역 잠금(`2_...md`) 통과 시 풀에서 랜덤 재생, 걷기 중이면 즉시 중단 후 재생.
- 리액션 풀은 현재 `hello_interaction` 단일 (attack 은 사용자 결정으로 제외, 에셋 보존).
- `Tick(float dt)` 분리: 비포커스 에디터 검증 툴이 dt 주입으로 로직을 전진시키기 위함.

## 완료 기준

- Play: 로밍(범위 [-600,600] 유지, 방향 플립), 클릭 리액션 재생 중 재입력 무시,
  종료 후 idle(0프레임) 복귀. (2026-07-07 Play 실측 로그로 확인)
