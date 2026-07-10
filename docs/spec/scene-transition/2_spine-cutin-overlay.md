# 2 — Spine 브랜드 컷인 오버레이 (D)

> **게이트**: 단위 0+1 로 "끊김 없는 전환" 검증 질문이 종결·커밋된 뒤 진입. 훅(단위 0)은 이미 no-op 로 존재하므로 이 단위는 그 위에 얹기만 한다(재작업 0).

## 목적

단위 0 의 cover(가림) 절정 순간에 Spine 그래픽 컷인을 재생해, 단순 페이드를 브랜드 연출로 격상한다. **이 스펙의 목적(브랜드 톤) 자체**다. 방향(into-battle / to-lobby)은 파라미터로 인지하되 **초기엔 공용 컷인 1벌**을 재생(variant 2벌 authoring 은 YAGNI, 단일이 실제 어색할 때만).

## 변경 대상

- `Assets/_Project/Resources/SceneTransition.prefab` — 전환 캔버스 안에 `SkeletonGraphic` 컷인 오브젝트 추가.
- `Assets/_Project/Scripts/Core/SceneTransition.cs` — 단위 0 의 no-op 컷인 훅을 실제 재생으로 채운다. 컷인 애니 이름은 컴포넌트 SerializeField(프리팹 authoring, 별도 SO 없음).
- 컷인용 Spine 에셋(SkeletonDataAsset). **1단계: 기존 로비 스켈레톤 재사용 placeholder — 오직 배관(훅→재생→cover-out 병행) 검증 전용. 미학 판정은 하지 않는다.** 2단계: 전용 스와이프 컷인 애니가 authored 되면 별도 커밋으로 교체.

## 구현

- 컷인 오브젝트는 **전환 캔버스 소속**(파괴되는 씬에 두지 않음 — 계약 #8). `SkeletonGraphic`(Canvas 용), SkeletonAnimation(월드) 아님.
- 재생 시퀀스: cover-in 완료 → 컷인 애니 재생(enter) → min cover / 로딩 대기 → 씬 활성 → 컷인 exit 와 cover-out 병행.
- **방향 파라미터**: `Go(sceneName)` 이 target 으로 direction 을 넘기고 훅이 받는다(구조는 유지). 초기 구현은 방향 무관 공용 애니 1벌 재생. 애니 2벌 분기는 후속.
- Spine import 함정 주의: 한글 파일명 NFC 정규화 + 런타임 4.2 데이터만(구 3.8 혼용 금지) — `docs/reference/lessons/` 및 memory 참조.

## 완료 기준

**배관(이번 단위 필수):**
- compile clean.
- Play 검증: START(→Battle) / 나가기(→Outgame) 전환에서 컷인 훅이 호출돼 SkeletonGraphic 이 화면 가림 위에 재생되고, 로딩 노출 없이 다음 씬으로 이어짐.
- min cover time 동안 컷인이 최소 1회 재생됨(깜빡임 없음 — 계약 #5).
- 전환 반복(START→나가기→START) 시 컷인·페이드 정상 재진입(멱등 — 계약 #2).

**미학(별도 판정, placeholder 로 통과 처리 금지):**
- placeholder 스켈레톤은 "컷인이 좋은가"를 판정하지 않는다 — 배관만 확인. 전용 authored 컷인이 나온 뒤 오프스크린/Play 스크린샷으로 육안 검증하고 사용자 확인.
