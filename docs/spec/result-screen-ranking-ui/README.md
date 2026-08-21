# result-screen-ranking-ui — 결과 화면을 랭킹 UI 로 재설계

> 상태: **완료 2026-07-20** (units 0~1) · **unit 2 추가 2026-07-29** (pending 레이스/칸수 픽스)
> · **unit 3 추가 2026-08-21** (남의 0점 → 「악몽 처치중!」)

## 상위 목표

`ResultScreen` 을 토너먼트 랭킹 화면답게 재설계한다. 세 가지가 목표다:

1. **점수가 화면의 주인공이 된다.** 현재 점수는 28pt 부제("내 점수 12,400")로 승리 탭(60pt)보다 작다.
2. **모바일에서 읽힌다.** 기준 1920×1080 · match=height 에서 22~23pt(뱃지 번호, 스탯 줄)는 실기기에서 작다.
3. **랭킹으로 읽힌다.** 내 순위가 즉시 보이고, 내 행이 리스트에서 즉시 찾아진다.

`battle-score-formula` 와 **독립**이다. 그 spec 의 unit 4 는 여기서 만든 분해 슬롯에 세 축을 채우기만 한다.

## 기존 뷰에서 보존할 것 (재설계 패리티 체크리스트)

이 뷰는 `result-screen-visual-upgrade` 를 거친 물건이다. 아래는 전부 **실제로 겪은 문제의 해결책**이라
재설계 과정에서 조용히 사라지면 안 된다. 각 항목은 구현 후 코드에서 존재를 확인한다.

| # | 항목 | 왜 |
|---|---|---|
| 1 | `canvas.overrideSorting = true` | 이 뷰는 루트 `ResultCanvas` 아래 **중첩** 캔버스라 평범한 `sortingOrder` 가 무시된다. 없으면 배틀 HUD·MENU 버튼 위로 못 올라간다 |
| 2 | `RenderRows` 의 detach-then-destroy | `Destroy` 가 지연이라, 봇 리스트가 실데이터로 교체될 때 한 프레임 이중 리스트가 보인다 |
| 3 | `UpdateLeaderboard` 의 `activeSelf` early-return | 팝업 닫힌 뒤(즉시 RESTART) 도착한 응답을 버린다. **팝업 열리기 전 응답은 BattleBridge 가 보관 후 재적용** — unit 2 |
| 4 | 폴백 리스트 자체 | 서버 응답 전/게스트/실패 시 빈 리스트를 막는다. **내용은 unit 1 에서 교체됨** — 아래 참조 |
| 5 | WAITING 슬롯 | 토너먼트 슬롯은 선할당(`maxEntryCount`, 현 서버 5)이라 미참가 슬롯도 렌더한다 |
| 6 | 서버 `rank > 0` 우선 | dev 서버가 `rank` 를 생략해서 위치 기반 파생이 필요하지만, 오면 서버 값이 이긴다 |
| 7 | `DisplayName` 10자 절단 | 긴 이름이 점수 컬럼을 침범한다 |
| 8 | `SafeAreaRoot` 부모 지정 | 노치/제스처바 |
| 9 | Dim 의 명시적 solid 스프라이트 | null-sprite `Image` 는 전체 화면 쿼드를 신뢰성 있게 안 그린다 |
| 10 | `Show*` 오버로드 6종 전부 | 다른 호출부가 있고 이 spec 범위 밖이다 |
| 11 | serialized 필드 없음 | 씬 diff 를 깨끗하게 유지하려는 의도적 선택 |
| 12 | `UiLayer.Apply` / `_built` 가드 / 버튼 리스너 해제 | 기존 계약 |

`ResultLeaderboardModelTests` 가 `BuildRows` 의 정렬·WAITING·본인행을 덮고 있다.
**서버 데이터 경로(`Row`, `BuildRows`, `DisplayName`)는 건드리지 않는다.**
폴백 경로(`BuildBotRows`)만 unit 1 에서 교체한다 — 테스트가 덮지 않는 private 메서드다.

## 작업 단위

| # | 문서 | 목적 |
|---|---|---|
| 0 | `0_ranking_layout.md` | 2컬럼 재배치 + 타이포 스케일 + 점수 히어로 + 순위 콜아웃 |
| 1 | `1_pending_fallback.md` | 봇 폴백 → "참가자 찾는 중" 대기 상태로 전환 (가짜 점수 제거) |
| 2 | `2_pending_race_bracket_fix.md` | Tally 연출 중 도착한 랭킹 드랍 픽스 + pending 칸수 10→5 통일 |
| 3 | `3_in_progress_row_label.md` | 배정만 되고 점수 미확정인 타 참가자 행을 `악몽 처치중!` 으로 표기 |

## 다음 spec 으로 넘어가는 것

`battle-score-formula` unit 4 가 좌측 컬럼의 **스탯 행**(`SetChips`)에 점수 3축을 채운다.
`(라벨, 값)` 쌍 배열을 받는 범용 구조라 레이아웃 코드는 건드리지 않는다 (계약 5).

## feature-wide 계약

1. 서버 데이터 경로(`Row`/`BuildRows`/`DisplayName`)는 불변. 렌더링 계층과 폴백 생성만 바꾼다.
2. 위 패리티 표 12항목은 전부 보존한다.
3. serialized 필드를 추가하지 않는다 — 색·치수는 기존처럼 `private static readonly` 상수다.
4. 팔레트는 인게임 HUD(`ScoreHudView`) 언어를 유지한다. 네이비 플레이트 + 골드.
5. 분해 칩 행은 `(라벨, 값)` 쌍 배열을 받는 **범용** 구조다. `battle-score-formula` unit 4 가 여기에 세 축을 넣는다.
6. 점수 카운트업·연출은 범위 밖. 값 표시까지다.

## 후속 후보 (범위 밖)

- 점수 카운트업 연출 + 순위 확정 펄스
- 리스트 스크롤 (슬롯이 10을 넘어가면 필요)
- 계약 지불로 잃은 점수 경고 (`battle-score-formula` README 열린 항목)
