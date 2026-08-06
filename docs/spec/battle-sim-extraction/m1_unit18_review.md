# unit 18 — 3렌즈 코드 리뷰 결과와 남은 작업 재정리

작성: 2026-08-06 · 대상: 33/44 이식 시점(`5444bde6`) · EditMode 2566 → 2568

## 검증 방법

세 리뷰어를 **다른 렌즈**로 독립 실행했다. 서로의 결과를 보지 못했다.

| 렌즈 | 물은 것 |
|---|---|
| **ECS 경계** | 맥락 경계가 이식 후에도 지켜지나 · 순서 그래프 재현 · 게이트 53 처분 |
| **Mono 코드품질** | 처음 보는 C# 개발자가 읽고 고칠 수 있나 · 실제 결함 · 테스트 적정성 · 과잉 추상화 |
| **서버권위 결정론** | 두 클라이언트의 판정이 갈리는 지점 · 규칙이 아직 뷰에 있는가 · 문서 vs 코드 |

**채택 기준**: 두 렌즈 이상이 같은 지점을 지목했거나, 한 렌즈가 지목하고 **내가 직접 재현/확인**한 것만 확정으로 다룬다. 나머지는 아래 "미채택" 에 이유와 함께 남긴다.

⚠ **세 리뷰 모두 테스트를 실행하지 않았다** — 전부 정적 판독이다. "문제 없음" 은 **코드 대조**이지 실행 대조가 아니다.

---

## 확정 — 즉시 조치함

### F1. `SimEntityId.Equals(object)` 무한 재귀 → 프로세스 사망 ✅ `2ae418a6`

`Core/SimWorld.cs:22` 가 패턴 변수 `e` 를 바인딩해 놓고 `o` 를 넘겼다. `o` 의 정적 타입이
`object` 라 오버로드 해석이 **자기 자신**에 바인딩 → 무한 재귀 → `StackOverflowException`
(catch 불가).

**두 렌즈가 독립적으로 찾았고 한쪽은 .NET 8 standalone repro 로 실증**했다.

2,566개 테스트가 전부 초록이었던 이유: `Dictionary`·`List.Contains`·NUnit 비교자가 전부
`EqualityComparer<T>.Default` → `IEquatable` 경로라 이 오버라이드를 **밟지 않는다**.
처음 밟히는 곳은 박싱 비교 — 직렬화·리플렉션·비제네릭 컬렉션, 즉 **엔진 밖 호스팅 경로**다.

회귀 테스트 2건 추가(값 타입 4종 박싱 동등성 + 해시 계약). 형제 셋이 맞았다는 사실이
오히려 눈을 가렸으므로 넷을 한 자리에서 본다.

---

## 확정 — 작업 단위로 이관

### F2. 초월함수가 sim 을 교차 런타임 이식 불가로 만든다 (CRITICAL)

`Math/SimMath.cs:51-52` 가 `Sin`/`Cos` 를 `Math.Sin`/`Math.Cos` 로 위임한다.
**IEEE-754 는 `+ - * / sqrt` 에만 정확 반올림을 요구하고 초월함수에는 아무것도 요구하지 않는다** —
플랫폼 libm 이 각자 다항식을 쓰고 마지막 ULP 가 갈린다.

**직접 확인한 사실**:
- 신 sim 호출처 2개 — `ProjectileMath.cs:30`(아치) · `EmissionLogic.cs:78`(패턴 각도)
- **둘 다 이산 판정에 먹힌다**: 아치는 `ProjectileMoveSystem.cs:162,215` 가 `transform.Position` 에
  쓰므로 **sim 상태 라인에 실린다**. 각도는 `req.direction` → `DirectionalLinear` 전진 →
  `SweepHitMath.SegmentHits` 의 `<= hitRadius²` 로 간다.
- 구 sim 호출처도 정확히 2개(`BallisticArc.cs:17` · `Emission/PatternDirection.cs:13`)이고
  **둘 다 `[BurstCompile]` 시스템이 소비**한다. Burst 는 초월함수를 libm 호출이 아니라
  **자기 구현으로 인트린식화**한다.

⇒ **unit 20 의 A/B parity 는 Burst-sin 대 libm-sin 을 비교하게 된다.** `SimMathParityTests` 는
EditMode(관리 경로)라 libm 대 libm 이 되어 이것을 볼 수 없고, 애초에 `Sin` 을 테스트하지 않는다.

**갈리는 시나리오**: 패턴 emitter 가 각도로 발사 → 서버와 클라의 `sin` 이 1 ULP 다름 →
그 프레임 스윕 선분이 `hitRadius²` 로부터 ~1e-7 안에 있는 적을 지남 → **서버 명중, 클라 빗나감.**
이후 두 판은 공유 상태가 없다.

**처분: 18-J 착수 전에 정책을 정한다.** 지금 호출처가 2개인데 18-J 는 기믹·보스·**도약**이라
아치 호출처가 늘어날 자리다. 늘어난 뒤에 정하는 건 순수하게 더 비싸다. 선택지 셋 →
[`m1_unit18_replan.md`](m1_unit18_replan.md) N1.

### F3. `SimMathParityTests` 가 문서가 주장하는 것보다 좁다 (HIGH)

세 파일이 비트 게이트를 주장하는데(`SimMath.cs:18` · `SimVec.cs:17-18` · `SimRandom.cs:16`)
실제 커버리지에 없는 것:

| 미검증 | 하중 |
|---|---|
| `Sin` `Cos` `SinCos` `Radians` `PI` | F2 |
| **`SimVec2` 오버로드 전부** | `MovementSystem.cs:183` 의 `NormalizeSafe(SimVec2)` = **모든 이동 유닛의 스텝 방향**(#17 "위치 갱신 단일 권한") |
| `SimRandom.CreateFromIndex` | `EmissionLogic.cs:101` — 모든 무작위 발사 패턴의 시드 |

**심각도 근거는 코드가 아니라 문서다.** 다음 세션이 `SimMath.cs:18` 을 읽고 "비트 게이트가
지켜준다" 고 믿은 채 출하한다.

⚠ **`Unity.Mathematics` 를 링크할 수 있는 창은 unit 20 에 닫힌다**(`SimVec.cs:11-14` 가 스스로
하는 논증). 그 논증은 미검증 표면에도 똑같이 적용된다 — F2 와 **같은 패스에서** 확장한다.

### F4. 성능 게이트가 엉뚱한 클러스터를 겨눈다 (HIGH)

`SimModifierPerfProbeTests.cs:70` 이 `perTickBytes < 1024` 를 단정하고 메시지까지 정확하다
(*"시스템 루프 안의 new/람다/박싱이 흔한 원인"*). 계측기 자신을 먼저 검증하는 것(`:38-44`)도 좋다.

**그런데 `:157` 이 돌리는 건 `ModifierCluster` 하나뿐이고, 직접 확인 결과 그 클러스터의
`SimCommandBuffer` 사용은 0 이다.** 실사용 6개 시스템(`ProjectileHit`·`ProjectileMove`·
`ProjectileEmitter`·`DamageApplication`·`LifecycleSystems`·`ResignationDrop`)이 전부 게이트 밖.

`SimCommandBuffer.cs:25-39` 는 op 당 display class + `Action<SimWorld>` = **힙 할당 2**.
호출처가 전부 hot path 다(착탄마다·투사체 만료마다·발사 1발마다·파괴 6지점).
구 sim 은 `EntityCommandBuffer(Allocator.Temp)` 로 **GC 할당 0** 이었다 — 타입 자신의 주석이
그렇게 적어 놨다. 모바일/IL2CPP 기준 **이식으로 생긴 가장 큰 거동 회귀**다.

**처분**: 할당 제거 + 게이트를 `ProjectileCluster`·`DamageCluster` 로 확장. 게이트를 먼저
확장하면 스위트가 빨개지므로 **같은 작업 단위**로 묶는다.

### F5. `SimTransform.Scale` 기본값 0 이 잠복 함정 (MEDIUM)

구 `LocalTransform` 은 어떤 경로로 만들어도 `Scale == 1`. 신 sim 은 `FromPosition` 팩토리만
1 을 넣고, `new SimTransform { Position = p }` 한 줄이면 0 이다.
`ProjectileHitSystem.cs:475`(`FlashVictim`)가 `xf.Scale` 을 `originalScale` 로 굽고 #24(18-J)가
그 값으로 복원하므로, **스폰 배선(18-K)이 팩토리를 안 쓰면 첫 피격 후 유닛이 영구 스케일 0.**

현 코드베이스 grep 상 직접 초기화 0건이라 지금은 잠재. **계약이 주석에만 있고 타입이 강제하지
않는다** — 18-K 배선 시점의 함정이라 그 단위의 완료 기준에 넣는다.

### F6. #18 `HazardCast` 등록 누락 위험 (MEDIUM)

두 렌즈가 지적. 이식은 끝났는데 `SimStep(18, ...)` 을 yield 하는 클러스터가 없다.
18-I/2 가 #33 클러스터를 만들 때 빠뜨리면 **캐스터형 방어유닛이 해저드를 안 깔고 18-K 골든에서야
드러난다.** `SimPipeline` 은 번호 **중복**만 막고 **누락**은 못 막는다.

**처분**: 18-I/2 완료 기준에 포함 + 18-K 에 **{1..44} 전수 등록 단정 테스트** 추가.

---

## 문서 정합성 — 코드보다 문서가 틀린 것

| # | 어긋난 곳 | 처분 |
|---|---|---|
| D1 | `README.md:155`(Editor/IL2CPP/**CoreCLR**) vs `20_ab_parity_swap.md:42`(Editor/IL2CPP) — CoreCLR 이 사라졌다. **권위 sim 을 실제로 호스팅할 런타임**이 게이트에서 빠진 것 | 한쪽을 고른다. M1 이 **서버측** 이식 가능성을 증명하는가 클라측만인가를 가르는 줄 |
| D2 | `SimMath.cs:18`·`SimVec.cs:17-18`·`SimRandom.cs:16` 의 비트 게이트 주장 vs 실제 커버리지 | F3 와 같은 패스 |
| D3 | `4_legacy_trace_golden.md`·`20_ab_parity_swap.md:28` 의 **동률 예외 목록이 stale**. 신 sim 이 이미 닫은 것들이 남아 있다(`HazardTypes.cs:58-66` 역-삽입순 명시 · `HazardCastSystem.cs:96-98` simId tie-break 신설) | ⚠ **위험이 반대 방향이다** — 비교기를 stale 목록으로 짜면 **진짜 회귀를 로그 한 줄로 격하**시킨다. unit 20 비교기 작성 **전에** 이식된 코드에서 목록 재유도 |
| D4 | `ProjectileMath.cs:22-24` *"sim 은 XZ 만 굴린다"* vs `ProjectileMoveSystem.cs:162,215` 가 사인을 `p.y` 에 굽는 `ArcPosition` 호출 | 주석이 코드가 안 하는 의도를 서술. **구 sim 에서도 이미 그랬다**(충실한 이식) — F2 정책 결정 시 함께 정정 |
| D5 | `20_ab_parity_swap.md:21-27` 이 `LocalTransform.Scale` 만 제외 vs `HitFlashTag`(`remaining`/`duration`/`originalScale`) 는 평범한 sim 컴포넌트라 해시에 잔존 | 제외 목록을 넓히면 spec 이 *"완료 기준을 낮추는 것"*(`:26-27`)이라 못박은 것과 충돌. 18-K 살베지 판정에서 결론 |
| D6 | `m1_unit18_handoff.md` 617줄 vs CLAUDE.md 의 *"30~80줄, source of truth 가 아니라 지도"* | 8배 초과. 인계가 계약을 이중화하기 시작했다 — 압축 |

---

## 미채택 — 이유와 함께

| 항목 | 왜 안 다루나 |
|---|---|
| `SimWorld._order` 무한 증가(O(생성 이력) 스캔) | **실재한다.** 다만 `_order` 에서 지우면 순회 순서 계약이 걸린 곳이 6군데라 지금 손대는 게 더 위험하다. 판 **길이**에 비례하는 비용이라 EditMode 프로브가 못 잡는 것도 사실 — **18-K 성능 재측정 항목**으로 이관 |
| `ModifierAuthoring` / `SimModifierAuthoring` 바이트 동일 중복 | 실재. `Sim` 접두사 정리(unit 20)와 같은 축이라 그때 한 번에 — 다만 **정책이 갈릴 수 있다**는 지적이 맞으므로 일치 단정 테스트를 F3 패스에 끼워 넣는다 |
| `bounceDamageMul` 가드 누락(형제 둘은 가드됨) | 실재하고 싸다. 다만 **구 sim 도 동일하게 무방비**라 고치면 골든이 갈릴 수 있다 — 18-L 이후 |
| `MovementSystem.StepChase` 전체 그리드 복사 · `FlowFieldBuilder` 큐 할당 | 성능. F4 와 같은 패스에서 측정 후 판단 |
| float→int 캐스트 아키텍처 차이 · IL2CPP FMA contraction | **도달 경로 미증명**. 리뷰어 스스로 "증명된 결함이 아니라 살아 있는 위험" 이라 표기. F2 정책 결정 시 같은 축으로 검토 |
| I2("누가 sim 을 부르나") 미집행 | 계획서 `m1_unit18_plan.md:312` 가 이미 잡아 뒀다. 중복 |

---

## 렌즈별 총평 (원문 요지)

- **ECS 경계**: 맥락 경계 위반 **0**. `world.Set`/`RemoveComponent`/`AddBuffer` 전수를 폴더별로
  대조한 결과 소유권이 지켜진다. 등록된 32 시스템의 캡처 번호·phase 가 청사진과 정확히 일치.
  같은-틱/1틱-지연이 뒤바뀐 채널 쌍 **0**. 게이트 53 처분 중 증발하면 안 되는 것 **없음**.
  ⚠ 다만 **물리적 강제가 사라졌다** — 구 sim 은 타입 시스템이 경계를 강제했고 신 sim 은
  주석과 리뷰에만 의존한다. 구조적 한계이지 현재 코드의 결함은 아니다.
- **Mono 코드품질**: CRITICAL 0. 제약 8·10 위반 **없음**(과잉 추상화를 특별히 찾았고 없었다 —
  이 라이브러리의 문제는 반대쪽, 중복과 인라인 미러). 주석은 *"이례적으로 잘 쓴 편"* 이고
  stale 1건뿐. 다만 **주석 3개 중 1개가 spec 저장소를 열어야 해소된다** — 구 sim 을 지우고
  그 문서 유지가 멈추는 날 고고학이 된다.
- **서버권위**: *"구조적 결정론은 좋다. 수치적 결정론은 아니고, 그 구멍이 현재 계획의
  **모든 게이트에서 보이지 않는 곳**에 몰려 있다."* 주변 비결정성 유입(`DateTime`·`Random`·
  `Guid`·스레드·정적 가변) **0건**. 해시 컨테이너 순회 5곳 전수 확인 결과 결과에 닿는 것 없음.
  명시적 tie-break 6곳 보존.
