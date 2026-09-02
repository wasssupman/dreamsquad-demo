# 2 — 부착 프리뷰 채널 (브리지 owner + 링 전용 경로)

> 선행: 0b(`SetAreaRange`, `RangeRingStyle`) · unit 1(`DcRangeSpec`). 결정 D1(스타일 3값) · Q5 · D5.

## 목적

host 의 **몸 중심**에 카탈로그가 준 원을 그린다. 캐리어(`_rangeRing`)는 배치 링과 공유하고 소유권만 새 owner.
위치는 매 프레임 host 의 sim 위치에서 다시 읽는다 — 재배치는 Entity 를 보존하고 셀만 바꾸므로 **Entity 가 앵커**다.

## 변경 대상

- `Bridge/BattleBridge.cs:7905` — `RangeDisplayOwner.AttachPreview` 추가.
- `Bridge/BattleBridge.cs:7920 SetRangeOwner` — `next != AttachPreview` 이면 `_attachPreviewLive = false`. Placement
  유효성 면제는 **상속하지 않는다**.
- `Bridge/BattleBridge.cs` — 신설
  ```csharp
  public void SetAttachPreview(Entity host, Wassup.Core.DcRangeSpec spec, in RangeRingStyle style);
  public void ClearAttachPreview();   // => ClearRange(RangeDisplayOwner.AttachPreview)
  ```
  상태: `_attachPreviewLive` · `_attachPreviewHost` · `_attachPreviewSpec` · `_attachPreviewStyle`.
- `Bridge/BattleBridge.cs:3471` LateUpdate — `SyncMonoUnitViews()` 뒤, 배치 마크 옆:
  `if (_attachPreviewLive && _rangeOwner == AttachPreview) RedrawAttachPreview();`
- `UI/Dreamcatcher/DreamcatcherFocusConfig.cs` — `attachRangeStyle`(색 · 채움 알파 · 선 알파, `pulse = false`).
  색은 시안 계열이되 `reticleColor`/`baseRingColor` 와 **다른 값**(밝은 쪽, `confirmPulseColor` 가족). `.asset` 명시 저작.

## 구현

- **`SetAttachPreview` 순서**: ① `spec.shape == None` → `ClearAttachPreview()` 후 return(채널 무접촉, 계약 3).
  ② **`_rangeOwner == Placement` 면 양보** — 그리지도 arm 하지도 않는다(계약 4). ③ 상태 저장 →
  **`SetRangeOwner(AttachPreview)` 먼저** → `RedrawAttachPreview()`. owner 를 먼저 잡는 이유: `ApplyRingTint` 가
  `_rangeInvalid` 를 읽고 그 리셋은 `SetRangeOwner` 가 하므로, 그리기를 먼저 하면 직전 배치가 무효 셀에 있었을 때
  첫 프레임이 채도 저하로 뜬다. 배치 경로가 「그리기 → owner」인 이유(타깃 마크 회수)는 이 채널에 없다.
- **`RedrawAttachPreview`**: 생존 판정은 `TryGetDefenderCell(host, out _)`(= `_defenderByTile` 등재 — pick·열거와
  **같은 술어**). `_em.Exists` 만 보면 사망 연출 중 시체 위에 링이 남는다. 살아 있으면 `LocalTransform`(기하 중심,
  양자화 없음) → 타일 좌표 `((p.x − origin.x)/tileSize, (p.z − origin.z)/tileSize)` → `SetAreaRange(center, r, style)`.
  footprint 오프셋을 **더하지 않는다**(sim 위치가 이미 중심 — 더하면 이중 계산). `ToView` 불필요(링은 타일 좌표).
  죽었으면 `ClearAttachPreview()`.
- 유효성(`SetPlacementRangeValidity`)은 배치 전용 — 부르지 않는다. 무효 락온은 unit 3 이 호출 자체를 안 한다.
- 타깃 마크는 그리지 않는다 — 발동은 트리거 시점이라 지금 서 있는 적 표시는 거짓이다.
- 신규 `.cs` 없음(브리지 partial 수정). `DreamcatcherFocusConfig.asset` 재직렬화 확인.

## 완료 기준

- [ ] **2×2 host**(전 방어유닛)에서 링 중심 = 앵커 + (0.5, 0.5) 타일 = 셀 경계 교점. 스프라이트는 발밑 기준이라
      링 중심과 스프라이트 중앙이 세로로 어긋나는 것은 정상(배치 링과 같은 규약).
- [ ] `spec.shape == None` 호출과 Placement 소유 중 호출은 `_rangeOwner` 를 바꾸지 않는다.
- [ ] `ClearAttachPreview()` 는 소유자가 `AttachPreview` 일 때만 지운다 · 다른 owner 획득 시 `_attachPreviewLive` 꺼짐.
- [ ] 사망 연출 중인 host 에 링이 남지 않는다(다음 LateUpdate 소멸).
- [ ] Subway 맵에서 host 중심 일치(StreetDay 는 오차가 숨는다).
- [ ] sim 파일 변경 0 · 골든 바이트 무변.
