# 5 — 연출 juice (촤라락 셔플·임팩트·안전)

## 목적

unit 4 의 검증된 코어 위에 발라트로/솔리테어식 시각 juice 를 얹는다. 촤라락 셔플 비주얼, 선물 2장 임팩트, stagger, test fast-forward, 중단 leak 안전. **정착 순서/흐름 계약은 unit 4 에서 이미 고정** — 이 단계는 눈요기와 견고성만 추가하며 순서를 바꾸지 않는다.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/GiftPhaseView.cs` — 셔플/임팩트 연출 + 안전 처리.
- `Assets/_Project/Scripts/Data/Dreamcatcher/GiftConfig.cs` — `shuffleSec` 등 이미 있음; 필요 시 juice 세부 필드 추가.

## 구현

1. **4-3 촤라락 셔플 비주얼**: 12장을 `shuffleSec` 동안 반복 스왑/스프레드(발라트로식) 후 **확정 순서(ordered12)로 재정렬 착지**. 셔플 궤적은 index 파생 결정론 배열(seeded RNG 지양, 프로젝트 관례) — 착지 순서는 반드시 캐시 그대로.
2. **4-2b 임팩트**: 선물 2장 등장 시 punch scale + 플래시/글로우(기존 절차 스프라이트·tint 재사용, 신규 무거운 VFX 지양).
3. **stagger**: 10장 등장·fly-out 을 index stagger 로 촤라락 느낌.
4. **Test fast-forward**(`GiftConfig.fastForwardInTestMode`): 시퀀스 스킵/압축 후 즉시 4-6(반복 iteration 보호).
5. **중단/재시작 leak 안전(critic)**: PrimeTween handle 보관 → Gift 도중 페이즈 강제 전환/파괴/재시작 시 `Sequence.Stop()`·정리. 재진입 시 트윈 중복/누수 0. `OnDisable`/`OnDestroy` 방어.

## 완료 기준

- [ ] 컴파일 통과, `read_console` 에러 0.
- [ ] Play 스크린샷 다회: 촤라락 셔플·선물 임팩트·stagger 시각 확인(발라트로 느낌).
- [ ] **착지 12장 == 캐시 순서 유지**(juice 후에도 계약 3 불변).
- [ ] Test 모드 fast-forward 동작(연출 스킵 후 배치 진입).
- [ ] 재시작 반복 시 트윈 leak/중복/에러 0(`Sequence.Stop` 정리 확인).
- [ ] 인게임 사이클 덱(핸드 카드 순서)이 연출 확정 12장과 일치(e2e).
- [ ] two-track: 일반 code-review(프레젠테이션/덱 조합). ECS 변경 0 → ecs-review 대상 아님.
