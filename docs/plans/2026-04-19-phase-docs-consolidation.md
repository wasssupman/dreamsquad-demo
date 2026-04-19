# Phase 문서 통합 계획 — Phase 0~8 구현 스펙 단일 출처화

> 목적: `docs/PHASE0.md` ~ `docs/PHASE8.md` 를 각 Phase의 **현재 구현된 스펙 단일 출처**로 정리한다. 이미 구현 완료된 보조 문서의 확정 내용은 해당 `PHASE{n}.md` 로 흡수하고, 보조 문서는 히스토리/리뷰 기록으로 격하한다.
>
> **리뷰 반영 버전 (2026-04-19)**: Critical 1건(Phase 8 compile blocker 오기), High 2건(파일 상한 / `CLAUDE.md` 갱신), Medium 3건(skill design doc 처리 / rollback 전략 / residual 경계) 반영 완료.

---

## 1. 목표

- Phase별 구현 스펙은 해당 `PHASE{n}.md` 하나만 보면 파악 가능하게 한다.
- `phase{n}-decisions.md`, `phase8-vfx-*`, `phase7-scope-proposal.md` 등 보조 문서의 확정/구현 완료 내용은 해당 Phase 문서에 통합한다.
- 기존 `PHASE{n}.md` 의 큰 구조는 유지하되, 문서 성격을 "초기 계획"에서 "구현 스펙 + 잔여 검증"으로 전환한다.
- Phase 9 착수 전에는 `PHASE8.md`, `residual-issues.md`, `phase9-prep.md` 만으로 현재 상태와 다음 작업을 판단할 수 있게 한다.
- 프로젝트 루트 `CLAUDE.md` 의 Phase 상태 정보도 현재 구현(§17 Tornado field 완결, Phase 9 prep 중) 기준으로 동기화한다.

---

## 2. 통합 원칙

1. `PHASE{n}.md` 의 top-level 구조와 기존 섹션 흐름은 유지한다.
2. 체크박스는 "계획 체크"가 아니라 "구현 결과 체크"로 갱신한다.
3. 보조 문서의 토론, 대안 비교, 리뷰 왕복 내용은 길게 복붙하지 않는다. 최종 결정과 실제 구현 결과만 요약한다.
4. 구현과 달라진 초기 계획은 현재 구현 기준으로 수정한다. 필요한 경우에만 "초기 계획에서 변경됨" 한 줄을 남긴다.
5. Play 검증, Android 실기 검증처럼 아직 확인되지 않은 항목은 완료 처리하지 않는다.
6. Phase 9로 이관할 항목은 `phase9-prep.md` 에 남기고, `residual-issues.md` 는 미검증/잔여 blocker/이관 포인터 중심으로 유지한다.
7. 보조 문서는 삭제하지 않고 상단에 superseded 배너를 추가해 히스토리 기록으로 남긴다.
8. **파일 상한 ≈ 400~500줄** 가이드라인. 초과 위험이 있는 Phase (7/8) 는 §13 이후 상세를 `phase{n}-extended.md` 로 분리하는 escape hatch 를 선제 정의.
9. **residual-issues.md 와 PHASE{n}.md 경계 명확화**:
   - `PHASE{n}.md` = 해당 phase 범위의 잔여 검증(Play/Android)만
   - `residual-issues.md` = Phase 간 걸친 issue + Phase 9 이관 포인터

권장 superseded 배너:

```md
> Superseded: 확정/구현 완료 내용은 `PHASE{n}.md`에 통합됨. 본 문서는 히스토리/리뷰 기록으로만 유지.
```

---

## 3. Phase별 통합 범위

### PHASE0.md

흡수 대상:

- `phase0-decisions.md`

통합 내용:

- 실제 좌표계와 맵/경로 구현 방식.
- ECS/MonoBehaviour 경계와 `BattleBridge` 역할.
- 초기 전투 루프, 배치, 승패, 로그 구현 결과.
- Android 실기 검증 유보 상태.

정리 방향:

- "최소 실시간 디펜스 루프 구현 스펙"으로 정리한다.
- Android 실기 검증은 잔여 검증으로 유지한다.

### PHASE1.md

흡수 대상:

- `phase1-decisions.md`

통합 내용:

- `DraftSession`, `DraftController`, `DraftView` 실제 구조.
- 10종 pool / 7종 picked 흐름.
- Restart 는 같은 pick 유지, Redraft 는 새 pick 흐름.
- draft 로그 스키마.

정리 방향:

- "드래프트 구현 스펙"으로 정리한다.
- P1 Android 검증 유보는 완료 처리하지 않는다.

### PHASE2.md

흡수 대상:

- `phase2-decisions.md`

통합 내용:

- `SkillData`, `SkillRuntime`, `SkillBar` 구현 구조.
- SlowField / RapidFire / PowerSurge 최종 스펙.
- aim-mode 상태 조정과 `GameManager.IsAiming` 협조 방식.
- Effects component 적용, cooldown, skill usage 로그.

정리 방향:

- "초기 3종 스킬 구현 스펙"으로 정리한다.
- Phase 4 이후 보강된 cost 연동과 충돌하지 않게 현재 상태를 보정한다.

### PHASE3.md

흡수 대상:

- `phase3-decisions.md`

통합 내용:

- `ProjectileData`, `ProjectileRef`, `ProjectileSpawnRequest`, `ProjectileState` 전달 흐름.
- `ProjectileMoveSystem`, `ProjectileHitSystem` 역할.
- HealthBar / HitFlash 구현 결과.
- 즉시 데미지 fallback 유지.

정리 방향:

- "투사체 + 전투 피드백 구현 스펙"으로 정리한다.
- Phase 3 당시 제외했던 Particle/VFX는 Phase 8에서 확장됐다는 후속 note 를 추가한다.

### PHASE4.md

흡수 대상:

- `phase4-decisions.md`

통합 내용:

- onPlace 효과.
- `SynergyBuff` 와 인접 시너지 재계산.
- defender death event queue.
- enemy attack.
- projectile splash.
- synergy / onPlace 로그.

정리 방향:

- "배치 판단 확장 + 시너지 + 적 반격 + splash 구현 스펙"으로 정리한다.
- 긴 리뷰/검토 흔적은 최종 결정만 요약한다.

### PHASE5.md

흡수 대상:

- 별도 decisions 문서 없음.
- 실제 코드와 Phase 6/8 후속 문서의 상태 정보를 참고한다.

통합 내용:

- Billboard Quad 도입 결과.
- Phase 5에서 구현된 타이머/브리핑/봇/측정 범위.
- Phase 5에서 Spine을 보류했고 Phase 8에서 하이브리드 Spine으로 도입했다는 후속 연결.

정리 방향:

- 현재 stub 성격을 줄이고 "구현된 Phase 5 범위"와 "후속에서 대체/확장된 범위"를 분리한다.

### PHASE6.md

흡수 대상:

- `phase6-decisions.md`

통합 내용:

- `CostRuntime`.
- `GamePhase` 상태 머신.
- placement countdown.
- cost UI.
- placement / skill cost 지불.
- Restart / Redraft reset 흐름.

정리 방향:

- 이미 구현 완료 문서에 가까우므로 보조 결정만 흡수하고 상태 문구를 표준화한다.

### PHASE7.md

흡수 대상:

- `phase7-scope-proposal.md`

통합 내용:

- 2-slot skill loadout.
- `SkillLoadoutController`.
- Tornado / Meteor / Portal 최종 스펙.
- Meteor warning ring + delayed AoE.
- Portal two-tap cast flow.
- skill pool / picked / seed 로그.
- `target_tile_b` 로그.

정리 방향:

- "구현 미시작" 상태와 빈 체크박스를 제거하고 실제 구현 스펙으로 갱신한다.
- Portal 동선 이상은 Phase 9 이관 항목으로 링크한다.
- Tornado 초기 cast-time pull 구현은 Phase 7 기록, **Phase 8 §17 에서 지속 field 로 재설계됨** 을 후속 note 로 명시 (해결 완료, blocker 없음).
- **분량 상한 주의**: 원 plan + impl + review 흡수하면 400줄 초과 위험. §14 이후 상세는 `phase7-extended.md` 로 분리 허용.

### PHASE8.md

흡수 대상:

- `phase8-vfx-enhancement-plan.md`
- `phase8-vfx-enhancement-impl.md`
- `phase8-vfx-review.md`
- `plans/2026-04-19-vfx-authoring-skill-design.md` 중 실제 적용된 policy

통합 내용:

- `DefenderUnitData` Spine 필드.
- `SpineDefenderView`, `SpineDefenderPool`.
- `DefenderAttackEventsSingleton` 기반 공격 애니메이션 트리거.
- attack-time facing snap.
- defender 10종 `skeletonDataAsset` / `spineSkinName` 설정 완료 상태.
- prefab-only VFX 정책.
- `VfxSpawner` prefab slot 필수 정책.
- `Placement_SKELETON`, `Meteor_Falling_SKELETON`, `Meteor_Burst_SKELETON`, `Tornado_SKELETON`, `Portal_SKELETON`.
- `BeamPulse`.
- `MeteorBurstEventsSingleton`, `DefenderAttackEventsSingleton`.
- scene slot wiring.
- `TornadoField` 캐리어 엔티티 기반 지속 field (§17, 커밋 `5d0a2ad` 에서 완결).
- `attackTargetCount` 기반 melee AoE.

정리 방향:

- Phase 8을 Spine + VFX 최종 구현 스펙 문서로 대폭 갱신한다.
- VFX 보조 문서의 fallback 정책 등 현재 구현과 다른 내용을 본문에서 현재 구현 기준으로 바로잡는다.
- P8 Play 회귀 검증(P8-10)과 VFX 카탈로그 사용자 승인만 잔여 항목으로 남긴다. **컴파일 blocker 는 없음**.
- **분량 상한 주의**: §1~§17 모두 포함 시 500줄 초과 예상. §13 이후 상세는 `phase8-extended.md` 로 분리 허용.

---

## 4. 보조 문서 처리 정책

### Superseded 표시 후 유지

- `phase0-decisions.md`
- `phase1-decisions.md`
- `phase2-decisions.md`
- `phase3-decisions.md`
- `phase4-decisions.md`
- `phase6-decisions.md`
- `phase7-scope-proposal.md`
- `phase8-vfx-enhancement-plan.md`
- `phase8-vfx-enhancement-impl.md`
- `phase8-vfx-review.md`
- `plans/2026-04-19-vfx-authoring-skill-design.md` ← **Medium 리뷰 반영**: 실운영 정책은 `.claude/skills/unity-vfx-authoring/` 스킬이 소스이므로 design doc 는 brainstorming 산출물로 superseded.

처리:

- 상단에 superseded 배너 추가.
- 확정/구현 완료 내용은 해당 `PHASE{n}.md` 로 흡수.
- 보조 문서는 의사결정 히스토리와 리뷰 기록으로만 유지.

### Active 유지

- `PRD.md`
- `TRD.md`
- `CODEX-HARNESSING.md`
- `residual-issues.md`
- `phase9-prep.md`

처리:

- `PRD.md`, `TRD.md`, `CODEX-HARNESSING.md` 는 이번 통합 작업 범위 밖.
- `residual-issues.md` 와 `phase9-prep.md` 는 Phase 8 통합 후 현재 상태에 맞게 갱신한다.

---

## 5. 실행 순서 (Phase별 독립 커밋, bisect 가능)

각 Phase 통합 = 1 커밋 기준. 중간 오류 시 단일 Phase 만 revert 가능하도록.

1. 보조 문서별로 해당 Phase 문서에 흡수할 최종 결정만 추출한다.
2. `PHASE0.md` 갱신 + `phase0-decisions.md` superseded 배너. **1 커밋**.
3. `PHASE1.md` + `phase1-decisions.md`. **1 커밋**.
4. `PHASE2.md` + `phase2-decisions.md`. **1 커밋**.
5. `PHASE3.md` + `phase3-decisions.md`. **1 커밋**.
6. `PHASE4.md` + `phase4-decisions.md`. **1 커밋**.
7. `PHASE5.md` 갱신 (별도 decisions 없음). **1 커밋**.
8. `PHASE6.md` + `phase6-decisions.md`. **1 커밋**.
9. `PHASE7.md` + `phase7-scope-proposal.md`. 분량 초과 시 `phase7-extended.md` 분리. **1 커밋**.
10. `PHASE8.md` + `phase8-vfx-enhancement-plan.md` + `phase8-vfx-enhancement-impl.md` + `phase8-vfx-review.md` + `plans/2026-04-19-vfx-authoring-skill-design.md`. 분량 초과 시 `phase8-extended.md` 분리. **1 커밋**.
11. `residual-issues.md` + `phase9-prep.md` 갱신:
    - `residual-issues.md` 경계: Phase 간 걸친 issue / Play·Android 검증 / Phase 9 이관 포인터만
    - `phase9-prep.md`: 맵/길찾기 Phase 9 범위만. Phase 8 compile blocker 언급 없음 (이미 해결됨)
    - **1 커밋**.
12. **`CLAUDE.md` 갱신 (프로젝트 루트)**:
    - `## 현재 단계` → "Phase 9 착수 대기" 로
    - `## 작업 지침` 의 "P0-NN" 레퍼런스를 "현재 Phase" 로 일반화
    - `현재 활성 Phase` → "Phase 9 prep" 으로
    - 절대 제약 8번 (Phase 0 범위 제한) 을 "현재 Phase 범위 제한" 으로 일반화
    - **1 커밋**.
13. 최종 grep 점검 (§6) 수행. 수정 있으면 추가 커밋.

**중간 점검**: Step 5 (PHASE3 완료) 이후 한 번, Step 10 (PHASE8 완료) 이후 한 번 — grep 최종 점검 쿼리를 mid-run 으로 실행해 일관성 확인.

---

## 6. 최종 점검 쿼리

정리 후 아래 검색 결과가 의도된 문맥만 남아야 한다.

```sh
# PHASE 파일에서 진행 중/미착수 표현이 남아 있는지
rg -n "구현 미시작|진행 중|초안|전달 준비|착수 가능" docs/PHASE*.md

# PHASE 파일에서 Phase 8 초기 상태 언급이 남아 있는지 (보조 문서만 남아야 함)
rg -n "Archer=Lamb|현재 Archer|코드 폴백" docs/PHASE*.md

# 과거 blocker 표현 — 보조 문서에만 있어야 함, PHASE*.md 에서는 제거
rg -n "다음 업데이트|Phase 8 범위 밖|TornadoPull" docs/PHASE*.md

# 미체크 체크박스는 실제 잔여 검증만
rg -n "\[ \]" docs/PHASE*.md docs/residual-issues.md docs/phase9-prep.md

# superseded 배너 누락 점검
rg -L "Superseded" docs/phase*-decisions.md docs/phase7-scope-proposal.md docs/phase8-vfx-*.md docs/plans/2026-04-19-vfx-authoring-skill-design.md
```

판정 기준:

- 과거 상태 문구는 superseded 보조 문서에만 남거나, 명시적으로 historical 문맥이어야 한다.
- `PHASE{n}.md` 의 빈 체크박스는 실제 잔여 검증만 의미해야 한다.
- `TornadoPull` 은 보조 문서에만 남아야 한다 (Phase 8 §17 해결 완료).
- Phase 9 이관 항목은 `phase9-prep.md` 중심, 중복 없음.

---

## 7. 완료 기준

- `PHASE0.md` ~ `PHASE8.md` 가 각 Phase의 구현 스펙 단일 출처로 동작한다.
- 각 PHASE 파일이 400~500줄 이내 (초과 시 `phase{n}-extended.md` 분리).
- 구현 완료된 보조 문서는 모두 superseded 표시가 있다.
- `residual-issues.md` 는 현재 남은 작업만 담는다.
- `phase9-prep.md` 는 맵/길찾기 Phase 9 범위만 담는다.
- `CLAUDE.md` 루트 문서가 실제 현재 Phase 상태 반영.
- Phase 9 전환 판단 문장이 명확하다:

> Phase 8 문서 통합은 완료. Phase 8 §17 Tornado field 까지 구현 완결, 컴파일 0 에러 검증됨. 남은 blocker 는 **P8-10 Play 회귀 검증** (사용자 작업) 뿐. Play 검증 통과 후 Phase 9 착수.
