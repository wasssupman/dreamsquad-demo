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

확인 2026-07-25 — 컴파일 에러 0 · EditMode 1332건(1330 pass / 0 fail / 2 기존 Ignore), 신규 5건(정상·무효 2종·없는 id·카탈로그 부재 시 생략·범위 밖 2종·범위밖+무효 동시 2건 보고) · 메뉴 실사 실행 로그 `카드 44장 중 0장에서 0건. 위반 없음.`

구현 노트(스펙과 다른 점 — review 지적): 스캔 소스는 "카탈로그"가 아니라 **폴더**(`Assets/_Project/Data/Dreamcatcher`, `UnitAssetScan.Enumerate<DreamcatcherCard>`)다. 현재는 등가다(카드 44장 전부 이 폴더, `DefenderCatalog.asset` 1개). 다만 README 가 validator 를 "BountyMark×제한 조용한 무효의 유일한 검출 수단"이라 규정하므로, **폴더 밖에 등록된 카드는 검사에서 빠진다** — 카드가 다른 폴더에 생기면 `DreamcatcherCardCatalog` 순회로 바꿀 것.

검증 코어는 순수 `CollectWarnings(card, knownUnitIds)` 이고, 배치 메뉴(`Wassup/Tools/Validate Dreamcatcher Attach Requirements`)와 `DreamcatcherCard` 인스펙터 HelpBox 가 이를 공유한다(`UnitVisualDataValidator` 선례). 인스펙터는 매 `OnInspectorGUI` 에셋 스캔을 피하려 id 존재 검사를 생략 — 그 항목은 배치 메뉴 담당.
