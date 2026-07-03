# 2 — 적 피격 마이크로바

## 목적

적이 맞는 순간에만 나타났다 페이드되는 마이크로 체력바. 상시 바의 클러터 없이 "내 화력이 먹히는가 + 얼마나 남았나"를 피격 타이밍(데미지 숫자와 동시)에 전달.

## 변경 대상

- 신규: `Assets/_Project/Scripts/Presentation/EnemyHitBarSpawner.cs` + `EnemyHitBarView.cs`
- 신규 prefab: bg/fill 2-스프라이트 마이크로바 (DamageNumber popup prefab 과 같은 방식으로 제작)
- `Assets/_Project/Scripts/Data/HealthDisplayStyle.cs` — 바 필드 추가
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `[SerializeField] EnemyHitBarSpawner`, `DrainDamageNumberEvents`(:1918) 확장
- `Assets/_Project/Scripts/Presentation/BoardSortOrder.cs` — 마이크로바 고정 오더 상수
- BattleScene — 스포너 GO + 참조 배선 (unity-feature-wiring)

## 구현

- SO 추가 필드: 바 크기(w/h), `headYOffset`, `holdSec`(≈0.8), `fadeSec`(≈0.3), bg 색, fill 색 램프(Gradient, hpRatio 기준).
- drain 에서 `spawner.Show(evt.entity, anchor, evt.hpRatio)`. anchor 해석은 BattleBridge 가: `spineUnitPool.TryGet` → `enemyViewPool.TryGet` → 뷰 transform(view 좌표), 둘 다 실패 시 `BoardSpace.ToView(evt.position)` 고정 위치 fallback. 기존 `damageNumberSpawner.Spawn` 호출은 그대로 병행.
- 스포너 내부 `Dictionary<Entity, EnemyHitBarView>` — 활성 바 있으면 fill 갱신 + hold 타이머 리셋(스택 금지). 풀링은 `DamageNumberPool` 패턴 미러.
- `EnemyHitBarView`: anchor Transform 을 hold 동안 따라감(+headYOffset), anchor 파괴 시 마지막 위치에서 페이드 계속. 빌보드는 `DamageNumberView` 와 같은 카메라-facing 처리. fill 은 스프라이트 X 스케일(피벗 좌측)로 표현.
- `hpRatio <= 0`(막타): fill 0 으로 표시 후 즉시 페이드 시작.
- sorting: `BoardSortOrder` 에 고정 상수(캐릭터/투사체(1000) 위, 데미지 숫자(32000) 아래 — 예: 16000) 추가, bg/fill 오더 bg+0/fill+1.
- 스포너 미할당 시 기존 데미지 숫자만 동작 (null 가드).

## 완료 기준

- compile 0 에러 + EditMode 무회귀.
- Play 검증: ① 피격 순간 바 등장 → hold → 페이드 ② 연속 피격 시 바 1개 유지 + fill 감소 ③ 막타 시 fill 0 후 소멸 ④ 데미지 숫자와 겹침/정렬 자연스러움 — 스크린샷 육안 확인.
- 다수 동시 피격(웨이브 밀집)에서 콘솔 에러/GC 스파이크 없음.
