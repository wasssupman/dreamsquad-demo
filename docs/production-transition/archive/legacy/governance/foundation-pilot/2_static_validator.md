# D3 — 정적 검증기

> **DORMANT · OWNER-GATED · NON-ACTIONABLE HISTORY.** 현재 Demo의 spec·작업 큐·검증 gate가 아니며 Project owner의 명시적 transition 활성화 전에는 실행·갱신하지 않는다.

## 목적

공식 export 전에 registry와 package closure의 구조적 오류를 표준 라이브러리만으로
반복 검출한다. 검증기는 production 저장소에 쓰지 않는다.

## 변경 대상

- `tools/verify_production_transition.py`
- `tools/test_verify_production_transition.py`
- `docs/production-transition/.gitattributes`
- `docs/production-transition/{shared,client,game-server}/fixtures/`

## 구현

- `prepare` mode는 준비 자료의 불완전·stale·blocked 상태를 보고하되 구조가 정합하면
  통과한다.
- `cutover` mode는 include record에 `complete/current/reviewed/ready`, 모든 blocking
  decision `decided`, area별 review를 요구한다.
- Decision↔record 역참조는 preparation에서도 양방향 exact consistency를 요구한다.
- dependency/reference closure, target path containment/uniqueness, SHA-256,
  Shared list/hash 동일성과 Client/Server 전용 파일 분리를 검증한다.
- 각 consumer의 virtual target inventory에서 dependency와 Markdown local link를
  `target_path` 기준으로 resolve해 이동 뒤 끊어진 참조를 실패시킨다.
- Registry의 source commit과 두 exact destination template을 검증하며 오타, target swap,
  서로 다른 freeze ID 확장을 허용하지 않는다.
- Strict cutover에서는 selected artifact가 candidate Git commit의 tracked blob이고 현재
  package bytes와 동일한지 검증한다. Preparation의 uncommitted 반복 작업은 허용한다.
- 전환 subtree의 text checkout은 LF로 고정하고, 실제 임시 Git repository의
  `core.autocrlf=true` checkout에서도 commit blob과 package source bytes가 같은지 회귀 검증한다.
  `text=auto` 판정으로 binary evidence는 byte 변환 없이 보존되는지도 함께 검증한다.
- 동일 입력은 정렬된 상대 경로와 canonical JSON으로 같은 manifest/package hash를 낸다.
- In-memory `dry_run_manifest`는 official schema와 같은 root 필드와
  `shared/client/game-server/references` 전달 구획을 쓴다. `references`는 consumer package가
  아니라 manifest·governance closure 구획이다.
- File entry의 stable record ID와 canonical `governance_attestation`으로 record gate,
  source/watch provenance, implementation wave/stage, exact review tuple과 관련 decision을
  target에서 독립 재감사할 수 있게 한다.
- dry-run 출력은 memory 또는 임시 위치에만 만들며 official freeze나 target import를
  생성하지 않는다.
- Unit test는 임시 디렉터리에 positive/negative registry fixture를 만들고 종료 시
  폐기한다. Repository에는 pilot의 package fixture만 남긴다.

## 완료 기준

- [x] positive preparation fixture가 통과한다.
- [x] stale, blocker, self-review, 다중-area review, closure, path, destination, hash,
  Shared mismatch, attestation 누락/비결정성, invalid commit, untracked/dirty source negative
  fixture가 실패한다.
- [x] `core.autocrlf=true`인 clean Git checkout에서도 selected transition source bytes와
  candidate commit blob이 동일하고 binary fixture는 byte-identical하게 보존된다.
- [x] 실제 pilot은 preparation mode를 통과하고 cutover mode는 미결 결정 때문에 실패한다.
- [x] 두 번의 dry-run hash가 동일하다.
