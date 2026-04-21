# Project Context — Defense Tournament

> 이 문서는 Claude Code 세션 시작 시 자동으로 컨텍스트에 주입된다. 프로젝트의 정체성, 작업 방식, 필수 제약만 담는다. 상세는 참조 문서를 읽는다.

---

## 프로젝트 한 줄

비동기 토너먼트 디펜스 게임을 만든다. 프로토타이핑 단계(Phase 0~10)를 끝내고 **프로젝트 구체화 단계** 에 진입했다. 이후 모든 구현은 `docs/spec/{feature-slug}/` 단위 스펙으로 관리한다.

## 운영 원칙

- **스펙 단위 개발**: 기능 추가/변경은 먼저 `docs/spec/{feature-slug}/` 에 분산 스펙을 작성한 뒤 작업 단위 파일(0~N) 순서로 구현한다.
- **스코프 엄수**: 현재 작업 중인 spec 의 범위를 넘는 기능은 만들지 않는다. 관련 후보는 같은 spec 폴더의 "후속 후보" 섹션이나 별도 spec 초안으로 분리한다.
- **코드 품질 우선**: 만든 코드는 본 게임에서 계속 쓴다. "확장 가능"을 이유로 과도한 추상화를 쌓지 않지만, 버릴 코드라는 전제로 쓰지도 않는다.

## 작업 방식 전환 이력

- **Phase 0~10 (프로토타이핑)**: `Phase N → 검증 → 다음 Phase` 순서 워크플로우. 관련 문서 (`PHASE*.md`, `phase*-prep.md`, `phase*-decisions.md`, `residual-issues.md`) 는 모두 `docs/prototype/` 로 보존.
- **현재**: spec-driven. `docs/spec/{feature-slug}/` 에 feature 단위 분산 스펙을 작성하고 파일번호 순서로 구현/커밋한다. Phase 개념은 더 이상 쓰지 않는다.

프로토타이핑 이력이 필요하면 `docs/prototype/PHASE{0..10}.md` 참조.

## 기술 스택

- **엔진**: Unity 6 · URP 17.3
- **언어**: C#
- **아키텍처**: 하이브리드 ECS — 전투 시뮬레이션만 ECS, 나머지 MonoBehaviour
- **필수 패키지**: Entities 6.x, Entities Graphics, Burst, Collections, Mathematics, Jobs, TextMeshPro, Input System, spine-unity, spine-csharp
- **타겟**: Android 실기기 + Unity Editor 플레이

## ECS 맥락 분리

전투 시뮬레이션은 **맥락(Context)별로 분리**된다:

- **Units** — 유닛 정의, 배치 상태, Health, 생성/소멸, IncomingDamage 버퍼, 사망 이벤트 큐
- **Movement** — 경로 따라가기, 위치 갱신, Portal 텔레포트, Tornado field pull step
- **Combat** — 타겟팅, 공격 쿨다운, 데미지 적용, 사거리 판정, 투사체, Meteor 해결, defender attack event
- **Effects** — 상태이상(Slow/DamageBoost/CooldownReduction), 시너지(SynergyBuff), 스킬 캐리어(TornadoField/PortalLink/MeteorPending)

**맥락 간 통신 규칙**:
- Component는 소유 맥락이 있다. 다른 맥락은 **읽기만** 가능, 쓰기는 소유 맥락만.
- 맥락 간 이벤트는 Buffer 또는 NativeQueue 싱글턴을 통한다. 직접 Component 수정 금지.
- 현재 운영 중인 NativeQueue 채널: `GoalReachedEventsSingleton`, `DefenderDeathEventsSingleton`, `MeteorBurstEventsSingleton`, `DefenderAttackEventsSingleton`.

폴더 구조: `Assets/_Project/Scripts/Battle/{Units,Movement,Combat,Effects}/`. 상세는 TRD 섹션 2.5 참조.

## 절대 제약 (위반 시 정지하고 질문)

1. **ECS 경계 엄수**: `BattleBridge` 클래스가 MonoBehaviour ↔ ECS 통신의 유일한 창구다. 그 외 MonoBehaviour에서 `EntityManager` / `World.DefaultGameObjectInjectionWorld` / `SystemAPI` 직접 호출 금지.
2. **맥락 경계 엄수**: Component 쓰기는 소유 맥락만. 맥락 간 직접 호출 금지.
3. **SubScene 금지**, **SystemBase 남발 금지**(ISystem 우선), **네트워크 코드 완전 금지**.
4. **Manager 싱글톤은 GameManager 1개만**. 그 외 `XxxManager` 싱글톤 금지.
5. **하드코딩된 수치 금지**. 모든 유닛 스탯/공격 패턴/스킬 값/VFX 파라미터는 ScriptableObject 또는 프리팹에서 나온다.
6. **상속 2단계 최대** (MonoBehaviour, ScriptableObject에 적용).
7. **인터페이스는 구현체 2개 이상일 때만 생성**. "나중을 위한" 추상 레이어 금지.
8. **현재 작업 중인 spec 범위를 넘어서는 기능 구현 금지**. 범위 밖 항목은 별도 spec 초안 또는 해당 spec 폴더의 "후속 후보" 섹션으로 이관 후 대기.

**전체 제약 목록은 `docs/TRD.md` 섹션 3(추상화 규칙), 섹션 5(금지 패턴)를 반드시 참조**하라.

## 참조 문서 (필요 시 읽는 순서)

| 상황 | 읽을 문서 |
|---|---|
| "이 기능을 왜 만드나?" | `docs/PRD.md` — 검증 가설, 운영 원칙 |
| "어떤 기술 제약이 있나?" | `docs/TRD.md` — ECS 경계, 맥락 분리, 추상화 규칙, 금지 패턴 |
| "feature 구현 상세는?" | `docs/spec/{feature-slug}/` — 분산 스펙 (README + 0~N 작업 단위). 하단 "문서화 구조" 참조 |
| "과거 어떻게 만들어졌나?" | `docs/prototype/PHASE{0..10}.md` — 프로토타이핑 단계 종료 스펙 (읽기 전용 아카이브) |
| "VFX 를 만드려면?" | `.claude/skills/unity-vfx-authoring/` + `unity-vfx-integration/` 스킬 |
| "Unity 씬 와이어링?" | `.claude/skills/unity-feature-wiring/` 스킬 |

## 문서화 구조 (spec 분산 형식)

**단일 대형 plan 문서 금지**. feature-level 구현 스펙은 `docs/spec/{feature-slug}/` 폴더로 분산한다.

### 폴더 레이아웃

```
docs/spec/{feature-slug}/
├── README.md                ← 개요 + 공통 원칙 + 파일 목록 표 + 후속 후보
├── 0_{topic}.md             ← 첫 작업 단위 (enum/contract 같은 토대)
├── 1_{topic}.md
├── ...
└── N_{topic}.md
```

각 파일 **1~3KB 범위**, 작업 단위당 "목적 / 변경 대상 / 구현 / 완료 기준" 4섹션 구조.

### 구성 원칙

- **1 파일 = 1 커밋 단위 작업**. subagent-driven-development 의 implementer 가 해당 파일 하나만 읽고 작업 완료 가능해야 함
- **README.md**: 상위 목표 + 작업 단위 목록 표 (파일번호 / 작업 구분 / 문서 / 목적) + 공통 원칙 4~6 bullet + "후속 후보" 섹션(현 spec 범위 밖 항목)
- **파일번호는 작업 순서**: 같은 feature 에 추가 작업이 생기면 기존 파일번호 뒤에 누적 (rev 표기 가능)
- **완료 기준**: 각 파일 하단에 "완료 기준" 섹션 필수. compile / 테스트 / 시각 검증 기준을 명시
- **변경 대상**: 파일 경로 명시 (예: `Assets/_Project/Scripts/Bridge/BattleBridge.cs`)

### 참고 예시

- `docs/spec/map-system/` — 맵 시스템 재설계 (21 작업 단위, 프로토타이핑 종료 시점의 최종 spec)
- `docs/spec/defender-on-place-skills/` — 방어 유닛 배치 시 스킬 pipeline spec
- `docs/spec/defender-drag-drop-deployment/` — D&D 배치 전환 spec

### design.md 와의 관계

`docs/plans/YYYY-MM-DD-{topic}-design.md` 는 **얇은 브레인스토밍 결과물** (목표, 아키텍처 요약, `spec/` 폴더 포인터). 실제 구현 상세는 모두 `docs/spec/{feature-slug}/` 안에 둔다. writing-plans 스킬은 생략 가능 — spec 파일이 곧 각 task 의 plan 역할.

## 작업 지침

### 기본 워크플로우

1. 사용자가 새 feature 를 요청하면, 먼저 `docs/spec/{feature-slug}/README.md` 를 만들어 목표 + 작업 단위 목록을 잡는다. 기존 spec 의 추가/수정이면 기존 폴더에 새 파일번호로 이어 쓴다.
2. 사용자 승인 후, 작업 단위 파일 `0_{topic}.md` 부터 순서대로 구현한다. **한 번에 한 파일**. 선행 의존이 있으면 같이 언급.
3. 구현 완료 후 사용자에게 "완료 확인"을 요청한다. 에디터 또는 실기기에서 확인 가능한 방식을 구체적으로 알려준다.
4. 사용자가 통과를 확인하면 해당 작업 단위 파일의 "완료 기준" 섹션 하단에 확인 일자 + 커밋 해시를 한 줄 추가하고 커밋한다.
5. feature 전체 종료 시 `docs/spec/{feature-slug}/README.md` 상단에 "상태: 완료 YYYY-MM-DD" 를 기재. 필요 시 handoff 요약 파일 생성.

### 작업 시작 전 자가 점검

코드를 작성하기 전에 스스로 점검한다:

- [ ] 이 기능이 현재 spec 범위 안인가?
- [ ] 인터페이스를 만들려 한다면, 구현체가 2개 이상 있는가?
- [ ] 이 코드에 테스트를 작성하는 것이 자연스러운가?
- [ ] "확장 가능"을 이유로 만드는 구조가 지금 실제로 쓰이는가?
- [ ] Component 쓰기가 소유 맥락 내에서만 일어나는가?
- [ ] 상속 계층이 2단계를 넘지 않는가?
- [ ] Unity 씬 와이어링이 필요한가? 그렇다면 `unity-feature-wiring` 스킬을 따랐는가?

### ECS 설계의 불확실성 대응

1. **작은 결정은 에이전트가 내리고 짧게 설명한다** — 사용자가 실시간으로 ECS를 학습하는 효과
2. **아키텍처 수준의 결정은 사용자에게 질문한다** — 여러 정답이 있는 경우에만. 작업 단위마다 질문하지 않고 묶어서 한 번에.

**질문 가치가 있는 결정의 예**:
- Component 소속 맥락이 애매할 때
- 맥락 간 이벤트를 Buffer로 할지 NativeQueue로 할지
- SystemGroup 구성과 업데이트 순서
- Burst 호환 불가한 API가 필요해 보일 때

**질문하지 않아도 되는 결정의 예**:
- 폴더 내 파일 네이밍
- private 메서드 분할
- 로컬 변수 이름
- using 순서, 코드 포맷

### 기술적 결정이 필요할 때 (우선순위)

1. 현재 spec README 의 "공통 원칙" 또는 해당 작업 단위 파일에 명시돼 있으면 그대로 적용
2. 없으면 `docs/TRD.md` 참조
3. 없으면 `docs/PRD.md` 참조
4. 없으면 **작업 시작 전에 사용자에게 한 번에 묶어서 질문**. 작업 중간에 질문하지 않는다.

### 테스트

- ECS 시스템 내부의 순수 계산 함수는 **EditMode 단위 테스트**를 작성한다.
- 판 흐름 수준의 통합은 **PlayMode 테스트** 1개 이상.
- 커버리지는 목표가 아니다. **회귀 방지 수준**이면 충분하다.
- 테스트 작성이 작업 진행의 병목이 되면 우선순위를 낮춘다. 다만 ECS 시스템의 핵심 계산(데미지, 이동, 타겟팅)은 반드시 단위 테스트를 유지한다.

### 금지 행동

- **스펙 스코프를 임의로 넓히지 않는다.** "이왕 만드는 김에..." 같은 판단 금지. 범위 밖 항목은 spec 의 "후속 후보" 섹션이나 별도 spec 초안으로 이관.
- **추상화 먼저 만들지 않는다.** 인터페이스부터 정의한 뒤 구현하는 방식 금지. 구체 구현부터 시작해서 반복이 생기면 그때 추출한다.
- **사용자 확인 없이 다음 작업 단위로 넘어가지 않는다.**
- **경계를 유혹적으로 넓히지 않는다.** "이 한 줄만 예외로 하면..." 금지. 경계 위반이 필요해 보이면 정지하고 질문.
- **맥락 폴더를 임의로 만들지 않는다.** 현재 허용된 맥락은 Units / Movement / Combat / Effects 4개. 새 맥락이 필요해 보이면 질문. (Presentation 폴더는 ECS 맥락이 아닌 MonoBehaviour View 계층임을 명심.)
- **Unity 씬 wiring 을 "사용자 수작업" 으로 미루지 않는다.** UnityMCP로 자동화 가능한 것은 전부 자동화한 뒤 Play 검증까지가 완료.

## 기억할 것

- **코드 품질은 타협 대상이 아니다.** 프로토타이핑 단계는 끝났다. 만든 코드는 본 게임에서 계속 쓰인다.
- **각 spec 은 고유의 검증 질문이 있다.** 그 질문에 답하는 데 필요하지 않은 모든 것은 제외된다. 작업 단위 파일의 "완료 기준" 을 그 질문의 구체 표현으로 삼는다.
- **"가벼운 설계"와 "재사용 가능"은 양립 가능하다.** 방법은 맥락 분리 + 추상화 규칙 준수 + 현재 spec 범위 유지.
