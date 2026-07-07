# Handoff — spine-runtime-4-2-upgrade (unit 0~3 완료, 2026-07-07)

## Commit

- `ba866888` docs(spec): 계획 수립
- `b758aa29` unit 0 — 3.8 스켈레톤 리소스 전량 퇴역
- `107dce7e` unit 1 — 런타임 3.8 → 4.2.120 스왑
- `0ea1343a` unit 2 — 예제 스켈레톤 임시 wiring + 파이프라인 스모크
- `26368fa9` unit 3 — 신규 리소스 수급 규약 문서화

## Implemented

- spine-unity **4.2.120** (`spine-unity-4.2-2026-05-29.unitypackage`) 로 교체. Unity 6000.4.3f1 에서 프로젝트 코드 **무수정 컴파일** (asmdef 이름 동일, `SkeletonDataModifierAsset`·`GetColor/SetColor` 존속)
- 3.8 리소스 전량 삭제 (Characters·_Project/Spine 세트 + 미임포트 원본 8종 + BattleScene 의 비활성 BellKnight 프리뷰 오브젝트)
- `SkeletonFlipX.asset` 은 `Characters/` 로 이동 (GUID 보존, R100 rename)
- [임시] Defender_Scout→spineboy-pro, Enemy_Vanguard→goblins(goblingirl) wiring — 실리소스 도착 시 원복
- lessons 03 재작성: "3.8 고정 금지 규칙" → "4.2 고정" + 수급 규약 8항목

## Key Files

- `docs/spec/spine-runtime-4-2-upgrade/` — README + unit 0~4 (각 파일 하단에 검증 기록)
- `Assets/_Project/Editor/SpineUpgradeSmoke.cs` — 배치 검증 스크립트 (unit 4 재사용 후 삭제 예정)
- `docs/reference/lessons/03-rendering-assets.md` — 4.2 규칙/수급 규약

## Verified

- 배치 컴파일 그린 (unit 0/1/2 각 1회) + BattleScene 로드 에러 0 (rootCount=14)
- `SpinePipelineSmoke` PASS: 4.2.22 데이터 로드, goblingirl 스킨 해석, AnimationState 재생, `SkeletonFlipXModifier.Apply()` rootScaleX=-1
- **미수행**: 에디터 GUI Play, URP 실렌더, Android 실기기 — 배치 `-nographics` 한계. unit 4 실리소스 임포트 시 확인 필요

## Notes

- 배치 `-importPackage` 는 컴파일 에러 상태에서 abort ("Scripts have compiler errors") → unitypackage 를 tar.gz 직접 추출로 우회 (GUID/meta 보존, lessons 03 기록)
- Defender 15종(Scout 제외)·Enemy_Tanker 의 `skeletonDataAsset` dangling GUID 는 **의도적 유지** — unit 4 재연결 대상 목록이기도 하다
- TMP 폰트 SDF 3종의 워크트리 churn 은 배치 실행 부산물 — 커밋 오염 방지 위해 미커밋 방치, 정리하지 않았음
- `Assets/Editor/SpineSettings.asset` 은 4.2 가 재생성한 기본값

## Follow-up

- **unit 4 (블록: 신규 리소스 수급)**: 규약(lessons 03) 체크리스트대로 임포트 → Defender 16종 + Enemy 재연결 → 임시 wiring 원복 → Spine Examples 유지/삭제 결정 → 에디터 Play + URP 렌더 + 실기기 확인
- 후속 후보 (README): URP 전용 셰이더 UPM, 4.2 물리 constraint, SkeletonGraphic
