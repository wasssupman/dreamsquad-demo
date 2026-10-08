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
docs/spec/unified-effect-layer/6_handoff_summary.md
docs/spec/skill-data-table/10_handoff_summary.md
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

## 진행 규칙

(2026-10-01 `CLAUDE.md` 재작성 때 그쪽 「작업 지침」에서 옮겼다.)

- 새 feature 는 README(목표 + 작업 단위 표)를 먼저 쓰고 사용자 승인을 받은 뒤 `0_` 부터 **한 번에 한 파일**씩 구현한다. 같은 feature 의 추가 작업은 기존 폴더에 다음 번호로 이어 쓴다.
- 작업 단위가 끝나면 사용자에게 확인 방법(에디터 · 실기기 중 무엇을 어떻게 보면 되는지)을 구체적으로 알리고 통과를 받는다. 통과하면 그 문서의 「완료 기준」 아래에 확인 일자 + 커밋 해시 한 줄을 남긴다. 확인 없이 다음 단위로 넘어가지 않는다.
- feature 가 끝나면 README 상단에 「상태: 완료 YYYY-MM-DD」, 그리고 `{N+1}_handoff_summary.md`. 설계(규칙 · 구조 · 정본 위치)가 바뀌었으면 `docs/blueprint/README.md` 의 해당 줄을, 파이프라인 구조(새 아키타입 · 정거장 · 앵커 파일 이동)가 바뀌었으면 `docs/reference/object-pipeline-map.md` 도 같은 커밋에서 갱신한다.
- 플레이 오브젝트(유닛 · 적 · 투사체 · 해저드 · VFX 등)를 신설하거나 생성→렌더 경로를 바꾸는 spec 의 README 에는 「파이프라인 커버리지」 섹션을 둔다 — `object-pipeline-map.md` 의 가장 가까운 아키타입 표를 복사해 대조하고, 해당 없는 정거장은 `N/A + 이유`.
- 스코프(무엇을 넣고 뺄지) 논의가 필요하면 사용자에게 묻는다. 뺀 항목은 README 「후속 후보」 나 아래 Follow-up Backlog 로.
- spec 에 넣지 않는 것: 세션 간 조율 로그(누가 무엇을 편집 중 · index.lock · 커밋 해시 추적), 완료된 다른 spec 의 구현 내역, 이 feature 밖 콘텐츠와의 우연한 상호작용. 재사용할 기존 코드의 포인터(이름 · 위치)는 넣는다.

## 예시

- `docs/spec/skill-data-table/` — 1부 · 2부로 이어 쓴 spec(보류 unit · 사용자 결정 표 · 인계 2개)
- `docs/spec/unified-effect-layer/` — 작은 spec(units 0~7 + 인계)
- `docs/spec/battle-core-rebuild/` — 대형 spec(하위 번호 5a~9c · 이식 제외 표 · 장부)

---

## 시작점 (2026-10-01 spec 초기화)

전투 코어 이전 spec 312개 · `docs/plans/` · `docs/prototype/` 는 지웠다(사용자 결정 — 옛 이력은 남기지 않고 **현재 설계가 시작점**). 다음 spec 은 아래 위에 쌓는다.

- **규칙 · 구조**: `CLAUDE.md` · `docs/reference/`(게임 규칙 `ingame-flow.md` · 전투 구조 `battle-core-architecture.md` 외)
- **남긴 spec 3개**(전부 완료 — 현행 계약이 아직 여기 있다):
  - `battle-core-rebuild/` — 순수 C# 전투 코어 · 계약 13 · 장부(`ledgers/` — `tools/battle-core-rebuild/check_ledgers.py` 가 읽는다)
  - `unified-effect-layer/` — 스킬 = 효과 한 층 · 인계 `6_handoff_summary.md`
  - `skill-data-table/` — 효과 표 · 소유 줄 · 시트 8탭(헤더 정본 `5_sheet_io.md` — `SheetHeaderDocTests` 가 읽는다) · 인계 `6_` · `10_handoff_summary.md`
- **초기화 뒤 완료된 spec**: `depth-parallax-removal/` — 뎁스맵 패럴랙스 기능 제거(2026-10-01). 의존성 전수표가 README 에 있다(삭제 spec 이라 handoff 없음). · `unity-6-6-upgrade/` — Unity 6000.6.3f1 전환(2026-10-07). 원인 6종 표와 「코어 경계」 보장의 이동(컴파일러 → 테스트 + 헤드리스)이 README 에 있다. · `demo-diet/` — 전투 로직만 남기는 정리(2026-10-07, 단위 0~4): 입구 값 `MatchEntryInput` · 출구 사건 seam, 아웃게임 코드/에셋 삭제 전수표, 벤더 7 참조 폐포 추림. · `battle-content-finish/` — 「옵션은 SO · 전투 콘텐츠만」 마무리(2026-10-07, 단위 0~5): 판 저작 SO 두 장 · 순방향/역방향 도달 검사로 고른 삭제 장부(`ledger.md`) · `Resources` 없음. 이 정리본이 somnia-client 로 옮겨 가는 payload 다.
- 옛 문서가 꼭 필요하면 태그 `archive/pre-spec-reset` 에서 꺼낸다(`git show archive/pre-spec-reset:docs/spec/<slug>/README.md`). 남은 문서 안의 옛 spec 이름 · 경로도 그 태그 기준이다. 평소엔 읽지 않는다.

## 진행 중 spec

- **`somnia-battle-rename/`** — somnia 이식 ④: `Wassup` → `Somnia.Battle.*` 개명 + somnia 레이아웃 재배치(반입 전, Demo 안에서). 2026-10-08 제안, 결정 D1~D5 대기. 단위 0~4.
- 직전 완료 = `battle-content-finish/`(2026-10-07 — 판 저작 → SO 두 장 · 죽은 타입/코드 · 닿지 않는 에셋 장부 491 · 일회성 에디터 도구/시트 push/패키지 6 · `Resources` → `RuntimeMaterialSet` SO · 문서 8, 단위 0~5) · 그 전 `tilemap-untangle/`(2026-10-07 — Tile 에셋 껍데기 → Sprite · `GridLayout` 권위 → 보드 평면 Transform + tileSize · Tilemap 패키지/모듈 제거, 단위 0~2) · `demo-diet/`(2026-10-07 — 단위 0~4, 커밋 12) · `unity-6-6-upgrade/`(2026-10-07). 다음 후보: somnia-client 이식(`Somnia.Battle.*` 개명 — 세션 메모리 `somnia-migration-goal`). 그 전 = `design-blueprint/`(2026-10-01 — `CLAUDE.md` 재작성 + 현시점 요약 `docs/blueprint/README.md` · 남은 후보는 그 README 「후속 후보」).

## Follow-up Backlog

지금 코드 기준으로 유효한 후보만 둔다. 초기화 전 백로그(옛 전투 시절 항목 약 800줄)는 지웠다 — 필요하면 그때 현재 코드에서 다시 연다.

### 사용 규칙

- 각 항목 **1~3줄 요약** (What · Why · Scope). 상세 설계는 새 spec 에서 다룬다.
- Scope: **S** = 단일 unit, **M** = 2~5 unit spec, **L** = 5+ unit spec.
- 같은 결의 작업은 테마 서브그룹(`###`)으로 묶고, 출처 spec 이 섞이면 항목 끝에 `(spec-slug)` 라벨.
- 새 spec 으로 승격되면 줄을 `→ docs/spec/{slug}/` 링크로 바꾸고, 더 이상 유효하지 않으면 지운다.

### spec 초기화 잔여 (2026-10-01)

- **Assets 안 지운 spec 경로 6곳** [S] · Unity 가 플레이 중이 아닐 때 한 커밋으로(재컴파일 1회).
  - `Scripts/UnitAi/UnitActionPhase.cs` — 머리 주석이 **옛 ECS 동작을 현재처럼 설명한다**(배치 중 · 사망 제외를 옛 쿼리가 한다 · 옛 공격/이동 시스템 추출 · Burst 호환). 지금은 `CombatPhase` 가 `u.Deploying || u.Dead` 로 건너뛰고 `DefenderAi` 가 배치 중을 입력으로 받는다 · asmdef 에 Burst 없음 → 주석을 다시 쓴다.
  - `Scripts/Data/StatImport/UnitStatImportDto.cs` · `DcSheetImportDto.cs` — 옛 JSON 계약 문서 → 현행 정본 `skill-data-table/5_sheet_io.md` 「실제 시트 설정」으로.
  - `Tests/EditModeAssets/UnitRosterInvariantTests.cs` — 주석 + **실패 메시지**의 「투영 규칙(spec unit 0)」 → `UnitStatFieldMapper`.
  - `Shaders/Prop_Outline_Sprite.shader` — 경로 한 줄 삭제(머리 주석이 이미 설명한다).
- **`attack_damage` 호환 코드 제거** [S] · 시트를 새로 만들어 그 열이 없다(`5_sheet_io.md` 「만들지 않는다」). `UnitStatImportDto.attackDamage` · `UnitStatApplier.WarnDeprecatedAttackDamage` · 테스트 3 · `SheetHeaderDocTests.NotInSheet`. 사용자 결정 대기.
- 코드 주석의 옛 출처 메모(510 파일)는 일괄 정리하지 않는다 — 현재 동작을 틀리게 말하는 것만 그 파일을 만질 때 고친다.

### 스킬 데이터 표 — 남은 것 (`skill-data-table`)

정본 = [`skill-data-table/README.md`](skill-data-table/README.md) 「후속 후보 (2부)」 — 상세는 옮기지 않는다.

- **unit 7 전체**(보류) · 방어유닛 · 적의 진영 버프 · 공격 변형 3종 배선(숙주 적합성 `HostProfile.OfDef` 포함). 규칙 U21~U24 는 결정돼 있다.
- **액티브 스킬을 드림캐쳐에서 분리**(U18) · `SkillData` · `DcSkills` 탭 은퇴.
- **옛 메커닉 번역 층 제거** — `BindingSpecView.ToMechanic` → `DcMechanic`/`DcPayloadSpec`(굽기 잎 검증 · 카드 문안이 아직 읽는다) · 거울 enum `DcCcKind`/`DcStackKind` · `EffectSlots` 의 이전 함수(테스트만 부르거나 호출 0).
- **센서스 개명 미실행 2** — B7 `CardBuffKind` → `BuffStat` · B10 `BindingOrigin` → `OwnerKind`(정수 해시 유지). (`skill-data-table/naming-census.md`)
- 배치 오라 ↔ 진영 버프 관계 정리 · 설정 탭 통합(`DcConfig` · `CostConfig`) · 분열 적 칸(`Enemies.split_*`) · 드림스톤 시트 · 순찰 소환물의 진영 버프 수혜(D2) · 적 소유 진영 버프의 직업 필터.
- 시트 정합 감사(2026-09-26) 남은 것 — 죽은 열 `Defenders.aggroRange` · `Enemies.aggroAttackDamage/Cooldown/Range` · 거짓 문안(은퇴한 유출 허용치 문구 `Card_IncubusPact` · `SkillMath` 반올림 주석) · 시트 밖 저작(폭탄 · 다연발 · 장판 능력 SO · 적중 CC · 적 tier 등).

### 통합 효과 층 — 남은 것 (`unified-effect-layer`)

정본 = [`unified-effect-layer/README.md`](unified-effect-layer/README.md) 「후속 후보」.

- **「범위 안 전부」를 발사 명세 선정 규칙으로 승격** · 지금은 전원 손잡이(`FanOutToAllCandidates`)로 충분하다.
- **부착 판정의 도발 가디언 검사 · `CastHazardSkill` 미등록 죽은 파일 처리.**

### 전투 코어 — 남은 것 (`battle-core-rebuild`)

인계: `battle-core-rebuild/11_handoff_summary.md`. 분류는 그 README 결정 ⑩(서버 권위 실시간 서버 예정 · 커맨드 = 서버 로직 키워드 · UI/에셋은 데모 · CI 시기상조)을 따른다. 약칭 `BCR` = `docs/spec/battle-core-rebuild/`.

**(가) 서버 권위 spec 을 열 때 자연 해소** — 따로 고치지 말고 그 spec 의 입력으로.
- **토너먼트 맵 결정권** [M] · 클라 우선순위 사슬(dev > 씬 > 서버)이 맵을 고른다. (`BCR/8b`)
- **서버 API 확장**(modeId · sortDirection · leaderboardId) [M] · v1 은 `KillScoreTimed` 단일 제출. (`BCR/README` 계약 13)
- **라이브 판 커맨드 기록 · 리플레이 · 결정론 등급 상향** [M] · 기록 형식이 곧 서버 계약. 스폰 측면 오프셋 순번 파생(X25)이 전제. (`BCR/ledgers/rules.md` X25)
- **프로필 저장 원자성 · 스키마 버전** [S~M] · 서버가 정본이 되면 로컬 저장의 역할이 바뀐다.
- **운영 관측 · 서버 우회** [S] · 계정 첫 판 참가 생략(`IsFirstMatch`)은 서버 `complete` 500 우회다(결정 ⑦-1 존치). 서버가 고쳐지면 별도 결정. (`BCR/8d`)

**(나) 코드 레벨 정식 설계 후속** — 결정 ⑩ 의 선순위.
- **판정 산식(`battle-core-architecture.md` §8-7)에 남은 사각 자** [S~M] · 폭탄맨 · 캐스터 4종의 일반 공격 대상 선정이 아직 체비셰프(`CombatPhase.PickChebyshevNearest` · `TargetRanking` — rules C20 현행 보류) · 최다 밀집 칸 선정도 사각(`CoreSkillContext` — 사용자 결정 ② 기본값). 사용자 재결정 대상(아래 (마)).
- **조용한 기본값 폴백 → loud 거절 + enum 전수 테스트** [M] · 목표 종류 · 공격 정책 · 스탯 변환 · 해저드 모양. (`BCR/ledgers/rules.md` M2 · E7)
- **겸용 파라미터 가방 분리** [M] · `SkillIntent` 보조 스칼라 · 25인자 생성자.
- **거대 단계 파일 분할** [S~M] · `CombatPhase` · `TickProjectilePhase`. 틱 순서는 무변. (`BCR/ledgers/rules.md` C24)
- **어셈블리 분할**(Outgame / BattleView / Data) [M] · 지금은 `BattleCoreUnity` · UI · Data 가 전부 `Wassup.Runtime` 한 asmdef 다.
- **뷰 무음 구독 · `Find` 폴백 전수 검사** [S] · 배선 누락이 경고 없이 돈다.
- **9b 규칙 증언 이식 잔여** [M] · (`BCR/ledgers/retire-test-pairs.md` 「부분 공백」)
- **디버그 커맨드 게이트** [S] · 릴리스 빌드에서의 차단 여부를 전수로 정한다.
- **하네스 런타임 어셈블리 분리** [S] · `CardProbe` · 골든 코퍼스가 런타임에 실린다. (`BCR/7e`)
- **`ClassFilter` Role 미설정 저작 검증** [S] · 미설정 저작을 빌더가 거절하게. (`BCR/9c`)
- **`IntentApplier` 포탈 판정의 몸 반영 여부** [S] · 대상 몸이 붙는지 판정 산식(§8-7)으로 재확인. (`BCR/9c`)
- **효과 census 미배정** [S] · `regenPerSec` 음수 고정 처리 · 결합식 바닥/천장 4개 SO 저작화.
- **틱 30Hz 실측 · 트리거 연쇄 깊이 근거** [S]
- ~~`[Explicit]` 라이브 서버 테스트 격리~~ · `demo-diet` 가 아웃게임 테스트 어셈블리째 지웠다 — somnia 이관.
- **헤드리스 lane 경로를 워크트리에서 떼기** [S] · csproj 4 의 `UnityScriptAssemblies` 기본값이 `wassup-core` 를 든다. 워크트리 정리 **전**에 main 으로. (`BCR/10`)

**(다) 데모 UI/에셋이라 보류** — UI · 에셋이 정본이 되는 시점에.
- UI 캔버스 정렬 상수 표 · 릴리스 빌드 경로 · 환경 전환 · 예고선 광휘 · 쿨타임 액체 셰이더 · 숫자 틱 팝(`BCR/5b`) · 공격음 START · 결과 단위 표기(`BCR/5c` · `BCR/8a`) · 상태 FX 우선순위 표(`BCR/6c`) · 희귀도 축(E21).

**(라) 시기상조** — CI(결정 ⑩).

**(마) 사용자 몫**
- **EditMode 선행 빨강 3** · 카드 아트 중복(개사기 · 별똥 타격 전용 아트 없음) · 카드 설명 어긋남 7장 · `bomb_man` 문안. 시트 · 아트에서 고친다.
- **효과 한국어 이름 `kind_ko` 43 검토.**
- ~~Android QA 빌드~~ · 모바일 빌드 스크립트(`scripts/mobile/`)는 `demo-diet` 에서 제거 — somnia CI 이관.
- ~~실제 랭킹 확인 · 실서버 쓰레기 계정 정리~~ · somnia 이관(이 리포엔 서버 코드가 없다).
- **규칙 재결정 — 분류표 보류 30행**(질문 20개로 묶여 있다 — `BCR/ledgers/rules.md` 「보류 항목의 재결정 질문 목록」) · **기본값 박제 5**(보스 도약 착지 선정의 사각 자 · 자는 유닛의 주기 스킬 · 저작 `CcOnHit` 이 탄을 타나 · 실드 부여 한 틱 지연 · 폭탄맨 · 회복 산출물의 공격자 배율).
- **방향 지정 배치(facing) 미이식** · 커맨드 자리는 있고 조준 입력이 없다. (`BCR/5b`)
- **마음 N개 공유 체력** [M] · `HeartMeter` 가 체력을 들어 이사 비용 0. (rules X29 · E13)

### unity-6-6-upgrade 잔여 (2026-10-07)

- **6.6 골든 드리프트 확인** [S] · 배치 검증을 생략해 `CoreGoldenTests` 를 6.6 에서 아직 안 돌렸다. 다음 EditMode.Core 실행 때 본다 — 빨강이면 재굽기는 사용자 결정(결정론).
- **벤더 obsolete 경고** [S] · GabrielAguiar `PrefabStage.prefabAssetPath` 등. demo-diet 단위 3 벤더 추림 뒤 남은 것만.
- **헤드리스 테스트 lane 과 Smart App Control** [S] · 이 머신은 새로 링크된 서명 없는 테스트 DLL 로드를 차단한다. 빌드 lane 만 쓴다 — 서명 또는 다른 러너는 필요해지면.

### demo-diet 중 발견 (2026-10-07 — 다이어트와 무관한 선행 상태)

- **`Defender_ShieldShuttle` 의 무기 궤적 리그가 없는 본을 따른다** [S] · 궤적 프리팹(`WeaponTrail_Slash` 변형)의 `BoneFollower.boneName = Gear` 인데 이 유닛만 스켈레톤이 CH4(Gear 본 없음)다. Play 중 `Bone not found: Gear` 가 매 프레임 찍힌다(한 판 907회). 리그 본을 CH4 의 손 본으로 바꾸거나 트레일을 뗀다 — 저작 결정이라 사용자 몫.
- **Hovl `HS_SwordMeshTrail.RefreshPresetPointAEffects` 가 씬 열기 때 1회 예외** [S] · `PrefabUtility.InstantiatePrefab` 의 parent 가 프리팹 에셋 안이라 거절. 벤더 스크립트의 에디터 타임 재생성 로직. 무해하지만 콘솔에 남는다.
- **손패 닫기 트윈의 PrimeTween 경고** [S] · Play 중 「Tween's 'endValue' equals to the current animated value … UIAlphaGraphic / HandPanel / 0.21s」가 판당 2회. `SelectionInput.CloseSelection` 이 집어 든 채/드래그 중 매 프레임 불려 이미 닫힌 손패에 알파 0 → 0 트윈을 건다(`CoreHandView.CloseFromSelection`). 무해(경고)지만 닫혀 있으면 트윈을 안 걸게 하거나 `warnEndValueEqualsCurrent` 를 끈다. (battle-content-finish 뒤 2026-10-08 Play 에서 관찰 — 선행 여부 미확인)
- **벤더 팩이 원래 들고 오지 않은 참조 129건**(GA 머티리얼 36 등 12 guid) · HEAD 트리에도 없던 셰이더·텍스처. 그 머티리얼이 실제 보이는지 Play 육안으로.
