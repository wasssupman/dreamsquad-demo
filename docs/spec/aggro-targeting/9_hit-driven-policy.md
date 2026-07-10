# Unit 9 — 정의 계층: 히트 구동 아그로 순수함수 (AggroPolicy / AggroTargeting)

> 재설계(근접→히트) 정의 계층. 드림캐쳐 2계층(`docs/reference/dreamcatcher-portability.md`) 계승 — ECS/MonoBehaviour 실행모델 무참조. `Unity.Mathematics`(`int2`/`float3`)는 순수 수학 라이브러리로 허용(`BounceRetarget` 선례).

## 목적

어그로의 **정책·기하 결정**을 아키텍처 무관 순수함수로 격리한다. ECS 시스템(해석 계층)은 이 함수를 호출만 한다. EditMode 단독 테스트로 회귀 고정.

## 변경 대상

- (신규) `Assets/_Project/Scripts/Battle/Combat/AggroPolicy.cs`
- (신규) `Assets/_Project/Scripts/Battle/Combat/AggroTargeting.cs`
- (신규) `Assets/_Project/Tests/EditMode/AggroPolicyTests.cs`

> 배치: `DcTrigger`/`BounceRetarget` 선례대로 소비자 근처(`Battle/Combat/`). 정의 계층 성격은 "ECS 타입 무참조"로 성립하고, 폴더가 아니라 참조로 판정한다.

## 구현

순수 함수 3개 (static, ECS 타입 무참조):

1. `bool AggroPolicy.CanAcquire(int held, int capacity, bool alreadyAggroed)`
   → `!alreadyAggroed && held < capacity`. capacity 게이트 + 선점.

2. `int AggroTargeting.SelectTargets(int2 gCell, int tileRange, int held, int capacity, ReadOnlySpan<Candidate> cands, Span<int> outIdx)`
   - `Candidate { int2 cell; float3 pos; bool aggroed; }` (Entity 무참조 — 인덱스로 반환).
   - `held < capacity` → 사거리 내 **비-어그로** 최근접부터 `outIdx.Length`(=maxTargets)까지 채우고, 부족분은 일반 최근접.
   - `held >= capacity` → 일반 최근접(겹친 어그로 팩 정리).
   - 거리 = Chebyshev 타일거리(사거리 판정) + 유클리드 제곱(정렬). `outIdx` 채운 개수 반환.

3. `bool AggroPolicy.ShouldRelease(bool guardianAlive)` → `!guardianAlive`. (단순하지만 해제 조건 확장 지점으로 유지.)

수치 입력은 전부 호출자가 데이터(SO)에서 넘긴다. 함수 내 상수 없음.

## 완료 기준

- [ ] 컴파일 + Burst 호환(순수 struct/primitive, Span). ECS 타입 무참조 확인.
- [ ] `CanAcquire`: held<cap & 미어그로 → true / held==cap → false / 이미어그로 → false.
- [ ] `SelectTargets`: 여유 시 비-어그로 우선(동일거리 비-어그로가 어그로보다 먼저), 상한 시 최근접, 사거리 밖 제외, maxTargets 초과 안 함.
- [ ] EditMode 테스트가 아키텍처 타입 없이 순수 입력만으로 통과.

완료: 2026-07-09 (EditMode 11 테스트 통과, 컴파일 클린 / 커밋 `feat(aggro) [aggro-targeting 9]`)
