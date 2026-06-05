# 2 — 폰트·머티리얼 (Bangers SDF + 아웃라인)

## 목적

데미지 숫자용 스타일리쉬 폰트와 강조용 머티리얼을 만든다. 자유 라이선스(OFL) 디스플레이 폰트 **Bangers**(굵고 만화풍, 데미지 팝업에 적합)를 TMP SDF 로 변환하고, 검은 아웃라인 + 페이스 dilate 머티리얼을 구성한다.

## 변경 대상 (신규 자산)

- `Assets/_Project/Fonts/Bangers-Regular.ttf` — 원본 폰트 (OFL, googlefonts/bangers)
- `Assets/_Project/Fonts/Bangers-OFL.txt` — 라이선스 원문 (재배포 의무)
- `Assets/_Project/Fonts/Bangers SDF.asset` — TMP SDF 폰트 에셋 (Dynamic, 512x512, SDFAA, sampling 90, padding 9)
- `Assets/_Project/Fonts/DamageNumber Outline Mat.mat` — 아웃라인 머티리얼

## 구현 (수행 완료)

1. `curl` 로 `google/fonts` 에서 Bangers-Regular.ttf + OFL.txt 다운로드 → `Assets/_Project/Fonts/`.
2. Unity force refresh 로 ttf 임포트.
3. `execute_code`(codedom) 로:
   - `TMP_FontAsset.CreateFontAsset(font, 90, 9, SDFAA, 512, 512, Dynamic, true)` → `Bangers SDF.asset`.
   - atlas 텍스처 + material 을 sub-asset 으로 `AddObjectToAsset`(영속화 필수).
   - `TryAddCharacters("0123456789-")` 로 숫자+마이너스 11글리프 사전 래스터(첫 히트 히치 방지).
   - 아웃라인 머티리얼: `new Material(fontAsset.material)` 복제 → `OUTLINE_ON`, `_OutlineColor=black`, `_OutlineWidth=0.22`, `_FaceDilate=0.12`.

## 계약/주의

- **Dynamic SDF**: 글리프는 원본 ttf 에서 필요 시 래스터. ttf 가 빌드에 포함돼야 함(`Assets/_Project/Fonts/` 유지). 숫자만 쓰므로 경량.
- **색은 머티리얼이 아니라 TMP 컴포넌트에서**: 페이스 색(흰→노랑→주황→빨강)은 unit 3 에서 `TMP.color`(버텍스 색)로 값에 비례해 설정. 아웃라인(검정)만 머티리얼 고정. → 머티리얼 하나로 모든 색 처리.
- 그라데이션(상단 밝게)은 unit 3 의 `TMP.colorGradient`(`VertexGradient`)로 컴포넌트 단위 적용.
- 재실행 멱등: 기존 같은 경로 에셋 삭제 후 재생성(safety_checks 끄고 실행).

## 완료 기준

- ✅ 4개 자산이 `Assets/_Project/Fonts/` 에 존재(.meta 포함). `Bangers SDF.asset` glyphs=11.
- ✅ 아웃라인 머티리얼 생성(OUTLINE_ON + 검정 아웃라인).
- 시각 검증은 unit 3 프리팹 + unit 4 Play 에서.

✅ 2026-06-05 자산 생성 확인 (execute_code 결과 + 파일 시스템 확인). 커밋 대기.
