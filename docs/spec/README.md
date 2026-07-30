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
- `docs/spec/spawn-point-alert/`

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

> 출처 spec 이 섞여 있다. 그룹 헤더 또는 항목 끝의 `(spec-slug)` 라벨로 출처 표기.

#### 액티브 드림캐쳐 (active-dreamcatcher-tile-aim + active-ally-zone — 완료 2026-07-30, 사용자 Play 확인 통과)

액티브 6종을 "화살표 + 타일 지정" 한 문법으로 통일하고(대상축 `SkillTargetType` 폐기), 아군 버프를
시간제 장판으로 바꿔 "액티브는 지정한 칸에 영역을 만든다" 를 성립시켰다. 선택 중 액티브 차단도 폐기.
상세: `docs/spec/active-dreamcatcher-tile-aim/4_handoff_summary.md` ·
`docs/spec/active-ally-zone/5_handoff_summary.md`.

- **감속장을 캐리어로** [M] · `ApplySlowField` 는 아직 **스냅샷**이다 — 시전 시점 반경 내 적에게 지속시간 모디파이어를 직접 걸어, 나중에 들어온 적은 안 걸리고 나간 적은 계속 느리다. 원칙("안에 있는 대상이 영향을 받는다")의 **마지막 예외**. `AllyBuffField`/`TornadoField` 패턴을 적 쪽에 그대로 적용하면 된다. (active-ally-zone)
- **장판 위 아군 하이라이트** [M] · 지금은 바닥 점등만이라 "누가 강화 중인가" 가 유닛에 안 붙는다. `SetHoverHighlight` 는 단일 슬롯 래치라 조준 틴트와 공존 불가 → 정식 경로는 `StatusFxKind` append + `ReconcileStatusFx` 의 `origin == Skill` 분기. **프리팹 1개(아트) 필요.** (active-ally-zone × active-dreamcatcher-tile-aim)
- **press 시점 범위 프리뷰** [S] · 카드를 집는 순간 어디에 얼마나 퍼지는지 보이면 "이 카드는 영역" 을 글로 배우지 않아도 된다. 지금은 끌어야 보인다. 액티브/부착 공통. (active-ally-zone)
- **적/아군 장판 프리뷰 색 구분** [S] · 조준 프리뷰가 적 대상·아군 대상 모두 같은 aim 타일이다. 장판 점등은 민트로 갈랐으니 조준도 같은 언어로. (active-dreamcatcher-tile-aim × active-ally-zone)
- **장판 겹침 시각 규칙** [S] · 두 장이 겹친 칸의 표현(현재 refcount 로 점등만 유지, 수명 페이드 없음). 칸별 색은 타일맵당 컬러 1개 제약 때문에 별도 설계가 필요하다. (active-ally-zone)
- **손가락 오클루전 오프셋** [S] · 타일 조준점이 손끝에 가려지는지 실기기 확인 후 판단. 배치 쪽에는 이미 가상 포인터 오프셋 선례가 있다. (active-dreamcatcher-tile-aim)
- **`TornadoField`/`PortalLink` 매치 경계 정리 누락** [S] · `DestroyBattleEntities` 에 없어 캐리어가 다음 판까지 산다. 적 전용이라 매치 사이엔 대상이 없어 실질 무해했지만 `AllyBuffField` 와 같은 구멍이었다. (active-ally-zone)
- **`SkillData.cooldownSec`/`cost` 완전 삭제** [S] · 액티브 흡수 후 dormant(각성치가 비용, 순환이 재등장 간격). 에셋 값만 남아 있다. (active-dreamcatcher-tile-aim)
- **`PendingDeployment` 제외 테스트 커버** [S] · 배치 대기 유닛이 장판/오라 멤버십에서 빠지는 규칙에 테스트가 없다(재배치 비행 창 포함). (active-ally-zone)
- **액티브 전용 카드 아트** [S] · 현재 uiTint/스킬명 폴백. (active-dreamcatcher-tile-aim)

#### 드래그 취소 (drag-cancel-affordance — 완료 2026-07-30, 사용자 Play 확인 통과)

유닛 트레이 / 드림캐쳐 손패 D&D 의 취소 수단. 트레이·손패 복귀 취소 + 격자 밖 관용(`Resolve` 가
`Vector2Int?` 반환)으로 "보드 밖에 놓으면 취소" 를 성립시켰다. 상세:
`docs/spec/drag-cancel-affordance/4_handoff_summary.md`.

- **출발 슬롯 지목** [S] · 취소 상태에서 그 유닛의 슬롯만 코랄 링/미세 확대로 "이 슬롯으로 돌아간다" 를 지목. rev3 에서 덮는 배너를 지울 때 함께 보류했으나 **덮지 않는 보강**이라 재고 가치가 있다. 현재 예고는 프리뷰 고스트 + 포인터 라벨뿐. (drag-cancel-affordance)
- **취소 수단 최초 발견 힌트** [S] · 취소 라벨은 취소 상태에 **들어간 뒤에만** 뜨므로 수단을 모르는 플레이어는 계속 모른다. 첫 배치 드래그 1회에 1.5초 안내 — `UserDragStarted` 훅이 이미 있고 `GimmickGuideView` 가 같은 훅을 쓴다. (drag-cancel-affordance)
- **취소 존 진입 햅틱** [S] · 모바일에서 취소 상태 진입 시 짧은 진동. 실기기 확인이 필요해 분리했다. (drag-cancel-affordance)
- **감각 노브 재조정** [S] · `placementOutsideToleranceCells` 1(가장자리가 빡빡하면 2, 취소가 멀면 0) · `cancelHintDwellSeconds` 0.18. 둘 다 `DragSwaySettings.asset` 에서 Play 중 실시간 반영 — 실기기 감각이 갈리면 여기부터. (drag-cancel-affordance)
- **손패의 기존 ESC 취소 제거** [S] · `DreamcatcherHandView.Update` 의 ESC → `CancelAllCardInteraction` 은 이 spec 이전 동작이라 남겼다. unit 2 철회 사유("모바일에서 드래그 중 back 은 손가락이 닿지 않는다")가 그것에도 적용되지만, 사용자에게 취소 수단으로 안내되지 않는 에디터 편의라 제거는 별건 판단. (drag-cancel-affordance)
- **재배치(DefenderRelocation) 취소** [M] · 이미 배치된 유닛을 드는 경로라 "무차감" 정의가 다르다(원위치 복귀가 취소). 트레이 존 판정을 그대로 쓸 수 없어 별도 설계 필요. (drag-cancel-affordance)
- **손패 패널 배경을 부채 크기로** [S] · 이 spec 은 **판정 rect** 만 카드 부채(310)에 맞추고 배경 그림은 그대로 뒀다. 배경을 키우면 "취소 영역 = 보이는 손패" 가 그림으로도 일치하지만 보드 가림이 60px 늘어난다. (drag-cancel-affordance × dreamcatcher-hand-drag-clearance)

#### 화염 스택 원거리 적 (enemy-fire-stack-shooter — 완료 2026-07-30, 사용자 Play 확인 통과)

킨들러(레인저 전용 하드 타겟팅 원거리 적) + 프로젝트 최초 Fire 스택 producer. 선행 결함
(투사체가 부여한 스택이 누적되지 않던 것)을 unit 0 에서 수정. 상세:
`docs/spec/enemy-fire-stack-shooter/4_handoff_summary.md`.

- **`ApplyStat` 의 투사체 귀속 결함** [S] · `ProjectileHitSystem` 의 `ApplyStat` 이 아직도 `source` 로 **투사체 엔티티**를 보내 발사마다 새 `StatModifierSlot` 을 만든다. `Enemy_Debuffer`(Needle 투사체, `DamageMul ×0.6 Multiplicative`)는 **한 기만 있어도** 0.6ⁿ 로 곱누적된다 — `modifier-stacking-policy` 가 "서로 다른 소스"로 진단하고 클램프 `[0.2, 5]` 로 막은 증상의 실제 뿌리로 보인다. `ApplyStack` 은 unit 0 에서 고쳤고 이쪽만 남았다. 고치면 곱누적 → 상시 ×0.6 이라 **라이브 밸런스가 움직이므로 수치 재조정과 한 묶음**이어야 한다. (enemy-fire-stack-shooter)
- **전투 스택 오버헤드 아이콘** [M] · `OverheadStackKind` 가 기믹 전용(`Fatigue`/`Heat`) 2종뿐이라 전투 스택(Fire/Ice/Bleed/Poison) 축적이 화면에 안 보인다. 5스택이 터지기 전까지 플레이어가 받는 신호가 피격뿐. `bleed-fighter-defender` 후속 후보와 **같은 항목** — 먼저 착수하는 쪽이 흡수한다. (enemy-fire-stack-shooter × bleed-fighter-defender)
- **화상 히트 VFX 분리** [S] · 스택 적재 순간의 피격 피드백과 5스택 발화 순간의 폭발 연출이 구분되지 않는다(둘 다 벤더 `FireballHit` 원샷). (enemy-fire-stack-shooter)
- **다중 공격자 DoT 합산** [M] · 킨들러 N기가 붙어도 화상은 한 슬롯(`(Stack, Fire)`)으로 접혀 `remainingTime = max` 갱신만 된다 — **화상 화력이 마릿수에 비례하지 않고 4.0 DPS 가 천장**. 2026-07-30 사용자 결정으로 **그대로 두기로 확정**했다(뒤집으려면 `enemy-fire-stack-shooter` README 계약 2·6-1 인용 후 재승인). 열려면 `dot-effect-extraction` 의 "다중 공격자 출혈 합산"과 같은 작업 — 도트 전용 가산 병합이 필요하고 난도질꾼도 같이 바뀐다. (enemy-fire-stack-shooter × dot-effect-extraction)
- **킨들러 전용 아트** [S] · 현재 Spine 은 공용 스켈레톤 + 파츠 placeholder(풀페이스 레이싱 헬멧 + 화염 틴트), 투사체는 벤더 as-is. ⚠ 부품 라이브러리가 **캐주얼 현대물**이라 로브/마법사 모자가 없다 — 판타지 캐스터 외형은 아트 신규 제작 사안. (enemy-fire-stack-shooter)

#### 발사 명세 시스템 (projectile-emission-pattern — units 0~5 완료 2026-07-28, Play e2e 대기)

- **카드 bake payload 화이트리스트 + terminal else** [S] · 카드 bake 의 payload `if/else if` 체인에 terminal `else` 가 없어, 배선 안 된 kind 가 조용히 슬롯으로 붙고 "부착됨"으로 집계된다(설명 공란). 단순 추가가 안 되는 이유는 `NextAttackDoubleFire`·`SelfBuffLethal` 처럼 **분기 없이 통과해도 정상인 kind** 가 섞여 있고 그 목록이 어디에도 없기 때문 — 실제 작업은 화이트리스트 명시다. `DcApplicability` 는 이미 전수 테스트로 강제되므로 bake 만 비대칭. 다음에 payload 를 추가하는 spec 이 흡수하면 된다. (projectile-emission-pattern)
- **범용성 갭 4종** [M~L] · 무타겟 패턴(방향/셀 지정) · host 독립 발사(사망 유언·bridge-cast) · 서브 발사(착탄 → 자식 패턴) · non-Damage 패턴(Stat/Stack/Heal outputs). 상세는 `docs/spec/projectile-emission-pattern/README.md` 후속 후보.
- **defender 패턴 개통 시 안정 키** [S] · 타겟 선택의 tie-break 가 같은 셀 중복 시 스냅샷 순서에 의존한다. 적 후보는 자유 이동이라 셀 공유가 상시 — `hostIsDefender` 경로를 여는 커밋에서 반드시 함께 처리. (projectile-emission-pattern)

#### 드림캐쳐 공격 결합 (dreamcatcher-attack-decoupling — 완료 2026-07-27)

- **페이로드 다연발(n초 간격 m발)** [M] · 비수가 발동당 1발만 쏜다. `VolleyMath.TickBurst`(머신거너 10연발)와 `SpawnNeedleCarrier` 를 그대로 재사용하면 되고, 실제 작업은 "버스트 상태를 어디에 두고 언제 틱하느냐" 하나다 → `docs/spec/dreamcatcher-payload-burst/`
- **방향탄 bounce 개통** [M] · 통통구슬×머신거너. `defender-directional-volley` 후속 후보의 사용자 결정("차단이 아니라 개통")이 살아 있다. 볼리 arm 이 bounce 필드를 적재 + `PathHit` pierce 소진 후 재조준 + `pierceCount>1` 합성 규칙.
- **`payload × trigger` 배선표** [M] · 현재 적용성 판정은 host 필터일 뿐이라 `AttackN × SelfTileAoe` 같은 조합이 통과한다(현 카탈로그엔 없음). `DcTrigger.GateComboSupported` 가 gate 조합에 하는 일과 같은 형태. critic 2종 모두 "별도 spec" 판정.
- **`FrontmostTarget × facing 유닛`** [S] · 붙지만 보너스가 inert(레인 타게팅이 우선순위를 덮는다). 경로 의존이 아니라 **타게팅 규칙 의존**이라 현재 지원 행렬로는 표현이 어색하다.
- **`Projectile_Shuriken_GA` 의 `flightMode` 미직렬화** [S] · 기본값(Homing) 의존이라 다음 사람이 대포를 ballistic 으로 오독한다. 명시 저장.

#### 손패 카드 시인성 (dreamcatcher-hand-card-face — 완료 2026-07-25)

손패 카드에서 아트를 걷고 타입색 헤더 + 대상 태그 + 효과 본문 구조로 교체(프로토 검증용 —
정식판은 원안 복귀 전제), 상단 툴팁은 조작 브리핑으로 전환했다.
상세: `docs/spec/dreamcatcher-hand-card-face/`.

- **아웃게임 카드 문법 통일** [M] — 덱빌더 그리드/덱 스트립/브라우저/상세 팝업을 같은 BG·태그
  문법으로. `CardCategoryStyle` 손패 함수(HandHeader/TargetTag)가 그대로 소스
- **실기기(Android) 폰트 가독 확인** [S] — 본문 18~24pt 는 에디터 Game 뷰 기준 검증. 태그 칩
  (10~15pt)이 최우선 확인 대상
- **PlayMode 기존 실패 6건 트래킹** [M] — 본 spec 검증 중 발견, spec 무관 판정(인증 서버 500 ·
  Gift 페이즈 진입 2건 · 씬 전환 · 덱 캐리인 0장 · CcEffect ECS 예외). 타 세션 in-flight 작업과
  대조 필요
- **태그 아이콘화 + 색약 팔레트 검증** [S~M] — 칩에 아이콘 병행(색약 대응 겸), 타입 3색 대비 검증

#### 점수 시스템 (battle-score-formula · score-tally-sequence — 둘 다 완료 2026-07-21)

최종 점수를 예산 소모 모델(시간+스트레스+킬)로 교체하고, 전투 종료 후 합산 연출을 붙였다.
상세: `docs/spec/battle-score-formula/`, `docs/spec/score-tally-sequence/`.

- **유출 한계 10 의 플레이 감각 미확인** [S] — 근거 없는 시작값. 조정 시 **한계×점당점수=예산**이라 `stressScorePerPoint` 를 짝으로 움직여야 한다 (battle-score-formula)
- **점수 재검증 / 무효 플래그** [L] — 서버가 배틀로그 입력값으로 재계산해 클라 제출값과 대조. 결정론적 재시뮬은 고정 타임스텝 도입이 선결 (battle-score-formula)
- **정예 등급 도입** [M] — 현재 잡몹 9종 + 보스 1종뿐이라 중간 등급이 없다. 밸런스 변경 (battle-score-formula)
- **계약 지불의 점수 손실 HUD 경고** [S] — 몽마의 계약 1회 = 900점인데 유출 카운터만 보이고 점수 손실 표시가 없다 (battle-score-formula)
- ~~Tally 흐름 PlayMode 테스트~~ → **완료** (`score-tally-sequence` unit 4). `TallyFlowTest` 2건. `onDone` 을 일부러 끊어 검출력까지 증명함
- **Tally 구간 무음** [S] — `SoundManager` 가 `phase == Battle` 에서만 BGM 유지라 4초 연출이 완전 무음. 축별 사운드와 함께 볼 것 (score-tally-sequence)
- **연출 카메라 동반 · 축별 사운드 · 신기록 갱신 강조** [S~M] — 상세는 spec README "후속 후보" (score-tally-sequence)

#### Next Wave 조기 호출의 시간점수 보상 (wave-pattern unit 9 이관, 2026-07-21)

unit 9 로 `Next Wave` 가 남은 웨이브 전체를 앞당기게 되면서 "빨리 불러 빨리 끝내기"가 실제로
성립한다. 사용자 인지 완료 — **밸런싱 영역이라 unit 9 스코프 밖으로 둔다.**

- **조기 클리어의 시간점수 보상 과다** [M] · `timeScorePerSecond: 100` × 180초 = 시간점수 예산
  18,000 으로 총 예산 37,300 의 약 48%. `ForceNextWave` 는 예정 시각 도달 여부를 검사하지 않아
  시작 직후 연타로 전 웨이브를 한꺼번에 쏟을 수 있다(연타 허용은 README 계약, unit 9 이전부터의
  동작). 조기 클리어가 지배 전략이 될 개연성이 높다. 제동 후보: 호출 쿨다운, "예정 시각 −N초부터만
  호출 가능" 게이트, 시간점수 상한 또는 곡선화. 어느 쪽이든 `stressScorePerPoint`·킬점수와의
  예산 균형을 함께 봐야 한다. (wave-pattern)

#### PlayMode 사전 실패 (2026-07-21 관측 3건 → 2026-07-30 재측정 13건)

> **2026-07-30 재측정 (page-local-presets 작업 중)**: 에디터 실행 기준 PlayMode **77개 중 13개**
> 사전 실패. 테스트가 40→77개로 늘며 실패도 늘었다. 아래 3건은 그대로 남아 있고, 추가 10건은
> 전부 ECS/Bridge·연출·서버 도메인이다. **격리 실행으로 사전 실패임을 확인**했다(프리셋 변경분이
> 없는 조합에서도 동일 실패):
> - `DragCancelZoneTest` · `DreamcatcherCursedRelicTest` · `DreamCocoonTest` ·
>   `DreamcatcherEffectTest`(2건) · `PlacementAuraTest`(3건) — 격리에서도 실패
> - `SceneTransitionSmokeTest` · `BountyMarkTest` — **격리에서는 통과**. 전체 실행 순서 의존
>   (교차 오염). 스위트 순서 위생 문제이고 특정 spec 의 회귀가 아니다.
> - `AuthE2ETest` — dev 서버 `uk_users_user_name` 중복키(500). 환경 문제이며 `e2e-test`
>   계정명이 서버에 이미 존재해 sign-up 이 실패한다.
>
> `DreamcatcherDeckCarryInTest` 의 원인은 확정됐다 — 폴백 덱이 **의도적으로 제거**됐다
> (`DreamcatcherHandController.ResolveAttachDeck`, "기본(fallback) 덱 제거 (사용자 결정
> 2026-07-15)"). 즉 제품 버그가 아니라 **stale 테스트**이고, 기대값을 0장으로 갱신하는 것이 맞다.

2026-07-21 관측: 에디터 실행 기준 PlayMode 40개 중 **3개** 실패. `596191c5` 와 `649991bb`
양쪽에서 동일해 first-session-tutorial units 10~12 와 무관하다.

- **`DreamcatcherDeckCarryInTest.SelectedSavedDeck_DrivesDraws`** [S] · `selectedDeckId = null` 일 때
  `ResolveAttachDeck()` 폴백이 **0장**을 돌려준다(기대 10). `DreamcatcherDeck_Default` 에셋 자체는
  10장이므로 폴백 경로나 씬 배선 쪽 문제로 보인다.
- **`SquadCarryInSmokeTest.FilledSquad_SkipsDraft_EntersPlacement`** ·
  **`DreamstoneCarryInSmokeTest.EquippedSquad_StartSquadMatch_EndToEnd`** [S] · START 후 페이즈가
  `Placement` 가 아니라 `Gift` 다. Gift 페이즈가 Placement 앞에 삽입된 뒤 테스트가 갱신되지 않은
  **stale 테스트**일 가능성이 높다 — 제품 버그가 아니라 기대값 갱신 문제인지 먼저 확인할 것.

> **배치 실행(`-batchmode -nographics`)으로 재면 14건으로 부풀어 보인다.** 나머지 11건은
> `EntitiesAssetGC.GetAdditionalRoots` → `Unity.Rendering.EntitiesGraphicsSystemUtility.RootsHandlerDelegate`
> 의 NRE 로, Unity 패키지 내부에서 GC 타이밍에 터져 그때 돌던 테스트에 임의 귀속된다.
> **PlayMode 판정은 에디터 실행으로 한다.** 배치는 EditMode 전용으로 쓸 것.

#### 첫 판 튜토리얼 개선 (first-session-tutorial units 10~12 이관, 2026-07-21)

- **신규 동작의 테스트 커버리지** [S] · `ClassHint` 전이(`OnPlacementCommitted → ClassHint →
  ContinueTapped → Start` + 12초 폴백)와 `AwakeningGaugeView.SetSuppressed`(=`SetActive` 직접 호출로는
  못 잡는 회귀)에 자동 테스트가 없다. `TutorialDragGuidanceTests` 는 레이아웃 헬퍼만, PlayMode 스모크는
  `UnlockTutorialStart()` 를 스스로 불러 ClassHint 를 통과하지 않는다. (first-session-tutorial units 10~12)
- **클래스 안내 문구 정합성 3건** [S] · 전부 사실 확인 완료, 문구는 사용자 작성본이라 보류.
  ① 배치 트레이 role 배지가 `원/수/근/술/보` 단일 글자라 안내문 클래스 이름과 겹치는 글자가 0개 —
  읽어도 트레이에서 대응시킬 앵커가 없다. ② `적 이동경로에 방해물을 설치` 는 캐스터 4종 중
  `BlockingCaster` 하나에만 맞다(나머지는 장판, 기본 스쿼드에 Blocking·Fire 둘 다 포함).
  ③ 표기 4중 드리프트 — 배지 `보` / `UnitLabels.ClassLabel` `서포트` / 유닛 desc `서포트` / 안내문
  `서포터`. 게임 desc 는 `어그로` 대신 `도발` 을 쓴다. (first-session-tutorial unit 11)
- **서포터가 첫 판 스쿼드에 없다** [S] · 기본 스쿼드는 카탈로그 앞 7개(Archer·Bastion·BlockingCaster·
  Bruiser·Cannon·FireCaster·Guardian)이고 유일한 Support 인 Healer 는 8번째다. 첫 판 트레이에 `보` 배지가
  한 칸도 없는데 안내문은 서포터를 설명한다. 안내문에서 빼거나 기본 스쿼드를 바꾸는 두 방향. (unit 11)
- **온보딩 총량** [M] · 로비 챕터A(2탭) → 첫 판(목표·배치·클래스 6줄·시작) → 선물 홀드 2회 →
  로비 챕터B(2탭) → 2판 각성 3단계 = 안내 14비트·22줄. 게임플레이를 만들지 않는 순수 해제 탭이 5회다.
  체감 후 뺄 것을 정한다(후보: 클래스 안내, 각성 0단계, 선물 2번째 홀드). (units 10~12 리뷰)
- **첫 판 각성 봉인의 첫인상 리스크** [S] · 첫 세션 전체(로비 A → 첫 판 → 결과)에서 "드림캐쳐" 라는
  단어가 한 번도 등장하지 않는다. 첫 판 이탈자는 게임의 차별점을 영영 못 본다. 대안은 "숨김" 대신
  "보이되 침묵"(버튼·게이지는 노출, 힌트만 억제) — 채택 시 unit 12 의 0단계가 불필요해진다. (units 10·12 리뷰)

#### 아웃게임 튜토리얼 (outgame-tutorial 종료 이관, 2026-07-21)

- **Android 실기기 QA** [S] · 노치·safe area dim 커버리지, Android 백키로 안내가 닫히는지(`Keyboard.current` 가 null 인 기기에서 미동작 가능 — 기존 `DreamcatcherHandView` 와 같은 제약), dim 톤 `UiOverlay.Dim` 알파 0.92 가 로비 배경 위에서 과한지. 링/홀 정렬은 실측 완료라 제외. (outgame-tutorial)
- **챕터 B 게이트를 독립 신호로 교체** [S] · 현재는 인게임 core 튜토리얼 완료 플래그를 재사용하는데, 그 실제 의미는 "core 튜토리얼이 발동하고 Battle 페이즈에 도달했다"다. `FirstSessionTutorialController` 의 fail-open 경로(참조 누락·affordable 슬롯 부재)를 탄 플레이어는 전투를 몇 판 하든 **챕터 B 를 영원히 못 본다**. 매치 카운트 같은 독립 신호가 의미에 맞다. (outgame-tutorial)
- **`SQUAD`/`DREAMCATCHER` 버튼 라벨 한글 통일** [S] · 좌측 열 4개 중 둘만 영문이라, 한국어 안내 문구("스쿼드와 드림캐쳐")를 읽고 라틴 라벨로 대응시켜야 한다. 같은 줄의 `프리셋`/`히스토리` 는 한글이라 잘못된 그룹핑도 유발한다. 로비 레이아웃 스펙 범위. (outgame-tutorial)
- **온보딩 인지 지표 관측** [M] · 현 spec 은 노출과 실행만 보장하고 인지는 검증하지 않는다. `2번째 판 시작 시 안내 없이 START 도달`, `첫 복귀 이후 세션에서 스쿼드/덱 패널 1회 이상 열기` 같은 사후 관측 지표가 필요. (outgame-tutorial)

#### 어그로 이동/클램프 (aggro-tile-chase 종료 이관, 2026-07-20)

- **cell-trim wall-slide** [S] · `ClampToBoundary` 가 양 축을 함께 clamp 해, 대각 desired 의 목적 셀이 벽이면 합법인 축 진행까지 취소된다(코너에서 넉백/외력이 흡수됨). 축 분해(x-only → z-only) 슬라이드로 완화. 어그로 경로는 cardinal 이라 무영향 — 잔여 노출은 impulse/tornado 품질. (aggro-tile-chase C1)
- **대각 코너 슬립 차단** [S] · 직교 이웃 둘이 벽이고 대각만 walk 면 모서리 틈으로 새어나간다. 신맵처럼 코너 많은 지형에서 시각적 노출 가능. (aggro-tile-chase C3)
- **동적 해저드의 chase 경로 무효화** [S] · chase field 는 획득 시 1회 계산이라, blocking hazard 가 나중에 경로를 막아도 재판정하지 않는다(해저드는 일시적이라 현 scope 밖으로 뒀음). 재빌드 트리거 또는 하강 실패 시 해제 규칙 필요. (aggro-tile-chase)
- **`aggroAttackRange` 데이터 결정** [S] · 현재 전 적 1 고정이라 "통로에서 2칸 밖 가디언"은 어그로가 안 걸린다(사양). 탱커를 더 멀리서도 끌려면 2로 올리거나 유닛별 분화 — 밸런스 결정. (aggro-tile-chase × aggro-standoff 승계)

#### 수동 맵 authoring (manual-map-authoring 종료 이관, 2026-07-19)

- **맵 authoring 에디터 툴화 + 분류값 재계산** [M] · 현재 레시피는 execute_code 로 road 배열→검증→WriteToDocument. 툴로 승격하면서 mergeDegree/chokepoint 를 저장값이 아니라 **로드/임포트 시 CellClassifier 로 재계산** — 수작업 값과 분류 정의의 조용한 드리프트 차단. (manual-map-authoring)
- **스테이지별 맵 운영** [M] · MapDocument 여러 장 + 스테이지→document 매핑. 현재는 ArkFunnel 1장 고정 배선. (manual-map-authoring)
- **시드 권한 일원화** [S] · fixedMapSeed(BattleBridge, 비0 코드 기본값)와 GameManager.debugFixedMatchSeed 이원화 정리. 비0 코드 기본값은 씬 저장 시 베이크되는 함정 + document 배선 중엔 무효인 유령 노브. 맵 빌드 로그에 시드 provenance(document/fixed/derived) 1줄 추가. (manual-map-authoring)
- **MapDocument 로드 방어** [S] · ToGeneratedMap 이 보조 배열(mergeDegree/chokepoint/propLayerId) 길이를 미검증 — 손상 에셋이 IndexOutOfRange 로 배틀 init 중단. PickGridSize 의 `math.abs(int.MinValue)` 음수 인덱스 경로도 동반 수리. (manual-map-authoring)
- **맵 설정 패널 잔여 위생** [S] · 패널이 표현 못 하는 mapSource(Manual/Fixture/Procedural_Legacy) 시 하이라이트/섹션 오표시, document 배선 중 크기/goalEdge 컨트롤 무피드백 no-op, SetGoalEdgeOnly 의 공유 SO 에셋 쓰기(Play 정지로 안 되돌아감). (manual-map-authoring)

#### 효과 트리거 통합 — 드림캐쳐↔기믹 (아키텍처, 파킹 2026-07-15)

- **트리거→효과 엔진 도메인 중립화** [M] · 드림캐쳐의 `DcMechanic`(이미 데이터주도 trigger→payload 중립계약)을 `Dc*` 명칭·`Data/Dreamcatcher/` 위치에서 떼어내 공용 `TriggerEffect*` 로 승격 + `EffectDomain{Dreamcatcher,Gimmick,...}` 태그로 소비처(오라/UI/dispel/밸런스) 구분. 기믹 Fatigue/Pickup/LastRun 을 그 위 rule 로 이관. **당장 안 함** — 논의·설계는 `docs/plans/2026-07-15-effect-trigger-unification-design.md` 에 기록. 권고: 0~1단계(태그+rename)만 저위험 선행, 시스템 이관은 2번째 기믹 생길 때(제약 8). rename 은 공유파일 광범위 → 세션 조율 필수.

#### 방향 지정 배치 · 다연발 (defender-directional-volley 종료 이관, 2026-07-17)

- **배치 취소/코스트 환불** [S] · 공격방향 페이즈엔 취소 제스처가 없다 — 드롭 = 코스트 확정. 데드존 릴리즈는 가이드를 유지하고 재스와이프를 기다린다. 취소를 넣으려면 `TryBeginDefenderDeployment` 의 spend 를 되감는 경로가 먼저 필요 — `drag-cancel-affordance` 는 커밋 **이전**에만 갈라져서 환불이 필요 없었고(계약 1) 그래서 이 페이즈를 범위 밖으로 뒀다. (defender-directional-volley × drag-cancel-affordance)
- **배치 후 방향 재지정** [S] · 유닛 탭 → 가이드 재오픈. `DeployedFacing` 은 현재 1회 기록 후 불변 계약이라 쓰기 소유권부터 재정의해야 한다. (defender-directional-volley)
- **레인 폭 파라미터화** [S] · 현재 1타일 고정(`LaneMath.IsInLane` 의 `side == 0`). 폭 2+ 는 side 허용치를 SO 로. (defender-directional-volley)
- **target-bound 투사체 wind-up 발사 보장** [M] · 일반 호밍 투사체는 RESOLVE 시 타깃을
  재판정하므로 wind-up 중 유일한 타깃이 죽거나 이탈하면 START 모션 뒤 투사체가 생략된다.
  방향탄은 `projectile-shot-sequence`에서 해결됐고 머신거너·폭탄맨은 구조상 노출되지 않는다.
  일반 투사체까지 바꾸려면 START 타깃 커밋·재타겟·빗나감 중 정책을 별도 spec에서 결정한다.
  (projectile-shot-sequence 후속)
- **버스트/스프레드 × Homing·Ballistic 조합** [S] · 볼리는 전 궤적에 열려 있으나 e2e 는 Directional 에서만 했다. Homing×버스트는 발마다 타겟이 재평가되지 않고 템플릿 스냅샷을 쓴다는 점 확인 필요. (defender-directional-volley)
- **머신건 연사음** [S] · 버스트 캐리어 발은 `DefenderUnitTag` 가 없어 drain 의 발사 SFX 게이트 밖 → 볼리당 1회. rat-tat-tat 을 원하면 battle-audio 쪽 게이트 재설계. (defender-directional-volley)
- **방향 가이드 정식 아트 + 머신건 아트** [S] · 가이드는 절차적 보드 스프라이트(레인 점등 + 삼각 화살표, unit 9), 유닛은 Marksman Spine + Sniper 파츠 플레이스홀더(guid 유지 교체 전제). (defender-directional-volley)
- **곡사 방향 발사** [S] · `DirectionalLinear` + arc 시각. 현재 방향탄은 평면 직선(sim-Y 없음). (defender-directional-volley)
- **tap-to-place 경로 연동** [S] · 공격방향 페이즈 진입점이 D&D `CommitPlacementAt` 하나뿐 — tap 확정 경로에도 같은 핸드오프가 필요하다. (defender-directional-volley × defender-tap-to-place)
- **bounce(통통구슬)×방향 유닛** [S] · 현재 통통구슬(`ProjectileBounce`)이 방향 유닛에 붙어도 inert(부착 가드는 ProjectileRef 만 보고 movement 무시 → 슬롯은 붙지만 Directional arm·PathHit 이 bounce 를 안 탐). **사용자 결정: 차단이 아니라 트리거당 N발(버스트/스프레드 캐리어 포함)이 각각 bounce 를 받아 진행**(각 방향탄이 경로 히트 후 다음 적으로 튕김). 과제 = 볼리 arm 이 bounce 필드를 template/캐리어에 싣기 + PathHit 이 pierce 소진 후 bounce 재조준을 지원(또는 bounce×pierce 합성 규칙 정의). 집계(count 합/range max/mul 곱)는 기존 재사용. (defender-directional-volley × dreamcatcher-attack-mod-bounce)

#### 로드아웃 규칙 위생 (game-start-loadout-gate 종료 이관, 2026-07-16)

- **스쿼드 저장 가드** [S] · 드림캐쳐 덱은 사용자 편집마다 즉시 저장하고 `LoadoutGate` 에서 출전만 판정한다. `SquadBuilderView.OnSave` / `SquadCharacterPageController.Save` 에도 `IsLoadedThisSession` 가드를 넣어, 직접 씬 실행 때 비어 있는 프로필을 저장하지 않도록 한다. (dreamcatcher-deck-autosave)
- **낡은 주석 정정** [S] · `PlayerProfile.cs:112` 의 "exactly 10, unique<=2" 중 **덱 크기 10 은 라이브와 일치**(현 `DeckRuleConfig_Default.asset` `deckSize=10` — 8→10 복귀), **"unique<=2" 만 stale**(라이브 `maxSquad=-1` → 타입캡 없음). **`DeckRules.cs:5-10` 은 건드리지 말 것** — 정확할 뿐 아니라 카탈로그 null → 폴백 10 함정을 경고하는 유일한 문서다. (game-start-loadout-gate · 2026-07-20 덱=10 확정 정정)
- **`7` 이중 하드코딩** [S] · `SquadSave.SlotCount` 와 `SquadDraw.FieldCount` 가 독립 상수다. 한쪽만 바꾸면 조용히 어긋나고 요구치가 도달 불가능해질 수 있다 — `LoadoutGate` 는 `min()` 으로 방어만 해뒀다. (game-start-loadout-gate)
- **`DreamcatcherDeckBuilderView.cs:45 DeckColumns = 10`** [S] · 현재는 라이브 `deckSize=10` 과 일치해 증상 없음(8장 시절의 셀 폭 불일치는 해소). 다만 `DeckColumns` 가 `EffectiveDeckSize` 를 읽지 않는 하드코딩이라 다음 deckSize flip 때 재발한다. (game-start-loadout-gate · 2026-07-20 덱=10 확정 정정)
- **`UiOverlay.Dim` 톤** [S] · alpha 0.92 는 전체화면 takeover 용이라 "유닛 2명 부족" 안내 팝업엔 무거울 수 있다. 공유 상수라 단독 spec 에서 바꾸지 않았다 — 조정하려면 전 팝업 영향 확인 필요. (game-start-loadout-gate)

#### 나이트매어 보스 스킬 (nightmare-whip-aura 종료 이관, 2026-07-12)

- **defender-side 오라 카드** [S] · `AllyMoveSpeedAura` 는 arm·오라 연출 모두 진영/kind 중립 — 카드 데이터 선언만으로 성립. 카드 taxonomy/밸런스는 product 결정. (nightmare-whip-aura)
- **채찍질 전용 연출 고도화** [S] · 현 WindAura 재사용 → 전용 채찍 스윙/버프 링 저작(unity-vfx-authoring). 발동-순간 원샷 arm(`payload.projectile`)도 SO 게이트로 잔존해 조합 가능. (nightmare-whip-aura)
- **수치 실측 튜닝** [S] · 펄스 0.5s/TTL 0.6s/+20%/3타일/오라 scale 1.2 — 전부 SO, 밸런스 실측 후 조정. (nightmare-whip-aura)
- nightmare-catcher 잔여 후속(기본공격 원거리화 · 위협 감쇠 · GA hitPrefab 전수 정비 · 폭격 피격 체감)은 `docs/spec/nightmare-catcher/README.md` 후속 후보 참조. **보스 어그로 면역은 `docs/spec/boss-jjangssen/3_boss_immunity.md` 로 승격(2026-07-29)** — 부착 1곳 차단 + 직접 행동정지·넉백 면역까지, `BossTag` 전체 적용(나이트메어 포함).

#### 보스 방어유닛 지향 이동 (헌터 재구현, 2026-07-11)

- **defender field dirty-skip 최적화** [S] · 방어유닛 셀 집합 불변 시 매 프레임 BFS 재빌드 skip. 현 그리드(20x10)에선 무의미 — 대형 그리드/프로파일 압박 시. (boss-defender-field, ecs-review M2)
- ~~**ecs-reviewer 채널 목록 stale**~~ — **완료 2026-07-11**. 재발 방지를 위해 목록 사본 자체를 제거 — 에이전트 정의가 CLAUDE.md § "ECS 맥락 분리"(source of truth) + 코드 실측 grep 을 가리키도록 변경. 코드 실측 18개 = CLAUDE.md 일치 확인. (boss-defender-field, ecs-review M1)

#### 유닛 상태 표현 / 인디케이터 (aggro-targeting 파생, 2026-07-09)

어그로 아이콘("!")을 만들며 드러난 일반화. **두 축으로 분리** — "느끼게 할 상태 연출" ↔ "훑어볼 정보 배지". 순차 진행 예정.

- ~~**상태별 프리팹 연출 인프라 (unit-status-fx)**~~ — **완료 2026-07-09** (`02a9db24`). `AggroIcon*` → `StatusFx*` 일반화: `StatusFxKind` + `StatusFxRegistry`(상태마다 프리팹) + `StatusFxSpawner`/`View`. 어그로 이관(현 "!" 폴백 유지). **잔여**: 실제 상태(스턴/빙결/독) registry 등록 + ECS 소스 훅, 어그로 전용 프리팹 연출(가디언 tether 등). (unit-status-fx)
- **모디파이어 인디케이터 스트립 (unit-modifier-indicators)** [M] · 버프/디버프(`ModifierStats` 델타·DoT 스택 Fire/Ice/Bleed/Poison)를 머리 위 아이콘 행으로. 스택/듀레이션 뱃지 + `+N` 오버플로. 상태 연출과 **다른 축**(정보 vs 느낌). 한 상태가 둘 다일 수 있음(예: 독=온-바디 VFX + 스택 아이콘). ~~드림캐쳐 부착 표기~~ 는 `docs/spec/unit-dreamcatcher-icons/` 로 분리 완료(2026-07-12) — 인디케이터 뷰 설계 시 해당 스트립과 y-오프셋/레이아웃 공존 고려. (aggro-targeting)

#### 곡사포 / 투사체 후속 (artillery-defender, projectile-trajectory-payload)

곡사포 유닛 완료(→ Promoted). 남은 후속:

- ~~**신규 유닛 프로필 reconcile**~~ [해결 2026-07-06] · 유닛을 프로필-소유(`ownedUnitIds`)에서 아예 제거하고 SquadBuilderView 가 `DefenderCatalog` 를 직독 → 모든 유닛 상시 오픈, 신규 유닛 자동 노출. 유닛 수집/가챠 도입 시 재검토(그때 소유 개념 부활). PlayerProfile.ownedUnitIds 삭제(JSON back-compat: 구 필드 무시).
- **slow-곡사포 / 임팩트 CC / arcHeight 거리비례 / 전용 Spine rig** [S/M] · artillery-defender 후속.
- ~~**Meteor→TileAoe 수렴 + GA 낙하 비주얼**~~ → `docs/spec/projectile-trajectory-payload/` units 7~9 **완료(2026-07-06)** — 레거시 3파일+큐 삭제(채널 15→14), Rock02 낙하+Hit_Rock03 파편, 스킬 aim/텔레그래프 격자 통일은 `placement-attack-range-preview/3_skill_aim_range.md`.
- **Bezier 궤적 / non-Damage payload / Homing+TileAoe** [S/M] · projectile-trajectory-payload 엔진 확장 후속.
- **적별 피격 반경(per-target hit radius)** [S] · 투사체 충돌은 현재 `투사체.hitThreshold` vs 적 **중심점 하나** 판정(`SweepHitMath.SegmentHits` / `ProjectileMoveSystem` HomingToEntity 도달). 적 SO엔 자기 hurtbox 필드가 없어(있는 건 `attackRange`=자기 공격 사거리뿐), `spineVisualScale 3.2` 보스처럼 몸 큰 유닛은 큰 몸통을 눈으로 관통해도 무판정 → 머신거너 등 직선 탄이 시각적으로 안 걸림. **제안**: `AttackUnitData.hitRadius` 필드(기본 0=현행 점 판정) + 스폰 시 `HitRadius` 컴포넌트 bake(1줄) + 충돌 2곳에서 `유효반경 = hitThreshold + target.hitRadius` 합산 + EditMode 1. 보스만 크게(≈1.2), 일반 적은 0으로 바이트 무회귀. **임시 완화**: `Projectile_MachineGunBullet.hitThreshold` 0.4→0.7(전 적 대상 균일 확대라 3.2배 보스는 여전히 부분 관통 — feature 착수 시 0.4 복귀 검토). projectile-trajectory-payload 엔진 확장. (nightmare-catcher 보스 피격 체감 관련)

#### 적 스폰/이동 비주얼 (enemy-spawn-positioning)

스폰 위치 개선(완료 2026-06-29, units 0~4) 후 남은 항목.

- **적 타일 이동 무결성** → `docs/spec/enemy-tile-movement-integrity/` (완료 2026-06-29 — `movement-lane-centering` 리프레임). 결함 3종: 코너 엣지-허깅 복원(target=0+deadband) · aggro 타일 제약 · 결정론 스폰. 레인 대형 시스템(II) · QuadUnit 뷰 누수는 후속 후보.
- **Quad 폴백 visualOffset 배선** [S] · Spine 없는 적의 `QuadUnitView` 경로에 `AttackUnitData.visualOffset` 전달. 현재 미배선(적=Spine 라 무영향).
- **유닛 간 separation/boid** [M] · 겹침 동적 해소(스폰 분산과 별개로 행진 중 밀집 완화).
- **블록 시 우회 재라우팅** [M] · 복도 차단 시 `BuildFlowField` rebuild 트리거(walk 마스크에 blockedCells 반영). flow field 유지 결론(유닛별 BFS 아님). 이동 아키텍처 별도 스펙.

#### 점수 HUD 타격감 (score-hud-impact-upgrade)

점수 HUD 임팩트 업그레이드 **완료(2026-07-07, units 0~4)** — 탄성 슬램/골드 아이덴티티/Kanit 폰트·골드 스파클 버스트·발광+샤인·패널 킥/마일스톤 플래시(Play 통과) + SoundManager 처치 틱(ElevenLabs `ScoreTick`, 피치 상승). 상세: `docs/spec/score-hud-impact-upgrade/`.

- **연속처치 heat · 킬 위치 "+N" 플로팅 · 진짜 URP Bloom · SFX 다양화** [S~M] · 상세는 spec README "후속 후보".
- ~~적별 차등 점수~~ → **완료** (`score-tally-sequence` unit 0 — HUD 가 유닛별 `killScore` 를 쓴다).
  ~~콤보 배수 스코어링~~ → **기각** — `battle-score-formula` 계약 10 이 콤보 배율을 금지한다.
  마일스톤 플래시는 누계 기준 → **1초 내 300점 순간 화력** 기준으로 교체됨.

#### 체력 표기 (unit-health-display)

적/방어유닛 체력 표기(완료 2026-07-04, units 0~3 — 적 피격 마이크로바 + 저체력 틴트, 방어유닛 타일 테두리 게이지, 투트랙 리뷰 반영). 상세 후속: `docs/spec/unit-health-display/README.md`.

- **킬 포어캐스트 마크** [M] · `IncomingDamage` + 비행 투사체 예약 데미지 ≥ 잔여 HP 인 적에 스컬 마크. 바가 못 주는 의사결정 정보. 투사체 데미지 귀속 필요.
- **체력 표기 poll 효율화** [S] · `SyncMonoUnitViews` 가 매 프레임 적/방어유닛 `Health` 조회 + 뷰 write(틴트/게이지). entity/cell→last-ratio 캐시로 skip. 비블로커(유닛 수 그리드 상한).
- **타일 게이지 시각 폴리시** [S] · fill inset `pad=0.18` SO화, 코너 조인트 갭 보정, 4-edge 계단식 → 연속 SDF 셰이더 교체.
- **hazard 체력 표시 / 상태이상 틴트 합성 / 웨이브 압력 게이지 / 보스 상시 바** — unit-health-display README 후속 후보.

#### 배치 프리뷰 / 범위 (placement-attack-range-preview, placement-drag-preview-polish, keyring-cord-preview)

드래그 배치 UX(공격범위 격자 표시 + 프리뷰 sway → **키링화** 완료 2026-07-05) 후 남은 항목.

- **배치 스킬 범위 표시** [M] · `onPlaceRange`/`hazardCastRange` 를 다른 색 채널로. 웜(공격)/쿨(스킬) 색코드 + 채널별 펄스 위상차, 필요 시 border 타일. 2번째 색 채널 시점에 `EnsureRangeTilemap`/펄스 로직 파라미터 추출. (range-preview)
- **Guardian 어그로 반경 시각화** [S] · `aggroRange` 를 또 다른 표기로(공격 범위와 별개 성격). (range-preview)
- **이미 배치된 유닛 선택/탭 시 범위 표시** [S] · 현재는 드래그 중만. (range-preview)
- **키링 중력 드롭 방식** [S] · 움직일 땐 유닛이 손가락에 붙고, 멈추면 중력으로 툭 떨어져 매달리는 물리감(사용자 제안, 현 스프링 follow 의 대안). (keyring)
- **키링 고리/줄 실제 아트** [S] · 현재 절차적 원 링 + 단색 LineRenderer. 금속 링/체인 스프라이트로 스왑. (keyring)
- **줄 sag 곡선** [S] · 현재 2점 직선. 정적 catenary 곡선으로 끈 느낌. (keyring)
- **배치 유닛 idle sway** [S] · 현재 sway 는 드래그 프리뷰 전용. 배치된 유닛의 상시 미세 흔들림. (drag-preview)
- **드롭 bounce / 세로·전후 흔들림** [S] · 드롭 착지 반동·다축 진자. (drag-preview)
- **fallback capsule 프리뷰 sway** [S] · Spine 없는 유닛 경로(현재 스킵, 키링 미적용). (keyring/drag-preview)

#### Modifier framework — Producer 확장 (modifier-framework-and-healer)

framework 코어 변경 0. 새 producer 레이어 추가로 다양한 효과 적용 경로 확보. producer-agnostic 설계 검증 시점.

- **Aura defender** [M] · 지속 영역 효과 producer (`AuraOutput[]` + `AuraApplySystem`). 일정 반경 ally 에 매 프레임/N초마다 StatModifier 발화.

#### Modifier framework — 내부 보강 (modifier-framework-and-healer)

framework 코어/UX/테스트 보강. 콘텐츠 확장 전후 모두 가치 있음.

- **Modifier UI 시각화** [M] · defender HUD + 적 머리 위 활성 modifier 아이콘 표시. ModifierStats / Slot buffer read-only 구독. UI 리소스 의존.
- **Dispel/Cleanse 채널** [S] · ModifierBuffer 슬롯 제거 채널 (kind/source 기반). CombineOp 별 면역 정책. 콘텐츠 디자인 선행.
- **Testability — Stack threshold dispatch** [S] · `BattleBridge._stackThresholds` 에 test 주입 API 또는 `IStackThresholdRegistry` 인터페이스 도입. skipped Test 3 활성화 목적.
- **Testability — AttackSystem outputs dispatch helper 추출** [S] · `OnUpdate` 의 4-way 분기를 `static ProcessAttackOutputs(...)` 로 추출. skipped Test 4 활성화 목적.
- **추가 EditMode 회귀 테스트** [S] · Stack threshold edge (5→6→5 재발화) / Consume 모드 stack 차감 / IncomingHeal drain Clear / RegenPerSec 누적. Testability 보강과 합쳐 진행.

#### Enemy 콘텐츠 / 비주얼 (enemy-unit-development)

신규 적 3종 + Tanker Spine 전환 후 남은 검증/콘텐츠 작업.

- **PlayMode 밸런스/시각 검증** [S] · Rootcaster 공격 후 1초 pause, Needler 빠른 투사체 연사, Runner 과속 체감, Tanker BellKnight Spine 크기/정렬 — 실기 확인 후 SO 값 튜닝.
- **적 projectile VFX 분리** [S] · 현재 defender projectile prefab + tint/scale 재활용. enemy variant prefab 또는 material variant 분리. 적/방어 투사체 식별성 개선.
- **WavePatternGenerator unit weight 지원** [S] · 현재 균등 확률. `AttackDeck.attackUnitPool` 반복 참조 또는 weight 필드 도입. Runner/Needler 과다 출현 회피.
- **Enemy attack animation event 일반화** [S] · `DefenderAttackEvent` → `UnitAttackVisualEvent` 로 일반화. `SpineUnitPool.NotifyAttack(entity, target)` 으로 적 공격 애니메이션 트리거 연결.

#### Hazard caster — 확장 (hazard-caster-defenders)

hazard caster defender 4종 MVP 이후 남은 확장 후보.

- **footprint sampler** [S] · `SampleRect(center, width, height)` 구현. HazardCastSystem 의 width/height 고정을 제거하고 rect 범위 multi-cell spawn 지원. 콘텐츠 디자인 선행.
- **target priority 정책** [S] · 현재 nearest(world distancesq). first-path-progress / random policy 추가. `HazardCastState.targetPriority` 필드 도입.
- **cast warning VFX / tile preview** [S] · cooldown 직전 타겟 셀 하이라이트 또는 파티클. BattleBridge drain 에서 visual hint 생성.
- **same-frame hazard 효과** [M] · 현재 next Simulation tick 적용. ECS 내부 drain 으로 이동 시 같은 frame 적용 가능. `HazardLifetimeSystem` 순서 재편 필요.
- **DefenderCatalogSO** [S] · 씬 레벨 draft catalog 수동 배선 대신 공유 `DefenderCatalogSO`로 통합. roster 증가 시 씬 배선 brittle 해질 때.

#### CC / Obstacle 확장 (cc-pipeline-and-obstacle)

- **큐브 spawn 게임 통합** [S] · 디펜더 능력 / 스킬 카드에서 `BattleBridge.SpawnObstacle` 호출. 현재는 디버그 메뉴만 진입점.
- **Obstacle 시각 Presenter** [S] · `ObstaclePresenter` MonoBehaviour, mesh/particle. 현재 큐브는 시뮬만 있고 렌더 없음.
- **추가 CcKind** [S] · Stun/Root/Reverse/Pull/Push 등 enum + `MovementSystem` switch case 추가. 콘텐츠 디자인 선행.
- **멀티셀 큐브 / 적-적 분산** [S] · 현재 단일 셀, 단일 큐브.
- **CC merge helper 추출** [S] · 3번째 CC caller 등장 시 `EffectSpawner.ApplyCc` 와 `CcApplySystem.MergeOrAdd` 듀얼 구현 통합 (I1).
- **ObstacleLifetimeSystem Burst 분리** [S] · 큐브 16+ 시점 `OnUpdate` Burst 분리 + `blockedCells` incremental (I4).

#### 렌더 파이프라인 / 시각 (board-visualization, wrapped)

board-visualization spec 자체는 ROI 부족으로 wrap 종료. 진단/실험은 `docs/spec/board-visualization/29_final_handoff.md` 참조.

- ~~**palette-and-overlay-fix**~~ [무효화 2026-07-03] · 대상(Legacy 렌더 텍스처/오버레이/tint 경로)이 legacy-render-removal 로 통삭제됨.
- **BattleScene MapView 잔재 씬 청소** [S] · 구 MapView GameObject(missing-script) + `BattleBridge.mapView` stale serialized 참조 제거. 씬 dirty WIP 정리 후 SaveScene 격리 절차로. (legacy-render-removal handoff)
- **17r prop-distribution-retry** [S] · V-001 잔존. Poisson 정공법 재구현.
- **23 volcano-theme-fill** [M] · 두 번째 테마 자산 채움.
- **BattleBridge.StartBattle Persistent allocates 경고** [S] · 반복 시작 시 leak 추적. ECS 컨텍스트 정리 경로 점검.

#### Seasonal — 후속 (seasonal-map-backdrop)

> 백드롭 서브시스템(BackdropMounter/SeasonBackdropData)은 Legacy3D 전용이라 legacy-render-removal unit 2 에서 통삭제(사용자 결정 2026-07-03). backdrop 의존 항목 3개(tint/exposure 튜닝·미세 시차·라이팅 매칭) 무효화로 제거. 시즌 시스템(SeasonRuntime/mapTheme)은 ACTIVE.

- **시즌별 차별화된 MapThemeData** [L] · 현재 4시즌 모두 forest 테마 공유. Lava/Lunar/Cosmic 전용 타일/장애물 정의. 별도 spec.
- **토너먼트 메타 hook** [M] · 서버 응답 → `SeasonRuntime` active season swap API.
- **시즌 배지 UI** [S] · 매치 시작 시 활성 시즌 배지 노출.

#### 배경 프랍 영역 풀 (prop-area-pools)

근경/원경 풀 분리(완료 2026-07-02, units 0~3) 후 남은 확장 후보.

- **영역별 밀도/falloff 리스트 이관** [S] · 현재 `tilePropDensity`/`ringPropDensity` 는 테마 전역. WeightedProp 리스트 단위 또는 영역별 파라미터로 세분화.
- **원경 카테고리 회피** [S] · `sameCategoryMinDistanceCells` 를 원경 링에도 적용(현재 근경 전용). 원경 나무 군집 자연화.

#### 프랍 접지/프레임 (prop-upright-root 파생)

- ~~**desert 테마 접지 fix**~~ [완료 2026-07-03] · desert prop_style_*/prop_dummy_* + 공유 forest dummy PropData 를 Tilted + offset 0 + 텍스처 BottomCenter 로 정합. 실제 렌더 sink 는 dummy 2종뿐(prop_style_* 는 공유 forest 프리팹의 baked data 로 이미 정상)이었고 나머지는 데이터 hygiene. Play 검증(`desert_dummy_grounding_verify.png`).
- **ObstaclePlacer 테스트 기존 실패** [S] · `ObstaclePlacerTests.Place_PreservesWalkAndMinimumPlaceRatio`(≥36 기대, 31). dea2733(phase10) 테스트, 맵 생성 결정론 실패. prop-upright-root 작업과 무관하게 HEAD 에서 이미 실패 — 회귀 아님. minPlaceableRatio/ObstaclePlacer 로직 별도 조사.

#### 모바일 디스플레이·Battle HUD 대응

모바일 aspect/framerate 수정(`GameManager.Awake` — 세로 1080 캡 + 기기 aspect 로 가로만 확장, `targetFrameRate=60` + `vSyncCount=0`) 후 남은 UI 후속.

- **UI CanvasScaler Height + Safe Area 통일** → `docs/spec/mobile-ui-safe-area/` [M, 설계 완료·승인 대기]. Battle/Outgame 전체를 full-bleed/safe root로 분리하고 16:9~20:9 + Android cutout/gesture를 검증한다.
- **Battle HUD Safe Action Tray** → `docs/spec/battle-hud-action-tray/` [L, 선행 spec 대기]. 비용·role·affordability 슬롯 정보, compact energy rail, tray↔hand 시각 정합, 배치 거부 원인 피드백.
- **남은 허용 유출 HUD** [S/M] · `defeatGoalReachedCount` 대비 현재 유출/잔여 허용치를 전투 중 상시 표시해 패배 원인 예측성을 높인다. Action Tray와 다른 상단 생존 정보 scope로 별도 승격.

#### PlayMode 스모크 위생 (subconscious-curse-expansion unit 4 실측, 2026-07-16)

전체 PlayMode 34 중 4건이 main 에서 이미 실패 — 내 커밋 이전(`ea155e65`) detached 재실행으로 재현 확정. 신규 회귀 아님, 테스트가 낡음:

- **DreamcatcherDeckCarryInTest** [S] · 8장 시절 mismatch 는 해소(라이브 `deckSize=10` 복귀 → 테스트의 10장 저장 기대와 일치). 잔여 과제는 제거된 기본(fallback) 덱 10장 기대(2026-07-15 사용자 결정으로 폐기됨) → **무폴백 계약**으로 재작성. (2026-07-20 덱=10 확정 정정)
- **SquadCarryInSmokeTest · DreamstoneCarryInSmokeTest** [S] · `RequestPlacement()` 직후 `Placement` 기대 — gift-phase(2026-07-13)가 `Gift` 를 삽입한 뒤 stale. Gift 통과(또는 우회 경로) 반영 필요. Dreamstone 쪽은 PrimeTween OnComplete 에러 표출도 동반.
- **MovementIntegritySmokeTest** [M] · "guardian aggro ≥1" 실패 — 원인 미조사(오늘 pull 의 gimmick 랜덤 배정 주입이 용의선상). 별도 조사.

#### 기타

- **Healer 전용 Spine asset** [S] · 현재 Archer Spine reuse. 전용 rig + idle/heal-cast/death 애니메이션. 시각 식별성, 기능 영향 없음. (modifier-framework-and-healer)
- **Spec 5~10 backfill** [S] · hybrid 진행 시 누락된 단위 spec 파일 작성. commit/handoff 가 임시 대체 중. 필수는 아님. (modifier-framework-and-healer)
- **VFX magic number 정리** [S] · `VfxSpawner` 의 y-offset / lifetime 하드코딩 → SerializedField 또는 ParticleSystem main.duration + startLifetime 으로 동기화. heal/placement/meteor 일괄 대상. (heal-vfx)
- **Heal VFX amount scaling** [S] · `HealAppliedEvent.amount` 를 `VfxSpawner.SpawnHealApplied` 에서 ParticleSystem main.startSize/startColor 에 매핑. 큰 힐 = 큰 펄스. 시그니처는 이미 amount 파라미터 확보됨. (heal-vfx)
- **GA 투사체 최종화** [S] · 디펜더별 최종 변종 선택(50종 중) + 스케일/높이 취향 미세조정 + 안 쓰는 변종 SO/프리팹 정리. (projectile-ga-reskin)
- **GA 투사체 모바일 최적화** [M] · 라이트/트레일 감축 · soft particle 토글 · 실기기 프로파일. tint 데이터-드리븐 recolor 는 별도(preserveVfxColors 우회 필요). (projectile-ga-reskin)

#### 스폰 예고 라인 — 후속 (spawn-point-alert, 2026-07-20 종료 이관 · 사용자 Play 확인 완료)

- **예고선 실기기 성능** [S] · lane 당 LineRenderer 3 + SpriteRenderer 1(3레인 = 12개), 매 프레임 폴리라인 재구축. Android 미측정. 부담되면 코너 정점만 유지하는 현 구조에서 갱신 주기를 낮추는 선택지. (spawn-point-alert)
- **보스 웨이브 예고 차별화** [S] · 보스 웨이브 lane 만 색/굵기를 달리해 구분. 현재는 크림슨 워닝 배너(boss-wave-cadence)가 별도로 존재해 중복 여부 판단 필요. (spawn-point-alert)
- ~~**Wave 1 사전 예고** [S]~~ · **2026-07-26 해소** — `wave-pattern/11`(스폰 리드인 2초) + `spawn-point-alert/3`(예보를 큐잉 웨이브 기준으로)로 Wave 1·강제 웨이브 모두 예고를 받는다. 남은 후보는 "배치 페이즈(배틀 시작 전)에 미리 노출"뿐 — 시작 시점 예지 필요. (spawn-point-alert)
- **예고 SFX** [S] · 라인이 그어질 때 저음 경고음. ElevenLabs 파이프라인 활용. (spawn-point-alert)

#### 파이프라인 커버리지 — 후속 (object-pipeline-map)

- **spec 파일 트리거 훅** [S] · `docs/spec/**/README.md` Write/Edit 시 파이프라인 커버리지 섹션 리마인더 주입(PostToolUse 훅). 템플릿 규칙 정착 후 잔여 누락 케이스 확인되면.
- **리뷰 게이트** [S] · two-track-review/critic 체크리스트에 "파이프라인 정거장 누락" 항목 추가.

#### 워크플로우 재현성 — 후속 (workflow-reproducibility)

- **문서 수명주기 정리** [S] · [`docs/production-transition/`](../production-transition/README.md) 로 승격(2026-07-29) — PRD/TRD·milestone 에 역사 보존 배너를 두고, 기준선·출처 지도·supersession을 별도 관리한다.
- **ADR 후보 로그** [M] · [`adr-candidates.md`](../production-transition/architecture/adr-candidates.md) 로 승격(2026-07-29) — 현재는 `ADR-CAND-###` 질문만 관리하며, 승인된 결정이 생길 때만 별도 공식 ADR과 `docs/decisions/`를 만든다.
- **deepinit ↔ AGENTS symlink 충돌 정책** [S] · deepinit 재실행 시 AGENTS.md 를 실제 파일로 재생성해 symlink 이 풀림 — 재적용 자동화 또는 deepinit 출력 위치 변경.
- **첫 실전 클론 체크리스트 완주 확인** [S] · 새 머신/팀원 첫 클론에서 루트 README 부트스트랩 체크리스트(훅 승인·Unity 첫 Play) 실전 검증.
- **thick 하네스 표준화** [S] · OMC/superpowers 를 `enabledPlugins`+`extraKnownMarketplaces` 로 커밋해 팀 동일 오케스트레이션(사용자 결정 시).

#### Outgame / squad / dreamcatcher — 후속 (outgame-scene-and-flow, squad-loadout, ingame-dreamcatcher)

- **드림캐쳐 카드 보유/콘텐츠 확장** [L] · ownedCardIds + 가챠/꿈런 파밍, 카드 콘텐츠 확장(기획 일반10+고유3+무의식2, 신규 메커닉 채널), 무의식 편입. (D 후속) — **다중 덱 수집/전환·이름 편집은 승격** → `docs/spec/page-local-presets/`
- **드림캐쳐 복합 효과** [L] · row-only/crit/pierce/splash/lowcost-summon/guardian-taunt/match-start-cost + 무의식 2장. 신규 메커닉/채널 필요. 트리거형 메커닉(개별유닛 바인딩 + N회 공격 발동) 토대는 → `docs/spec/dreamcatcher-unit-trigger/` 로 부분 승격 (2026-07-08).
- **진짜 MaxHealthMul 채널** [M] · 현재 HP 카드는 DmgTakenMul 프록시. 정확한 max-HP 증가 채널(Health/Units 맥락).
- **스쿼드 class/특성** [L] · class 라벨(완료, C unit0)을 이용한 슬롯 조건 + 타입별 특성(스탯 합산, 하드캡 15%). 가챠/꿈런 파밍/교환/리롤/등급. — **다중 스쿼드 수집/전환은 승격** → `docs/spec/page-local-presets/`
- **한글 TMP 폰트** [S] · 현재 LiberationSans only → UI 라벨 영문. 로컬라이즈 패스에서 한글 폰트 에셋 도입.
- **반복 씬 로드 ECS leak 점검** [M] · 2-씬 전환으로 BattleScene 반복 로드 → 기존 **BattleBridge.StartBattle Persistent allocates 경고** 백로그가 더 중요. 재진입 시 ECS World/Persistent 정리 경로 검증.

### Promoted / Closed

- **무의식 저주 유물 2종** → `docs/spec/subconscious-cursed-relics/` (완료 2026-07-15 — 플레이스홀더 `깊은 잠`/`꿈결 가속`을 재앙의 심장(시한부 공속·3타 강공·사망 폭발)과 금이 간 성배(전군 화력↑/생존↓)로 교체. 기존 mechanic/effect 조합만 사용하고 신규 메커니즘·인터페이스·ECS 채널은 추가하지 않음. 기존 `LethalTimer` 중복 부착 사전검증과 회귀 테스트 보강. 신규 2장 및 문자 placeholder 3장 카드 아트 완성.)
- **Dreamcatcher empower aura** → `docs/spec/dreamcatcher-empower-aura/` (완료 2026-07-15, units 0~3, `34c99cf3`~`33d3b90f` — 구 슬러그 `unit-buff-debuff-aura` 에서 개념 전환. **ModifierOrigin 출처 1급 태깅 프레임워크**(생산자 19곳, `ModifierHeader.origin`, 머지 키 아닌 메타데이터 — 같은 크기 버프도 슬롯 단위로 출처 구분; dispel/UI 재사용 토대) 위에서 **드림캐쳐 출처 스탯 모디파이어가 활성인 유닛에만** 온-바디 파워업 오라(`StatusFxKind.Empowered`, 순수 `ModifierAuraClassifier`, 상태 구동 reconcile·defender 한정, revoke=identity 자동 해제). 드림스톤/시너지/on-place/slow 배제. **버그 2건 수정**: `ApplyActiveDcEffectsTo` 드림스톤 origin 오태깅(`ActiveDcEffect.origin`) · revoke 감소형(Multiplicative) 미중립화(op-aware `EnqueueStatModifierRaw`, PlayMode 회귀). 미의도 `fallbackDeck` 제거. 오라=5요소(쉘·창끝·백라이트·충격파링·스파크) 파랑/주황 이중톤 + 전용 텍스처 2종, `EmpowerAura.prefab` 정식화. EditMode 780/PlayMode 2, 투트랙 리뷰 반영. 후속: 전용 셰이더 글로우·강도별 단계 오라·출처 태그 재사용(dispel/modifier UI))
- **Gift phase presentation — "카드 딜러의 선물"** → `docs/spec/gift-phase-presentation/` (완료 2026-07-14, units 0~3 + 코드리뷰 rev1, `cb3d99d9`~`f8b6d9a3` — gift-phase 연출 전면 재작성을 5 서사 비트로: 내가 짠 덱(하단중앙 딜-인→5×2 그리드) → 존재의 개입(Lucid 금빛 강림/Rim 적색 침투, 뒷면 플립 리빌 2.1x+1s 읽기 홀드, 내 덱 움찔) → 융합(스택 수렴 출렁+리플 지퍼+잔상 트레일+글로우 리플) → 제시(부채꼴 좌→우=소비 1→12, 스윕) → 각성치 장전(수신 앵커 링 n세그먼트+가속 케이던스+12번째 피니셔 찰칵). 프레임 3종=UiRoundedSprite 링+DraftCardFoil 시머 2층(셰이더 신작 0, **포일은 _MainTex 미샘플 오버레이라 단층 불가**). 좌표수학=`GiftPhaseLayout` 순수+EditMode 12. 총시간 판정=**PhaseChanged 실측**(명목치 합산은 7.0s 적발 이력) 5.95s+홀드 1s. 리뷰 10건 반영(피니셔 조기증발·판당 28텍스처 누수·NRE 소프트락·PrimeTween 풀 400 선예약 등). 덱순서 계약·ECS·씬 배선 불변. 후속: 실아트 교체·fly 타깃 정렬·게이지 연동·SFX)
- **각성 손패 카드감(Dreamcatcher hand deal-in)** → `docs/spec/dreamcatcher-hand-deal-in/` (완료 2026-07-13, units 0~4, `3f574a9c`·`f34ce20e` — 각성 손패를 StS/HS 카드감으로 재설계. 평면 UI 행 → 포물선 아치 부채(가운데 솟음)+겹침+접선회전, 슬롯 `target`+매프레임 **스프링 모델**(focus/idle/드래그/딜이 한 모델 공유). **모바일: hover 없음 → press-to-lift**(`OnPointerDown/Up`=focus, raise/확대/펴짐/최상단/이웃 scatter). 딜 소스=**하단 덱**(각성 버튼 아님, 초기 "버튼 정확좌표 딜"은 UI스럽다는 사용자 판정으로 폐기)→곡선 상승 OutBack 안착+원근 틸트+squash flex+버튼 pulse. 무입력 **idle bob/sway**(index 위상차). 퇴장=하단 덱 침강. critic 리뷰 반영(focus 가드·`OwnedByInteraction` 헬퍼). 모든 연출 realtime(TimeManager 도메인, `timeScale`=1 불변). ECS 변경 0·채널 불변. 트위닝 육안검증=사용자 포커스 Play. **후속**: 진짜 버텍스 커브(②-A)+꼬깃꼬깃(③)은 서브디바이드 메시/셰이더 토대 공유라 별도 spec)
- **선물 페이즈(Gift Phase)** → `docs/spec/gift-phase/` (구현 완료 2026-07-13, units 0~5 + 코드리뷰, `29d9ffd7`~`4d4eee69` — 배치 직전 `GamePhase.Gift` 삽입. "루시드의 선물"(스킬 Active 2) / "림의 선물"(무의식 2) 랜덤 이벤트 → 저장10+선물2=12장 발라트로식 셔플 연출 → 각성버튼 fly-out → 배치. **CycleDeck 무변경**(Gift 에서 덱 1회 생성·`Hand(전체)` 연출·배치 재사용, 이중셔플 없음). Lucid=기존 SkillLoadout 재사용, Rim=Subconscious 시드추출+폴백. 무의식 카드 2장 저작(기존 effects 채널)·덱빌더 제외. HUD 노출을 `PhaseChanged(Placement)` 로 재게이팅. 순수 프레젠테이션+Mono, ECS 변경 0. **연출 시각 상세조정은 후속 스펙**. 트위닝 육안검증=사용자 포커스 Play)
- **유닛 드림캐쳐 아이콘** → `docs/spec/unit-dreamcatcher-icons/` (완료 2026-07-12, units 0~2 — 배치 유닛 머리 위 부착 카드 미니 타로 스트립. `card.art` 재사용(신규 에셋 0), HandController registry + `AttachmentsChanged` 이벤트 구동, Squad 골드/Unit 청록 프레임, 사망 회수→소멸. 순수 프레젠테이션, ECS 변경 0. 트리거 진행도 뱃지·부착 연출은 후속)
- **보스 방어유닛 지향 이동** → `docs/spec/boss-defender-field/` (완료 2026-07-11, units 0~3, `dc298ceb` — 방어유닛 walkable 이웃 multi-source BFS "defender field"(Effects 싱글톤+매 프레임 재빌드) 를 보스(`BossTag`)가 Marching 에서 flow-follow. 지나친/뒤 배치 방어유닛에 역주행 재교전, 전멸까지 사냥(leak-proof), 0마리면 goal 마칭(무상태 fallback). FSM/채널 변경 0, 비-보스 무회귀 라이브 확인. 폐기된 enemy-hunter-targeting 의 직선추격/wall-slide 는 재도입 금지 계약)

- **Enemy walk anim speed match** → `docs/spec/enemy-walk-anim-speed/` (완료 2026-07-10, units 0~2 — 적 Spine 걷기 애니를 실제 view 변위 기반 재생속도로 변조해 발 미끄러짐(문워크) 제거. `skeleton.timeScale = battleScale × walkFactor`(sim-time 정규화 속도/refSpeed, min/max/스무딩/텔레포트가드 = `WalkAnimSpeedStyle` SO + BattleBridge 미러). 포탈 텔레포트 무시·standoff 바닥. **회귀 수정**: timeScale 트랙 전역이라 걷기 배율이 공격/사망/배치까지 늦추던 것 → 로코모션 루프(Loop==true)에만 적용. 튜닝 확정 refSpeed 1.2/max 3.0. 순수 프레젠테이션, ECS 변경 0. 후속: 코너 접지 스냅·Android 프로파일)
- **Attack anim speed match** → `docs/spec/attack-anim-speed-match/` (완료 2026-07-10, units 0~1 — 공격 Spine 애니를 실제 발사 주기에 compress-to-fit → 공속이 "빠른 스윙"으로 체감. `TrackEntry.TimeScale = max(1, animDuration / max(cooldownDuration/attackSpeedMul, hitDelaySec))`. **별도 튜닝 SO 없이 공격속도 필드(SO attackCooldown+버프+hitDelay)에서 직접 파생**(SoT 불변, 사용자 결정). 하한 1.0=구조 상수(느린 공격 자연+대기), 상한 없음(attackSpeedMul [0.2,5] 캡+authoring 규율). 산식 critic 1회 준수 판정+MEDIUM/LOW 반영. 시뮬 rate/데미지 불변. 후속: hit 프레임 정렬)
- **Result screen visual upgrade** → `docs/spec/result-screen-visual-upgrade/` (완료 2026-07-08, units 0~3 — 결과 팝업 리더보드를 인게임 HUD 언어(네이비/골드 홀로그램)로 리스킨: `UiRoundedSprite` 공용 절차 스프라이트 + 행별 플레이트·순위 배지(금/은/동)·본인 골드 강조·WAITING 회색 + **RESTART 하단 고정 3영역 앵커 레이아웃**(단일 VerticalLayoutGroup 겹침 결함 제거). 순수 `BuildRows` + EditMode 6. tournament-play-report 배선 불변, 순수 프레젠테이션. 배경은 시즌 아트 시도 → 인게임에서 풀스크린이 보드 덮어 폐기, `UiOverlay.Dim` 유지. 직렬화 필드 0(씬 diff 0). 후속: 등장 애니메이션·ScrollRect·한글 폰트)
- **Damage number visual upgrade** → `docs/spec/damage-number-visual-upgrade/` (완료 2026-07-07, units 0~3 + Play 튜닝 다회 — 순수 프레젠테이션(ECS 변경 0). 머리위 앵커(sim-Y drop 회피)·카메라축 겹침방지 격자·청록→골드→오렌지 팔레트·정점 그라데이션·TimeManager 델타 교정·index 결정론 셰이크/회전 + 하프톤/글로우/흰아웃라인/드롭섀도 머티리얼(비-모바일 Distance Field 변종) + 클러스터 스파크. 스파크는 별→**GA Circle18 라운드 도트 버스트 + 폰트색 틴트 emissive + 임팩트 플래시** 로 재작업(GA 텍스처만 재활용, 단일 경량 PS). 2트랙 critic BLOCKER 2건 반영. 후속: unit 2 Android 실기 프로파일 게이트·유닛별 정밀 앵커·진짜 emissive(URP Bloom))

- **Placement enemy see-through** → `docs/spec/placement-enemy-see-through/` (completed 2026-07-06, units 0~6, `9941f27` — 드래그 배치 중 적 유닛(Spine·Quad 혼합)을 반투명화해 가려진 뒤 보드 타일 노출. cutout↔transparent 런타임 전환(Quad)·PMA skeleton.A(Spine)·그림자 페이드·health tint 합성·매프레임 재적용. 프리뷰 불투명/최상단(unit 5) + 배치 하이라이트 적 위로(unit 6). 순수 프레젠테이션, ECS 변경 0·채널 14개 불변. two-track APPROVE(0~4)+M1 반영. 스텐실/후처리 리빌·블로킹 하자드 반투명은 후속)

- **Portal VFX upgrade** → `docs/spec/portal-vfx-upgrade/` (completed 2026-07-06, unit 0 — 물빔(WaterBeam 어거지) 제거 + 스월 지속화(loop+사이클 오버라이드, LocationVfx 가 duration 무시하는 원인 해소). 룬 게이트 실험은 사용자 반려·롤백. 입구/출구 시각 구분은 후속 후보)
- **Object pipeline map** → `docs/spec/object-pipeline-map/` (completed 2026-07-06, unit 0, 커밋 `aeccbc3a` — 플레이 오브젝트 생성→렌더 정거장 체크표 `docs/reference/object-pipeline-map.md`(아키타입 10종, `.cs` 앵커 57건 실측) + CLAUDE.md 파이프라인 커버리지 필수 섹션 규칙(N/A+이유 강제). artillery-defender 사후 대조로 카탈로그 등록 확인 포인트 승격. 훅/리뷰 게이트는 후속)

- **Workflow reproducibility** → `docs/spec/workflow-reproducibility/` (completed 2026-07-06, units 0~3 — fresh clone 워크플로우 재현: `.claude` 표준 추적+settings 분할(훅·read-only 권한 커밋) + auto-memory 27건 → `docs/reference/lessons/` 승격 + AGENTS=CLAUDE symlink + 루트 README 부트스트랩. critic APPROVE-WITH-CHANGES 반영, fresh clone 실측 검증. MCP/LFS 는 범위 밖)

- **Artillery defender** → `docs/spec/artillery-defender/` (completed 2026-07-06 — 곡사포 유닛: `Projectile_ArtilleryShell`(Rock ballistic) + `Defender_Artillery`(range7/cd3.5/dmg60, Cannon Spine 재사용) + DefenderCatalog 등록. projectile-trajectory-payload 엔진의 첫 Play 실증. 신규유닛 프로필 reconcile 은 후속)

- **Projectile trajectory × payload** → `docs/spec/projectile-trajectory-payload/` (엔진 완료 2026-07-06, units 0~5 — 투사체를 궤적(Homing/BallisticArc)×페이로드(SingleSplash/TileAoe) 직교 2축으로 분해. 홈잉 무회귀 이관 + BallisticArc 궤적 + TileAoe 반경 AOE + 곡사 발사 배선. 커밋 `e5836bc`~`27a452a`, 양트랙 리뷰 3게이트, EditMode 498/499. Play e2e 는 artillery-defender 로 이관. 신규 시스템/큐/맥락 0)

- **Placement keyring cord preview** → `docs/spec/keyring-cord-preview/` (completed 2026-07-05, squash 머지 `d197bc7` — 드래그 프리뷰 키링화: 고리=손가락(공중)·유닛=보드 스프링 follow(무게추 흔들림)·**하이라이트는 마우스 고정**(스윙 유닛 아님). 이전 drag-preview sway 완전 교체(SO 스키마도). camUp 수직분리·워밍업 금지 등 되돌리면 안 되는 설계는 handoff 참조. 탐색 이력 16커밋은 `feature/keyring-cord` 브랜치. 중력 드롭·아트 스왑은 후속)
- **Placement attack-range preview** → `docs/spec/placement-attack-range-preview/` (completed 2026-07-04, units 0~2 — 드래그 배치 중 공격범위를 노란 격자 outline 로 동기 펄스 표시. `Tilemap.color` tint + 전용 `_rangeTilemap`(sorting -12) + Chebyshev `RangeToTiles`. e2e 드래그 추종 Play 검증)
- **Placement drag-preview polish** → `docs/spec/placement-drag-preview-polish/` (completed 2026-07-04, units 0~1 + rev — 프리뷰 빌보드 각도 정합 + 매달린 키링 velocity-lean sway(SO 튜닝) + 프랍 위 정렬. Play(MCP) 검증)
- **Dreamstone loadout** → `docs/spec/dreamstone-loadout/` (completed 2026-07-06, units 0~7 — 스쿼드 4슬롯 장착 + set-then-apply 반입 + 개별 아이템 64종(순차 id·캐파 내 티어 스탯) + 코스트 생산속도 매치 배선 + 아이콘 스크롤 피커. 리뷰 4단 + 실측 검증. 획득/인벤토리는 후속)

- **Legacy render removal** → `docs/spec/legacy-render-removal/` (completed 2026-07-03, units 0~4 — Legacy MapView 렌더/Legacy3D 모드/시즌 백드롭/테마 LEGACY 43필드 완전 삭제, ~6,300줄 순삭. Tilemap 경로 무회귀. 씬 MapView 잔재 청소는 follow-up)

- **Projectile GA reskin** → `docs/spec/projectile-ga-reskin/` (completed 2026-07-03, units 0~6 — GabrielAguiar UniqueProjectiles Vol4 50종 라이브러리 + 스트립/스왑 툴 + ViewPool as-is 가드(streak/preserveVfxColors) + 높이오프셋 + ProjectileOffset sorting + muzzle-hit. 실게임 검증 PASS. 최종 변종선택/스케일/미사용정리는 사용자 취향 후속)

- **Prop upright root** → `docs/spec/prop-upright-root/` (completed 2026-07-03, units 0~1 — 프랍을 90° 타일맵 루트에서 떼어 upright 저작 프레임(+Y=위). 루트 flip + 블롭 마이그레이션 + EditMode 테스트. desert 접지는 follow-up)

- **Prop area pools** → `docs/spec/prop-area-pools/` (completed 2026-07-02, units 0~3 — 근경 playAreaProps / 원경 distantRingProps 독립 WeightedProp[] 풀 분리 + 인스펙터 영역별 weight. tileProps/placementWeight 등 retire)

- **Dreamcatcher deck builder** → `docs/spec/dreamcatcher-deck-builder/` (completed 2026-06-03, 10장 빌더+저장+인게임 반입 MVP. 10·고유≤2)

- **Ingame dreamcatcher** → `docs/spec/ingame-dreamcatcher/` (completed 2026-06-02, 인게임 카드 선택+효과 MVP. 드래프트 prep 단계 대체. modifier-framework 버그 수정 동반)
- **Squad loadout** → `docs/spec/squad-loadout/` (completed 2026-06-02, 편성+반입 MVP. 드래프트 유닛선택 대체)
- **Outgame scene & flow** → `docs/spec/outgame-scene-and-flow/` (completed 2026-06-02, 2-씬 분리 + 프로필 영속 기반. B/C/D 의 토대)
- **Seasonal map backdrop** → `docs/spec/seasonal-map-backdrop/` (completed 2026-05-22, 4시즌 + Skybox 전환)
- **Modifier framework — Legacy migration** → `docs/spec/modifier-legacy-migration/` (completed 2026-05-01)
- **Modifier framework & Healer** → `docs/spec/modifier-framework-and-healer/` (completed 2026-05)
- **CC pipeline & Obstacle** → `docs/spec/cc-pipeline-and-obstacle/` (completed 2026-04-29)
- **Enemy unit development** → `docs/spec/enemy-unit-development/` (completed 2026-04-30, PlayMode 검증 후속)
- **Board visualization** → `docs/spec/board-visualization/` (wrap 2026-04-27 — ROI 부족 중단, palette-and-overlay-fix 로 후속)
