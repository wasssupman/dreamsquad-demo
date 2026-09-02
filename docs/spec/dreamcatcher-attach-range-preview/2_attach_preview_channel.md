# 2 — 부착 프리뷰 채널 (브리지 owner + 뷰 링 전용 경로)

> 선행: unit 0(`SetAreaRange`) · unit 1(`DcRangeSpec`). 사용자 결정 Q4(별도 색)·Q5(무효 = 표시 0).

## 목적

host 유닛의 **몸 중심**에 카탈로그가 준 원을 그리는 채널을 만든다. 배치 링·스킬 조준과 같은
캐리어(`_rangeRing`)를 쓰고 소유권만 새 owner 로 가른다. 그리는 위치는 매 프레임 host 의 sim 위치에서
다시 읽는다 — 재배치는 Entity 를 보존하고 셀만 바꾸므로 **Entity 가 앵커**다.

## 변경 대상

- `Bridge/BattleBridge.cs:7905` — `RangeDisplayOwner` 에 `AttachPreview` 추가.
- `Bridge/BattleBridge.cs:7920 SetRangeOwner` — `next != AttachPreview` 이면 `_attachPreviewLive = false`
  (배치 마크의 `_placementMarkLive` 와 나란히). Placement 유효성 면제는 **상속하지 않는다**.
- `Bridge/BattleBridge.cs` — 신설
  ```csharp
  public void SetAttachPreview(Entity host, Wassup.Core.DcRangeSpec spec, Color color);
  public void ClearAttachPreview();   // => ClearRange(RangeDisplayOwner.AttachPreview)
  ```
  상태 4개: `_attachPreviewLive` · `_attachPreviewHost` · `_attachPreviewSpec` · `_attachPreviewColor`.
- `Bridge/BattleBridge.cs:3471` LateUpdate — `SyncMonoUnitViews()` 뒤, 배치 마크 갱신 옆에
  `if (_attachPreviewLive && _rangeOwner == AttachPreview) RedrawAttachPreview();`.
- `UI/Dreamcatcher/DreamcatcherFocusConfig.cs` — `public Color attachRangeColor` 신설(시안 계열,
  리티클 `reticleColor` 와 같은 가족 · 라임 `rangeColor` 와 색상으로 갈림). `.asset` 에 명시 저작.
- `Core/TilemapMapView.cs` — unit 0 의 `SetAreaRange(center, radius, color)` 그대로 사용. 추가 없음.

## 구현

- **`SetAttachPreview` 순서**: `spec.shape == None` → `ClearAttachPreview()` 후 **return** (채널을
  건드리지 않는다 — 계약 3). 아니면 상태 저장 → `RedrawAttachPreview()` → `SetRangeOwner(AttachPreview)`.
  배치 경로와 같은 「그리기 → owner」 순서.
- **`RedrawAttachPreview`**: `HasLiveEntityManager()` + `_em.Exists(host)` + `LocalTransform` 읽기 →
  타일 좌표 `((p.x − origin.x)/tileSize, (p.z − origin.z)/tileSize)` → `tilemapMapView.SetAreaRange(...)`.
  sim 위치는 이미 **기하 중심**(unit 10)이라 footprint 오프셋을 더하지 않는다(더하면 이중 계산 —
  `TryGetDefenderRestViewPos` 주석). `ToView` 는 쓰지 않는다(링은 타일 좌표를 직접 받는다).
  host 가 사라졌으면(`Exists` false) `ClearAttachPreview()`.
- **재배치 추종**은 LateUpdate 재읽기로 자연히 된다. 드래그 중 host 의 sim 위치는 보통 고정이지만
  Entity 기준이라 셀 재키잉(`BattleBridge.Relocation.cs:181`)에 무관.
- **유효성**: `SetPlacementRangeValidity` 는 배치 전용이라 부르지 않는다. 무효 락온은 unit 3 이
  호출 자체를 안 한다(Q5).
- 타깃 마크(누가 맞나)는 그리지 않는다 — 발동은 트리거 시점이라 지금 서 있는 적을 표시하면 거짓이다.

## 완료 기준

- [ ] `SetAttachPreview(host, Circle r)` 후 링이 host 몸 중심에 반경 r 로 뜬다(Editor Play 수동 1회 —
      1×1 이면 셀 중심, 값 `_Range = r`, `_HalfExtent = 0`, 색 = `attachRangeColor`).
- [ ] `spec.shape == None` 호출은 `_rangeOwner` 를 바꾸지 않는다(브리지 단위 테스트 또는 로그 단언).
- [ ] `ClearAttachPreview()` 는 소유자가 `AttachPreview` 일 때만 지운다(`ClearRange` 가드 그대로).
- [ ] 다른 owner 획득(배치 드래그 시작 등) 시 `_attachPreviewLive` 가 꺼진다.
- [ ] host 엔티티 파괴 후 다음 LateUpdate 에 링이 사라진다.
- [ ] Subway 맵에서 host 중심과 링 중심 일치(StreetDay 는 오차가 마크 반경 아래로 숨는다 —
      `RefreshRangeTargetMarks` 주석).
- [ ] sim 파일 변경 0 · 골든 바이트 무변.
