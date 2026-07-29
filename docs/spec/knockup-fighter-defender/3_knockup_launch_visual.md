# 3 — 넉업 띄우기 연출 (뷰 수직 호핑)

## 목적

"공중으로 띄운다"를 화면에서 성립시킨다. sim 무변경 — 적 유닛 view 의 수직 오프셋 one-shot.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/` — 호핑 애니메이션 (SpineUnitView 확장 또는 소형 컴포넌트, 기존 view 구조를 보고 결정)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 히트 이벤트 drain 에서 호핑 구동

## 구현

1. **구동 신호 = 말파이트의 히트 사건**: 기존 히트 이벤트 drain(UnitAttackVisual 또는
   AttackOutputLog — 히트 대상이 실려 오는 쪽) 에서 attacker 유닛 데이터의
   `knockupOnHitSec > 0` 이면 히트 대상들의 view 에 호핑 재생.
   CcEffect(Stun) 쪽 kind 분기 금지(계약 4) — frost_arrow 등 일반 스턴은 안 뜬다.
2. **호핑 커브**: view 로컬 Y 로 포물선 상승·하강, 총 시간 ≈ knockupOnHitSec(뜬 동안 = 스턴 동안).
   높이/시간 커브 값은 SO(유닛 또는 소형 설정 SO — DragSwaySettings 선례) — 하드코딩 금지.
   **sim-Y 에 넣지 않는다** — BoardSpace 가 sim-Y 를 drop 하므로 화면에 안 보인다(프로젝트 기지).
3. `SyncMonoUnitViews` 가 매 프레임 view 위치를 sim 기준으로 덮는 구조라면, 호핑 오프셋은
   sync 이후 가산되는 로컬 채널로 둔다(카메라 연출의 채널 분리와 같은 결) — 구현 지점은
   SpineUnitView 의 기존 오프셋 처리 방식을 따른다.
4. 배치 스킬(unit 1 StunNearby) 경로에도 같은 호핑 재생을 연결한다 — 공격/배치 양쪽에서 재사용.

## 완료 기준

- [ ] 에디터 Play: 말파이트 공격에 히트된 적들이 떠올랐다 떨어지고, 그동안 이동/공격 정지(스턴)와 시간이 일치
- [ ] 오버헤드 체력바·그림자·정렬이 호핑 중 어색하지 않은지 스크린샷 확인
- [ ] frost_arrow(일반 스턴) 대상은 뜨지 않는지 교차 확인
