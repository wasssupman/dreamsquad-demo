# 시트 챗봇용 프롬프트 — 드림캐쳐 전체 반영

아래 프롬프트를 **구글 스프레드시트 챗봇(Gemini in Sheets 등)** 에 `7_full_dreamcatcher_export.json` 내용과 함께 붙여넣는다. JSON 은 전 드림캐쳐 SO 전량 스냅샷(29카드).

---

너는 이 구글 스프레드시트를 편집하는 어시스턴트다. 아래 JSON 은 게임 클라이언트에서 export 한 **드림캐쳐 마스터데이터 전량 스냅샷**이다. 이 데이터를 시트의 각 탭에 반영해라.

## 규칙

1. **JSON 의 top-level 키 = 시트 탭 이름**이다: `DcCards`, `DcCardEffects`, `DcMechanics`, `DcAttackMods`, `DcSkills`, `DcConfig`. (`_note` 키는 무시.) 각 배열을 같은 이름의 탭에 반영한다. 해당 탭이 없으면 새로 만든다.

2. **각 배열의 원소 = 한 행**, **객체의 키 = 열 헤더**다. 1행은 헤더, 2행부터 데이터. 모든 객체에 등장하는 키의 합집합을 헤더로 삼고, 특정 행에 없는 키는 그 셀을 비운다.

3. **헤더 순서**: 각 탭 첫 객체의 키 순서를 따른다. 기존 시트에 이미 헤더가 있으면 그 순서를 유지하고, JSON 에만 있는 새 열(예: `DcMechanics` 의 `triggerPeriodSeconds`, `triggerFraction`, `ccKind`, `stackKind`, `buffStat`)은 오른쪽에 추가한다.

4. **업서트(덮어쓰기, 중복 생성 금지)**:
   - `DcCards` / `DcSkills` / `DcConfig` → 키 = `id`. 같은 `id` 행이 있으면 그 행을 갱신, 없으면 새 행 추가.
   - `DcCardEffects` / `DcMechanics` / `DcAttackMods` → 키 = (`cardId`, `slot`) 복합. 같은 조합 행이 있으면 갱신, 없으면 추가. `slot` 오름차순 정렬.
   - JSON 에 없는 기존 행은 **지우지 마라**(부분 반영).

5. **값은 그대로**: enum 은 문자열 그대로(예: `AttackN`, `Stun`, `AttackDamage`), 숫자는 숫자로, 불린/빈값은 그대로. 한글 텍스트(`displayName`/`description`)도 원문 유지. 임의 변형·번역·반올림 금지.

6. 반영 후 **탭별 추가/갱신된 행 수를 요약**해서 알려줘.

## JSON

```json
<여기에 7_full_dreamcatcher_export.json 전체 붙여넣기>
```

---

## 재생성 방법 (클라이언트 측)

JSON 스냅샷을 다시 뽑으려면 Unity 에서 **`Window/Wassup/Unit Stat Import` → "Export Dreamcatcher SO → JSON Files"** 버튼으로 6탭 파일을 뽑고 탭명 키로 병합한다. (자동화는 `docs/spec/dreamcatcher-sheet-sync/7_schema_ext_new_fields.md` 하단 참고 — 세션에서 헤드리스 export 로 생성함.)
