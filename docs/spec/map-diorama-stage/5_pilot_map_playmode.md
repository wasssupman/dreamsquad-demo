# 5 — 파일럿 스테이지 + PlayMode 스모크 + 육안 검증 축 4종

## 목적

새 파이프라인의 기능 검증을 닫는다. "디오라마 저작 → 열린 마당 전투"가 실제 플레이 가능한 맵 하나로 성립함을 보이고, 전투 접점 감사(설계 문서)에서 도출한 "코드 무변경·행동 변화" 지점 4곳을 육안으로 확정한다. **밸런스 품질은 범위 밖** (README 계약 10).

## 변경 대상

- 신규 `Assets/_Project/Prefabs/Maps/MapStage_Pilot.prefab` — **KayKit Platformer Pack 조립** 파일럿 맵: `platform_{2x2|4x4}x1` 바닥판 타일링(윗면 = 논리 Y0 보정) + `barrier`/`pillar` 외곽 링(컬트오브램식 닫힌 마당) + `structure_A/B`·`pillar` 내부 차단 + `flag_A` 스폰 2 + `signage_finish` 골 1. 머티리얼은 팩 동봉 URP Lit 2장 그대로
- 신규 `Assets/_Project/Editor/MapStageDummyGenerator.cs` — **일회용 MenuItem 조립 생성기** (unityMCP execute_code 불가 우회 관용구): 레이아웃 테이블 → KayKit 프리팹 인스턴스 배치 + 저작 컴포넌트 부착 + footprint 값을 프랍 이름의 `NxNxH` 에서 제안. 수정→재생성 반복 가능이 목적, 프리팹 확정 후에도 재현용으로 유지
- `MapStagePool.asset` — 파일럿 entry 등록 (기존 덱/플랜 아무거나 짝 — 밸런스 무관)
- `Assets/_Project/Tests/PlayMode/DioramaStagePlayTests.cs` — unit 2 가 신설(스모크). 이 unit 은 필요 시 파일럿 검증 케이스만 추가

## 구현

**PlayMode 스모크는 unit 2 로 이동했다** (critic M-12 — units 2~4 의 «무회귀» 주장에 라이브 경로 테스트가 동행해야 하므로). 이 unit 은 **전체 스위트 재실행 + 파일럿 맵 + 육안 검증**만 담당한다.

**KayKit 조립 규칙**: 바닥판 윗면 = 논리 Y0 보정은 **프리팹 저작(트랜스폼)으로만** 한다 — 런타임 코드 보정 없음(계약 7: 높이는 논리에 없다. critic open question 확정).

**육안 검증 축 5종** (KayKit 더미맵 = 이 파일럿 맵에서 진행 — 2026-08-18 사용자 결정. 결과를 이 문서 하단에 기록):
- ① 순찰 소환물: 소환사 셀이 Walk 가 된 상태에서 순찰 영역·복귀가 자연스러운가 ("Place=벽" 전제 3곳이 뒤집힌 결과)
- ② 공중 적: 차단 프랍 위를 넘는 그림이 수용 가능한가
- ③ 어그로 추격: 마당 전체 경로에서 포위 접근이 자연스러운가
- ④ 골 균열/붕괴: 마커 뷰 연출 재생 (unit 4 재확인)
- ⑤ **가디언 배치 범위** (critic C-2 — 결정 (a) 확정): 전 마당 배치가 허용된 상태에서 배치 그림·BlockZone 차감이 제품 의도대로 보이는가

## 완료 기준

- [ ] 전체 스위트 무회귀: EditMode 두 lane + PlayMode 8분 lane (critic M-4)
- [ ] 파일럿 맵 에디터 Play 1판 완주 (승패 무관, 프리즈/콘솔 에러 0)
- [ ] 육안 검증 축 5종 기록 완료 — 수용 불가 판정이 나오면 해당 축을 후속 spec 후보로 격상하고 이 spec 은 기록으로 닫는다
- [ ] `docs/reference/object-pipeline-map.md` 프랍/타일 표 갱신 (구조 변경 확정 — 워크플로우 5)
