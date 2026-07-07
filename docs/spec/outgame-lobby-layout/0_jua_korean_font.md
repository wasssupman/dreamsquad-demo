# 0 — Jua 한글 TMP 폰트 에셋

## 목적

타이틀 "꿈결특공대"를 렌더할 한글 지원 TMP 폰트를 확보한다. 프로젝트에 한글 폰트가 없어(LiberationSans/Anton/Bangers 전부 라틴 전용) 신규 임포트가 필요하다. 폰트: **Jua (주아체)**, OFL 라이선스, 둥글고 캐주얼한 라운드 계열.

## 변경 대상

- `Assets/_Project/Fonts/Jua-Regular.ttf` (신규, OFL 원본)
- `Assets/_Project/Fonts/Jua-OFL.txt` (라이선스 텍스트)
- `Assets/_Project/Fonts/Jua SDF.asset` (신규, TMP 폰트 에셋)

## 구현

1. Jua-Regular.ttf 를 OFL 배포처(Google Fonts / `google/fonts` 저장소 raw)에서 내려받아 `Assets/_Project/Fonts/`에 배치한다. OFL 라이선스 텍스트도 같은 폴더에 `Jua-OFL.txt`로 저장한다 (기존 Anton/Bangers 폴더 관례와 동일).
2. Unity 임포트 후 TMP Font Asset 생성:
   - **Atlas Population Mode: Dynamic** — 타이틀 텍스트가 바뀌어도 런타임에 글리프를 채운다. 한글 전체 정적 SDF(11,172자)는 만들지 않는다.
   - Sampling Point Size / Atlas Resolution 은 TMP 기본값(예: 512×512, 자동)로 두되 타이틀 크기에서 또렷하면 충분.
   - Render Mode: SDFAA.
3. 생성된 `Jua SDF.asset`의 source font file 이 임포트한 ttf 를 가리키는지 확인.

## 완료 기준

- `Assets/_Project/Fonts/Jua SDF.asset`이 존재하고, TMP Font Asset 타입으로 임포트 에러 없이 로드된다.
- `read_console`에 폰트 관련 컴파일/임포트 에러가 없다.
- 임시 TMP 텍스트에 "꿈결특공대"를 입력해 5글자가 tofu(□) 없이 렌더됨을 확인 (다음 unit 3에서 실제 적용).
- OFL 라이선스 텍스트가 폴더에 동봉되어 있다.
