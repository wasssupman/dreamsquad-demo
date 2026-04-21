# Phase 5 — 수직 슬라이스 마감: Billboard / Timer / Bot / Measurement

> Phase 5는 외부 플레이어 세션에 필요한 최소 시각 품질과 측정 장치를 붙인 단계다. Phase 5 당시 Spine은 보류했고, 유닛 렌더는 Billboard Quad 기반으로 정리했다. Spine 하이브리드와 prefab VFX는 이후 Phase 8에서 도입됐다.

---

## 1. 목표

- Cube 기반 유닛 비주얼을 Billboard Quad로 교체해 전투 가독성을 높인다.
- 3분 타이머와 결과 화면 비교 정보를 도입해 한 판 단위의 압박감을 만든다.
- 더미 봇 스코어와 측정 로그를 붙여 H1/H2/H3 검증을 위한 세션 데이터를 남긴다.
- 이후 Sprite/Spine/VFX 확장을 위해 렌더 데이터가 ScriptableObject를 통해 흐르도록 유지한다.

### 비목표

- Spine 도입. Phase 5에서는 보류했고 Phase 8에서 MonoBehaviour 하이브리드로 도입했다.
- 정교한 아트 파이프라인.
- 서버/비동기 토너먼트 백엔드.

---

## 2. 구현 스펙

### 2.1 Billboard 렌더

- 유닛/투사체 렌더는 Quad mesh + material 기반으로 전환됐다.
- `DefenderUnitData.visualMesh`, `visualMaterial`, projectile visual 필드를 통해 렌더 자산을 참조한다.
- Billboard 단계는 기능 구분과 시야 가독성 확보가 목적이며, 최종 애니메이션 품질은 Phase 8 Spine/VFX로 확장됐다.

### 2.2 Timer / Result

- 전투는 제한 시간 기반 루프를 갖는다.
- 결과 화면은 Victory/Defeat와 Restart/Redraft 흐름을 제공한다.
- Restart/Redraft semantics는 이후 Phase 6 GamePhase와 cost reset 규칙으로 확정됐다.

### 2.3 Bot Score / Measurement

- 더미 봇 비교와 세션 로그는 H1/H2/H3 검증을 위한 기반이다.
- 실제 분석 자동화는 프로토타입 단계에서 최소 범위로 유지한다.
- 로그 스키마는 이후 Phase 6~8에서 cost, skill loadout, VFX/phase 정보와 함께 확장됐다.

---

## 3. 작업 결과

- [x] Billboard Quad 기반 유닛/투사체 시각화.
- [x] 체력바와 피격 플래시가 Quad 기반 렌더와 공존.
- [x] Timer / Result 화면 흐름.
- [x] Restart / Redraft가 후속 Phase 상태 머신에서 유지됨.
- [x] 측정 로그 기반 유지.
- [ ] Android 실기기 장시간 플레이 검증.

---

## 4. 후속 연결

- Phase 6: CostRuntime, GamePhase, Placement 페이즈가 Timer/Result 흐름을 명시화했다.
- Phase 7: 매 판 2종 skill loadout으로 판별력을 확장했다.
- Phase 8: Billboard fallback을 유지한 채 defender Spine 하이브리드와 prefab VFX를 도입했다.

---

## 5. TRD 금지 패턴 재적용

- 렌더 개선은 MonoBehaviour/Presentation 또는 ECS RenderMesh 경로에 한정하고 Combat/Movement 로직을 변경하지 않는다.
- 렌더 자산 참조는 ScriptableObject 필드에서 나온다.
- GameManager 외 singleton을 만들지 않는다.
- "향후 애니메이션"을 이유로 미사용 추상화를 만들지 않는다.

---

**문서 버전**: v1.0 (구현 스펙 통합)
**상태**: 구현 완료. Android 실기기 검증은 residual 검증으로 관리.
