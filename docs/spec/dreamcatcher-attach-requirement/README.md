# dreamcatcher-attach-requirement — 부착 대상 제한 필드 (클래스 / 특정 유닛)

상태: 완료 2026-07-26 (units 0~5 + unit 7 rev + unit 8). 최신 커밋:
unit 7 `12f5b644` · unit 8 `874a54ad`. 인계 → `9_handoff_summary.md`

검증: EditMode **1343건**(1341 pass / 0 fail / 2 기존 Ignore, 신규 27건) · PlayMode 신규 **2건** pass(부착 게이트 e2e + 무차감 보장) · unit 8 Push payload 회귀 **2/2 pass**. 문안 배선 검증은 PlayMode 가 아니라 EditMode 3건(`DcAttachRequirementWiringTests`) — 씬 런타임 로드가 뒤따르는 전투 테스트를 오염시켜 unit 5 에서 의도적으로 옮겼다(경위는 `5_text_wiring.md`). validator 실사 스캔 `카드 44장 중 0건`. PlayMode 전체 잔여 실패 6건은 clean 트리 재현으로 **사전 실패** 확정.

## 상위 목표

Unit 타입 드림캐쳐에 **부착 시점 정적 술어**("가디언에만 부착 가능" / "방패셔틀에만 부착 가능")를 데이터 필드로 추가한다. 기존 capability 게이트(투사체/데미지 output — `DreamcatcherAttachEval`)는 그대로 두고 그 위에 겹친다. 필드는 시트(DcCards 탭)에서 제어된다.

검증 질문: *시트에서 `attachType=Class, attachValue=Guardian` 을 넣으면 그 카드는 가디언이 아닌 유닛에 드래그 시 invalid 리티클이 뜨고, 커밋 시 무차감 거절되며, 문안에 "가디언 전용"이 표기되는가? 필드를 비운 기존 카드는 전부 무회귀인가?*

## 작업 단위

| 파일 | 작업 구분 | 목적 |
|---|---|---|
| `0_field_and_eval.md` | 토대 | 필드 append + `MeetsAttachRequirement` 독립 순수 함수 + EditMode |
| `1_bridge_gate.md` | 게이트 | UI 판정 + 커밋 preflight 두 소비처 배선 + PlayMode e2e |
| `2_sheet_sync.md` | 시트 | `DcCardDto` append + export blank 규칙 + 라운드트립 |
| `3_validator.md` | 위생 | 에디터 validator — 무효/없는 id/범위 밖 설정 조기 검출 |
| `4_card_text_formatter.md` | 문안 | 포매터 접두 조립 + 골든 (optional resolver) |
| `5_text_wiring.md` | 배선 | 소비처 4곳 resolver 주입 + 씬 wiring + Play 육안 |
| `6_handoff_summary.md` | (종료 시) | 인계 요약 |
| `7_field_shape_rev.md` | rev | 필드 형태 3→2 (`attachType`+`attachValue`) |
| `8_push_header_bootstrap.md` | 시트 | 제한 카드 0장에서도 Push가 `DcCards` 신규 2열을 자동 생성 |
| `9_handoff_summary.md` | 인계 | unit 7~8 반영 최종 인계 |

## Feature-wide 계약

- **정의 계층 append-only**: 카드 끝에 `attachType`(`DcAttachType{None, Class, UnitId}`) + `attachValue`(string) **2필드**. zero-init = 제한 없음 → 기존 카드 무손상. `attachType` 이 `attachValue` 를 읽는 방식을 정한다 — `Class`=클래스 이름(대소문자 무시), `UnitId`=유닛 id(ordinal). 값 칸이 하나라 "종류를 바꿨는데 옛 값이 되살아나는" 함정이 구조적으로 없다(unit 7 rev).
- **적용 범위 = `CardType.Unit` 의 defender 부착 경로만** (2026-07-25 사용자 결정). Squad(host 무제약 계약 유지)·Active·BountyMark 비적용. 클래스 제한은 **단일 클래스**(같은 결정). 유닛 매칭 키는 `DefenderUnitData.id`(저장용 안정 키, ordinal) — 표시명 아님.
- **판정 = 단일 함수, 두 소비처**: `MeetsAttachRequirement` 를 `WouldDreamcatcherCardApply`(UI 스냅샷)와 `ApplyDreamcatcherCardToUnit`(커밋 preflight)이 각각 호출한다. **`WouldApply` 시그니처는 확장하지 않는다** — 커밋 경로는 `WouldApply` 를 부르지 않고, 비-Unit 호출처는 새 인자를 읽지 않으며, 독립 함수면 기존 EditMode 편집이 0곳이다.
- **fail-closed**: 값을 못 읽거나(빈 값 · `Class` 인데 오타·숫자·`None`) host 데이터 조회 실패 시 불허 + loud 경고. 제한이 조용히 풀리는 쪽보다 눈에 띄게 안 붙는 쪽을 택한다. 값 해석 규칙의 단일 지점은 `DreamcatcherAttachEval.TryParseAttachClass`.
- **커밋 거절 = 카드 전체·무차감**: preflight 는 `DefenderUnitTag` 검사 직후·모든 쓰기 전. `-1` 반환이 `DreamcatcherHandController.cs:342` 에서 `AttachAndSpend` 전에 걸려 무차감·카드 잔류를 보장한다.
- **시트**: `DcCardDto` 에 **2필드** append(reflection 양방향 자동). `attachType` 셀은 이름 문자열(`None`/`Class`/`UnitId` — `StringEnumConverter`), `attachValue` 는 자유 문자열(`Guardian` / `shield_shuttle`). **제한 해제는 `attachType=None` 을 명시** — 빈 셀은 "유지"라서 해제 수단이 아니다. 전 카드가 `None` 이어도 Push 전용 키 없는 헤더 시드가 두 키를 운반하므로 첫 `Push to Sheet`에서 `DcCards` 오른쪽에 두 컬럼이 자동 생성된다(unit 8). 일반 카드 export의 blank 규칙은 유지한다.
- **문안 자동 표기** (2026-07-25 사용자 결정): 포매터가 "가디언 전용" / "{유닛명} 전용" 접두를 조립하고, 유닛 표시명은 caller 주입 resolver(실패 시 id 폴백)로 해석 — 포매터는 카탈로그를 직접 알지 않는다.

### 의도된 동작 (버그로 오인 금지)

- **사망 teardown 창의 비대칭**: `_defenderByTile.Remove`(`BattleBridge.cs:2660`)와 ECS 엔티티 파괴는 수명이 달라, 바인딩은 제거됐지만 엔티티는 남은 프레임에 제한 카드만 먼저 거절된다(무제한 카드는 `_em.Exists` 기준이라 통과). 무차감이라 실피해 없음. 조회 방식을 바꿔도 둘 다 `_defenderByTile` 소스라 해소되지 않는다.
  - 실제 창은 이 서술보다 **더 좁다**(review 확인): 드래그 픽(`TryPickDefenderAtScreen`)도 같은 `_defenderByTile` 을 돌기 때문에 바인딩이 사라진 프레임에는 hover 자체가 안 잡혀 드래그 경로로는 도달하지 않는다. 커밋 경로에 직접 진입하는 코드(테스트·미래 자동 부착)만 해당. 거절 시 전용 경고 문구가 나오므로 로그로 구분된다.
- **BountyMark×제한 = 조용한 무효**: `Classify` 가 `HasBountyMark()` 카드를 `AimMode.EnemyMark` → `ApplyBountyMark` 로 라우팅해 defender 게이트를 통과하지 않는다(배제는 코드 수준 자동 보장). 게이트 함수를 안 타므로 런타임 경고조차 없어 unit 3 validator 가 유일한 검출 수단.
- **`axis` 재사용 안 함**: `CardTargetAxis` 에도 ClassGuardian/Cost1 이 있으나 ① `axis` 는 Squad 효과 대상 집합 + PlacementAura 수혜 축으로 load-bearing(Unit 카드도 `RegisterPlacementAura` 로 소비) — 겸용하면 두 의미가 얽힌다 ② `axis` 는 Ranger/Guardian 뿐, `DefenderClass` 는 Fighter/Caster/Support 까지 ③ "특정 유닛 id" 가 axis 어휘에 없다.

## 파이프라인 커버리지

N/A — 플레이 오브젝트 신설·생성→렌더 경로 변경 없음 (부착 판정 데이터 + UI 문안만).

## 후속 후보

- 복수 클래스 제한(flags) — 카드 기획이 생기면
- Squad 부착 제한 — `ApplyDreamcatcherCardHosted` 가 host entity 를 안 받으므로 host 스레딩 + entity→data 조회 + Squad 조기 return 교체 (국소 변경)
- 부착 제한 ∧ 발동 게이트(`dreamcatcher-trigger-gates`) 동시 표기 시 문안 길이 정리 — 태그 칩化(hand-card-face `TargetTag` 전례)
- 덱빌더 카드 상세에 제한 대상 유닛 초상 표기
