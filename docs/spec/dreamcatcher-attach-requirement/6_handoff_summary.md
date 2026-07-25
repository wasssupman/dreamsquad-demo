# 6 — Handoff Summary

## Commit

| 커밋 | unit |
|---|---|
| `ab2baef0` | unit 0 — 정의 계층 필드 + 순수 판정 |
| `763d052c` | unit 1 — 브리지 게이트 배선 (UI 판정 + 커밋 preflight) |
| `536ddabe` | unit 2 — 시트 동기화 (DcCards 부착 제한 3열) |
| `facacfc1` | unit 3 — 에디터 validator |
| `26ee3e07` | unit 4 — 문안 접두 포매터 |
| `e9e5f184` | unit 5 — 문안 resolver 배선 (화면 노출) |
| `5292ab04` | 리뷰 반영 — ecs M2(중복 헬퍼 제거) + 이 handoff |
| `a906c46b` | 리뷰 반영 — code MEDIUM 4건 |

## Implemented

- `DcAttachRequireKind{None, Class, UnitId}` + 카드 필드 3개. zero-init = 제한 없음이라 기존 카드 44장 무손상(YAML 키 미기록 유지).
- `DreamcatcherAttachEval.MeetsAttachRequirement` — plain 입력/출력 순수 판정. `HasInvalidAttachRequirement` 는 "정상 거절"과 "데이터 실수"를 나누는 공유 술어.
- 게이트가 두 지점에서 같은 함수를 쓴다: `WouldDreamcatcherCardApply`(드래그 리티클) · `ApplyDreamcatcherCardToUnit`(커밋 preflight). 거절은 카드 전체 `-1` → 각성 무차감·카드 잔류.
- 시트 3열(이름 문자열 enum). **제한 해제는 `attachRequire=None` 명시가 유일한 수단** — 빈 셀은 blank=keep.
- 에디터 validator: 무효 설정 / 없는 유닛 id / 범위 밖 설정(type!=Unit·BountyMark). 마지막 항목은 런타임 경고조차 없어 validator 만이 잡는다.
- 문안 접두 "가디언 전용" / "{유닛명} 전용" — 포매터 한 곳(`LinesWithFallback`)에 넣어 세 표면이 공유. 유닛명은 `DefenderCatalog.DisplayNameOf` 주입, 실패 시 id 폴백.

## Key Files

- `Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherCard.cs` — 필드 3개 + append 규율 주석
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherAttachEval.cs` — 판정 2함수
- `Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs` — `TryGetDefenderDataByEntity` / `PassesAttachRequirement` / 두 소비처
- `Assets/_Project/Editor/UnitStatImport/DcAttachRequirementValidator.cs` — 순수 `CollectWarnings` + 메뉴 + 인스펙터 HelpBox
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardText.cs` — `AttachRequirementLine`
- `Assets/_Project/Scripts/Data/StatImport/DcSheetImportDto.cs` + `Editor/UnitStatImport/DcSheetExporter.cs` — 시트 왕복 + blank 규칙

## Verified

- EditMode **1340건**(1338 pass / 0 fail / 2 기존 Ignore) — 신규 24건.
- PlayMode 신규 e2e 1건(가디언/레인저 host × Class·UnitId·무효 × UI판정·커밋반환 교차) pass.
- validator 실사 실행: `카드 44장 중 0장에서 0건. 위반 없음.`
- PlayMode 전체 53건 중 실패 **6건 = 사전 실패**. 변경을 stash 한 clean 트리(52건)에서 같은 6건이 같은 메시지로 실패함을 직접 재현해 확인했다(AuthE2E 서버 500 중복키 · 폴백 덱 0장 · CardBuffs 가디언 dmgTaken · Gift↔Placement drift 2 · SceneTransition 전체실행 순서의존).

## Notes (되돌리면 안 되는 의도)

- **`WouldApply` 시그니처를 확장하지 않았다.** 커밋 경로는 `WouldApply` 를 부르지 않고 자체 preflight 체인을 쓰며, Squad 조기 return 호출처는 새 인자를 읽지 않는다. 독립 함수라서 기존 EditMode 편집이 0곳이었다.
- **fail-closed 는 의도**다. 무효 설정·host 조회 실패 시 불허. 제한이 조용히 풀리는 것보다 눈에 띄게 안 붙는 쪽.
- **사망 teardown 창의 비대칭은 버그가 아니다** — `_defenderByTile` 제거와 엔티티 파괴의 수명이 달라 그 프레임에 제한 카드만 먼저 거절된다(무차감). 조회 방식을 바꿔도 둘 다 같은 소스라 해소되지 않는다. README "의도된 동작" 참조.
- **`axis` 를 재사용하지 않은 이유**가 README 계약에 있다(Squad 효과 축으로 load-bearing · Ranger/Guardian 만 표현 · 유닛 id 개념 없음). 통합하려는 리팩터 전에 반드시 읽을 것.
- **씬 와이어 검증을 PlayMode 로 하지 말 것.** 처음엔 씬을 런타임 로드하는 PlayMode 배선 테스트를 썼는데 전체 실행에서 `DreamcatcherCombatDamage` 2건 + `GateE2E.ExecutionStrike` 1건이 새로 실패했다(단독 3/3 통과 → 오염). OutgameScene 로드가 아웃게임 부트스트랩을 돌려 뒤따르는 전투 테스트의 장착 상태를 바꾼다. 씬 에셋 텍스트를 보는 EditMode 테스트로 교체했다(`DcAttachRequirementWiringTests`).

## Review

투트랙 리뷰 완료 — 양쪽 **머지 가능·블로커 없음**(CRITICAL/HIGH 0).

- **핵심 불변식 확인됨**: UI 판정(`WouldDreamcatcherCardApply`)과 커밋 preflight 는 모든 입력에서 갈리지 않는다. `type != Unit` 도 양쪽이 함께 제한을 무시한다(Squad 커밋은 `ApplyDreamcatcherCardHosted` 로 라우팅돼 Unit 경로에 오지 않는다). 부분 적용 위험 0, `-1` → `Spend` 전 반환, 지불이 거절보다 먼저 오는 창 없음 — 모두 코드 추적으로 확인.
- **`data == null` 은 도달 불가**: `_defenderByTile` 삽입 지점이 `CreateDefenderEntity` 한 곳이고 거기서 null 이면 8줄 뒤 `unitData.health` 에서 throw. fail-closed 가드는 비용 0이라 유지.
- 반영한 findings: ecs M2(중복 헬퍼) · code M1~M4(README 허위 기재 · 시트 잔존값 부활 · 리플렉션 주입 무보호 · 경고 사유 미분리) + 테스트 취약점 3건.
- 반영하지 않은 것: ecs M1(PlayMode 엔티티 정리 — 프로젝트 전반 관례라 이탈 안 함) · ecs L1(부착 in-flight 재배치 — 기존 동작).

## Follow-up

- **사용자 체감 확인 대기**: 실제 제한 카드를 1장 저작해(시트 또는 에셋) 손패·덱빌더 상세에서 접두가 보이는지 육안 확인. 아직 제한이 걸린 카드는 0장이라 화면에서는 기능이 보이지 않는다.
- 시트 작업: DcCards 탭 오른쪽에 `attachRequire` / `attachRequireClass` / `attachRequireUnitId` 3열 추가 필요(아직 안 했음). 첫 import 시 카드 에셋 YAML 대량 diff 가 정상 발생한다.
- BountyMark×제한의 조용한 무효는 validator 만 잡는다 — `OnValidate` 승격은 README 후속 후보.
- **PlayMode 테스트의 ECS 정리 부재(리뷰 M1, 프로젝트 전반)**: 이 spec 의 e2e 를 포함해 PlayMode 테스트 21개 중 `TearDown` 에서 엔티티를 파괴하는 것은 **하나도 없다**. World 가 러너 세션 내에서 공유되므로 잠재적 누수다. 이 spec 만 다르게 하지 않고(관례 이탈이 오히려 혼란) 프로젝트 차원 후속으로 남긴다.
- **부착 in-flight 재배치(리뷰 L1, 기존 동작)**: `PendingDeployment` 중에도 부착이 가능하다. 초기 배치와 동일한 성질이고 비행이 짧아 실피해는 없다 — 문서화만.
- 복수 클래스 제한(flags) · Squad 부착 제한 · 접두 태그 칩化 — README 후속 후보.
