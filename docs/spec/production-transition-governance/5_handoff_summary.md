# D5 — Foundation + Pilot 인계

> 상태: **Foundation 승인 · pilot gameplay decision 3건 open · official cutover blocked**

## Commit

- Demo D0: `1ed33c317cf34f05dfc146960fe9356837165f5f`
- Demo D1: `0dcc70625fd8ecb6055a512fac0c842b1a1d0852`
- Demo D2: `3469ed1b9c54288dda5e61d36112432531b7e315`
- Demo D3: `f56c628a414f9c91ce6650e0fff94ce58e3fef99`
- Demo C1/S1 alignment record: `613d8621d6dabb848a669519cc824ce27554db8a`
- Demo D4: `b7f4735e2eaf1c2d6f1b7e1bde9f2f4ade61f90e`
- Somnia Client C1: `372ce99d47c9475b9d660932cd7e76f300095feb`
- Somnia Game Server S1: `e2aa25f1295f16d917fc9202d661190da3c94be0`
- Demo D5: `cf79811c5c387fbfff7cbbd024be502c97fe8670`
- 이 문서의 Client 포인터는 C1 rebase 뒤 갱신했으며 현재 Demo 게시 상태는 Git ref를 정본으로 한다.

## Implemented

- 전역 one-time freeze와 세 package 계약
- 구조화 registry/review/decision 계약
- preparation/cutover 정적 검증기
- Client·Game Server preparation 문서 정합
- `unit-lifecycle` 세 package pilot

## Key Files

- `docs/production-transition/README.md`
- `docs/production-transition/governance/`
- `tools/verify_production_transition.py`
- `docs/production-transition/{shared,client,game-server}/cards/unit-lifecycle.md`

## Verified

- `python tools/verify_production_transition.py prepare`: PASS, errors 0. 미결 review,
  readiness와 decision은 warning으로 정확히 노출.
- `python tools/verify_production_transition.py cutover`: 의도한 FAIL. 현재
  `transition_state: preparing`, candidate scope, 미결 `PT-DEC-UL-001..003`을 차단.
- `python -m unittest tools.test_verify_production_transition`: 38 tests PASS.
- Dry-run package SHA-256:
  - Shared: `a75d645437d41a4f3da59b396493106027d75c40fd646fe85a69804ae80b4b4e`
  - Client-only: `017c9d93231983cb76a708a95258c75ca03c722d87d03b0f648f9c504c0561f1`
  - Game Server-only: `9b8c063f1ced16561c5b27b3bdd5146b9ae16d364a1b2911c22d06b3c56451f3`
  - References partition: `eb8f6ad862e0221929a36b6c03ff9b51881597d0c1ee4b1451da83edacea0817`
  - Manifest aggregate: `128a699e0c509e2e23e73e2abeb70abc20a9419afef0a716291672dd62a47a0b`
  - Dry-run manifest bytes: `3fd58005d632ae4de90f31bee5809014fd7b6ef87f9c1480fff8d1a7e1d26794`
- Target inventory는 Client-only와 Game Server-only가 겹치지 않고 Shared 3개 경로/hash가
  양쪽에서 같다. 이동 뒤 target 경로 기준 Markdown link와 dependency closure도 닫혀 있다.
- Dry-run governance attestation은 selected record 11개, exact review 0개, 관련 open
  decision 3개를 source/watch provenance와 implementation wave/stage까지 self-contained하게
  보존한다. Decision↔record edge도 양방향 일치를 검증하며 실제 review/decision을 추정해
  채우지 않았다.
- Preparation은 uncommitted 반복 작업을 허용한다. Strict cutover의 include artifact는
  candidate Git commit의 tracked blob과 raw bytes가 같지 않으면 실패한다.
- 전환 subtree는 LF checkout을 고정하며, 실제 `core.autocrlf=true` 임시 Git repository의
  delete/checkout 이후에도 commit blob과 selected source raw bytes가 같음을 회귀 검증했다.
  `text=auto`는 binary fixture를 `-text`로 판정해 원래 bytes도 보존한다.
- Somnia Game Server 표준 script: locked restore, format verify, Release build와 6 tests PASS.
- Somnia Client: JSON, governance, strict UTF-8와 trailing whitespace 검사 PASS.

## Notes

- 2026-08-11 사용자 결정으로 Foundation preparation 구조가 승인됐고, 이후 현재 범위의
  세 저장소 commit 생성도 별도로 승인됐다. 이는 역할별 exact review나 freeze 승인이 아니며
  당시 push는 승인·수행되지 않았다.
- `PT-DEC-UL-001..003`은 모두 `open`으로 보류하며 pilot은 `blocked` 상태를 유지한다.
- 현재 자료는 공식 freeze가 아니며 production input도 아니다.
- 미결 gameplay decision과 실제 owner review를 자동으로 채우지 않는다.
- `44c87885` baseline과 기존 review는 historical/stale/preparatory다.
- Client의 7개 문서는 작업 시작 전부터 미추적이었으며 삭제·clean 없이 보존·정합한 뒤
  명시 승인으로 C1 commit에 포함했다.
- 두 production 저장소와 Demo 어디에도 `docs/migration-input/<freeze-id>`를 만들지 않았다.
- Runtime, API, Unity serialized asset, package와 `ProjectSettings`는 변경하지 않았다.

## Follow-up

- 변경된 Demo spec과 연결된 domain 하나만 골라 registry와 card를 갱신한다.
- 실제 transition 결정 때 Product가 production-v1 include/exclude 목록을 잠근다.
- 공식 freeze 뒤에는 Demo re-freeze 없이 production errata/ADR로 수정한다.
