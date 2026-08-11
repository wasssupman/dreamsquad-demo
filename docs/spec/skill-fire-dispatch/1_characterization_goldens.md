# 1 — 무보호 4종 특성화 골든 신설

## 목적

이전 대상 12행 중 **동작 골든이 있는 것은 5행뿐**이다(자장가 · 장막 · 가호 · 빈사폭주 ·
발사 명세). 나머지는 순수 함수 테스트(`BlinkMathTests`·`AuraPulseTests`)만 있어 arm 의
실제 거동을 아무도 안 본다. **가장 위험한 이전(궁극기·도약)이 정확히 무보호 구간에서
일어난다.**

프로젝트 자체 규칙 — "증상 단언을 먼저 넣고 빨간 것을 확인한 뒤 고친다". 여기서는
"이전 전에 현행 거동을 고정하고, 이전 후 그대로인지 본다"가 그 형태다.

## 변경 대상

| 파일 | 대상 |
|---|---|
| `Tests/EditMode/UltimateLeapCharacterizationTests.cs` | 궁극기 — 개시(상태 부착·착지점 고정)·예고 만료·착지 3단(텔레포트 요청 / 슬램 캐리어 1개 / 상태 해제 쌍) |
| `Tests/EditMode/SelfBlinkCharacterizationTests.cs` | 도약 — 밀집 셀 해석 → `BlinkRequestEvent` + 도약 비주얼 + 슬램 파라미터 |
| `Tests/EditMode/WhipAuraCharacterizationTests.cs` | 채찍질 — 펄스마다 같은 진영·host 제외·반경 내 대상에 이속 모디파이어(TTL 갱신 포함) |
| `Tests/EditMode/ThresholdTileAoeCharacterizationTests.cs` | 경계 자폭 — 임계 1회 소모·캐리어 파라미터 |

## 구현

- **EditMode bare World 로 세운다** — `ProjectileEmitterIntegrationTests` 선례(브리지 없이
  `new World(...)` 에 감시 시스템을 직접 올리고 슬롯을 손으로 구성, 고정 dt 로 tick).
  PlayMode 는 실프레임 델타라 수치 재현이 안 된다(계약 10).
- **단언은 "무엇이 나갔나"에 건다** — 채널로 나간 이벤트의 개수·대상·파라미터. 내부
  구현이 아니라 관측 가능한 출력이라 이전 후에도 그대로 성립한다.
- **슬롯을 손으로 만드는 함정 주의**: 저작이 틀려도 초록이 되는 형태다(`BossLullabyTest`
  머리 주석이 지적한 것). 여기서는 **arm 거동 고정이 목적**이라 손 구성이 맞고, 저작
  검증은 기존 bake 테스트가 담당한다는 것을 파일 주석에 남긴다.
- **지금 코드가 이상해 보여도 그대로 고정한다.** 특성화 테스트는 옳음이 아니라 **현재**를
  기록한다. 버그로 보이는 것이 있으면 고치지 말고 문서에 적어 후속으로 넘긴다.

## 완료 기준

- [ ] 4개 테스트 파일 그린 (신규 EditMode)
- [ ] **각 테스트가 실제로 무는지 확인** — 대상 arm 의 파라미터를 임의로 바꿔 돌리면
      빨개지는 것을 1회씩 확인하고 결과를 이 문서에 한 줄로 남긴다
- [ ] EditMode 전량 그린 (사전 존재 실패 제외)
- [ ] 코드 변경 0 — 이 unit 은 테스트만 추가한다(프로덕션 파일 diff 없음을 검산)
