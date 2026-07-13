# 2 — 타일 타겟 일반화 + 배선/Play

## 목적

Active-Tile/Portal(메테오·포탈 등 셀 대상 스킬)도 유닛 대신 **타일 월드 좌표**로 같은 찰싹 흡수. 씬 배선 완료 + Play 검증.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/CardFlyPresenter.cs` — 타겟 소스에 셀 분기.
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardDragSlot.cs` — `CommitActiveTile`/`CommitActivePortal` 성공에서도 발사.
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 셀→월드 헬퍼(없으면 추가).
- 씬: presenter/bridge 참조 배선.

## 구현

1. **타겟 분기**: Defender/Squad/Active-DefenderUnit → 유닛 host 앵커(unit 0/1). Active-Tile/Portal → **타일 셀→월드**
   좌표. 포탈은 진입 셀.
2. **셀→월드**: `BattleBridge` 에 이미 있으면 재사용, 없으면 `CellToWorld(cell)` 게이트 추가(TryScreenToCell 의 역).
3. **타일 임팩트**: 유닛 없는 타일은 Spine 펀치 대신 **링 충격파 + 버스트 + 흔들림**만(unit 1 의 유닛-무관 부분 재사용).
   타일 스킬 자체 VFX(메테오 낙하 등)와 겹치지 않게 타이밍/세기 조정.
4. **배선/Play**: presenter GameObject + bridge/camera 참조 배선. 유닛 부착·타일 스킬 각각 확정해 찰싹 검증.

## 완료 기준

- compile 성공, 콘솔 CS 에러 0.
- Play — 유닛 부착 / 타일 스킬(메테오 등) 확정 모두에서 카드가 해당 타겟으로 날아가 찰싹 흡수 + 임팩트.
- 타일 스킬은 카드 흡수 임팩트와 스킬 고유 VFX 가 자연스럽게 이어짐(중복·충돌 없음).
- 씬 배선 완료 — 사용자 수작업 없이 Play 에서 동작.
- 페이즈 이탈/재시작 무회귀(고스트·VFX 잔류 0).
