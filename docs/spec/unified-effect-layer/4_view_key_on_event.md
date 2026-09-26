# 4 — 탄 그림은 사건만으로 (코어 + Unity · H6)

## 목적
`CoreProjectileViewPool.SpawnFromEvent` 가 생성 사건을 받고 **월드 탄을 되찾아** 탄 정의 줄을 읽는다(제약 4 위반 · 같은 틱에 착탄·소멸한 탄은 그림 없음). 사건이 탄 정의 줄을 값으로 나르게 한다.

## 변경 대상
- `Scripts/BattleCore/Match/CoreEvent.cs` — `ProjectileSpawned` 팩토리가 이미 있는 `DefIndex` 필드를 채운다(사건 종류 번호 무변).
- `Scripts/BattleCoreUnity/View/CoreProjectileViewPool.cs` — `FindProjectile` 조회 제거, `e.DefIndex` 사용.
- 트레이스(`Scripts/BattleCore/Harness/CoreTrace.cs`)는 `DefIndex` 를 기록하지 않는다 — **뷰 전용 필드라 미기록**을 그 자리 주석으로 명시(계약 7). 골든 무변.

## 구현
- 비행 0 탄: 그림을 띄우고 같은 배달 묶음의 `ProjectileHit` 로 바로 착탄 연출. 풀 반납 무변.

## 완료 기준
- 탐침 `타격_운석도_떨어지는_운석_그림이_뜬다` 해제 초록 · 짝 `현행_…` 뒤집거나 삭제.
- PlayMode Core 무회귀 · 골든 11 일치.
- 사용자 플레이 확인: 액티브 운석 · 배치 스킬 · 캐논 폭격 탄 그림이 오늘과 같다.
- ⚠ 비행 0 × 프리팹 있는 탄(`census.md` 표 1 ★ — `Projectile_Meteor` 를 쓰는 자리 폭발 카드 6장)은 오늘 그림이 없을 수 있고(추정 — 같은 틱 소멸) 이 unit 뒤 **운석 그림이 새로 뜰 수 있다**. 구현 전에 오늘 동작을 계측으로 확인하고, 달라지면 의도인지 사용자에게 묻는다.
