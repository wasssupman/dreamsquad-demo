# 0 — OnDamagedN payload 개통: DamagedCounter 위드닝 (rev 2)

## 목적

OnDamagedN 이 임의 payload 를 실행할 수 있게 한다. **rev 2 (critic CRITICAL 반영)**: DcTriggerSlot 통합안은 폐기 — counter 쓰기가 DamageApplicationSystem(Units)에서 일어나므로 Combat 소유 DcTriggerSlot 로 옮기면 교차-맥락 쓰기가 된다. DamagedCounter 가 Units 버퍼로 분리된 원래 이유를 유지하고, **버퍼를 위드닝**한다.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Units/DamagedCounter.cs` — payload 필드 append: `DcPayloadKind payload`, `float magnitude`, `int tileRange`, `int aoeDataIndex` (기존 필드·소유 불변. gate 필드는 unit 1 에서 append)
- `Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs` — OnDamagedN bake 분기: NextAttackDoubleFire 전용 가드 해제, payload 별 bake (SelfTileAoe = AOE-view/양수 magnitude 가드, 기존 SelfTileAoe bake 분기와 동일 규칙)
- `Assets/_Project/Scripts/Battle/Units/DamageApplicationSystem.cs` — DamagedCounter 발동 지점에 payload 디스패치: NextAttackDoubleFire = 기존 플래그 로직 그대로, SelfTileAoe = `ShieldBreakEvent` enqueue (이 시스템이 이미 쓰는 큐 — 신규 boundary 0), 그 외 = unhandled 경고
- `Assets/_Project/Scripts/Battle/Units/ShieldBreakEvent.cs` — struct 주석에 "OnDamagedN×SelfTileAoe 도 이 채널 공유" 명시
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` `DrainShieldBreakEvents` — 로그에 origin(실드파열 vs 피격트리거) 구분 (critic MED)

## 구현

- **회귀 핀 선행 (TDD, critic MED)**: 리팩터 전에 가시반격(Thornmail, OnDamagedN×NextAttackDoubleFire) 경로의 회귀 테스트가 있는지 확인하고 없으면 먼저 작성 — "5번째 피격 → 다음 공격 2연발"을 고정한 뒤 bake/디스패치를 리팩터한다.
- bake: period ≤ 0 가드 유지. 기존 Thornmail 에셋은 무변경(payload=NextAttackDoubleFire 로 bake 되는 데이터 계약 동일).
- 향후 적측 OnDamagedN 이 열리면 ShieldBreakEvent.host 의 defender 가정이 깨진다 — v1 은 디펜더 카드 전용임을 struct 주석에 함께 명시.

## 완료 기준

- [ ] Thornmail 회귀 테스트 선행 확보 후 green 유지 (리팩터 전/후 동일)
- [ ] compile + EditMode 전체 green
- [ ] OnDamagedN×SelfTileAoe 가 드레인까지 흐르는 것 e2e 1회 (unit 2 카드로 대체 가능 — gate 없이 임시 에셋로도 무방)
