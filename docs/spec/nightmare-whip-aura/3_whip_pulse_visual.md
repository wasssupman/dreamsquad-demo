# 3 — 채찍질 오라 비주얼 (rev 2)

## 목적

채찍질이 게임에서 보이게 한다. **rev 2 (최종)**: 메커닉이 선언한 루핑 오라가 host 에 부착돼 따라다닌다 — 유닛 발산 오라로 읽힘.

## rev 이력

- **rev 1 (기각)**: 펄스마다 보스 위치에 원샷 hit-VFX(`vfx_Hit_Cylinder04`, blink 퍼프 선례). 커밋 `bfa858fb`. **기각 사유(사용자 2026-07-12)**: 월드 고정 수직 기둥이 보스가 지나간 자리에 남아 "지형에 박힌 말뚝/지형 효과"로 오독됨. 오라는 유닛을 따라다녀야 한다.
- **기각된 대안 — unit-status-fx 편입**: `StatusFxKind.WhipAura` + bridge reconcile 에서 payload kind 스캔. **기각 사유(사용자)**: 개별 드림캐쳐 메커닉의 연출을 범용 인프라/bridge 에 kind 분기로 넣으면 메커닉 지식이 프레젠테이션 인프라로 누수 — 메커닉이 늘 때마다 bridge 분기 증식. **드림캐쳐 연출은 드림캐쳐가 관리한다.**
- **rev 2 (채택)**: 메커닉 데이터가 자기 오라를 선언(`DcPayloadSpec.auraPrefab/auraScale`), 드림캐쳐 파이프라인이 구동(bake 등록 → `DcAuraVisualPool` 추종). **kind-blind** — auraPrefab 을 선언한 어떤 payload kind 든 분기 0 으로 동일 경로.

## 변경 대상

- `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs` — `DcPayloadSpec.auraPrefab/auraScale` (정의 계층: 에셋 참조 허용, Entities 무참조 유지)
- `Assets/_Project/Scripts/Presentation/DcAuraVisualPool.cs` — 신규 (plain class, 씬 배선 없음)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 베이크 등록·LateUpdate Sync·teardown Clear 3점 (전달만, kind 분기 없음)
- `Enemy_Boss_Nightmare.asset` — 채찍질 mechanic `auraPrefab=WindAura(URP)`, `auraScale=1.6`, `projectile=null`(rev 1 원샷 제거)

## 구현

- **소유**: 룩 선언 = 메커닉 데이터(payload). 구동 = 드림캐쳐 프레젠테이션 풀. bridge = 게이트웨이 전달 3점.
- **라이프사이클**: 베이크 시 `(host, prefab, scale)` 등록 → host 엔티티 생존 동안 인스턴스 1개가 host 뷰 위치 추종(뷰 풀링이라 parenting 금지 — StatusFxView 관용구) → host 사망 시 파괴, 배틀 teardown 시 전체 Clear. 뷰 일시 부재는 비활성 대기. host 당 1개(첫 선언 승리).
- **rev 1 원샷 arm 코드는 잔존** — SO 로 게이트되고(`payload.projectile`, 현재 미사용) 다른 메커닉의 발동-순간 연출 옵션으로 유효. 데이터만 제거.
- VFX = PixPlays `ElementalAuras/WindAura`(URP) — 바람 스트릭, 이속 버프 주제 일치. 스케일/프리팹 교체는 SO.

## 완료 기준

- [x] 컴파일 클린 + EditMode 그린.
- [x] Play: 오라가 보스를 따라다니고(이동 추종), 지형 잔상 없음. 보스 사망/배틀 종료 시 정리.
- [x] bridge/범용 인프라에 payload kind 분기 0 (kind-blind 계약).

확인 2026-07-12 — 컴파일 클린 + EditMode 701/703 그린. 스크립트 배틀 캡처 6샷: WindAura 스트릭이 보스 중심으로 휘감겨 이동 추종(t=39.0→40.7 위치 이동 확인), 월드 잔상 없음, 콘솔 에러/경고 0. 커밋은 unit 3 rev 2 커밋 참조.
