# 0 · 배치 — 머리 위 앵커(view-space) + 카메라축 점유 격자 겹침 방지

## 목적

두 배치 결함을 함께 해결한다. 코덱스 에셋 의존 없음 — 즉시 구현 가능.

1. **머리 위 앵커**: 현재 숫자는 유닛 몸통 중하단에 깔려 가려진다. 유닛 머리 언저리 위부터 떠서 위로 드리프트하게 한다.
2. **겹침 방지 격자**: 동시 다발 데미지(단일 적 다단 히트, 근접 다수 타겟 AoE)가 겹치지 않고 화면상 느슨한 격자로 배열되게 한다.

## 좌표계 전제 (critic 반영 — 반드시 준수)

- `BoardSpace.ToView(simWorld)` 는 **sim-Y(높이)를 버리고** `x`,`z` 만 써서 tilted 타일맵 보드 평면 위 world 점을 돌려준다(`BoardSpace.cs`). 따라서:
  - **머리 앵커를 sim-Y 에 더하면 화면에서 무효**다. 앵커는 **ToView 이후 view 공간에서** 올려야 한다.
  - 보드 평면의 world X/Y 로 격자를 짜면 카메라 pitch(Battle 58°)에 따라 세로가 `cos(pitch)` 로 압축되고 깊이(world-Z)가 무시돼 **화면 겹침을 보장 못 한다**. 격자는 **카메라 빌보드 축(camera.right / camera.up) 투영**으로 짠다.
- 데미지 숫자는 **Battle 페이즈 전용**(전투 중에만 발생) → 그 수명 동안 카메라 pitch 는 고정. 페이즈 간 pitch 변동은 숫자 수명과 무관하므로 라이브 재계산 불필요. 단 basis 는 매 스폰 시 현재 카메라에서 읽는다(하드 베이크 금지).

## 변경 대상

- `Assets/_Project/Scripts/Presentation/DamageNumberSpawner.cs` — 앵커 계산(view-space)·카메라축 점유 격자·monotonic 스폰 카운터 소유
- `Assets/_Project/Scripts/Presentation/DamageNumberView.cs` — **`Play` 가 view-space 위치를 그대로 받도록 변경(내부 `ToView` 재적용 제거)** · spawn index 수신 · 반납/`OnDisable` 시 셀 해제 콜백
- `Assets/_Project/Scripts/Presentation/DamageNumberStyle.cs` — `headViewOffset`, `cellSize`, `maxSearchRings` 직렬화 필드(스포너 bare float 대신 Style 로)
- (테스트) `Assets/_Project/Tests/EditMode/DamageNumberPlacementTests.cs` — `FindFreeCell` 순수 함수 회귀

## 구현

### 변환 소유 (이중 ToView 제거)

- `ToView` 는 **스포너 1곳**에서만. 스포너가 `viewPos = BoardSpace.ToView(worldPos)` 로 발치 view 위치를 구하고, 앵커·격자 오프셋까지 얹은 **최종 view-space 위치**를 `View.Play(finalViewPos, …)` 로 넘긴다.
- `DamageNumberView.Play` 는 받은 위치를 **그대로 `_startPos` 로 사용**(현 `_startPos = ToView(worldPos)` 제거). 재변환 금지 — 안 하면 이중 변환으로 위치가 깨진다.

### 머리 위 앵커 (view-space)

- 앵커 = `viewPos + Vector3.up * headViewOffset`. **ToView 이후 world-up** 으로 올린다(sim-Y 는 ToView 가 버리므로 무효 함정 회피). 기존 `driftUp`(line 72, 동일 `Vector3.up` 축)과 축을 맞춰 리프트↔드리프트 kink 방지. 이후 driftUp 이 추가로 올린다. (격자 오프셋만 카메라축 — 아래 별도.)
- `headViewOffset` 은 `DamageNumberStyle` 직렬화 필드(placeholder 기본값 Play 튜닝). 유닛별 실제 키는 이벤트 payload 에 없으므로 **단일 고정 view 오프셋**으로 처리한다. 대다수 적/디펜더가 유사 스케일이라 충분. 보스/거대 유닛 정밀 앵커는 후속 후보(뷰 bounds 해석 경로).
- 완료 기준은 "정확한 정수리"가 아니라 **"대표 유닛에서 몸통 중하단을 덮지 않고 머리 위에서 뜬다"** 로 검증(고정 오프셋의 한계 정직화).

### 카메라축 점유 격자

- 스포너가 점유 집합 소유: `HashSet<Vector2Int>`(점유 셀) + `Dictionary<DamageNumberView, Vector2Int>`(뷰→셀).
- 셀 좌표 = 카메라축 투영: `u = dot(finalViewPos, camRight)`, `v = dot(finalViewPos, camUp)` → `cell = (floor(u/cellSize.x), floor(v/cellSize.y))`. 화면 정렬 격자.
- 점유 시 **고정 나선 순서**로 가장 가까운 빈 셀 탐색, 셀 중심으로 위치를 카메라축 오프셋(`center += du*camRight + dv*camUp`) 이동. 최대 `maxSearchRings`, 전부 차면 의도 셀 유지(degenerate).
- **나선은 위쪽(+v) 우선 편향** — 나선 오프셋 상수 배열의 순서로 인코딩(분기 없이). 숫자가 몸통 아래로 안 내려감.
- `cellSize` 기본값은 **렌더 글리프 footprint 기준**으로 잡는다: 최대 폰트 크기(≈11.7)·최대 자릿수(4~5자리)에서 겹치지 않을 폭. 근거 없는 임의값 금지 — Play 에서 4자리 숫자로 비겹침 검증하고 측정값 기록.

### 결정론 index

- 스포너에 **monotonic 스폰 카운터**(`int _spawnSeq`, `StartBattle`/세션 시작 시 0 리셋). 매 `Spawn` 에 `index = _spawnSeq++`. `Play(pos, amount, index)` 로 전달.
- 나선 탐색 tie-break·(unit 1)셰이크 방향·미세 회전은 **오직 index** 로 결정(seeded RNG·frame-count 등 시간 소스 금지 — 프로젝트 구조적 결정론 원칙).

### 셀 누수 방지

- 셀 해제는 **멱등**: `DamageNumberView` 가 자연 종료(`Finish`→콜백)뿐 아니라 **`OnDisable`** 에서도 완료 콜백을 호출(이미 종료면 no-op). 강제 비활성/도메인 리로드/`StopBattle`(자식 비활성) 시 셀 고아 방지.
- 스포너의 `active dict` 은 **self-healing**: 완료 콜백(`OnComplete`)이 셀을 `_occupied` 에서 제거 + dict 에서 제거. 뷰가 자연 종료·OnDisable 어느 쪽이든 콜백 경유로 셀이 풀린다. 데미지 숫자 수명(<1s)이 배틀 경계보다 짧아 별도 BattleBridge teardown clear 는 불필요(결합 회피). 반복 전투 후 집합 크기 = 활성 뷰 수.

## 완료 기준

- compile 성공, 콘솔 에러 0.
- Play: 데미지 숫자가 대표 유닛 **머리 위에서 뜨고 위로 올라간다** — 몸통 중하단을 덮지 않는다(스크린샷). sim-Y 무효 함정을 피해 실제로 화면에서 올라감을 확인.
- Play: 단일 적 다단 히트 + 근접 다수 AoE 에서 숫자가 **겹치지 않고 화면 격자로 위쪽으로 퍼진다**. **4자리 숫자**로도 인접 셀 비겹침 확인.
- 풀 반납/`OnDisable`/`StopBattle` 후 점유 집합 누수 0(반복 전투 후 집합 크기 = 활성 뷰 수).
- `DamageNumberPlacementTests`: 점유 셀 집합·의도 셀 입력에 `FindFreeCell` 이 겹치지 않는 가장 가까운 셀을 결정론적으로 반환(RNG/시간 미사용). run_tests 통과.

---

- **검증 2026-07-07**: compile 에러 0 · `DamageNumberPlacementTests` **7/7 통과** (MCP). 머리위/격자 육안 튜닝(headViewOffset·cellSize)은 unit 1 과 함께 Play 세션에서 확정 예정 — 값은 `DamageNumberStyle` 직렬화라 Play 중 실시간 조정.
