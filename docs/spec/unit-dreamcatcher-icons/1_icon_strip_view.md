# 1 — Icon Strip View

## 목적

부착 카드 목록을 유닛 머리 위 미니 카드 스트립으로 렌더하는 뷰/스포너. `AttachmentsChanged` 이벤트 시점에만 리빌드, per-frame 은 앵커 추종/빌보드만.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/DcIconStripSpawner.cs` (신규)
- `Assets/_Project/Scripts/Presentation/DcIconStripView.cs` (신규)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `TryGetUnitViewAnchor` 공개 wrapper (기존 private `ResolveUnitViewTransform` 위임, 로직 0)

## 구현

1. **`DcIconStripSpawner`** (MonoBehaviour):
   - SerializeField: `DreamcatcherHandController hand`, `BattleBridge bridge`, `Camera billboardCamera`(미할당 시 Camera.main), 오프셋/카드 높이/간격/플레이트·타입별 테두리 색 튜닝 필드.
   - `OnEnable` 구독 / `OnDisable` 해제 + 전량 회수. `AttachmentsChanged` → `Rebuild()`.
   - `Rebuild`: `hand.GetAttachments` → host 별 그룹핑 → host 마다 스트립 뷰 Ensure(entity 키 딕셔너리 + 풀, StatusFxSpawner 선례), 앵커는 `bridge.TryGetUnitViewAnchor`. 이번 리빌드에 없는 host 의 스트립은 Hide+풀 회수.
   - 프레임 스프라이트는 `UiRoundedSprite.Make`(플레이트 fill + CardType 별 테두리 색) 2종 캐시.
2. **`DcIconStripView`** (MonoBehaviour):
   - 슬롯 = 프레임 SR + 아트 SR 쌍. 카드 수만큼 활성화, 가로 중앙 정렬 배치.
   - 아트 스케일은 `sprite.bounds` 로 목표 월드 높이에 정규화 (원본 1024×1536, PPU 무관).
   - `art == null` 폴백: 아트 SR 비활성, 플레이트+테두리만 (색 카드 플레이트).
   - 앵커 추종(파괴 시 마지막 위치 유지) + 카메라 빌보드 — StatusFxView 패턴.
   - sortingOrder 는 StatusFx(15000) 아래 14500/14501 (프레임/아트).
3. **오프셋 기본값은 Sleep "Zz" 와 겹치지 않게** 상향 — 정확한 값은 unit 2 Play 에서 튜닝.

## 완료 기준

- compile 통과 (Unity 콘솔 에러 0).
- ECS 컴포넌트/시스템 변경 0 — BattleBridge 추가는 read-only wrapper 1개뿐.
- 씬 배선 전이므로 시각 확인은 unit 2 에서. 이 단계는 코드 리뷰 수준(이벤트 구독 대칭, 풀 수명주기, 폴백 분기)으로 확인.

확인 2026-07-12 — compile 에러 0, 커밋 `ece5f267`. 사후 아키텍트 리뷰(정당 5/과잉 1/위반 0)로 `_listPool` 제거(unit 2 커밋에 포함).
