# Spec: 준비단계 공격패턴 플로우 변경 (prep-attack-pattern-flow)

> 상태: **완료 2026-06-04**
> 범위: **Squad 모드 (MAP SETUP / `SquadPrepView`) 한정**. Draft 모드(`DraftView`)는 건드리지 않는다.

## 검증 질문

> MAP SETUP 진입 시 공격패턴이 짧게(약 1초) 자동으로 보였다 사라지고, 이후 드캐 3중1 → 배치로 이어지는가? 그리고 배치 중·전투 중 언제든 토글 버튼으로 공격패턴을 다시 열람할 수 있는가?

## 목표

현재(Squad) 준비 플로우: `MAP SETUP(공격패턴 FadeIn 후 계속 표시 + 토글)` → START → 드캐 3중1 → 배치. START 를 누르면 공격패턴 strip 이 `SetActive(false)` 되어 이후 단계에서 볼 수 없다.

변경 후:

1. **공격패턴 자동 인트로 + 자동 진행** — Squad 전투 진입 시 공격패턴이 자동으로 펼쳐졌다가 약 1초 dwell 후 접히고, **START 대기 없이 곧바로 다음 페이즈(드캐 3중1 → 배치)로 자동 진행**한다. (기존 START 게이트 제거)
2. **지속 토글** — 공격패턴("!" 토글)과 맵 설정("MAP SETTINGS" 토글)이 모두 살아 있어, **사전 배치 중·전투 중** 언제든 토글로 다시 열람/조정할 수 있다.

## 확정된 결정 (사용자 인터뷰 2026-06-04)

- 적용 범위: **Squad 모드만**. Draft 모드는 기존 Unroll→dwell→Roll 유지.
- **START 게이트 제거** — MAP SETUP 의 타이틀/START 버튼/대기 화면은 사라진다. 진입 → 공격패턴 1초 자동 인트로 → 자동으로 드캐 → 배치.
- 자동 인트로 = 펼침 후 **약 1초** dwell → 자동 접힘 → 진행.
- 토글 가용 구간: **사전 배치 중 · 전투 중**. 드캐 3중1 선택 중에는 불가.
- 맵 설정은 **`MapSettingsPanelView` 자체 토글 버튼**으로 접근(이미 존재). 별도 화면 게이트로 두지 않고, 배치 중 토글로 조정.

## feature-wide 계약

- `WavePatternStripView` 의 공개 API(`Unroll/FadeIn/Roll/SnapHidden/RebuildFromDeck/SetToggleEnabled`, `CurrentState`)는 변경하지 않는다. 새 동작은 호스트(`SquadPrepView`)의 조합으로 만든다.
- `MapSettingsPanelView` 는 자체 toggle 버튼 + 접이식 패널을 이미 갖는다. `SquadPrepView` 는 이를 active 로 유지만 하고 별도 표시/숨김 제어를 하지 않는다(패널 기본 접힘).
- `SquadPrepView` 는 더 이상 자체 화면 chrome(타이틀/START)을 만들지 않는다. 역할은 (a) map-settings + wave-strip 자식의 **Canvas 호스트**, (b) 진입 시 자동 인트로 재생, (c) 인트로 종료 후 `gameManager.RequestPlacement()` **1회** 호출이다.
- 자동 진행은 인트로 코루틴이 **realtime** 으로 대기하다 마지막에 `RequestPlacement()` 를 호출한다. 그 전까지 `timeScale==1` 이므로 Roll 퇴장이 깨끗이 끝난 뒤 드캐 모달(timeScale=0)이 뜬다.
- 드캐 모달(`DreamcatcherSelectionView`, canvas `sortingOrder=50`)은 strip/맵설정(`sortingOrder=8`)보다 위에 풀스크린으로 렌더된다. 따라서 드캐 선택 중 토글 버튼은 **모달에 의해 자연 차단**된다 — 별도 disable 코드 없이 **검증**으로 확인한다.
- strip/맵설정 생존은 `SquadPrepView` GameObject(=Canvas 호스트)가 진입 이후에도 active 로 남는 것에 의존한다. 코드에서 자식을 비활성화하지 않는다.
- ECS 경계와 무관한 순수 MonoBehaviour/UI 변경이다. `BattleBridge` 및 ECS 맥락은 건드리지 않는다.

## 작업 단위

| 파일 | 작업 | 문서 | 목적 |
|---|---|---|---|
| 0 | 자동 인트로 + 자동 진행 | `0_attack-pattern-auto-intro.md` | 진입 시 펼침→1초 dwell→접힘→`RequestPlacement()` 자동 호출 (START 게이트 제거) |
| 1 | 지속 토글 | `1_persistent-toggle.md` | strip + 맵설정 active 유지 → 배치/전투 중 토글 + 드캐 차단 검증 |

## 후속 후보 (현 스코프 밖)

- Draft 모드에도 동일 1초 자동 인트로 통일 (현재는 Unroll→dwell→Roll 별도 타이밍).
- 토글 버튼 시각 개선(아이콘/위치/툴팁) 및 전투 중 HUD 와의 레이아웃 정합.
- 자동 인트로 dwell 시간/skip(탭) 정책을 SO 또는 설정값으로 노출.
- **배치/전투 중 맵 설정 변경 시 맵 재생성 안전성** — 토글로 노출된 `MapSettingsPanelView` 가 배치 이후 맵을 재빌드하면 이미 배치한 디펜더/진행 상태와 충돌할 수 있다. 별도 검증/가드 필요.
