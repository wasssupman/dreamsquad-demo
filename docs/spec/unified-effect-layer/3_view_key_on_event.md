# 3 — 탄 그림은 사건만으로 그린다 (Unity)

## 목적
`CoreProjectileViewPool` 이 생성 사건을 받을 때 **월드의 탄을 되찾아** 탄 정의 줄을 읽는다(제약 4 「사건으로 상태를 되묻지 않는다」 위반이자, 같은 틱에 착탄·소멸한 탄은 그림이 안 뜨는 결함). 사건이 탄 정의 줄을 값으로 나르게 한다. H6 — 하드 케이스 2 의 낙하 그림 탐침이 이 구멍을 잰다.

## 변경 대상
- `Scripts/BattleCore/Match/CoreEvent.cs` — `ProjectileSpawned` 에 탄 정의 줄(`DefIndex`) 싣기(기존 필드 재사용 가능하면 재사용, 아니면 필드 추가 — 사건 종류 번호 무변).
- `Scripts/BattleCore/Trace/` — 생성 사건 트레이스에 같은 값(골든 트레이스 형식이 바뀌면 재베이크 사유로 기록).
- `Scripts/BattleCoreUnity/View/CoreProjectileViewPool.cs` — `SpawnFromEvent` 의 `FindProjectile` 조회 제거.

## 구현
- 비행 0 인 탄: 그림을 띄우고 같은 배달 묶음의 `ProjectileHit` 로 바로 착탄 연출(오늘 액티브 운석과 같은 그림 순서).
- 풀 반납 경로는 무변(`ProjectileDespawned`).

## 완료 기준
- 탐침 `타격_운석도_떨어지는_운석_그림이_뜬다` 해제 초록 · `현행_타격_운석은_즉발이라_…` 뒤집거나 삭제.
- PlayMode Core 무회귀 · 골든 11(트레이스 형식이 바뀌면 사유 기록 후 재베이크 — 이벤트 수·순서는 동일해야 한다).
- 사용자 플레이 확인: 액티브 운석·배치 스킬 탄 그림이 오늘과 같다(무변 확인).
