# 4 — 탄 그림은 사건만으로 (코어 + Unity · H6)

## 목적
`CoreProjectileViewPool.SpawnFromEvent` 가 생성 사건을 받고 **월드 탄을 되찾아** 탄 정의 줄을 읽는다(제약 4 위반 · 같은 틱에 착탄·소멸한 탄은 그림 없음). 사건이 탄 정의 줄을 값으로 나르게 한다.

## 변경 대상
- `Scripts/BattleCore/Match/CoreEvent.cs` — `ProjectileSpawned` 팩토리가 이미 있는 `DefIndex` 필드를 채운다(사건 종류 번호 무변).
- `Scripts/BattleCoreUnity/View/CoreProjectileViewPool.cs` — `FindProjectile` 조회 제거, `e.DefIndex` 사용.
- 트레이스(`Scripts/BattleCore/Harness/CoreTrace.cs`)는 `DefIndex` 를 기록하지 않는다 — **뷰 전용 필드라 미기록**을 그 자리 주석으로 명시(계약 7). 골든 무변.

## 구현
- **라이브 그림 무변이 규칙이다.** 오늘 비행 그림은 「드라이버가 한 프레임의 틱을 다 돈 뒤 배달할 때 탄이 월드에 남아 있다」일 때만 뜬다(초안의 「비행 시간 > 0」은 거짓 — 직선·왕복·호밍 탄은 `FlightTime = 0` 으로 거리로 산다). 이 조건을 사건 값만으로 옮긴다: `ProjectileSpawned` 를 보류하고 같은 배달 묶음에 `ProjectileDespawned` 가 오면 버리고, 남은 것만 `LateUpdate` 앞에서 발행 순서대로 세운다(시각 난수 순서·앵커 시점 무변).
- 풀 반납 무변.

## 완료 기준
- 탐침 `타격_운석도_떨어지는_운석_그림이_뜬다` 해제 초록 · 짝 `현행_…` 뒤집거나 삭제.
- PlayMode Core 무회귀 · 골든 11 일치.
- 사용자 플레이 확인: 액티브 운석 · 배치 스킬 · 캐논 폭격 탄 그림이 오늘과 같다.
- 비행 0 × 프리팹 있는 탄(`census.md` 표 1 ★ — 자리 폭발 카드 6장)이 이 unit 전후로 같은 그림(비행 그림 없음 · 착탄 연출만)인지 PlayMode 로 계측해 단언. 오늘 그림이 「추정」과 다르면 정지하고 보고.
