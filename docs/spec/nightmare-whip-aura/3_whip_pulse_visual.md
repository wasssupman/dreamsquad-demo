# 3 — 채찍질 펄스 비주얼 (rev 1 스코프 추가, 사용자 요청 2026-07-12)

## 목적

채찍질 발동이 무연출이라 게임에서 안 보인다 — **버프가 실제로 나간 펄스**(대상 ≥1)에 보스 위치에서 hit-VFX 1회를 재생해 가시화한다. blink 퍼프 선례(nightmare-catcher rev 3) 그대로: `ProjectileHitEvents` 채널 재생, 신규 시스템/채널 0.

## 변경 대상

- `Assets/_Project/Data/Projectiles/.../Projectile_WhipPulse.asset` — 신규 (hit-VFX 전용 ProjectileData, `Projectile_BlinkPuff` 미러)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — SelfBlink 퍼프 베이크 분기 조건에 `AllyMoveSpeedAura` 추가
- `Assets/_Project/Scripts/Battle/Combat/BossPeriodicTriggerSystem.cs` — whip 분기에 hit-VFX enqueue
- `Enemy_Boss_Nightmare.asset` — 채찍질 mechanic `payload.projectile` = WhipPulse

## 구현

- **베이크**: `AllyMoveSpeedAura && payload.projectile != null` → `slot.projectileDataIndex = GetOrCreateProjectileDataIndex(...)` (SelfBlink 분기와 동일, null = 무연출 유지 — dataIndex -1 가드).
- **arm**: 펄스에서 **1개 이상 버프를 enqueue 한 경우에만** `ProjectileHitEvent { position=host 위치, dataIndex, payload=SingleSplash, source=host }` 1건 enqueue (`ProjectileHitEventsSingleton` TryGetSingletonRW — blink 선례). 0-대상 no-op 펄스는 무연출(효과 없는 연출 금지).
- **VFX 선정**: GA `hitPrefab` 머즐 함정(lessons) 회피 — 후보를 **오프스크린 렌더 픽셀 검증**으로 선별 후 배정. 이속 버프 성격에 맞는 파동/링 계열 우선, `hitVfxScale` 로 크기 튜닝.
- 스코프 밖 유지: 대상별 퍼프(1s 펄스 × N 마리 = 스팸), 3타일 반경 표시 링, 전용 신규 Shuriken 저작. 필요해지면 후속.

## 완료 기준

- [x] 컴파일 클린 + EditMode 그린 (로직 무변경 — enqueue 1건 추가).
- [x] 오프스크린 렌더로 선정 VFX 픽셀 유효 확인 (머즐/빈 프리팹 아님).
- [x] Play: 보스 주변에 아군 있을 때만 펄스 연출 재생, 단독 행군 시 무연출. 사용자 육안 확인.

확인 2026-07-12 — 후보 12종 오프스크린 렌더 대조(Axe 계열=도끼 메쉬 실루엣 노출로 기각) 후 `vfx_Hit_Cylinder04`(골드 에너지 버스트, 무기 실루엣 없음) 선정, `Projectile_WhipPulse`(hitVfxScale 1.3, heightOffset 0.7, preserveVfxColors). 스크립트 배틀 + 조건 캡처(미니언 cheb≤3.6 일 때만 촬영) 8샷으로 in-play 검증: 보스 위치 골드 펄스 + 1초 간격 잔상 확인, 콘솔 에러/경고 0. EditMode 701/703 그린. 크기/프리팹 교체는 SO 값이라 코드 무변경 튜닝 가능.
