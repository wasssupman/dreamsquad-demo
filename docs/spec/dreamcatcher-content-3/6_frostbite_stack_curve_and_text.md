# 6 — 동상 계단 감속 + 스택 임계 문안 (rev of unit 1)

## 목적

unit 1 의 동상은 3중첩에서만 감속이 걸리고, 카드 문안은 `공격마다 → 대상에게 빙결 1스택 · 4초` 로 끝나 **임계 효과(3중첩 둔화 / 5중첩 기절)가 어디에도 표시되지 않았다**. 중첩 표시(오버헤드 아이콘)도 v1 제외였으므로 플레이어에게는 카드가 아무 일도 안 하는 것처럼 보인다.

두 가지를 바꾼다:

1. **1중첩부터 계단식 감속** — 중첩이 쌓이는 게 즉시 체감된다 (사용자 결정 2026-07-31).
2. **스택 임계를 카드 문안에 노출** — `StackModifierSO` 의 규칙을 formatter 가 읽어 요약 라인을 만든다.

## 변경 대상

- `Assets/_Project/Data/Dreamcatcher/StackModifier_Ice.asset` — thresholds 재작성
- `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs` — `DcPayloadSpec.stackModifier` (SO 참조) 추가
- `Assets/_Project/Data/Dreamcatcher/Card_Frostbite.asset` · `Card_EmberBite.asset` — 참조 연결
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardText.cs` — 임계 요약 라인 생성
- `Assets/_Project/Scripts/Battle/Effects/Modifiers/StackModifierTickSystem.cs` — 파생 스탯 슬롯 분리
- `Assets/_Project/Tests/EditMode/DreamcatcherCardTextTests.cs`

## 구현

### 1. 감속 곡선 (StackModifier_Ice)

| atStack | mode | derived | 값 |
|---|---|---|---|
| 1~4 | Edge | ApplyStat MoveSpeedMul ×0.9 / ×0.8 / ×0.7 / ×0.6 | duration 4 |
| 5 | Consume | ApplyStun | magnitude 1(초) |

감속 duration 은 **스택 지속(perAppDuration=4)과 같게** 맞춘다. unit 1 의 1.5초는 다음 중첩 전에 끊겨 "느려졌다 풀렸다"가 반복됐다. 병합 키가 동일해 상위 중첩이 하위 magnitude 를 덮어쓰므로(`ModifierApplySystem.ApplyStat`) 계단은 자동으로 최신 중첩 값만 남는다.

### 2. formatter 가 임계를 읽는 경로

`DreamcatcherCardText` 는 payload 만 보는 순수 함수라 임계 규칙을 모른다. 수치를 문자열에 박으면 제약 6 위반이므로 **payload 에 `StackModifierSO` 참조를 추가**한다 — `projectile` / `auraPrefab` / `pattern` 과 같은 선례이고, 정의 계층이 Entities 타입을 얻지는 않는다.

런타임 임계 조회는 **바뀌지 않는다**. `BattleBridge.BuildStackThresholdRegistry` 의 kind→rules 레지스트리가 그대로 권위이고, payload 의 참조는 **문안 전용**이다. 즉 SO 하나가 두 소비자(sim·UI)의 단일 소스가 되고 씬 배선(`stackModifierAuthoring`)의 역할도 그대로다. null 이면 요약 라인만 생략된다(기존 동작).

### 3. 요약 라인 규칙

`ThresholdRule[]` 을 순회해 한 줄로 접는다. ApplyStat 이 **1부터 연속·등차**면 계단으로 접고(`중첩당 이동 속도 -10%`), 아니면 중첩별로 나열한다. 판정·환산은 순수 static 으로 분리하고 EditMode 로 검증한다(제약 10 — 분기 있고 sim 수치 표기라 회귀 가치 있음).

```
공격마다 → 대상에게 빙결 1스택 · 4초
빙결 중첩당 이동 속도 -10% · 5중첩 기절 1초 (중첩 소모)
```

- CC 어휘는 기존 `CcLabel` 과 통일한다 — 심에서 5중첩 파생은 `CcKind.Stun` 이므로 "동결"이 아니라 **기절**로 쓴다. 별도 상태로 오해시키지 않는다.
- DoT 파생은 초당 피해로 환산해 적는다(`tickInterval>0` 이면 magnitude 는 틱당 피해). 화상물기: `출혈 5중첩 → 초당 피해 10 · 4.85초 (중첩 소모)`.
- Consume 은 `(중첩 소모)` 로 표기한다 — 리셋 후 재누적이 이 카드들의 사이클이라 생략하면 주기가 안 읽힌다.

### 4. 파생 감속 슬롯 분리

`StackModifierTickSystem` 의 ApplyStat 은 `source=피해자, stackId=0` 이라 `BattleBridge.EnqueueMoveSpeedMul`(배치/스킬 감속)과 병합 키 4개가 전부 겹친다. 1중첩부터 상시 감속이 되면 충돌 빈도가 급증하므로 **스택 파생은 kind 별 전용 stackId** 를 쓴다. 존 감속(`source=Entity.Null`)은 원래 별도 슬롯이라 무영향.

### 5. `frost_arrow` 재명명 (작업 중 발견)

"빙결"은 `StackKind.Ice` 의 표시 라벨과 **같은 단어**였다. 동상이 쌓는 것이 "빙결 1스택"이라,
카드 선택 화면에서 동상을 찾다가 `frost_arrow`(기절 0.6초)를 여는 충돌이 실제로 발생했다.
시트(`DcCards`)에는 이미 "스턴메이커"가 authoring 돼 있고 SO 만 옛 값이었다 — SO 를 시트에 맞춘다.
스택 라벨 "빙결"은 유지한다(사용자 결정 2026-07-31). 상세는
`docs/spec/dreamcatcher-data-hygiene/3_abbreviated_display_names.md` 각주.

## 완료 기준

- [x] EditMode 전체 green (문안 테스트 갱신 포함) — 1662건 · 실패 0 · skip 2(기존 `[Ignore]`)
- [ ] 덱빌더/툴팁/손패에서 동상·화상물기 카드에 임계 요약 라인이 보인다
- [ ] Play: 1타부터 눈에 띄게 감속, 중첩마다 더 느려지고 5타에서 기절 후 재누적

**미해결 드리프트(이 spec 범위 밖)**: `DcCards` 시트와 SO 의 displayName 이 2건 더 어긋나 있다 —
`poke_needle`(에셋 "비수" / 시트 "불나방"), `bouncy_bead`(에셋 "튕구슬" / 시트 "바운스샷").
임포터를 돌리면 함께 정리된다.
