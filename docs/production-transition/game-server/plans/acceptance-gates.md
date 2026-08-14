# Production Game Server Acceptance Gates

## Intake gate

- Server receipt의 manifest/common/game-server hash와 official audit copy가 일치한다.
- `common`과 `game-server`만 소비하며 Client presentation 문서를 gameplay 구현 정본으로 사용하지 않는다.
- Imported 규칙이 master roadmap, AGENTS와 accepted decision을 override하지 않는다.

## Authority·determinism gate

- Simulation은 Unity, infrastructure, wall clock, ambient randomness와 global mutable state에 의존하지 않는다.
- 같은 ruleset/input/seed/ordered commands가 같은 canonical result와 event order를 만든다.
- Duplicate, invalid와 late command가 state/result를 중복 변경하지 않는다.

## Domain·session gate

- `domain-coverage.md`의 모든 included row가 rule과 automated acceptance scenario를 가진다.
- Snapshot/resync/reconnect 뒤 canonical state와 pending intent disposition이 일치한다.
- Terminal result와 score는 한 번 확정되고 Client 계산에 의존하지 않는다.

## Release gate

- Replay/audit가 authoritative progression과 result를 보존한다.
- Load, fault, recovery, observability, security와 persistence는 production 목표를 통과한다.
- Client vertical slice가 representative network condition에서 accepted/rejected/corrected 흐름을 검증한다.

Transition source의 fixture/evidence는 이 gate의 통과 증거가 아니다. 실제 tests와 운영 evidence는
Somnia Game Server에서 생성한다.
