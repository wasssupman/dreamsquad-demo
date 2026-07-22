# 0 — 구조화 카드 텍스트 포맷 계약

## 목적

카드 수치를 SO에서 읽어 공통 문법으로 조립한다. 포맷터는 ECS/Bridge를 참조하지 않는
순수 UI helper다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardText.cs`
- `Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherCard.cs`
- `Assets/_Project/Tests/EditMode/DreamcatcherCardTextTests.cs`

## 구현 계약

### 공통 헤더와 문법

- 헤더: Squad는 `레인저/가디언/1코스트/전체 · 스쿼드 버프`, Unit은 `유닛 부착`,
  Active는 `액티브`다.
- Squad 효과 대상은 `레인저 아군`, `가디언 아군`, `1코스트 유닛`, `모든 아군`이다.
- 트리거는 `항상`, `N번째 공격마다`, `N번째 피격마다`, `이 유닛이 사망하면`,
  `N초마다`, `HP N% 이하`, `이 유닛이 적을 처치하면`, `실드 파괴 시`로 통일한다.
- 효과 라인은 `[트리거] → [효과]`로 시작하고, 지속시간·조건·비용·재사용시간은 `·`로 잇는다.

### 데이터 매핑

| 입력 | 표시 규칙 |
|---|---|
| `CardEffect` | 대상 + 공격력/공격 속도/체력/이동 속도/각성 회복 속도 + signed `%` |
| `DamageVsCc` | `CC 상태 적에게 ... 피해 +N%` |
| `ProjectileBounce` | 최대 범위, 튕김 횟수, 피해 감쇠 |
| `AttackN` 계열 | 투사체/광역 피해, 2연발, CC, 스택, 강공 |
| 특수 payload | 즉시·완주·표식·호스트 생존 조건을 별도 라인으로 표시 |
| `SkillData` | 타일/유닛 지정, 범위, 배율 또는 피해/끌어당김 속도, 지속시간, 비용, 재사용시간 |

- `ApplyCcToTarget(Impulse)`는 넉백 속도와 지속시간을 모두 표시한다. `Tornado`는 피해를
  표시하지 않고 범위·끌어당김 속도·지속시간만 표시한다.

### 수치 형식

- 공통 숫자: `value.ToString("0.##", InvariantCulture)`; `7.50`은 `7.5`로 표시한다.
- 퍼센트: 양수 `+N%`, 음수 `-N%`. 배율은 `xN`, 시간은 `N초`로 표시한다.
- `warningSec <= 0`인 Meteor는 `0초 후`를 출력하지 않고 즉시 착탄으로 표시한다.
- NaN/Infinity는 authoring 금지. formatter는 UI 보호용 0 fallback을 두며 validation은 후속이다.
- 지원되지 않은 enum 조합이 섞이면 부분 요약과 fallback을 함께 표시하지 않는다. authored
  description이 있으면 전체 fallback으로 전환하고, 없으면 지원 라인만 유지한다.

## 완료 기준

- [x] 변경 파일 컴파일 성공, 신규 오류 없음.
- [ ] 모든 매핑에 EditMode assertion이 있다.
- [x] `Body`와 `BodyCompact`의 내용은 같고 블록 간격만 다르다.
- [x] 수치는 authored description의 숫자가 아니라 구조화 필드의 최신 값에서 나온다.
- [x] 지원 데이터가 있는 현재 카드 전부가 비어 있지 않은 구조화 요약을 만든다.
- [x] 지원되지 않은 mechanic은 부분 요약을 조용히 숨기지 않고 fallback 경로를 탄다.
