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

- **`SetAttachPreview` 순서**(리뷰 H-2 반영): ① `shape == None` 또는 반경 0 → `ClearAttachPreview()` 후 return
  (채널 무접촉, 계약 3). ② **그릴 수 있는 host 인지 획득 전에** 확인(레지스트리 등재 + `LocalTransform`) — 그릴 수
  없는 host 로 채널을 훔쳤다 반납하면 직전 표기가 한 프레임 사라진다. ③ **owner 가 None 또는 AttachPreview 일 때만
  획득** — Placement 뿐 아니라 **SkillTelegraph 에도 양보**한다. 착탄 예고는 투사체 비행 동안 살아 있고 Defender 카드
  드래그는 `IsAiming` 을 켜지 않아 실제로 겹친다; 훔치면 예고 없이 착탄하고 `ClearSkillTelegraph` 는 owner 불일치로
  복구하지 못한다. ④ 상태 저장 → `SetRangeOwner(AttachPreview)` → `RedrawAttachPreview()`. owner 를 그리기 앞에 잡는
  이유: `ApplyRingTint` 가 `_rangeInvalid` 를 읽고 그 리셋은 `SetRangeOwner` 가 한다.
- **`RedrawAttachPreview`**: 생존 판정은 `TryGetDefenderCell(host, out _)`(= `_defenderByTile` 등재 — pick·열거와
  **같은 술어**). `_em.Exists` 만 보면 사망 연출 중 시체 위에 링이 남는다. 살아 있으면 `LocalTransform`(기하 중심,
  양자화 없음) → 타일 좌표 `((p.x − origin.x)/tileSize, (p.z − origin.z)/tileSize)` → `SetAreaRange(center, r, style)`.
  footprint 오프셋을 **더하지 않는다**(sim 위치가 이미 중심 — 더하면 이중 계산). `ToView` 불필요(링은 타일 좌표).
  죽었으면 `ClearAttachPreview()`.
- 유효성(`SetPlacementRangeValidity`)은 배치 전용 — 부르지 않는다. 무효 락온은 unit 3 이 호출 자체를 안 한다.
- 타깃 마크는 그리지 않는다 — 발동은 트리거 시점이라 지금 서 있는 적 표시는 거짓이다.
- 신규 `.cs` 없음(브리지 partial 수정). `DreamcatcherFocusConfig.asset` 재직렬화 확인.

## 완료 기준

> 구현 커밋 `80c00663`(2026-09-02). 브리지 wiring 이라 EditMode seam 이 없다 — 아래는 unit 4 Play 에서 확인한다.

- [ ] **다칸 host**(2×2 대다수 · 캐논 2×3 · 배스티온 3×2 · 버스터즈 1×2)에서 링 중심 = sim 기하 중심 = 앵커 + ((W−1)/2, (H−1)/2).
      짝수 변은 셀 경계 위에 온다(`AttachRangePreviewTest` 가 캐논으로 검증). 스프라이트는 발밑 기준이라
      링 중심과 스프라이트 중앙이 세로로 어긋나는 것은 정상(배치 링과 같은 규약).
- [ ] `spec.shape == None` 호출과 Placement 소유 중 호출은 `_rangeOwner` 를 바꾸지 않는다.
- [ ] `ClearAttachPreview()` 는 소유자가 `AttachPreview` 일 때만 지운다 · 다른 owner 획득 시 `_attachPreviewLive` 꺼짐.
- [ ] 사망 연출 중인 host 에 링이 남지 않는다(다음 LateUpdate 소멸).
- [ ] Subway 맵에서 host 중심 일치(StreetDay 는 오차가 숨는다).
- [ ] sim 파일 변경 0 · 골든 바이트 무변.
