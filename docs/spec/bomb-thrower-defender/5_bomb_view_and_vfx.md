# 5 — 데굴데굴 구르는 폭탄 뷰 + 퓨즈 블링크 + 폭발

## 목적

폭탄이 **데굴데굴 굴러가는** 느낌을 살린다. sim 은 무변경 — 뷰가 plain 값(`elapsed`/`flightTime`/`fuseSec`)을 읽어 구르기/착지/블링크로 해석(계약 8). 폭발은 기존 TileAoe 크레이터 재사용.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/ProjectileViewPool.cs` — 폭탄 뷰 구르기 회전 + 퓨즈 블링크
- `Assets/_Project/Scripts/Data/ProjectileData.cs` — (필요 시) `RollAlongPath` facing 모드 또는 구르기 파라미터
- `Assets/_Project/Data/Projectiles/Projectile_Bomb.asset` (신규 또는 기존 recolor) — 폭탄 투사체 SO

## 구현

- **확장 지점 = `ProjectileViewPool.SyncTransforms`(:142-157)**: 이미 `ProjectileState`(`movement/elapsed/flightTime/arcHeight`)를 읽어 Ballistic/SkyFall 의 view-Y 를 만든다(BattleBridge 가 EntityManager 를 넘겨주는 기존 패턴 — Iron Law 준수, 신규 배선 0). `GrenadeToCell` 분기를 **같은 자리**에 추가: `fuseSec` 읽어 블링크, 이동 델타로 roll. (unit 1 이 `fuseSec` 를 `ProjectileState` 에 이미 추가.)
- **데굴데굴 회전**: 기존 `ProjectileFacing.SpinAroundUp`(팽이/월드-업 회전)이 아니라 **진행에 동기화된 tumbling roll**. sim XZ 이동에 맞춰 스프라이트를 진행축 기준 굴린다(코인 구르듯). 구현 택1:
  - (a) `ProjectileFacing.RollAlongPath` 신규 모드 — 이동 거리/방향에 비례한 roll 각(`spinSpeed` 재사용).
  - (b) GrenadeToCell 뷰 전용 회전(dataIndex/movement 로 식별) — enum 안 늘림.
  - 회전율은 시각 파라미터(SO `spinSpeed` 또는 신규) — 하드코딩 금지.
- **낮은 arc**: `bombArcHeight≈0` 이라 지면 구르기로 읽힘(높이 lob 아님). travel 중 살짝 통통(선택).
- **착지+퓨즈 블링크**: `elapsed >= flightTime`(착지) 이후 `fuseSec` 동안 폭탄 정지 + **점멸/스케일 펄스**(폭발 예고). 블링크 강도 = f(남은 fuse) — 뷰-side 자명 매핑(인라인). `elapsed >= flightTime+fuseSec` 에서 엔티티 소멸(폭발) → 회전/블링크 자동 종료.
- **타입별 색**(계약 8) — **후속 이관**: `bombType`(0/1/2)이 request/state 에 이미 실려 있으나(뷰가 읽을 준비 완료), 3종 색은 하드코딩 금지라 SO 색 데이터 홈이 선결. 3종 구분은 v1 에서 효과(데미지/수면/스턴)로 체감되므로 색은 폴리시로 분리. README 후속 후보.
- **폭발 VFX**: `ProjectileHitEvent`(TileAoe, impact 중심 크레이터) **기존 재사용** — 신규 VfxSpawner 슬롯 없음. 전용 폭발 아트는 후속 후보.

## 완료 기준

- [x] compile 0 에러 (`RollAlongPath` facing + GrenadeToCell view-arc + 퓨즈 스케일 점멸).
- [ ] Play 시각: 폭탄이 착지 셀까지 **굴러가는 느낌**(tumbling, 지면) · 착지 후 fuse 동안 점멸 · 폭발 시 크레이터 1회 · 콘솔 0. (unit 6 통합 Play)
- [ ] 기존 투사체 뷰(불릿/메테오/볼리) 회귀 없음. (unit 6 Play + EditMode 로 확인)
- (이관) 타입별 색 = README 후속 후보. 3종 구분은 효과로 체감.

확인 2026-07-21 · compile 0. Play 시각(데굴데굴/점멸)은 unit 6 통합 Play(에셋 배선 후).
