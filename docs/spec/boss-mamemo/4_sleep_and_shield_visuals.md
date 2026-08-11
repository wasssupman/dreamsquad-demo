# 4 — 연출 (수면 표식 · 실드 부여)

## 목적

세 능력이 **화면에서 읽히는지**를 맞춘다. 이 unit 의 코드 변경은 작고, 대부분은 "이미 있는 것을
확인하고 안 만드는" 판정이다.

## 조사 결과 — 만들 것이 거의 없다

| 항목 | 상태 | 근거 |
|---|---|---|
| 수면 표식(Zz) | **이미 진영 무관** | `StatusFxKind.Sleep` 주석이 "적·아군 공통", 소스가 `CcEffect` 버퍼이고 `_ccEffectQuery` 에 진영 컴포넌트가 0개다 |
| 수면 표식 프리팹 | **없는 게 정상** | `StatusFxRegistry` 의 Sleep 엔트리는 `prefab: 0` + `fallbackGlyph: 1`. Aggro·Marked·Stun 도 같은 방식이다 — 폴백 글리프가 이 4종의 **현재 사양**이지 구멍이 아니다 |
| 실드 게이지 | **unit 2 에서 개통됨** | 적 분기의 `shieldRatio` 리터럴 `0f` 를 `ShieldRatioOf` 로 교체 |
| 실드 부여 VFX | **배선 재사용** | 아래 |

## 구현 — 실드 부여는 가디언과 같은 채널을 쓴다

두 arm 이 처음엔 `ProjectileHitEvent` + `slot.projectileDataIndex`(blink·whip 퍼프 컨벤션)로
연출을 쏘게 돼 있었다. 이걸 **`ShieldGrantedEvents`** 로 바꿨다.

- **저작이 0 이 된다** — `ShieldGrantedEventsSingleton` → `BattleBridge.DrainShieldGrantedEvents`
  → `VfxSpawner.SpawnShieldGranted` 가 `shield-guardian-defender` unit 4 에서 **이미 배선돼 있다.**
  전용 `ProjectileData` 를 만들 필요가 없다.
- **같은 사건은 같은 연출** — "실드가 부여됐다" 는 출처(가디언 / 마메모)가 달라도 같은 그림이어야
  플레이어가 배운 것을 재사용한다.

두 arm 모두 **실제로 부여된 발동만** 연출한다(whip 선례 — 효과 없는 연출 금지):
경계 arm 은 버퍼가 있을 때만, 주기 arm 은 `granted > 0` 일 때만 쏜다.

> **대가(수용)**: `ShieldGrantedEvent` 는 `float3 position` 하나만 싣는다 — 진영·host 가 없어
> **아군 실드와 적 실드의 연출을 못 가른다.** 가르려면 필드 1개 추가가 필요하고, 그건 이 spec
> 밖이다. 현재 연출이 placeholder 라 지금은 문제가 되지 않는다.

## 변경 대상

| 파일 | 내용 |
|---|---|
| `Battle/Combat/HealthThresholdSystem.cs` | 실드 연출을 `ShieldGrantedEvents` 로 |
| `Battle/Combat/BossPeriodicTriggerSystem.cs` | 동일 |

`StatusFxRegistry` · `VfxSpawner` · 씬은 **무변경**이다.

## 완료 기준

- [x] 컴파일 에러 0 · PlayMode 5건(자장가 2 · 실드 2 · 적 실드 1) 무회귀
- [x] EditMode 2163 중 2160 통과 · 실패 0 · 스킵 3(전부 기존 `[Ignore]`)
- [x] **PlayMode 전체 기준선 확보** — 101건 중 17건 실패인데 **전부 사전 존재**다.
      unit 1 문서가 "기준선 미확보" 로 남긴 항목을 여기서 닫는다.
      방법: 이 spec 이 만진 세 파일(`AuraPulse`·`BossPeriodicTriggerSystem`·`HealthThresholdSystem`)을
      unit 1 **이전 커밋 버전으로 되돌려** 실패 테스트를 재실행했고, **같은 테스트가 그대로 실패**했다
      (PlacementAura ×3 · StructureLivePlay ×3 · DragCancelZone · DreamCocoon · KindlerFireStack).
      `SceneTransitionSmokeTest` 는 되돌린 판에서 통과 — **flaky** 다.
      또한 units 2·3·4 전후의 전체 실패 목록이 **완전히 동일**했다.
- [ ] **Play 육안(사용자)** — 아래 4가지

| 볼 것 | 판정 |
|---|---|
| 자는 방어유닛 머리에 표식이 뜬다 | 폴백 글리프라 소박한 게 정상. **위치/크기**가 어색하면 `StatusFxRegistry` 의 Sleep 엔트리 `localOffset (0.35, 1.37, 0)` · `scale 0.65` 를 조정 — 그 값은 **적 기준으로 튜닝**된 것이라 방어유닛에서 어긋날 수 있다 |
| 마메모 체력바에 실드가 얹힌다 | unit 2 의 게이지 개통 확인 |
| 호위 잡몹 체력바에도 실드가 보인다 | 악몽의 가호 확인 |
| 실드 부여 순간 VFX 가 뜬다 | 가디언 실드와 같은 그림이면 정상 |

> **연출 판정보다 먼저 볼 것**: 자장가는 표식이 아니라 **사격이 멈추는가**로 판정한다.
> 표식은 보조 신호이고, unit 1 의 검증 질문은 "일을 멈추는가" 다.
