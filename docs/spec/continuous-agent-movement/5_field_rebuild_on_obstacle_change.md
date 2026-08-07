# unit 5 — 장애물 변경 시 flow field 재빌드 (D1-b)

## 목적

**막으면 돌아간다**를 게임 규칙으로 만든다.

flow field 는 지금까지 맵 빌드 시 1회만 굽는다. 해저드·장애물이 셀을 막아도 필드는 모르고, 적은 장애물 쪽으로 걸어가다 충돌 해결에 막힐 뿐이다. 평활화(unit 7)를 넣으면 이 상태가 **직선으로 처박혀 정체**하고, 오목한 배치에서는 지역 최소값에 갇힌다.

## 변경 대상

- 신규: `Assets/_Project/Scripts/Battle/Effects/FlowFieldRebuildSystem.cs`
- 신규: `Assets/_Project/Tests/EditMode/FlowFieldRebuildTests.cs`
- 수정: `Assets/_Project/Scripts/Battle/Effects/FlowFieldSingleton.cs` — `blockedSignature` 추가
- 수정: `Assets/_Project/Scripts/Battle/Effects/ObstacleSignature.cs` (신규 순수 함수)

## 구현

### 재빌드 주체

**Effects 맥락 ISystem.** `FlowFieldSingleton` 의 *내용* 갱신이므로 소유 맥락이 쓴다 — `DefenderFieldSystem` 이 이미 같은 형태다(할당은 `SimFieldInstaller`, 내용은 시스템).

`[UpdateAfter(ObstacleLifetimeSystem)]` + `[UpdateBefore(MovementSystem)]`.

**할당은 하지 않는다.** 그리드 크기가 안 변하므로 기존 `flow`/`dist` 배열에 **in-place** 로 다시 굽는다. 라이프사이클은 계속 설치자가 소유한다.

### dirty 판정 — 순서 무관이어야 한다

`ObstacleLifetimeSystem` 이 매 프레임 `blockedCells` 를 Clear 후 재수집하므로 "바뀌었나"를 따로 물어야 한다. 판정은 **개수 + 교환법칙 결합(XOR)** 이다:

```
signature = (count << 1) ^ Σ⊕ hash(cell)
```

집합 순회 순서에 의존하면 청크 순서 변화만으로 헛 재빌드가 나고 **결정론이 깨진다**. XOR 은 교환·결합법칙을 만족해 순회 순서와 무관하다.

⚠ 해시 충돌 시 재빌드를 건너뛴다. 셀 좌표는 작은 정수쌍이라 실사용 격자(≤180셀)에서 충돌 확률이 무시 가능하고, 대가는 "한 프레임 늦게 반영"이 아니라 "그 변경을 영영 놓침"이므로 **개수를 시그니처에 포함**해 완화한다.

### 재빌드 입력

정적 마스크(`walkMask`)에서 장애물 셀을 뺀 **합성 마스크**를 Temp 로 만들어 `FlowFieldBuilder.BuildFromSources` 에 넘긴다. `walkMask` 자체는 **덮어쓰지 않는다** — 그건 지형이고 장애물은 별개 층이다.

### `AggroChaseCell` 무효화

어그로된 적의 chase field 는 획득 시 1회 계산해 부착한다. 장애물이 생기면 그 필드가 **낡은 경로**를 가리킨다.

재빌드 시 `Aggroed` + `AggroChaseCell` 을 **함께 제거**한다 → 적은 Marching 으로 돌아가고 다음 히트에 재획득한다. 버퍼만 지우면 `MovementSystem` 의 chase 분기가 필드를 못 찾아 **정지**하므로 안 된다.

이는 (b) 이전에도 있던 결함이고 (b)가 가시화한 것이다.

## 스코프에서 뺀 것

README 가 한때 "`AggroStateSystem` 의 `Allocator.Temp` walk 마스크 재계산을 unit 5 에서 없앤다"고 적었으나 **철회한다.**

합성 마스크를 `FlowFieldSingleton` 에 캐시하면 벽의 진실이 두 곳(캐시 + `NavGrid` 의 프레임 합성)이 되어 unit 1·2 가 세운 **단일 진입점 계약을 깬다**. 얻는 것은 180바이트 Temp 할당 제거뿐이다. 계약을 파는 값으로 너무 싸다.

## 완료 기준

- [ ] compile 통과
- [ ] `ObstacleSignature` 순수 함수 테스트 — 순서 무관 / 개수 반영 / 빈 집합
- [ ] `FlowFieldRebuildTests` — 장애물 추가 시 dist 변화 / 제거 시 원복 / 무변경 프레임엔 재빌드 없음(시그니처 동일) / 완전 봉쇄 시 차단 구역 `int.MaxValue`
- [ ] EditMode 실패 0
- [ ] **봉쇄 시나리오 Play**: 차단 해저드로 경로를 완전히 막으면 적이 벽면에 모여 해저드를 때리고, 파괴 직후 이동이 재개되는가
- [ ] `ecs-reviewer` 통과 (시스템 순서 · 싱글턴 쓰기 · 구조 변경 안전성)

## 주의

완전 봉쇄가 가능하다 — 연결성 가드를 새로 만들지 않는다. 그때 거동은 "적이 벽면에서 차단 해저드를 부순다"이고 `destructible-blocking-hazards`(구현 완료)가 담당한다.

---

**완료 기준 확인**: (미확인)
