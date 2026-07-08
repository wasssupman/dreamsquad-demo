# 4 · 이식 가이드 + 구 spec 계약 정리

## 목적

키링 연출을 다른 프로젝트에서 재구축할 때 필요한 지식을 가이드 문서 1개로 남긴다 (지식 이식 — 코드 이동/asmdef 없음, 2026-07-08 사용자 결정). 코드와 모순이 된 구 spec 계약을 정리한다.

## 변경 대상

- 신설: `docs/reference/keyring-portability.md`
- 수정: `docs/spec/lobby-keyring-drag/README.md` — 계약 1("인게임 무변경·코드 공유 없음") 폐기 주석 + `keyring-unify` 포인터

## 구현

가이드 목차 (30~80줄, prose 복제 금지 — 포인터와 함정 중심):

1. **동작 모델 계약** — 고리=손가락, 무게추 스프링(spring/damping/maxSpeed 의미), 기울임=줄 방향+maxAngle 클램프(머리중심 회전), 하이라이트=안정 목표(스윙 위치 아님), 낙하=중력+바운스.
2. **가져갈 파일** — `KeyringSim.cs`, `KeyringStyle.cs`, `KeyringHologramCommon.hlsl`, `UICordHologram.shader`/`WorldCordHologram.shader`, `Sprites/Keyring/*`, 머티리얼 2종.
3. **컨텍스트 접점** — 새로 쓰는 것은 좌표 산출(스크린→로컬/월드)과 세션 관리(시작/갱신/종료/취소)뿐. 인게임형(월드)·아웃게임형(UGUI) 두 레퍼런스 구현 포인터.
4. **함정 리스트** (겪은 순서 아닌 심각도 순):
   - 수직 분리는 camUp(화면 세로) 기준 — 월드-up 은 기울어진 카메라에서 겹침.
   - 워밍업(가속 램프) 금지 — 억제 후 풀릴 때 큰 탄성 스냅. 튐은 maxSpeed 로.
   - 기울임 회전 중심은 머리 — 발/중심 피벗이면 반대로 흔들림.
   - 줄 폭 sub-pixel 이면 렌더 컬링 (인게임 초기 "줄 안 보임" 원인).
   - 줄 끝은 머리 안쪽(cordAttachDrop) — rect 상단이면 투명 여백 위에 뜸.
   - 홀로 셰이더는 vertex color 를 곱함 — 스타일 적용 시 white 강제, 틴트색 잔존 시 오염.
   - 공유 include 는 순수 float + t 파라미터 — CG(fixed/UnityCG)↔URP(HLSL) 헤더/타입 비호환.
   - UI(uv.y)↔LineRenderer(uv.x) 길이 축 전치 + 글리치 축 재지정 + wrap=Clamp.
   - 가산 블렌드는 밝은 배경 washout — 도입 시 밝은 배경 렌더로 먼저 판정.
   - 시간 구동 효과의 회귀 검증은 same-frame A/B (전/후 캡처 diff 무의미).
   - 스타일 미할당 폴백도 "정상 동작"이라 이전 실패를 육안으로 놓침 — 실제 스타일 렌더 여부를 확인.

lobby-keyring-drag README 계약 1 은 삭제하지 않고 취소선/주석으로 폐기 표기 (역사 보존) + "현행 계약은 `docs/spec/keyring-unify/`" 포인터.

## 완료 기준

- `docs/reference/keyring-portability.md` 존재, 목차 4절 충족, 80줄 이내.
- `lobby-keyring-drag/README.md` 계약 1 폐기 표기 — 문서·코드 모순 해소.

확인 2026-07-08 — 가이드 작성(4절, 함정 11건 — unit 2 에서 실증된 uv swap 누락 포함),
구 계약 1 취소선 폐기 + keyring-unify 포인터. 커밋은 본 unit 커밋 해시 참조.
