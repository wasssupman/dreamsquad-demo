# Spec — Outgame Lobby Characters

> 상태: 완료 (2026-07-07)

## 상위 목표

OutgameScene 로비에 살아있는 느낌을 주는 캐릭터 연출을 얹는다. AI 영상 추출 프레임 기반
스프라이트 애니메이션 캐릭터 2종(hello, world)을 배치하고, 터치 리액션과 배경 낮/밤
디졸브 전환을 연결한다. 로그인 게이트와의 노출 규칙도 정리한다.

## 검증 질문

로그인한 플레이어의 로비에서 hello가 좌우 로밍하고, 아무 캐릭터나 터치하면 리액션
애니메이션과 함께 배경이 낮↔밤으로 전환되는가? 로그인 전에는 캐릭터가 보이지 않는가?

## 작업 단위

| # | 파일 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_sprite_pipeline.md` | 프레임 전처리 파이프라인 | 완료 |
| 1 | `1_hello_character.md` | hello — walk/idle/리액션 + 로밍 | 완료 |
| 2 | `2_world_character_reaction_lock.md` | world — idle/리액션 + 전역 잠금 | 완료 |
| 3 | `3_background_daynight_dissolve.md` | 배경 낮/밤 디졸브 전환 | 완료 |
| 4 | `4_login_gate_characters.md` | 로그인 게이트에 캐릭터 포함 | 완료 |
| 5 | `5_handoff_summary.md` | 인계 요약 | 완료 |

## Feature-wide 계약

- 캐릭터는 **UI Image + Animator** 로 렌더한다 (로비가 Screen Space Overlay 캔버스 +
  전체화면 배경이라 SpriteRenderer 는 가려짐). 클립은 `Image.sprite` 에 키프레임.
- 프레임 에셋 정규화: `{char}_{anim}_{NNN}.png`, 24fps. 원샷 리액션은 Animator 파라미터
  없이 `Animator.Play(state)` + Exit Time 복귀 전환으로 처리.
- 터치 리액션은 `LobbyReactionLock`(정적, Manager 싱글톤 아님) 전역 잠금을 거친다 —
  한 캐릭터 재생 중 모든 캐릭터의 새 리액션 차단. 잠금 획득 성공이 곧
  `ReactionStarted(Component)` 이벤트이며, 배경 전환 등 공용 연출은 이것만 구독한다.
- 배경 전환은 앞/뒤 두 레이어 스왑 방식. 스타일(노이즈/원형 확산/수평 스윕/크로스페이드,
  ±골든 틴트)은 `LobbyBackgroundDissolve` 인스펙터에서 선택. 방향별 틴트: night→day 새벽
  금빛, day→night 초저녁 남보라.
- 캐릭터 그룹(`MenuCanvas/LobbyCharacters`)은 `OutgameMenuController.ApplyAuthGate()` 가
  menuRoot 와 함께 토글 — 로그인 전 비노출.
- 튜닝값(로밍 범위/속도, 전환 시간, 틴트 색)은 전부 프리팹/씬 SerializeField 또는
  머티리얼 에셋. 하드코딩 없음.

## 파이프라인 커버리지

N/A — 전투 플레이 오브젝트가 아닌 OutgameScene UI 캐릭터/배경 연출.
`docs/reference/object-pipeline-map.md` 대상 아님.

## 후속 후보 (이번 스코프 밖)

- 캐릭터 프레임(현재 낱장 비압축 PNG 277장)을 SpriteAtlas 로 묶어 메모리/로딩 최적화.
- 리액션 로직 공통 컴포넌트 추출 — 셋째 캐릭터 추가 시점에 (현재 hello/world 2벌 중복).
- hello `attack` 애니메이션 재사용 — 에셋/컨트롤러 상태는 남아 있고 리액션 풀에서만 제외됨.
- 배경 전환을 다른 트리거(씬 전환, 시간대 시스템)에 연결.
- world 리액션 전용 연출(현재는 배경 전환만 공용으로 발생).
