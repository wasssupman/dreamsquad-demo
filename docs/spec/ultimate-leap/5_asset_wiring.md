# 5 — 짱쎈놈 배선 + 통합 Play 검증

## 목적

궁극기를 짱쎈놈에 실제로 물리고, 전 유닛을 통합 Play 로 검증한다. 여기까지 와야 처음으로 발동한다.

## 변경 대상

- `Assets/_Project/Data/Enemies/Enemy_Boss_Jjangssen.asset` — `nightmareMechanics` 에 슬롯 추가
- `CLAUDE.md` — NativeQueue 채널 목록에 `UltimateLeapVisualEventsSingleton` 27번째 등재 (+ 근거 한 줄)

## 슬롯 값 (초안 — Play 튜닝 대상)

```yaml
- trigger:  { kind: 5 (HealthThreshold), fraction: 0.7 }   # 30% 에서 1회. 0.5 이상 = 생존당 1회 보장
  payload:
    kind: UltimateLeap
    duration: 2            # 예고 초 (계약: 하드코딩 금지 — 여기가 소유)
    magnitude: 2           # 밀집도 탐색 반경 (기존 SelfBlink 와 동일 의미)
    tileRange: 6           # 착지 링 스냅 상한 (동일 의미)
    slamDamage: 100        # 일반 도약 슬램 50 의 2배 — 궁극기 체급. Play 로 확정
    slamTileRange: 2       # 예고·피해 반경. 일반 도약 1 보다 넓게
    projectile: (EarthSlamSpikes 계열 — 기존 착지 VFX 재사용, 필요시 별도 지정)
```

- `nightmareMechanics` 는 시트 동기 대상이 아니다(임포터 참조 0) — SO 직접 수정으로 확정된다.
- 경계 배치 확인: 0.7 은 0.2 의 배수가 아니고 0.5·0.9 와도 안 겹친다(README 계약 10).

## 통합 Play 검증 (이 spec 의 검증 질문에 답하는 항목들)

- [ ] 체력 30% 도달 → 보스가 위로 이탈, 뷰·HP 바 사라짐
- [ ] 착지 예고 빨간 타일이 즉시 표시되고, **그 타일 위 방어유닛을 재배치로 빼는 회피가 성립**
- [ ] 이탈 2초 동안: 방어유닛들이 타겟을 다른 적으로 전환 · 보스 체력 불변(DoT 포함) · 보스 위치 불변
- [ ] 2초 후 예고 타일 정중앙에 강하 착지, 예고된 타일과 **정확히 같은 범위**에 슬램 피해
- [ ] 착지 후 즉시 정상 행동(공격·사냥) 복귀
- [ ] 일반 도약(50%·10%)은 이전과 동일 — 피격 가능, 아치 비행
- [ ] 이탈 중 배틀 종료(전멸·나가기) 시 콘솔 에러 0, 예고 잔류 0
- [ ] 슬로모(손패 오픈) 중 발동 시 예고·카운트다운이 함께 느려짐
- [ ] EditMode 전체 무회귀

## 완료 기준

- 위 체크리스트 전부 + **사용자 Play 감각 확인**
- CLAUDE.md 채널 목록 갱신이 같은 커밋에 포함
- 종료 시 `6_handoff_summary.md` 작성 + `docs/spec/README.md` 등재
