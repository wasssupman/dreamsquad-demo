# Spec Documentation Structure

이 폴더는 프로토타이핑 이후의 feature 단위 구현 스펙을 보관한다. 새 기능은 `docs/spec/{feature-slug}/` 폴더 하나로 관리하고, 구현 단위는 번호가 붙은 작은 문서로 나눈다.

## 기본 구조

```text
docs/spec/{feature-slug}/
├── README.md
├── 0_{topic}.md
├── 1_{topic}.md
├── ...
├── N_{topic}.md
└── {N+1}_handoff_summary.md
```

## README.md

feature 의 입구 문서다.

- 현재 상태
- 목표
- 연결 문서
- 구현 문서 목록
- feature-wide 계약과 공통 원칙
- 비목표 또는 후속 후보

README 는 상세 구현서를 대신하지 않는다. 다음 작업자가 어디까지 완료됐고 어떤 번호 문서부터 읽어야 하는지 안내하는 인덱스다. 단, feature 전체에 영향을 주는 load-bearing 계약은 README 에 남긴다.

## 번호 문서

`0_{topic}.md` 부터 작업 순서대로 작성한다.

권장 섹션:

- 목적
- 변경 대상
- 구현
- 완료 기준

원칙:

- 1문서 = 1커밋에 가까운 작업 단위
- 1~3KB 정도의 작은 문서 유지
- 파일 경로를 명시
- 완료 기준은 compile/test/Play 확인 기준까지 포함
- 기존 번호를 재사용하지 않고 뒤에 추가
- 구현 완료 후에도 바뀌면 안 되는 계약만 갱신한다
- diff 설명이나 코드 흐름을 사후 문서화하지 않는다

## Handoff Summary

feature 구현이 끝났거나 세션 인계 가능성이 높으면 마지막 번호로 `{N+1}_handoff_summary.md` 를 작성한다.

예:

```text
docs/spec/map-system/20_claude_handoff_summary.md
docs/spec/wave-pattern/5_handoff_summary.md
```

필수 섹션:

- Commit
- Implemented
- Key Files
- Verified
- Notes
- Follow-up — 본 문서에 상세 항목을 적지 말고 본 README 하단 **Follow-up Backlog** 섹션으로 옮기고 한 줄 포인터만 남긴다

권장 길이:

- 30~80줄
- 핵심 파일 5~15개
- 완료 동작 5~10개

handoff 는 source of truth 가 아니다. 최신 상태와 계약은 README/번호 문서가 우선하고, 구현 상세는 코드와 커밋 히스토리가 우선한다. handoff 는 다음 에이전트가 무엇을 읽고 무엇을 건드리지 말아야 하는지 빠르게 파악하기 위한 지도다.

## Source Of Truth

```text
README.md                 최신 상태 + feature-wide 계약
{N}_{topic}.md            작업 단위 계약 + 완료 기준
{N+1}_handoff_summary.md  커밋 이후 인계 지도
code + git history        구현 상세
```

문서는 구현 상세를 전부 따라가지 않는다. 하지만 계약이 바뀌면 문서도 같이 바꾼다.

## Review 반영 기준

- 코드 버그를 유발하는 계약 공백: 코드 + 테스트 + 관련 spec 갱신
- 구현과 문서의 표현 불일치: 문서 갱신
- 단순 구현 설명 요구: handoff 에 짧게 쓰거나 생략
- 미래 확장/취향 제안: 후속 후보 또는 Follow-up 으로 이동

## 기존 예시

- `docs/spec/map-system/`
- `docs/spec/defender-drag-drop-deployment/`
- `docs/spec/defender-on-place-skills/`
- `docs/spec/wave-pattern/`

---

## Follow-up Backlog

종료된 spec 의 follow-up 후보를 한 곳에 모은다. 개별 spec 의 handoff 에는 한 줄 포인터만 남기고 항목은 여기로 이관한다.

### 사용 규칙

- 각 항목 **1~3줄 요약** (What · Why · Scope). 상세 설계는 새 spec 에서 다룬다.
- Scope: **S** = 단일 unit, **M** = 2~5 unit spec, **L** = 5+ unit spec.
- **같은 결의 작업은 테마 서브그룹** (`#### {테마명}`) 으로 묶는다. 예: "Modifier framework — Legacy migration".
- 출처 spec 이 여럿 섞이기 시작하면 항목 끝에 `(spec-slug)` 라벨로 표기.
- 새 spec 으로 승격되면 줄을 `→ docs/spec/{slug}/` 링크로 대체한다.
- 더 이상 유효하지 않으면 줄을 삭제하거나 한 줄 사유와 함께 `Promoted` 로 옮긴다.

### Active

> 현재 항목 전부 `modifier-framework-and-healer` (closed 2026-05) 출처. 이후 다른 spec 출처가 섞이기 시작하면 항목 끝 라벨로 표기.

#### Modifier framework — Producer 확장

framework 코어 변경 0. 새 producer 레이어 추가로 다양한 효과 적용 경로 확보. producer-agnostic 설계 검증 시점.

- **Aura defender** [M] · 지속 영역 효과 producer (`AuraOutput[]` + `AuraApplySystem`). 일정 반경 ally 에 매 프레임/N초마다 StatModifier 발화.
- **Projectile on-hit modifier** [S] · `ProjectileResolveSystem` hit 시 ModifierApply 채널 enqueue. 화염/빙결/둔화 투사체. MoveSpeedMul 도입 시 적용 확장.

#### Modifier framework — 내부 보강

framework 코어/UX/테스트 보강. 콘텐츠 확장 전후 모두 가치 있음.

- **Modifier UI 시각화** [M] · defender HUD + 적 머리 위 활성 modifier 아이콘 표시. ModifierStats / Slot buffer read-only 구독. UI 리소스 의존.
- **Dispel/Cleanse 채널** [S] · ModifierBuffer 슬롯 제거 채널 (kind/source 기반). CombineOp 별 면역 정책. 콘텐츠 디자인 선행.
- **Testability — Stack threshold dispatch** [S] · `BattleBridge._stackThresholds` 에 test 주입 API 또는 `IStackThresholdRegistry` 인터페이스 도입. skipped Test 3 활성화 목적.
- **Testability — AttackSystem outputs dispatch helper 추출** [S] · `OnUpdate` 의 4-way 분기를 `static ProcessAttackOutputs(...)` 로 추출. skipped Test 4 활성화 목적.
- **추가 EditMode 회귀 테스트** [S] · Stack threshold edge (5→6→5 재발화) / Consume 모드 stack 차감 / IncomingHeal drain Clear / RegenPerSec 누적. Testability 보강과 합쳐 진행.

#### 기타

- **Healer 전용 Spine asset** [S] · 현재 Archer Spine reuse. 전용 rig + idle/heal-cast/death 애니메이션. 시각 식별성, 기능 영향 없음.
- **Spec 5~10 backfill** [S] · hybrid 진행 시 누락된 단위 spec 파일 작성. commit/handoff 가 임시 대체 중. 필수는 아님.

### Promoted

- **Modifier framework — Legacy migration** → `docs/spec/modifier-legacy-migration/`
