# Codex 리뷰 가이드

> 이 프로젝트에서 Codex로 코드 리뷰를 실행하는 방법을 설명한다.
> 생성일: 2026-05-06

---

## 1. 리뷰 채널 구조

이 프로젝트는 **투트랙 리뷰** 구조를 사용한다.

| Track | 담당 | 검사 항목 |
|---|---|---|
| **Track A** | `adversarial-review` (Codex 네이티브) | 일반 코드 품질, 보안, 런타임 실패 경로, 데이터 손실 위험 |
| **Track B** | `ecs-reviewer` (프로젝트 스킬) | ECS 컨텍스트 경계, NativeQueue lifecycle, Burst 호환성, BattleBridge 위반 |

ECS 배틀 시뮬레이션 파일이 변경되면 두 트랙을 모두 실행해야 한다.
그 외 변경(UI, ScriptableObject 데이터, 테스트만)은 Track A만으로 충분하다.

---

## 2. ECS 파일 감지

리뷰 요청 시 아래 경로 패턴 중 하나라도 변경 파일에 포함되면 Track B를 추가한다:

```
Assets/_Project/Scripts/Battle/       ← ECS 배틀 시스템 전체
Assets/_Project/Scripts/Bridge/BattleBridge.cs  ← MonoBehaviour↔ECS 게이트웨이
```

확인 명령:
```bash
git diff --name-only
git diff --name-only --cached
```

Claude Code 환경에서는 `ecs-review-detector` hook이 자동으로 감지하고 컨텍스트를 주입한다.
Codex 환경에서는 `.codex/hooks/ecs-review-detector.mjs` 가 같은 조건을 감지하고 투트랙 리뷰 컨텍스트를 주입한다.

---

## 3. Codex에서 투트랙 리뷰 실행

### 3.1 Track A — adversarial-review

`/codex:review` 또는 adversarial-review 명령을 실행한다.

**이 프로젝트에서 집중할 공격 표면:**

- NativeContainer (`NativeQueue`, `NativeArray`, `NativeList`) 누수 — 배틀 리셋/씬 전환 시 Dispose 미호출
- `EntityCommandBuffer` 없는 iteration 중 structural change — 런타임 크래시
- 이벤트 채널 동일 프레임 재진입 — 무한 루프
- MonoBehaviour에서 `EntityManager` 직접 호출 — BattleBridge 위반 (CRITICAL)
- ScriptableObject 수치 없는 하드코딩 — 절대 제약 위반

### 3.2 Track B — ecs-reviewer

`ecs-reviewer` 스킬을 로드하고 변경 파일을 검토한다.

체크리스트 위치:
- Claude Code: `.claude/skills/ecs-reviewer/references/hybrid-ecs-review-checklist.md`
- Codex: `.codex/skills/ecs-reviewer/references/hybrid-ecs-review-checklist.md`

**반드시 확인할 항목:**

```
□ ISystem 사용 (SystemBase 금지 — 관리 참조 불필요 시)
□ [BurstCompile] 어트리뷰트 존재
□ RequireForUpdate<T>() 또는 명시적 틱 이유
□ UpdateInGroup/UpdateBefore/UpdateAfter 순서 정확성
□ ECB 없는 iteration 중 structural change 없음
□ OnUpdate 내 NativeArray/Query 할당 시 모든 경로에서 Dispose
□ 컨텍스트 경계 — Component 쓰기는 소유 컨텍스트만
□ NativeQueue 채널 — 배틀 리셋 시 Dispose 명시
□ BattleBridge 외 MonoBehaviour에서 ECS 직접 접근 없음
```

---

## 4. 판정 기준

두 트랙의 판정을 합산한다. 더 엄격한 쪽이 최종 판정이 된다.

| Track A | Track B | 최종 |
|---|---|---|
| approve | approve | **APPROVE** |
| approve | needs-attention | **BLOCK** |
| needs-attention | approve | **BLOCK** |
| needs-attention | needs-attention | **BLOCK (양측 블로커)** |

---

## 5. 프로젝트 고정 제약 (Hard Constraints)

리뷰 시 아래 위반은 severity와 무관하게 **즉시 BLOCK**:

1. `BattleBridge` 외 MonoBehaviour에서 `EntityManager` / `SystemAPI` 직접 호출
2. 컨텍스트 경계 위반 — 소유하지 않은 컨텍스트의 Component를 직접 수정
3. `NativeQueue` / `NativeArray` Dispose 경로 누락
4. 하드코딩 수치 (모든 스탯/공격 패턴/VFX 파라미터는 ScriptableObject에서)
5. `GameManager` 외 싱글톤 `XxxManager` 추가

전체 제약: `docs/TRD.md` 섹션 3(추상화 규칙), 섹션 5(금지 패턴)

---

## 6. 관련 파일

| 파일 | 설명 |
|---|---|
| `.claude/agents/ecs-reviewer.md` | ecs-reviewer 에이전트 정의 |
| `.claude/skills/ecs-reviewer/SKILL.md` | ecs-reviewer 스킬 상세 |
| `.claude/skills/ecs-reviewer/references/hybrid-ecs-review-checklist.md` | ECS 리뷰 체크리스트 |
| `.claude/skills/two-track-review/SKILL.md` | 투트랙 리뷰 오케스트레이션 |
| `.claude/hooks/ecs-review-detector.mjs` | ECS 파일 자동 감지 hook (Claude Code) |
| `.codex/config.toml` | repo-local Codex hooks feature flag |
| `.codex/hooks.json` | Codex UserPromptSubmit / Stop hook 연결 |
| `.codex/hooks/ecs-review-detector.mjs` | Codex 리뷰 요청 + ECS 변경 감지 및 컨텍스트 주입 |
| `.codex/hooks/two-track-review-stop-gate.mjs` | ECS 투트랙 리뷰 결과 누락 시 1회 continuation |
| `.codex/skills/ecs-reviewer/SKILL.md` | Codex용 ecs-reviewer 스킬 상세 |
| `docs/reference/review-skill-comparison.md` | 전체 리뷰 도구 비교 |

---

## 7. Codex Hook 자동화

Codex hook은 리뷰를 직접 대신 실행하지 않고 **리뷰 라우팅과 누락 방지**만 담당한다.

동작:
1. 사용자가 `리뷰`, `검토`, `검수`, `review`, `audit`, `check` 계열 프롬프트를 입력한다.
2. `git diff --name-only` 와 `git diff --name-only --cached` 에 ECS 경로가 있는지 확인한다.
3. ECS 변경이 없으면 일반 Codex 리뷰만 수행한다.
4. ECS 변경이 있으면 Track A common review 와 Track B `$ecs-reviewer` 를 모두 수행하도록 컨텍스트를 주입한다.
5. Stop hook은 ECS 투트랙 리뷰에서 Track A / Track B / 최종 판정 중 일부가 빠진 경우 한 번만 continuation을 요청한다.

ECS 감지 경로:
```text
Assets/_Project/Scripts/Battle/
Assets/_Project/Scripts/Bridge/BattleBridge.cs
```

주의:
- `UserPromptSubmit` 과 `Stop` hook은 matcher가 적용되지 않으므로 스크립트 내부에서 조건을 필터링한다.
- `Stop` hook의 `decision: "block"` 은 거절이 아니라 Codex가 한 번 더 응답하도록 만드는 continuation prompt다.
- common review는 Codex built-in `codex review` 명령이 자동 실행되는 것이 아니라, 현재 Codex 세션의 리뷰 태도로 수행된다. 필요 시 별도로 `codex review --uncommitted` 를 실행한다.
