# One-time Transition Plan

> **Project owner activation 전에는 실행하지 않는 plan**

## 0. Dormant maintenance

- 중요한 Demo Product/architecture 결정이 끝난 뒤, 사용자가 별도로 요청한 transition 작업에서만
  `maintenance/change-register.md`와 living rule/plan을 갱신한다.
- 구현 diff, fixture와 evidence를 따라 쓰지 않는다. stale과 누락은 허용한다.
- `freezes/`나 production의 `docs/migration-input/`을 만들지 않는다.

## 1. Final reconciliation

1. Project owner가 reconciliation 범위와 candidate Demo revision을 지정한다.
2. Open change entry와 current Demo source를 한 번 대조한다.
3. Client experience map과 Server domain coverage를 `included | excluded | decision-blocked`로 확정한다.
4. Included scope의 blocking decision을 모두 `decided`로 만든다.
5. Product owner와 두 tech owner가 의미와 consumer-local policy 적합성을 검토한다.
6. Temp dry-run에서 living file inventory, links, schema, partition과 hashes를 검증한다.

Reconciliation은 Demo content를 수정하지 않는다. 규칙 공백이 있으면 transition 문서를 보완하거나
scope를 명시적으로 제외하며 Demo spec/code 변경을 요구하지 않는다.

## 2. `demo-approved`

Project owner가 `governance/audit-events/1-demo-approved.json`을 한 번 승인한다. Event는
candidate Demo revision, transition subtree를 제외한 Demo content SHA-256, approved scope와
approval reference를 기록한다. `audit-events/`는 official 사건이 생길 때만 만들며 freeze payload가 아니다.
Approved scope는 freeze coverage에서 `included`인 모든 row를 `client:<Surface ID>` 또는
`game-server:<Domain ID>`로 정규화한 정확한 집합이며, 누락·추가·중복을 허용하지 않는다.
이후 Demo content가 달라지면 이 transition은 terminal cancel이며 두 번째 approval을 만들지 않는다.

## 3. `demo-frozen`

승인된 bytes로 다음 audit snapshot을 정확히 한 번 만든다.

```text
freezes/<freeze-id>/
  manifest.json
  common/
  client/
  game-server/
  references/transition-policy.md
  receipts/                    # copy 검증 뒤 채움
```

Manifest path는 canonical relative POSIX Markdown path여야 하고 Unicode `Cc` 제어 문자를 포함할
수 없다. `archive`, `maintenance`, `fixture`, `fixtures`, `evidence` path segment도 허용하지 않는다.
`common`은 각 target inventory에 동일 파일 목록과 bytes로 배정한다. Project owner는 manifest와 세 partition hash를
고정하는 `governance/audit-events/2-demo-frozen.json`을 한 번 승인한다. 사건 레코드는
freeze inventory나 consumer 전달 대상이 아니다.

이 시점에 immutable해지는 payload는 manifest, 세 partition과 policy다. `receipts/`는 manifest
inventory 밖의 append-only 감사 영역으로 비워 두며 coordinated transfer에서 consumer별 파일을
정확히 한 번만 추가한다.

## 4. Coordinated transfer

고정 destination은 다음과 같다.

```text
Client:      somnia-client/docs/migration-input/dreamsquad-demo/<freeze-id>/
Game Server: somnia-game-server/docs/migration-input/dreamsquad-demo/<freeze-id>/
```

- Client에는 `manifest + common + client + policy`만 복사한다.
- Game Server에는 `manifest + common + game-server + policy`만 복사한다.
- 두 target은 복사 전에 source-side 사건의 Project owner 권한·순서·revision·freeze ID를
  검증하지만 사건 파일 자체를 bundle에 넣지 않는다.
- 각 저장소는 현지 정책에 따라 파일 수, byte count, manifest/common/assigned bundle hash를
  검증하고 receipt를 한 번 작성한다.
- Copy가 끊기면 같은 freeze ID와 manifest로만 재개한다. 다른 bytes는 새 transfer가 아니다.

## 5. `transfer-completed`

두 receipt가 [`schemas/receipt.schema.json`](schemas/receipt.schema.json)을 만족하고 audit copy와
target bytes가 같을 때 Project owner가
`governance/audit-events/3-transfer-completed.json`을 한 번 승인한다.
Receipt와 event 3을 Demo audit copy에 보존하고 같은 coordinated transaction 안에서 두 target의
intake record에 연결한다.

`transfer-completed` 뒤에는 receipt를 포함한 freeze audit tree 전체가 immutable하다.

이후 production 구현은 각 저장소에서 별도 activation·plan·review를 받아 진행한다.

## 실패 처리

| 시점 | 처리 |
|---|---|
| `demo-approved` 전 | candidate를 폐기·수정 가능; official event가 아님 |
| approval 뒤 content 변화 | transition terminal cancel; 재승인·재동결 없음 |
| freeze 전 dry-run 실패 | 원인 해결 전 freeze 금지 |
| freeze 뒤 copy 중단 | 동일 bytes만 재개 |
| freeze 뒤 의미 오류 | production errata/ADR/change control; Demo package 수정 금지 |
| 한 target receipt 거절 | activation 보류 또는 initiative 중단; 두 번째 export 금지 |
