# Tournament-Seed Map Select — 서버 시드 기반 맵 배정

**상태: 구현 완료 2026-07-23 (유닛 0~2 EditMode 1291 green — 사용자 Play 확인 대기)**

## 목표

지금은 매판 로컬 랜덤(matchSeed 파생)으로 맵풀 인덱스를 고른다. 이를 `/tournament/play` 응답의 **`data.tournament.seed`** 로 바꿔, **같은 토너먼트 참가자 = 같은 (맵, 덱)** 을 만든다. 산식은 정말 심플하게:

```
index = tournamentSeed % mapPool.Count      // 문제 있으면 0번
```

(요구는 "seed 기반 결정론적 배정" — 산식 자체는 modulo 면 충분. 같은 seed → 항상 같은 인덱스.)

## 타이밍 문제와 해법 (이 spec 의 유일한 설계 포인트)

`BeginMatch()`(play 호출, `GameManager.OnEnable`)와 맵 빌드(`Start`→`PrepareDraftMap`)는 **같은 프레임**이라 응답이 절대 못 온다. 해법: **로비 `OnStartGame` 에서 play 를 선발행**하고 즉시 씬 전환 — 전환(페이드+씬로드) 동안 응답이 도착한다. 전환을 막는 게이트는 없다: 맵 빌드 시점에 시드 미도착이면 그냥 "문제" → 0번.

## 작업 단위 목록

| # | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | Api | `0_seed_parse.md` | `PlayState` 에 `tournament.seed` 파싱 추가 |
| 1 | Api/UI | `1_lobby_preissue.md` | 로비 선발행 + 시드 정적 노출(`TournamentMatchReporter`) |
| 2 | Bridge | `2_pool_select_swap.md` | 맵풀 인덱스 선택을 tournament seed 로 교체 + 순수 함수/테스트 |
| 3 | Handoff | `3_handoff_summary.md` | 인계 (종료 시) |

## Feature-wide 계약

- **산식**: `index = (int)(seed % (ulong)count)`. seed 는 서버 uint64(실측 9128566303723636648) — `ulong` 로 다룬다.
- **"문제" = 시드 부재 일체 → 0번**: 게스트/미로그인(HasAccount=false), play 실패/타임아웃, 응답 미도착(전환이 너무 빨랐거나 직접 Play), 토너먼트 테스트 모드 진입. 전부 같은 폴백 — 분기 없이 `HasTournamentSeed` 하나로 판정.
- **선발행 소유권**: 로비 `OnStartGame` 이 `BeginMatch` 를 선발행하면 `GameManager.OnEnable` 의 기존 호출은 **재발행하지 않고 그 attempt 를 승계**한다(중복 attempt 발행 금지). 로비를 안 거치는 진입(에디터 직접 Play, 테스트 모드)은 기존대로 OnEnable 발행 — 시드는 맵 빌드에 못 미치므로 0번(허용).
- **`fixedMapSeed` 디버그 노브 유지**: 비0이면 기존 로컬 선택(`MapPoolSelect.SelectIndex`) 그대로 — 특정 맵 강제 디버깅 경로 보존. 라이브 씬 값은 0(확인됨).
- **선택만 교체, 빌드 불변**: `MapGridBattleAdapter.Build(seed, …)` 인자·배치칸·데코프랍 시드 체계는 안 건드린다. 바뀌는 것은 풀 **인덱스 계산 한 줄**.
- **same-map-same-wave 승계**: 맵별 덱 waveSeed 비0 고정(맵-웨이브 레퍼런스) → 시드가 맵을 고르면 웨이브도 따라온다. 토너먼트 공정성 완성.
- **attempt 라이프사이클 불변**: epoch/stale 드롭, PendingMatchStore 저장/정리, complete/abandon 흐름은 그대로. 선발행은 호출 "시점"만 앞당긴다.

## 후속 후보

- 씬 전환 게이트(응답 대기 스피너 + 타임아웃) — 미도착→0번 빈도가 실측으로 거슬리면.
- 게스트/오프라인용 로컬 랜덤 유지 옵션(현재는 일괄 0번).
- `tournament.seed` 를 웨이브/기믹 등 다른 결정론 소스로 확장.
