# 3 — 에디터 validator (무효 설정 조기 검출)

## 목적

부착 제한의 무효/무의미 설정을 **에디터에서** 리포트한다. 카드 정의에는 bake 단계가 없어서 unit 1 의 경고는 플레이어가 드롭을 시도하는 런타임에만 나온다 — 잘못된 시트 값이 실제 플레이 세션까지 살아가고, 무효 카드는 부착도 소모도 되지 않아 매치 내내 손패 슬롯을 점유한다(리뷰 확정 실패 모드).

## 변경 대상

- `Assets/_Project/Editor/` — 신규 validator (기존 에셋 스캔 유틸 `UnitAssetScan` 재사용)

## 구현

카탈로그의 `DreamcatcherCard` 전체를 스캔해 아래를 리포트한다. MenuItem 1개 + 리포트 로그(경고 목록 + 총계) 형태로, 기존 시트 import 창의 로그 관행을 따른다.

1. **무효 설정** — `attachRequire==Class` 인데 `attachRequireClass==None` / `attachRequire==UnitId` 인데 `attachRequireUnitId` 가 빈 문자열. 둘 다 fail-closed 라 그 카드는 어떤 유닛에도 붙지 않는다.
2. **없는 유닛 id** — `attachRequireUnitId` 가 `DefenderCatalog.units` 의 어느 `id` 와도 일치하지 않음. 오타/리네임 잔재 검출.
3. **범위 밖 설정 (조용한 무효)** — `attachRequire != None` 인데 `type != Unit`(Squad/Active), 또는 `HasBountyMark()` 인 적 타겟 카드. 이 조합은 게이트 함수를 아예 통과하지 않으므로 런타임 경고조차 안 난다 — validator 만이 잡을 수 있는 유일한 항목이다.

시트 export 시점 경고로 대체하지 않는다: export 는 Unity→시트 방향이고, 문제 값은 시트→Unity import 로 들어온다.

## 완료 기준

- compile 통과.
- 임시로 위 3종 위반 카드를 **코드로 생성**(`ScriptableObject.CreateInstance`)한 EditMode 테스트에서 각 위반이 정확히 1건씩 리포트된다.
- 현재 카탈로그 전체 스캔 결과 위반 0건(기존 카드는 전부 `attachRequire==None`).
