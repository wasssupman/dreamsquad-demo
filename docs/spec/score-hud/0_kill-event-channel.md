# 0 — 킬 이벤트 채널 (EnemyKilledEvent)

## 목적

적이 데미지로 사망할 때(디펜더가 처치) Units→Presentation 단발 신호를 보낸다. `BattleBridge` 가 드레인해 라이브 점수 HUD 를 올린다. 채널 수 **15 → 16**. `damage-number-popup` unit 0 과 동일 패턴.

## 변경 대상

- (신규) `Assets/_Project/Scripts/Battle/Units/EnemyKilledEvent.cs`
- (신규) `Assets/_Project/Scripts/Battle/Units/EnemyKilledEventsSingleton.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 필드/생성/파괴/해제 + 스텁 드레인
- `CLAUDE.md` — 채널 목록 (15 → 16, `EnemyKilledEventsSingleton` 추가)

## 구현 (완료)

- `EnemyKilledEvent { float3 position; }` (position reserved — 향후 킬 위치 연출용).
- `EnemyKilledEventsSingleton { NativeQueue<EnemyKilledEvent> queue; }`.
- BattleBridge: `_enemyKilledEventQueue` 필드 + DamageNumber 채널 바로 뒤에 생성/파괴/해제 미러.
- `Update()` 드레인 시퀀스에 `DrainEnemyKilledEvents()` 추가 + 누수 방지 스텁(Clear). 실 드레인은 unit 3.

## 완료 기준

- compile: CS 에러 0.
- 채널 16개로 생성/파괴/해제(코드 검토) + `CLAUDE.md` 갱신.
- 런타임 효과는 unit 1(enqueue) + unit 3(드레인+HUD) 후.

✅ 2026-06-05 compile 클린(unit 1 과 함께 force refresh 검증). 커밋: 9549e55
