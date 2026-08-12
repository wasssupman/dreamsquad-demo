# Demo → Production Transition

> 상태: **dormant · owner-gated · not Demo authority**
>
> Project owner가 현재 요청에서 production-transition 작업을 명시적으로 활성화하지
> 않았다면 이 subtree를 읽거나 갱신·검증하지 않는다.

## 목적

계속 바뀌는 single-player Demo에서 production Client와 authoritative Game Server가
보존해야 할 **규칙과 구현 계획**을 축적한다. 이 자료는 Demo를 설계하거나 검증하는
문서가 아니며 Demo와 불일치하면 stale한 downstream으로 남는다.

Production의 실제 API, DTO, protocol, runtime과 저장 구조는 각 production 저장소의
정책과 ADR이 결정한다. 이곳의 문서는 그 결정을 대신하지 않는다.

## Demo firewall

- Demo가 유일한 upstream이다. Demo 정본은 `CLAUDE.md`, 활성 `docs/spec/**`, 적용 가능한
  TRD/PRD, 코드·에셋·테스트다.
- Transition 갱신은 사용자가 명시한 **별도 후행 작업과 별도 commit**으로만 수행한다.
  Demo feature 작업, CI, 완료 기준, review, build와 같은 작업에 끼워 넣지 않는다.
- Demo 변경을 watch하거나 freshness를 자동 계산하지 않는다. 누락과 stale을 허용하며
  official freeze 직전에 한 번만 전수 reconciliation한다.
- Transition 문서를 근거로 Demo 코드·spec·asset·test를 추가하거나 바꾸지 않는다.
- `archive/legacy/`와 `maintenance/`는 official bundle 대상이 아니며 정상 탐색 경로도 아니다.
- Freeze, 이동과 production 구현 activation은 서로 다른 승인이다.

## 두 consumer와 common

Official consumer는 정확히 두 개다.

| Consumer | 책임 | 받는 문서 |
|---|---|---|
| Production Client | 사용자 입력, pending/accepted/rejected/corrected UX, authoritative projection, UI·VFX·SFX·camera·haptics | `common + client + policy` |
| Production Game Server | gameplay ruleset, command validation, canonical state transition, ordering·time·numeric·RNG, score/result | `common + game-server + policy` |

[`common/`](common/README.md)은 세 번째 consumer package가 아니다. 한 번 저작하고 official
freeze 때 두 consumer bundle에 byte-identical하게 각각 포함하는 공통 partition이다.

## 문서 지도와 권위

1. [`governance/transition-policy.md`](governance/transition-policy.md) — 역할, 금지사항,
   Project owner의 3개 one-shot 사건
2. [`governance/one-time-transition-plan.md`](governance/one-time-transition-plan.md) —
   reconciliation부터 receipt까지의 단 한 번인 실행 절차
3. [`common/`](common/README.md) — 양쪽이 동일하게 보존할 기술 중립 의미
4. [`client/`](client/README.md), [`game-server/`](game-server/README.md) — consumer별 규칙,
   coverage와 production 구현 plan
5. [`governance/decision-register.md`](governance/decision-register.md) — 아직 owner가 결정해야
   하는 Product/기술 질문
6. [`maintenance/change-register.md`](maintenance/change-register.md) — 비차단 후행 capture inbox
7. [`archive/legacy/`](archive/legacy/README.md) — historical, non-normative, non-export

같은 의미가 여러 문서에 있으면 위 순서를 따른다. Archive의 과거 registry, evidence,
fixture, ADR 후보와 lifecycle 표현은 living 문서를 override하지 않는다.

## Living 문서 계약

각 규칙은 최소한 `Rule ID / 책임 owner / invariant / 허용·금지 동작 / semantic input·outcome /
production 제약 / 미결 decision / Demo source pointer`를 가진다. 구현 타입, wire DTO, code path,
fixture와 commit 증거는 normative 규칙 본문에 복제하지 않는다.

Coverage row는 `included | excluded | decision-blocked` 중 하나다. Dormant 준비 중에는
`decision-blocked`를 허용하지만 official freeze의 included 범위에는 미결 blocker가 없어야 한다.

## One-time lifecycle

```text
dormant/preparing
  -> demo-approved
  -> demo-frozen
  -> transfer-completed
```

세 사건은 Project owner가 각각 한 번 승인한다. Product owner, tech owner 또는 steward의
승인은 이를 대신하지 않는다. 중단된 copy는 같은 freeze ID와 같은 bytes만 재개할 수 있다.
Freeze 이후 오류는 production errata/change control로 처리하며 Demo re-freeze나 두 번째
이동은 허용하지 않는다.

현재 official event와 `freezes/`는 존재하지 않는다.
