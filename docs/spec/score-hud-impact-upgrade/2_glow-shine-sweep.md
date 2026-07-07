# 2 — 발광 펄스 + 대각 샤인 스윕

## 목적

처치 순간 점수 숫자가 **발광 펄스**(순간 밝아졌다 안착) + **대각 라이트 스트릭 스윕**(좌→우로 훑는 광택)으로 "빛나며 오른다". 배경(바쁜 전투 화면)과 분리되어 강렬하게 읽히게 한다. 진짜 URP Bloom이 아닌 모바일 안전 가짜 글로우.

## 변경 대상

- `Assets/_Project/Scripts/UI/ScoreHudView.cs`
- 머티리얼: `Kanit Outline Mat.mat`(unit 0 에서 생성, Kanit SDF atlas)에서 파생한 **TMP SDF 머티리얼** glow/underlay 변종. 별도 `Score Impact Mat` 이 필요하면 Kanit atlas 기준으로 저작.
- 샤인 오버레이용 additive `Image`(+`RectMask2D`)

## 구현

- **발광 펄스**: 처치 시 TMP 머티리얼 글로우/underlay 파라미터를 짧게 펄스(PrimeTween `Tween.MaterialProperty`/`MaterialColor` 또는 코드 SetFloat), 또는 값 뒤 라디얼 글로우 `Image` 알파/스케일 펄스. 데미지 스펙의 "가짜 글로우 밴드"와 같은 판단(모바일 안전).
- **샤인 스윕 — `DraftCardFoil_UI` 재활용 금지**: 그 셰이더는 UGUI **Image** 셰이더(UnityUI.cginc)라 TMP 폰트 머티리얼로 못 쓰고(텍스트 렌더 깨짐), `_Time` 기반 무한 shimmer 라 one-shot 아니다. 대신 **별도 additive `Image` 오버레이**를 숫자 위 `RectMask2D` 안에서 좌→우 1회 이동(PrimeTween one-shot, 처치마다 트리거). 글리프 모양 마스킹이 필요하면 숫자 자체를 마스크로 쓴다(foil 셰이더 아님).
- **가독성 보강**: 필요 시 어두운 드롭섀도/아웃라인으로 배경 분리(데미지 스펙 강도 rev와 동일 결).
- **직렬화/에셋**: 글로우 강도·펄스 시간·스윕 속도·색은 `[SerializeField]` + `.mat` 파라미터. 하드코딩 금지.

## 완료 기준

- compile: CS 에러/경고 0.
- Play: 처치 시 발광 펄스 + 샤인 스윕이 보이고, 바쁜 배경 위에서도 숫자가 강렬히 분리됨.
- 오프스크린 렌더/스크린샷으로 글로우·스윕 육안 확인(플랫 bg는 관대 — 최종은 사용자 Play).
- 진짜 Bloom/post-FX 미사용 확인(모바일 안전).
