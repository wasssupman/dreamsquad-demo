# Kickoff Handoff — CC Pipeline & Obstacle

**Status**: 브레인스토밍 완료, 구현 미착수.
**Spec 폴더**: `docs/spec/cc-pipeline-and-obstacle/` (README + 0~9 작업 단위).
**작성**: 2026-04-28.
**다음 작업자**: oh-my-claudecode:executor (Sonnet).

## 본 spec 의 자리

적의 이동에 가벼운 ECS 임펄스를 도입하여 (1) 디펜더 공격 넉백, (2) 디펜더 배치 시 밀어내기, (3) 디버그-spawn 큐브 차단을 구현한다. 부수 작업으로 기존 `SlowEffect` 를 통일된 `CcEffect` buffer 로 마이그레이션. CC 확장은 enum + switch case 추가만으로 가능하도록 설계.

## 브레인스토밍 결정 요약

| 결정 | 채택 | 이유 |
|---|---|---|
| 물리 모델 | 가벼운 ECS 임펄스 (Unity Physics ❌) | 셀-기반 시뮬레이션 유지, 모바일 부담 최소 |
| 큐브 수명 | 시간 기반 자동 소멸 (HP/Taunt ❌) | 적 공격 시스템 신설 회피, 본 spec 범위 통제 |
| 넉백 트리거 | 디펜더 SO 필드 + 미래 스킬 카드 (양쪽) | 일반화된 큐 채널이 둘 다 흡수 |
| 큐브 배치 | 디버그 메뉴/키 (실제 게임 통합은 후속) | 본 spec 검증 질문은 게임감, 등장 메커니즘은 별개 |
| Impulse vs 큐브 | 큐브 우선. 임펄스도 cell trim 통과 | 큐브가 진짜 벽이라는 game feel 보존 |
| CC 추상화 | `CcEffect` IBufferElementData + `CcKind` enum. 기존 `SlowEffect` 도 마이그 | DOTS 에서 인터페이스 dispatch 불가, 통합 buffer + switch 가 Burst 호환 추상화 |

## 구현 순서 (1 파일 = 1 commit)

```
0 ──▶ 1 ──▶ 2★ ──▶ 3 ──▶ 4 ──▶ 5
                   │           ▶ 6
                   ▼
                   7 ──▶ 8 ──▶ 9★
```

★ = 사용자 PlayMode manual 확인 필수 게이트. Unit 2 = Slow 회귀 게이트, Unit 9 = feature 게이트.

## 절대 보존 (되돌리지 말 것)

- `EffectSpawner.ApplySlow(em, entity, duration, multiplier)` 시그니처 유지. BattleBridge.cs:901 호출자 영향 0. 내부만 buffer 경로로.
- DefenderSO 5개 신규 필드 default 0. 기존 SO asset 영향 0.
- `LocalTransform` writer 는 MovementSystem 단독 (불변).
- `CcEffect` buffer / `ObstacleSingleton.blockedCells` = Effects 맥락 소유, Movement read-only.
- Tornado pull / Portal 분기 변경 금지 (MovementSystem 안의 기존 분기).
- `DamageBoost` / `CooldownReduction` / `TornadoField` / `PortalLink` 의 `EffectTickSystem` 루프 보존 (CC 패밀리 아님 — 디펜더 buff / carrier).
- FlowFieldBuilder 는 `ObstacleSingleton` 참조 ❌ (큐브 때문에 재경로 안 함).
- 적은 본 spec 범위에서 공격 능력을 가지지 않는다.

## 작업 시 주의

- **Burst 호환 유지**: switch on enum + `DynamicBuffer<CcEffect>` 순회는 Burst-compile 가능. 인터페이스 dispatch / managed type 사용 ❌.
- **SO 직접 read 금지**: 디펜더 SO 데이터는 ECS 미러 컴포넌트 (`DefenderRuntimeData` 류, 실제 이름은 코드 확인) 를 통해 읽는다. Unit 4 가 미러 필드 추가도 책임.
- **Unit 5 의 attacker 식별**: `IncomingDamage` buffer 또는 `DefenderAttackEventsSingleton` 중 어느 통로가 attacker entity 정보를 들고 있는지 실제 Combat 코드 확인 후 그 통로 사용.
- **Unit 6 의 트리거 위치**: `defender-on-place-skills` spec 이 구축한 on-place 디스패치 (`BattleBridge.ActivateDeployedDefender` 또는 그 후속) 와 합류. 별도 트리거 시점 만들지 말 것.
- **Unit 2 회귀 검증 필수**: Slow 효과를 발동하는 디펜더로 적 1마리에 적용 → 속도 변화 시각 확인. commit 전 사용자 manual 확인 받기.
- **단위 테스트**: 회귀 방지 수준만 (CLAUDE.md). EditMode 필수 = ClampToBoundary, CcEffect merge, Movement 합성 수학.

## 사용자 확인 protocol

각 unit commit 후:
- **Unit 0, 1, 3, 4, 7, 8**: compile + test 통과 보고 → 사용자에게 "다음 unit 진행해도 됨?" 한 줄 확인.
- **Unit 2 ★**: PlayMode Slow 효과 회귀 시각 확인. 사용자 통과 후 다음 진입.
- **Unit 5, 6**: 샘플 SO 값 채워 PlayMode 시각 확인.
- **Unit 9 ★**: feature 4 시나리오 (기본 차단 / 다중 적 / knockback × cube / push × cube) 사용자 확인 → spec 종료 → `10_handoff_summary.md` 작성 + README 상태 갱신.

각 unit 완료 후 해당 작업 단위 파일의 "완료 기준" 섹션 하단에 확인 일자 + 커밋 해시 한 줄 추가 (CLAUDE.md 기본 워크플로우).

## 작업 시작점

`docs/spec/cc-pipeline-and-obstacle/0_cc_data_model.md` 를 읽고 그 파일만 가지고 Unit 0 작업 진행. README.md 의 공통 원칙 8줄 + 본 handoff 의 "절대 보존" 섹션을 상시 컨텍스트로 유지.
