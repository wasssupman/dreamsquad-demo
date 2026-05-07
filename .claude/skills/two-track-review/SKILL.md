---
name: two-track-review
description: >
  투트랙 병렬 코드 리뷰. code-reviewer(일반 품질/spec 준수)와 ecs-reviewer(ECS 도메인)를
  동시에 실행하고 findings를 수렴해 단일 판정을 출력한다.
  트리거: "투트랙 리뷰", "two-track review", 또는 ecs-review-detector hook이 ECS 변경을 감지한 경우.
---

# Two-Track Review

## 개요

code-reviewer와 ecs-reviewer를 병렬로 실행해 일반 품질 리뷰와 ECS 도메인 리뷰를
동시에 수행한다. 각 리뷰어는 독립적으로 동작하고, 현재 세션(lead)이 결과를 수렴한다.

두 리뷰어가 검사하는 영역은 거의 겹치지 않는다:
- **code-reviewer**: spec 준수, lsp 타입 에러, SOLID, 보안, 일반 안티패턴
- **ecs-reviewer**: ECS 컨텍스트 경계, NativeQueue lifecycle, Burst 호환성, BattleBridge 위반

## 실행 절차

### Step 1 — 변경 범위 확인

```bash
git diff --name-only
git diff --name-only --cached
```

ECS 파일 포함 여부를 확인한다. hook이 이미 파일 목록을 주입했다면 그것을 사용한다.

ECS 변경이 없으면 사용자에게 알리고 단일 code-reviewer로 전환한다.

### Step 2 — 두 에이전트 병렬 스폰

**단일 메시지에 두 Agent 호출을 함께 전송한다 (순차 금지).**

스폰 전에 lead가 직접 실행:
```bash
git diff --name-only          # unstaged
git diff --name-only --cached # staged
git diff --name-only HEAD~1..HEAD  # 마지막 커밋
git diff HEAD~1..HEAD -- <변경 파일들>  # 실제 diff 내용
```
위 출력을 각 에이전트 프롬프트의 `{DIFF}` 자리에 첨부한다.

```
Agent 1:
  subagent_type: "oh-my-claudecode:code-reviewer"
  model: "opus"
  prompt: |
    아래 변경 파일과 diff를 리뷰하라.

    변경 파일:
    {git diff --name-only 출력}

    Diff:
    {git diff HEAD~1..HEAD 출력 (또는 staged diff)}

    - Stage 1(spec 준수) → Stage 2(코드 품질) 순서
    - lsp_diagnostics를 모든 변경 파일에 실행
    - 출력: CRITICAL/HIGH/MEDIUM/LOW 그룹화 + APPROVE/REQUEST CHANGES 판정

Agent 2:
  subagent_type: "ecs-reviewer"
  model: "opus"
  prompt: |
    아래 변경 파일과 diff를 ECS 도메인 관점에서 리뷰하라.

    변경 파일:
    {git diff --name-only 출력}

    Diff:
    {git diff HEAD~1..HEAD 출력 (또는 staged diff)}

    - CLAUDE.md, docs/TRD.md의 ECS 제약을 hard constraint로 적용
    - .claude/skills/ecs-reviewer/references/hybrid-ecs-review-checklist.md 사용
    - 출력: CRITICAL/HIGH/MEDIUM/LOW 그룹화 + Residual Risk/Test Gaps
```

### Step 3 — Findings 수렴

**판정 규칙 (더 엄격한 쪽 우선):**

| Track A | Track B | 최종 판정 |
|---|---|---|
| APPROVE | APPROVE | **APPROVE** |
| APPROVE | REQUEST CHANGES | **REQUEST CHANGES** |
| REQUEST CHANGES | APPROVE | **REQUEST CHANGES** |
| REQUEST CHANGES | REQUEST CHANGES | **REQUEST CHANGES (양측 블로커)** |

**Findings 병합:**
- 동일 `file:line`을 양측이 모두 리포트 → 하나로 합치고 "(양측 확인)" 표시
- severity는 더 높은 쪽 채택
- ECS 경계/컨텍스트 판단은 ecs-reviewer 우선
- spec 준수/타입 에러는 code-reviewer 우선

### Step 4 — 통합 리포트 출력

```
## Two-Track Review 결과

### 판정: APPROVE / REQUEST CHANGES

### Track A — code-reviewer (CRITICAL: N, HIGH: N, MEDIUM: N, LOW: N)
[주요 findings 발췌]

### Track B — ecs-reviewer (CRITICAL: N, HIGH: N, MEDIUM: N, LOW: N)
[주요 findings 발췌]

### 통합 Findings

#### CRITICAL
- [file:line] — [설명] [출처: A/B/양측]

#### HIGH
- ...

#### MEDIUM / LOW
- ...

### 수정 후 재리뷰
수정이 완료되면 다음 중 선택:
- "투트랙 재리뷰" — 두 트랙 다시 실행
- "code-reviewer만" — spec/타입 확인
- "ecs-reviewer만" — ECS 경계 확인
```

## Codex / 비-Claude Code 환경

`Agent` 툴이 없는 환경에서는 다음 순서로 순차 실행한다:

1. 플랫폼 네이티브 리뷰어 실행 (Codex: adversarial-review)
2. ecs-reviewer 내용을 현재 세션에서 직접 실행

병렬이 아니지만 두 트랙의 검사 항목은 동일하게 커버된다.
