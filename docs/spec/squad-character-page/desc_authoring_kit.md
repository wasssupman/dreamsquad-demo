# desc 저작 킷 — 시트 세팅용 데이터 + 프롬프트

> squad-character-page unit 6·7 부속. Defenders 시트 `desc` 컬럼을 채우기 위한 도구.
> 워크플로: 이 프롬프트 + facts 를 LLM 에 넣어 desc 생성 → 시트 `desc` 컬럼에 붙여넣기 → 임포트.
> facts 는 SO 실측(2026-07-18). 밸런스/메커니즘 바뀌면 재추출(export 또는 execute_code).

---

## ① 설명 생성 프롬프트 (LLM 에 그대로 복사)

```
너는 비동기 토너먼트 디펜스 게임의 방어 유닛 "설명문(desc)" 카피라이터다.
아래 표는 각 유닛의 실제 게임 데이터다. 각 유닛의 한국어 desc 를 작성하라.

[규칙]
- 길이: 1~2문장(약 30~70자). 캐릭터 상세 패널에 들어가는 짧은 소개.
- 톤: 캐주얼하고 개성 있게. 역할·전투 방식이 직관적으로 전달되게.
- 정확성: 표의 메커니즘만 반영한다. 표에 없는 능력을 지어내지 마라.
  (원/근접, 방향 다연발, 다중 타격, 도발 유지, 배치 효과, 해저드 설치, 치유 등)
- 숫자(체력·데미지·사거리·코스트)는 나열하지 마라 — 별도 스탯란에 표기된다.
  단 "장거리 저격" "초고속 연사" 같은 정성적 뉘앙스는 OK.
- 이름의 테마를 활용하라: 파이어/아이스/포이즌/블로킹 캐스터는 표엔 "해저드 설치"로만
  나오지만 이름대로 불/얼음/독/방벽 속성을 살려 서로 다르게 쓴다.
- 같은 클래스라도 개성을 분리하라(예: 스나이퍼=장거리 일격, 머신거너=근거리 난사,
  스카우트=값싼 정찰). 클래스(레인저/가디언/파이터/캐스터/서포트)와 등급 느낌을 살려라.

[출력 형식]
JSON 배열만 출력한다(설명·머리말 없이). 각 원소는 {"id","desc"} 두 키.
시트 import / 파일 갱신에 바로 쓸 수 있는 형태다:
[
  { "id": "archer", "desc": "..." },
  { "id": "sniper", "desc": "..." }
]
id 는 아래 표의 값을 그대로(변경 금지), 17개 전부 포함.

[유닛 데이터]
id            이름          클래스     등급     전투    특성
archer        아처          Ranger    Rare    원거리   배치 시 주변 속박
artillery     아틸러리       Ranger    Common  원거리   장거리·느린공속·고화력(포격)
bastion       배스티온       Guardian  Rare    근접     초고체력 탱커·3체 동시타격·도발2·배치 즉시 광역폭발
blocking_caster 블로킹캐스터  Caster    Epic    근접     해저드 설치(방벽/차단 성격)
bruiser       해적          Fighter   Epic    근접     고체력·3체 동시타격·배치 즉시 광역폭발
cannon        캐논          Ranger    Common  원거리   고화력·느린공속·배치 즉시 광역폭발
fire_caster   파이어캐스터   Caster    Epic    근접     해저드 설치(불 속성)
guardian      가디언        Guardian  Common  근접     2체 동시타격·도발2·배치 시 주변 아군 강화·해저드 설치
healer        힐러          Support   Rare    근접     아군 치유·3체 대상
ice_caster    아이스캐스터   Caster    Epic    근접     해저드 설치(얼음 속성)
machine_gunner 머신거너      Ranger    Common  원거리   방향지정 10연발 난사·배치 시 전방 발사·저화력 고속
marksman      마크스맨      Ranger    Common  원거리   중장거리 정밀사격·배치 시 전방 발사
piercer       피어서        Ranger    Common  원거리   관통 사격·배치 시 전방 발사
poison_caster 포이즌캐스터   Caster    Epic    근접     해저드 설치(독 속성)
ranger        레인저        Ranger    Common  원거리   초고속 연사·저화력·배치 시 스킬 쿨다운 감소
scout         스카우트      Ranger    Common  원거리   최저코스트 정찰·배치 시 코스트 획득
sniper        스나이퍼      Ranger    Rare    원거리   초장거리 일격·고화력·저체력·배치 시 전방 발사
```

---

## ② 현재 desc 베이스라인 (JSON)

각 SO 에 시드된 현재 desc 를 `[{"id","desc"}]` 로 뽑아 **`defenders_desc.json`** (같은 폴더)에 저장해뒀다 — SO 실측. 프롬프트 입력 겸, 개선 전 시작점. import DTO(`DefenderStatDto`)의 부분갱신 형태와 동일(빈 필드 생략 = 유지).

```json
[
  { "id": "archer",  "desc": "레인저 · 원거리형. 배치 시 주변 속박." },
  { "id": "bastion", "desc": "가디언 · 근접형. 최대 3체 동시 타격, 최대 2체 도발 유지, 배치 즉시 광역 폭발." },
  { "id": "sniper",  "desc": "레인저 · 원거리형. 배치 시 전방 발사." }
  // … 전체 17종은 defenders_desc.json 참조
]
```

---

## ③ 사용 순서

1. 시트 `Defenders` 탭에 헤더 `desc` 컬럼이 없으면 추가(헤더명 정확히 `desc`).
2. LLM 에 ①프롬프트(+ `defenders_desc.json` 을 시작점으로)를 넣어 **JSON `[{"id","desc"}]` 17개**를 받는다.
3. 받은 JSON 의 `desc` 를 시트 `id` 행에 맞춰 `desc` 셀에 채운다(id 로 매칭). **빈 셀 = 기존 SO 값 유지**(부분 갱신). (서버 POST 엔드포인트가 생기면 이 JSON 을 바로 전송 — 스키마 후속 후보.)
4. Unity `UnitStatImportWindow` 로 Defenders 임포트 → 각 SO.desc 갱신 → 상세 패널 반영.
5. 역방향: `UnitStatExporter` 가 `Defenders.json` 에 현재 desc 를 써주므로 언제든 회수. `defenders_desc.json` 은 desc만 뽑은 경량 스냅샷.

## 주의

- **캐스터 4종(fire/ice/poison/blocking)** 은 export facts 상 "해저드 설치"로 동일하게 보인다.
  속성 구분은 **이름**에만 있으니 프롬프트가 이름 테마로 분리하게 해뒀다.
- desc 는 체력·공격력과 **동일한 plain 시트 필드**다. 특수 컬럼(`_descAuto` 등) 없음.
- 숫자를 desc 에 넣지 말 것 — 밸런스 변경 시 desc 가 거짓이 된다(스탯란이 SoT).
