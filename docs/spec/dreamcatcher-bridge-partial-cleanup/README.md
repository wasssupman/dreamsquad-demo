# Dreamcatcher Bridge Partial + Dormant Cleanup — 번역자 물리 분리 & 구 3중1/SkillBar 은퇴

> 상태: 작업 중 (2026-07-12 시작)
>
> 배경: 드림캐쳐 설계 리뷰(2026-07-12)의 권고 1·4. BattleBridge(4,587줄) 안에서 계속 자라는
> 드림캐쳐 카드 번역자 구간을 partial class 로 물리 분리하고, 실플레이 검증이 끝난 지금
> dormant 코드 3벌(구 3중1 컨트롤러/SelectionView/SkillBar)을 완전 삭제한다.

## 검증 질문

> 번역자 분리 후 컴파일/기존 테스트가 전부 그린이며 동작 변화가 0인가? dormant 3벌 삭제 후
> BattleScene 에 missing script 참조가 없고, 살아있는 드림캐쳐 경로(각성 손패)와 기존
> PlayMode 테스트의 회귀 커버리지(효과 적용/덱 반입)가 유지되는가?

## 작업 단위

| # | 문서 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_bridge_partial.md` | 리팩토링 | `BattleBridge.Dreamcatcher.cs` partial 분리 — 순수 이동, 동작 변화 0 |
| 1 | `1_dormant_retirement.md` | 삭제+씬+테스트 | dormant 3벌 삭제 + 씬 GameObject 제거 + 죽은 이벤트 제거 + 테스트 이관 |
| 2 | `2_handoff_summary.md` | 인계 | 종료 시 작성 |

## Feature-wide 계약

1. **unit 0 은 순수 이동(move-only)**: 코드 수정 없이 멤버를 새 파일로 옮긴다. diff 는
   "삭제 + 동일 텍스트 추가 + `partial` 키워드"만이어야 한다. 리팩토링(디스패치 테이블화 등)은
   범위 밖 — 후속 후보 유지.
2. **경계 불변**: BattleBridge 는 여전히 유일한 Mono↔ECS 창구다. partial 은 물리 파일 분리일
   뿐 클래스/접근성/멤버 시그니처 불변.
3. **씬 GUID 위생**: 신규 `.cs` 는 Unity 가 생성한 `.meta` 와 짝으로 커밋(GUID 재생성 사고 방지).
   씬 오브젝트 삭제는 UnityMCP 로 수행하고 저장 후 missing-reference 검사.
4. **회귀 커버리지 보존**: 테스트는 "삭제"가 아니라 "살아있는 경로로 이관"이 원칙 —
   덱 반입 테스트는 `DreamcatcherHandController.ResolveAttachDeck` 로, 효과 적용 테스트는
   이미 bridge 직접 구동이라 중화 헬퍼만 제거. 은퇴한 3중1 auto-pick 플로우 전용 테스트만 삭제.
5. **로거 스키마 불변**: `RecordDreamcatcherOffer`/`RecordDreamcatcherPick` 는 죽은 API 가
   되지만 로그 스키마(토너먼트 서버 계약 가능성) 보존을 위해 이번 spec 에서 삭제하지 않는다.

## 후속 후보

- payload kind 디스패치 테이블화 (리뷰 권고 2 — kind ~12종 도달 시)
- Effects stackId-remove 프리미티브 (리뷰 권고 3 — <1 디버프 카드 선행 조건)
- `SkillRuntime` 씬 컴포넌트/클래스 은퇴 검토 (SkillBar 삭제 후 소비자 0 — 단 Active 캐스트
  API 의 `skillRuntime?.` 가드와 얽혀 있어 별도 확인 필요)
- `RecordDreamcatcherOffer`/`Pick` 로거 API 및 스키마 필드 정리 (순환/사용 이력 로깅으로 대체 시)
