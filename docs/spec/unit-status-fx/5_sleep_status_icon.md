# 5 — Sleep 상태 아이콘 (Zz)

## 목적

combat-action-lock 이 만든 Sleep 상태(적·아군 공통, wake-on-hit)를 어그로 "!"와 같은 방식의
머리 위 상태 연출로 표시한다. 본 spec 의 "새 상태 = registry 항목 + reconcile 훅 몇 줄" 계약의
첫 실전 적용. (combat-action-lock 후속 후보 "잠 zzz 프레젠테이션" 소화)

## 변경 대상

- `Assets/_Project/Scripts/Data/StatusFxKind.cs` — `Sleep` append
- `Assets/_Project/Scripts/Data/StatusFxRegistry.cs` — Entry 에 `fallbackGlyph` 필드 (아래)
- `Assets/_Project/Scripts/Presentation/StatusFxView.cs` — 글리프별 절차 폴백 스프라이트
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `ReconcileStatusFx` 에 Sleep 쿼리
- `Assets/_Project/Data/Config/StatusFxRegistry.asset` — Sleep 항목 추가

## 구현

1. **enum**: `StatusFxKind.Sleep` (append-only 계약 준수).
2. **폴백 글리프 일반화**: 현 절차 폴백은 "!" 하드코딩. 두 번째 상태가 생겼으므로
   `FallbackGlyph { Exclamation = 0, Zz = 1 }` enum 을 Entry 에 추가 — 기본값 0 이라
   기존 Aggro 에셋 직렬화 하위호환(재저장 불필요). 스프라이트 캐시는 글리프별 static.
   Zz = 큰 Z + 우상단 작은 z 픽셀 드로잉 (기존 "!" 드로잉과 같은 기법).
3. **reconcile 훅**: `CcEffect` 버퍼 보유 엔티티 쿼리(캐시) → 버퍼에
   `kind==Sleep && remainingTime>0` 있으면 `Ensure(e, StatusFxKind.Sleep, anchor)`.
   앵커는 기존 `ResolveEnemyViewTransform` 재사용 — defender 도 spine 브랜치로 해석됨
   (BattleBridge:2133). CcEffect 는 Effects 소유지만 **읽기만** 하므로 맥락 계약 준수.
4. **registry 에셋**: Sleep 항목 — prefab 없음(폴백), offset +1.5Y·scale 0.5 는 Aggro 관례,
   tint 는 수면 연상 라벤더/블루 계열, glyph=Zz. 값은 에셋에서 조정 가능(하드코딩 금지).
5. wake-on-hit/만료 시 buffer 에서 Sleep 이 사라지면 reconcile EndFrame 이 자동 회수 —
   해제 코드 불필요(상태 구동 계약).

## 리뷰 반영 (two-track, 2026-07-11)

- **ecs-review M1**: 스파인 미보유 defender 는 앵커 해석 실패로 Zz 미표시(잠재 — 현 로스터 16기
  전원 스파인 보유) → `ResolveEnemyViewTransform` 을 `ResolveUnitViewTransform` 으로 정정(L1)하고
  `defenderFallbackViewPool` 분기 추가. 호출처 3곳(어그로/히트바/Sleep) 동반 갱신.
- **ecs-review M2 (수용된 특성)**: reconcile 스캔이 O(전체 유닛) — CcEffect 버퍼가 전 유닛 상주라
  쿼리가 narrow 하지 않음. TD 규모(수십~수백)에서 문제 없어 **별도 후속 없이 수용**. 만약 프로파일에
  등재되면 그때 이 spec 에 rev 로 Effects 토글 enableable `AsleepTag` 를 통합 처리한다(사용자 결정 2026-07-11:
  후속 분리 대신 spec 내 통합).
- **code-review LOW 통합 반영 (2026-07-11)**: 글리프 캐시 배열 크기를 enum 에서 유도(수동 커플링 제거),
  `FillRect` 인덱스 컨벤션(x 포함/y 미포함) 주석 명시.
- 에셋 확정치: offset (0.35, 2.2), scale 0.65, 틴트 (0.38, 0.52, 1) — 시인성 튜닝 결과.

## 완료 기준

- [x] 컴파일 클린, 기존 어그로 "!" 외형/동작 무손실 (glyph 기본값 하위호환)
- [x] Play: Sleep 걸린 유닛 머리 위에 Zz 표시 (∞ Sleep 계측 + Game View 스크린샷 검증)
- [x] Play: Sleep 해제(만료) 시 Zz 자동 회수 — reconcile 카운터로 확인 (wake-on-hit 동일 경로)
- [ ] 어그로 + Sleep 동시 부착 — 인프라 계약상 동작(`(entity,kind)` 키), 시각 확인은 실플레이 시 자연 확인 예정

사용자 확인 2026-07-11 (Zz 스크린샷).
