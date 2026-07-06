# 리뷰 스킬 / 에이전트 비교

> 생성일: 2026-05-06  
> 대상: ecs-reviewer, OMC critic/harsh-critic/code-reviewer/quality-reviewer, Codex adversarial-review/stop-review-gate, superpowers code-reviewer

---

## 1. 역할 분류

| 도구 | 출처 | 리뷰 대상 | 핵심 질문 |
|---|---|---|---|
| **ecs-reviewer** | 로컬 프로젝트 스킬 | ECS 코드 (Unity 전용) | "ECS 패턴이 올바른가?" |
| **critic** | OMC agent | 플랜/스펙 | "이 계획으로 구현 가능한가?" |
| **code-reviewer** | OMC agent | 일반 코드 | "안전하게 출시 가능한가?" |
| **quality-reviewer** | OMC agent | 일반 코드 | "로직이 실제로 동작하는가?" |
| **harsh-critic** | OMC benchmark | 플랜 + 코드 | "어떤 결함도 놓치지 않았는가?" |
| **adversarial-review** | Codex 플러그인 | 코드 변경 | "이 변경을 무너뜨릴 수 있는가?" |
| **stop-review-gate** | Codex 플러그인 | 직전 턴만 | "ALLOW / BLOCK?" |
| **code-reviewer** | superpowers 플러그인 | 코드 (git range) | "플랜 대비 완성도는?" |

---

## 2. 도구별 핵심 특징

### ecs-reviewer — 유일한 도메인 전문가
- 다른 모든 도구는 언어/프레임워크 무관. ecs-reviewer만 Unity Entities 6.4 기준.
- 리뷰 시작 전 패키지 버전 확인 강제 (Entities 6.4 ≠ 1.x 패턴 구분).
- 프로젝트 규칙을 hard constraint로 주입: BattleBridge 단일 게이트웨이, 4개 컨텍스트 경계, NativeQueue 8채널 lifecycle, Burst 호환성, ISystem 우선, 맥락 간 component 직접 쓰기 금지.
- 출력: Findings first → CRITICAL/HIGH/MEDIUM/LOW → Residual Risk/Test Gaps.

**ecs-reviewer만 검사하는 항목:**
- Entities 6.4.0 vs 1.x 패턴 구분
- `BattleBridge` 단일 게이트웨이 위반
- NativeQueue create/drain/dispose lifecycle
- Burst 비호환 managed API 사용
- 맥락 소유 외 Component 쓰기
- `UpdateInGroup`/`UpdateBefore`/`UpdateAfter` 순서 충돌
- 배틀 리셋 시 singleton 중복 축적

---

### critic / harsh-critic — 가장 복잡한 프로토콜
- **5단계**: Pre-commitment 예측 → 검증 → Multi-perspective → Gap 분석 → Self-Audit + Realist Check.
- **ADVERSARIAL 모드 자동 에스컬레이션**: CRITICAL 1개 또는 MAJOR 3개+ 발견 시 전환. 인접 코드까지 범위 확장.
- **Realist Check**: 심각도 라벨 압력 테스트. 이론적 최악이 아닌 실제 최악 기준으로 재보정. 다운그레이드 시 "Mitigated by:" 명시 필수.
- **ralplan 전용 게이트**: principle-option consistency, pre-mortem quality, expanded test plan 체크.
- **harsh-critic vs critic**: harsh-critic은 benchmark용 공격적 버전. OMC 4.11.6 기준 거의 동일하나 `level: 3`, 출력에 ralplan 요약행 없음.
- 판정: REJECT / REVISE / ACCEPT-WITH-RESERVATIONS / ACCEPT.

---

### code-reviewer (OMC) — 2단계 강제 순서
- Stage 1(spec 준수) → Stage 2(코드 품질) 강제 순서. Stage 1 통과 전 스타일 지적 금지.
- `lsp_diagnostics` 강제 실행. 타입 에러 있으면 APPROVE 불가.
- model=haiku 호출 시 Style Review Mode 전환, 미지정 시 Logic+Security.
- 판정: APPROVE / REQUEST CHANGES / COMMENT.

---

### quality-reviewer — 범위 명시적으로 제한
- 보안 리뷰 의도적으로 제외 (security-reviewer 위임).
- SOLID 원칙, 순환 복잡도(< 10), 안티패턴 집중.
- 긍정 관찰(Positive Observations) 필수 포함.
- model=haiku → Style mode, 명시적 요청 시 Performance mode / Quality Strategy mode.
- 판정: EXCELLENT / GOOD / NEEDS WORK / POOR.

---

### Codex adversarial-review — 철학이 근본적으로 다름
- 다른 도구들: "문제를 찾는다." Codex adversarial: **"신뢰를 깨뜨린다."**
- 기본 전제: 변경은 subtle한 방식으로 실패 가능. 증명될 때까지 유죄.
- 출력이 **JSON** (다른 도구는 모두 마크다운). 각 finding에 `confidence: 0~1` 점수 포함.
- 스타일/네이밍/저가치 클린업 리포트 금지. 물질적 위험만.
- 공격 표면 우선순위: auth/tenant isolation, 데이터 손실/복구 불가, 레이스 컨디션, 관찰 불가 장애.
- 판정: `needs-attention` / `approve`.

---

### Codex stop-review-gate — 가장 단순, 자동화
- 사람이 호출하지 않음. Codex stop 이벤트 hook으로 **자동 발화**.
- 이전 턴이 코드 변경 없으면 즉시 `ALLOW`. 추가 조사 없음.
- 출력: 첫 줄이 무조건 `ALLOW: reason` 또는 `BLOCK: reason`.

---

### superpowers code-reviewer — 가장 균형 잡힌 템플릿
- 에이전트 정의가 아니라 **프롬프트 템플릿** (git BASE_SHA..HEAD_SHA 직접 주입 방식).
- 유일하게 **"강점 먼저, 문제 후"** 순서 명시적 강제.
- 심각도 용어: Critical / Important / Minor (타 도구와 다름).
- 판정: `Ready to merge? Yes | No | With fixes`.

---

## 3. 워크플로우 내 위치

```
플랜 작성
  └─ [critic / harsh-critic] ← pre-implementation gate
        ↓
     구현
        ↓
  [code-reviewer / quality-reviewer] ← post-implementation gate
  [Codex adversarial-review]         ← 동일 구간, 더 공격적
  [Codex stop-review-gate]           ← 각 Codex 턴 종료 시 자동 발화

ECS 코드 작성 시
  └─ [ecs-reviewer] ← domain-specific gate (병렬 또는 대체 투입)
```

---

## 4. 언제 어떤 도구를 쓰는가

| 상황 | 추천 도구 |
|---|---|
| ECS 코드 변경 후 아키텍처/경계 검증 | **ecs-reviewer** |
| 플랜/스펙 작성 후 구현 전 검증 | **critic** |
| 구현 완료 후 merge 전 전반적 검증 | **code-reviewer** (OMC) |
| 로직 결함/SOLID/안티패턴 집중 검토 | **quality-reviewer** |
| 최대 강도 플랜 비판이 필요할 때 | **harsh-critic** |
| Codex 변경의 보안/안전성 adversarial 검증 | **adversarial-review** |
| Codex 세션 종료 자동 게이트 | **stop-review-gate** (자동) |
| 균형 잡힌 PR merge 판단 | **superpowers code-reviewer** |
