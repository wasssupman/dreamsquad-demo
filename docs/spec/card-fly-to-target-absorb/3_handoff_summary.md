# 3 — handoff summary

## Commit
- `56d78acf` — unit 0 (카드 비행 presenter)
- `5aab7bfe` — unit 1+2 (묵직 임팩트 반응 + 타일 타겟 일반화 + 하스스톤 3축 아치 + GA VFX)

## Implemented
- 커밋 성공 시 손패 카드(UGUI art 스냅샷 고스트)가 타겟으로 **하스스톤식 3축 아치**(rise→하늘 apex hang→가속 slam)로 날아가 찰싹.
- 착지 = 묵직 임팩트: `SpineUnitView.PlayPunch`(스케일 펀치) + `FlashWhite`(흰 틴트 펄스) + GA `vfx_Hit_Cylinder02`(에너지 기둥+플래시+아크) + `CameraImpactKick`(미세 킥) + `SoundManager.PlayCardAbsorb`.
- 타겟 전종: 유닛(Attach, Transform 매프레임 추적) / Active-Defender·Tile·Portal(셀 → view 중심 고정). 유닛만 펀치/플래시, 그 외 공통 월드 반응.
- 안착 즉시 scale/alpha dissolve(셰이더 dissolve 회피). 커밋 실패/취소 = 비용 0·연출 없음.
- ECS 시뮬 변경 0(순수 프레젠테이션). bridge 게이트웨이 read 추가만(`TryGetUnitView`, `SpawnCardAbsorbVfx`, `GridCellToViewCenter`).

## Key Files
- `UI/Dreamcatcher/CardAbsorbFlightPresenter.cs` — 3축 아치 비행 + splat + dissolve (튜닝 노브 다수).
- `UI/Dreamcatcher/DreamcatcherHandView.cs` — `FlyCardToUnit`/`FlyCardToCell` + `FireAbsorbImpact(World)` choreography + `EnsureCameraKick`.
- `UI/Dreamcatcher/DreamcatcherCardDragSlot.cs` — `CommitNow(commit, onSuccess)` 확장, 4개 커밋 사이트 발화.
- `Presentation/SpineUnitView.cs` — `PlayPunch`/`FlashWhite`. `Presentation/CameraImpactKick.cs`(신설, rig 존중).
- `Presentation/VfxSpawner.cs` — `SpawnCardAbsorb(viewPos)` + `cardAbsorbPrefab` 슬롯. `Audio/SoundManager.cs` — `cardAbsorbClip`/`PlayCardAbsorb`.
- `Bridge/BattleBridge.cs` — `TryGetUnitView`/`SpawnCardAbsorbVfx`/`GridCellToViewCenter` 게이트웨이.

## Verified
- compile 클린(에러 0). presenter 스폰 no-throw 스모크. 사용자 Play 검증(비행·임팩트·VFX 느낌 승인).
- VFX 오프스크린 렌더 11종 비교 → Cylinder02 선정(URP 렌더 정상, 마젠타 없음).

## Notes (되돌리면 안 되는 의도)
- **VFX 재사용 금지**: Rock03/Cylinder04/RotatingSpheres03 은 이미 사용 중 → Cylinder02(미사용) 선택. 되돌리지 말 것.
- **카메라 킥은 LateUpdate additive-self-cancel** — 타일맵 카메라 pitch 라이브 재계산과 안 싸우게. 직접 transform 절대세팅 금지.
- **sim/view 경계**: 임팩트 VFX/비행은 **view** 좌표(유닛 transform / `GridCellToViewCenter`). `VfxSpawner.SpawnCardAbsorb` 는 ToView 안 함(다른 Spawn* 과 반대).
- **presenter/CameraKick 은 런타임 생성**(SerializeField 오버라이드 없음) → 튜닝은 **코드 기본값**이 런타임 값.
- **씬 배선**: `BattleScene` VfxSpawner `cardAbsorbPrefab` 참조만 커밋(유저 조명/hand WIP 는 격리 — hunk 단위 스테이징). `SoundManager.cardAbsorbClip` 미할당(무음) — 클립 넣으면 SFX.

## Follow-up
- `SoundManager.cardAbsorbClip` 실제 "찰싹" 클립 할당(현재 무음).
- 스쿼드 부착 앵커(대표 유닛 vs 중심) — 현재 `TryGetUnitViewAnchor` 반환 그대로.
- Active 스킬별 차등 연출(현재 공통 찰싹) — 후속 후보.
- 전역 카메라 임팩트 셰이크 서비스(처치/보스 공유) — 후속 후보.
