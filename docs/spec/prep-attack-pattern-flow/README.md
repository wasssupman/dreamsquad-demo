# Spec: 준비단계 공격패턴 플로우 변경 (prep-attack-pattern-flow)

> 상태: **초안 — 사용자 승인 대기** (2026-06-04)
> 범위: **Squad 모드 (MAP SETUP / `SquadPrepView`) 한정**. Draft 모드(`DraftView`)는 건드리지 않는다.

## 검증 질문

> MAP SETUP 진입 시 공격패턴이 짧게(약 1초) 자동으로 보였다 사라지고, 이후 드캐 3중1 → 배치로 이어지는가? 그리고 배치 중·전투 중 언제든 토글 버튼으로 공격패턴을 다시 열람할 수 있는가?

## 목표

현재(Squad) 준비 플로우: `MAP SETUP(공격패턴 FadeIn 후 계속 표시 + 토글)` → START → 드캐 3중1 → 배치. START 를 누르면 공격패턴 strip 이 `SetActive(false)` 되어 이후 단계에서 볼 수 없다.

변경 후:

1. **공격패턴 자동 인트로** — MAP SETUP 진입 시 공격패턴이 자동으로 펼쳐졌다가 약 1초 dwell 후 자동으로 접힌다. (기존 `FadeIn()` + 계속 표시 대체) 맵 설정 패널과 START 버튼은 그대로 유지된다.
2. **지속 토글** — START 이후에도 공격패턴 strip 과 "!" 토글 버튼이 살아 있어, **사전 배치 중·전투 중** 언제든 토글로 공격패턴을 열람할 수 있다.

## 확정된 결정 (사용자 인터뷰 2026-06-04)

- 적용 범위: **Squad 모드만**. Draft 모드는 기존 Unroll→dwell→Roll 유지.
- MAP SETUP 화면(맵 설정 패널 + START 버튼) **그대로 유지**. "대기 없음"은 공격패턴 연출에만 적용 — START 게이트는 유지된다.
- 자동 인트로 = 펼침 후 **약 1초** dwell → 자동 접힘.
- 토글 가용 구간: **사전 배치 중 · 전투 중**. 드캐 3중1 선택 중에는 불가.
- 맵 설정 패널(`MapSettingsPanelView`)은 위치/동작 변경 없음.

## feature-wide 계약

- `WavePatternStripView` 의 공개 API(`Unroll/FadeIn/Roll/SnapHidden/RebuildFromDeck/SetToggleEnabled`, `OnDwellInterrupt`)는 변경하지 않는다. 새 동작은 호스트(`SquadPrepView`)의 조합으로 만든다.
- 드캐 모달(`DreamcatcherSelectionView`, canvas `sortingOrder=50`)은 MAP SETUP/strip(`sortingOrder=8`)보다 위에 풀스크린으로 렌더된다. 따라서 드캐 선택 중 토글 버튼은 **모달에 의해 자연 차단**된다 — 별도 disable 코드를 추가하지 않고, 차단을 **검증**으로 확인한다.
- strip 의 생존은 `SquadPrepView` GameObject(=strip 의 Canvas 호스트)가 START 이후에도 active 로 남는 것에 의존한다. START 시 비활성화하는 것은 `_panel`(타이틀+START+맵설정)뿐이며 strip GameObject 는 active 로 유지한다.
- ECS 경계와 무관한 순수 MonoBehaviour/UI 변경이다. `BattleBridge` 및 ECS 맥락은 건드리지 않는다.

## 작업 단위

| 파일 | 작업 | 문서 | 목적 |
|---|---|---|---|
| 0 | 공격패턴 자동 인트로 | `0_attack-pattern-auto-intro.md` | MAP SETUP 진입 시 펼침→1초 dwell→자동 접힘 |
| 1 | 지속 토글 | `1_persistent-toggle.md` | START 이후 strip 생존 + 배치/전투 중 토글 + 드캐 차단 검증 |

## 후속 후보 (현 스코프 밖)

- Draft 모드에도 동일 1초 자동 인트로 통일 (현재는 Unroll→dwell→Roll 별도 타이밍).
- 토글 버튼 시각 개선(아이콘/위치/툴팁) 및 전투 중 HUD 와의 레이아웃 정합.
- 자동 인트로 dwell 시간/skip(탭) 정책을 SO 또는 설정값으로 노출.
