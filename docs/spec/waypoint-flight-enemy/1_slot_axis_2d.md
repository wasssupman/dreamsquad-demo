# unit 1 — 슬롯 축 2차원화 (슬롯 = 목적지 × 통행층)

## 목적

라우팅 슬롯의 의미를 «통행층» 에서 **«(목적지, 통행층)»** 으로 넓힌다. 슬롯 1개(골 × Path)면 현행과 동치 — **행동 변화 0.**

## 변경 대상

- `Assets/_Project/Scripts/Battle/Effects/FlowFieldSingleton.cs` — 슬롯별 목적지 테이블 + `SlotFor(dest, layers)`
- `Assets/_Project/Scripts/Bridge/SimFieldInstaller.cs` — 맵 빌드 시 (목적지 × 층) 전 슬롯 굽기
- `Assets/_Project/Scripts/Battle/Effects/FlowFieldRebuildSystem.cs` — `sources` 를 슬롯 루프 **안**으로
- `Assets/_Project/Tests/EditMode/` — 슬롯 조회·재빌드 테스트

## 구현

### 슬롯 정의

```
슬롯 키 = (destCell, maskValue)
destCell = (-1,-1) 센티널 = 골(멀티 소스, goals 배열 전체)
         = 그 외        = 웨이포인트 셀 하나(단일 소스)
```

- `FlowFieldSingleton` 에 `NativeArray<int2> destCells`(슬롯별) 추가. `maskValues` 와 나란히 선다.
- **목적지 목록** = [골] + 전 경로의 웨이포인트 셀 **중복 제거**(같은 셀을 두 경로가 쓰면 필드 1벌). 순서 = 저작 순서(경로 인덱스 → 경로 내 인덱스) — 결정론은 저작에서 온다.
- **층 목록** = 이 판 로스터의 `EffectiveTraversalLayers` 합집합(로스터는 필드 설치 시점에 안다 — `BattleBridge:475`·`BuildBriefingWavePlan` 선례). 최소 1개 = `TraversalSlots.DefaultMask`.
- **슬롯 0 = (골, DefaultMask) 고정.** `PrimarySlot` 의 의미가 «골 슬롯»으로 보존되어 기존 소비처(frontmost·블링크·스폰 예고 등 8곳)가 **무수정으로 계약 2를 만족**한다. 이 고정이 이 unit 의 무회귀 핵심이다.

### `SlotFor` 교체 — 완전일치 유지, 축만 추가

```csharp
public int SlotFor(int2 destCell, byte unitLayers)   // 못 찾으면 PrimarySlot
```

모든 호출처는 목적지를 명시하는 2축 API를 사용한다. 교집합 매칭으로의 전환은 traversal-layers D1 그대로 **보류** — 층 값이 합집합에서 나오므로 완전일치가 항상 명중한다. 조용한 폴백 경고는 unit 3(스폰 시 검증)이 담당한다.

### 재빌드 — `sources` 를 루프 안으로

`FlowFieldRebuildSystem.cs:64~67` 이 `sources` 를 루프 밖에서 한 번(항상 골) 계산한다. **이 계산을 슬롯 루프 안으로 옮기고 `destCells[m]` 를 읽는다** — 이것이 이 unit 의 변경 실체다. 골 센티널이면 `goals` 전체, 아니면 그 셀 하나.

⚠ 장애물 오버레이 스킵(Air)은 **unit 4 소관** — 여기서는 전 슬롯이 동일하게 오버레이를 받는다.

### 설치자

`SimFieldInstaller.InstallNavFields` 가 `GeneratedMap.waypointCells` 로 목적지 목록을 만들고 전 슬롯을 초기 굽기 한다. 메모리: 슬롯당 ≈2.2KB(180셀), 목적지 6 × 층 2 = 12슬롯 ≈ 26KB — 판단 요소 아님.

## 완료 기준

- [x] 컴파일 에러 0 · **기존 EditMode 전량 그린** — 경로 없는 맵은 슬롯 1개로 현행과 byte-동치
- [x] 슬롯 조회 테스트: (골, Path)=0 · (웨이포인트, Path)=해당 슬롯 · 미등록 조합 = PrimarySlot
- [x] 재빌드 테스트: 장애물 추가 → 골 슬롯과 웨이포인트 슬롯의 `dist` 가 **각자의 목적지 기준**으로 재계산됨(웨이포인트 슬롯 dist[웨이포인트 셀] == 0)
- [x] 중복 제거 테스트: 두 경로가 같은 셀 공유 → 슬롯 수 증가 없음

완료 확인: 2026-08-11 — Unity 컴파일 에러 0, EditMode 2,117건 중 실패 0
(2,114 통과·기존 Ignore 3). 기존 다중층 테스트의 슬롯 번호 직접 가정은
`SlotFor(GoalSentinel, layer)` 조회로 바꿔 슬롯 0 고정 계약을 명시했다. ecs-reviewer
native lifecycle 점검 반영 후 재검증 완료. 이 문서와 동일 커밋.
