# Spec — Lobby Keyring Drag

> 상태: **완료 2026-07-07**
> 커밋: `f731b36b`(spec) · `f076a76b`(0 SO) · `2643383b`(1 rig) · `a3366f50`(2 낙하/구분) · `249ae848`(3 wiring)

## 상위 목표

인게임 드래그 배치에서 검증된 키링 동작 모델(`docs/spec/keyring-cord-preview/`)을
아웃게임 로비 캐릭터(hello, world)에 이식한다. 캐릭터를 스와이프하면 키링 모드
(고리=손가락, 줄, 캐릭터가 매달려 스프링 스윙)로 전환되고, 놓으면 그 x 위치의
바닥으로 중력 낙하 + 작은 바운스 후 착지해 기존 행동(로밍/idle)을 재개한다.

## 검증 질문

로비 캐릭터를 스와이프하면 키링처럼 매달려 따라오고, 놓으면 그 자리 바닥으로
떨어져(공중이면 낙하+작은 바운스) 착지 후 원래 행동을 재개하는가?
클릭 리액션은 드래그와 충돌하지 않는가?

## 작업 단위

| # | 파일 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_settings.md` | `LobbyKeyringSettings` SO + 에셋 | 튜닝값 공급원 |
| 1 | `1_drag_rig.md` | 드래그 세션 + 스프링 추종/기울임 + 고리·줄 리그 + suspend/resume 접점 | 키링 모드 본체 |
| 2 | `2_fall_bounce.md` | 중력 낙하 + 바운스 착지 + 재잡기 + 클릭 가드 | 놓기 연출 |
| 3 | `3_scene_wiring.md` | OutgameScene 와이어링 + Play 검증 | 완성 |
| 4 | `4_handoff_summary.md` | 인계 요약 | 종료 시 작성 |

## Feature-wide 계약

1. **인게임 무변경.** `DefenderDragPlacementController` / `DragSwaySettings`(코드·에셋)
   불가침. 가져오는 것은 동작 모델과 파라미터 구성뿐, 코드 공유 없음.
2. **좌표계 = 캔버스 px.** 모든 계산은 캐릭터 부모 RectTransform 로컬 좌표.
   Screen Space Overlay 캔버스이므로 `ScreenPointToLocalPointInRectangle(camera: null)`.
3. **고리 = 손가락.** 캐릭터 머리 목표 = 손가락에서 `ropeLength` 아래. 위치는
   스프링+감쇠+속도상한 지연 추종. **워밍업(가속 램프) 금지** — 인게임 계약 승계
   (억제 후 풀릴 때 큰 스냅).
4. **기울임은 머리 중심.** reparent 없이 수학으로: `pos = 머리점 + Rotate(머리→피벗
   오프셋, θ)`. θ는 줄 방향에서 유도, `maxAngle` 클램프. (발/중심 피벗이면 반대로
   흔들림 — 인게임 교훈 승계.)
5. **바닥 y = 캐릭터 초기 `anchoredPosition.y`** (Awake 캡처). 착지 x는
   `landingMinX/MaxX` 클램프.
6. **상태머신 `Idle → Dragging → Falling → Idle`.** Falling 중 BeginDrag 로 재잡기
   허용. 픽업~착지(suspended) 동안 캐릭터 클릭 리액션 차단.
7. **단발 클릭 = 리액션, 스와이프 = 키링. 스와이프 중에는 IDLE 만 재생**
   (2026-07-07 사용자 결정). 드래그 시작 시 진행 중 리액션/걷기를 강제 종료하고
   idle 상태를 즉시 Play + `LobbyReactionLock` 해제. 드래그 자체는 락을 잡지
   않는다(다른 캐릭터의 리액션은 드래그와 공존 가능).
8. **캐릭터 접점은 `ILobbyKeyringTarget`** (`SuspendForKeyring`/`ResumeFromKeyring`,
   구현체 hello·world 2개). 드래그 컴포넌트는 캐릭터 내부 상태를 직접 만지지 않는다.
9. **리그(고리/줄)는 런타임 생성** UI Image. 고리 스프라이트는 절차적 annulus 텍스처
   1회 생성·공유, 세션 종료/OnDisable 시 정리(인게임 CleanupSession 패턴).
10. **모든 수치 = `LobbyKeyringSettings` SO.** 하드코딩 금지. 시간은 `Time.deltaTime`
    (로비는 슬로우모 없음) + 기존 로비 스크립트처럼 `Tick(dt)` 주입 가능 구조.

## 파이프라인 커버리지

N/A — 전투 플레이 오브젝트가 아닌 OutgameScene UI 캐릭터 인터랙션.
`docs/reference/object-pipeline-map.md` 대상 아님 (outgame-lobby-characters 와 동일 사유).

## 후속 후보 (현 스코프 밖)

- 실제 고리/줄 아트 스왑 (현재 절차적 annulus + 단색 사각 Image).
- 드래그 중 전용 매달림 애니메이션 (현재는 idle 유지).
- 착지 먼지 VFX / 스쿼시&스트레치.
- 줄 sag 곡선 (인게임 후속 후보와 동일).
- 셋째 캐릭터 추가 시 리액션 로직 공통화와 함께 부착 검증.
