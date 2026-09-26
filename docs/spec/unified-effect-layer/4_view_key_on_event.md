# 4 — 탄 그림은 사건만으로 (코어 + Unity · H6)

## 목적
`CoreProjectileViewPool.SpawnFromEvent` 가 생성 사건을 받고 **월드 탄을 되찾아** 탄 정의 줄을 읽는다(제약 4 위반 · 같은 틱에 착탄·소멸한 탄은 그림 없음). 사건이 탄 정의 줄을 값으로 나르게 한다.

## 변경 대상
- `Scripts/BattleCore/Match/CoreEvent.cs` — `ProjectileSpawned` 팩토리가 이미 있는 `DefIndex` 필드를 채운다(사건 종류 번호 무변).
- `Scripts/BattleCoreUnity/View/CoreProjectileViewPool.cs` — `FindProjectile` 조회 제거, `e.DefIndex` 사용.
- 트레이스(`Scripts/BattleCore/Harness/CoreTrace.cs`)는 `DefIndex` 를 기록하지 않는다 — **뷰 전용 필드라 미기록**을 그 자리 주석으로 명시(계약 7). 골든 무변.

## 구현
- **라이브 그림 무변이 규칙이다.** 오늘 비행 그림은 「사건 배달 때 월드에 탄이 살아 있다」 = 비행 시간 > 0 일 때만 뜬다. 조회를 없애도 이 조건을 **사건 값(비행 시간)** 으로 그대로 옮긴다 — 비행 0 탄은 비행 그림 없이 착탄 연출만(자리 폭발 카드 6장 무변). 타격 운석은 unit 1 에서 낙하 시간을 가지므로 이 규칙 안에서 그림이 뜬다.
- 풀 반납 무변.

## 완료 기준
- 탐침 `타격_운석도_떨어지는_운석_그림이_뜬다` 해제 초록 · 짝 `현행_…` 뒤집거나 삭제.
- PlayMode Core 무회귀 · 골든 11 일치.
- 사용자 플레이 확인: 액티브 운석 · 배치 스킬 · 캐논 폭격 탄 그림이 오늘과 같다.
- 비행 0 × 프리팹 있는 탄(`census.md` 표 1 ★ — 자리 폭발 카드 6장)이 이 unit 전후로 같은 그림(비행 그림 없음 · 착탄 연출만)인지 PlayMode 로 계측해 단언. 오늘 그림이 「추정」과 다르면 정지하고 보고.
