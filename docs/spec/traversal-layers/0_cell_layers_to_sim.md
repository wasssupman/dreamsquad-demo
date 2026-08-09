# unit 0 — 셀 층 비트를 sim 으로 전달 (행동 변화 0)

## 목적

**sim 이 «이 칸이 어떤 종류인가»를 볼 수 있게 한다.**

지금 sim 의 모든 경로 계산은 `walkMask` 1비트(= `tiles == Walk`)만 안다. 셀의 층 비트필드는 **이미 존재하지만**(`placeMask` — `Ground`/`Path`) `GeneratedMap`(Mono 쪽)에만 있고 sim 으로 넘어오지 않는다. 이것이 README §0-2 가 짚은 **유일한 진짜 갭**이다.

이 unit 은 **값을 넘기기만 한다.** 소비자는 0 이다 — 읽는 쪽은 unit 2b 부터다.

## 왜 새 배열을 만들지 않나

rev 2 계약 1: **셀 층 비트필드는 한 벌을 배치·통행이 공유한다.** `placeMask` 는 이름만 «배치»이고 내용은 «칸의 종류»다 — `PlacementLayer.cs:8` 이 *"층 이름은 **공간** 기준(어떤 종류의 칸인가)이지 유닛 클래스 기준이 아니다"* 라고 스스로 못박는다.

셀 배열을 두 벌 만들면 저작·페인터·직렬화·파생 폴백이 전부 두 벌이 되는데, «배치 층»과 «통행 층»이 서로 달라야 할 칸의 예가 지금 하나도 없다(제약 8).

## 변경 대상

- 수정: `Assets/_Project/Scripts/Battle/Effects/FlowFieldSingleton.cs` — `cellLayers` 필드 + `Dispose`
- 수정: `Assets/_Project/Scripts/Bridge/SimFieldInstaller.cs` — 설치 시 함께 복사
- 신규: `Assets/_Project/Tests/EditMode/CellLayersInstallTests.cs`

## 구현

### 1. 싱글턴에 자리 하나

`walkMask` 바로 옆에 같은 수명으로 둔다.

```csharp
public NativeArray<byte> cellLayers;   // 셀이 여는 PlacementLayer 비트
```

`IsCreated` 불변식에는 **넣지 않는다** — `walkMask`·`goals` 와 같은 이유다(픽스처가 뒤집히는 걸 막는다). `Dispose` 에는 넣는다.

### 2. 설치 — `walkMask` 를 만드는 그 루프에서

`SimFieldInstaller` 는 `walkMask` 를 만드는 **유일한 지점**이다(`:58`). 같은 루프에서 층도 채운다.

```
cellLayers[i] = map.placeMask.IsCreated
    ? PlacementLayers.Sanitize(map.placeMask[i])
    : PlacementLayers.Derive(map.tiles[i]);      // 방어적 폴백
```

폴백을 두는 이유: 빌더 산출물 불변식은 «`IsCreated` ⇒ `placeMask` 생성됨»이지만, 픽스처와 `BuildFallbackLinear`(네 번째 맵 생산자) 경로까지 그 불변식을 강제하지 않는다. 파생은 런타임과 **같은 단일 정의**(`PlacementLayers.Derive`)를 쓴다.

**소유권**: `walkMask` 와 동일 — 실패 시 `catch` 에서 직접 dispose, 성공 시 싱글턴으로 이관해 `Teardown` 이 회수한다. 여기서 새 라이프사이클을 만들지 않는다.

### 3. 아직 아무도 읽지 않는다

`walkMask` 도 `cellLayers` 도 이 unit 에서는 소비되지 않는다. **`walk[i] = (tiles[i] == Walk)` 를 건드리지 않는다** — 바꾸면 행동 변화 0 이 깨진다.

## 완료 기준

- [x] compile 에러 0 · EditMode **2008 중 2005 통과 · 실패 0** (나머지 3은 기존 `[Ignore]`). 기존 테스트 기대값 갱신 **0건** — 행동 변화 0 의 증거
- [x] 신규 `CellLayersInstallTests` 7건: ① 저작본이 정본 ② `Sanitize` 로 미정의 비트 제거 ③ 저작본 부재 시 `tiles` 파생 폴백 ④ 길이가 `walkMask` 와 같다 ⑤ **`walkMask` 산출 무변경**(회귀 축) ⑥ `Teardown` 이 실제로 해제 ⑦ `Teardown` 멱등

  ⚠ ⑥에서 함정을 하나 밟았다 — `IsCreated` 로는 해제를 확인할 수 없다. 테스트가 들고 있는 건 컴포넌트 **struct 의 복사본**이고 `NativeArray.IsCreated` 는 그 복사본의 버퍼 포인터만 보므로, 원본이 `Dispose` 돼도 계속 `true` 다. 실제 해제는 **stale 복사본 접근이 던지는가**로 확인해야 한다.

---

**완료 기준 확인**: (미확인)
