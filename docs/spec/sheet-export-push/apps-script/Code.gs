/**
 * Wassup sheet-export-push — Apps Script 업서트 엔진 (generic, 프로젝트 무관).
 *
 * Unity 에디터 "Push to Sheet" 가 POST 하는 { "<탭명>": [행객체], ... } 를 받아
 * 각 탭에 키 기준으로 업서트한다.
 *
 * 계약(비파괴):
 *  - 업서트: 키 매칭 행은 갱신, 없으면 맨 아래 추가.
 *  - blank=keep: 행 객체에 없는 컬럼은 그 셀을 건드리지 않는다(비우지도 않음).
 *  - 헤더: 기존 순서 유지, JSON 에만 있는 새 키는 오른쪽에 새 열로 추가.
 *  - 고아(시트엔 있고 이번 JSON 엔 없는 키): 삭제하지 않고 목록만 리포트.
 *  - 값 원문 유지(enum=문자열, 숫자=숫자, 한글 원문).
 *
 * 이식: KEY_CONFIG 만 대상 프로젝트의 탭↔키로 바꾸면 그대로 재사용.
 *
 * 주의: 동기화 컬럼에 수식(formula)을 두지 말 것 — 매트릭스 재기록 시 값으로 대체된다.
 *       메모/수식은 계약 밖 별도 열(동기화되지 않음)에 둔다.
 */

var KEY_CONFIG = {
  Defenders:     ['id'],
  Enemies:       ['id'],
  DcCards:       ['id'],
  DcSkills:      ['id'],
  DcConfig:      ['id'],
  CostConfig:    ['id'],
  DcCardEffects: ['cardId', 'slot'],
  DcMechanics:   ['cardId', 'slot'],
  DcAttackMods:  ['cardId', 'slot'],
};

// preset-sheet-import unit 6 — list-SoT 탭(키 없이 전체 교체). 프리셋처럼 시트가 Unity
// 리스트 전체의 미러라 삭제/재정렬을 반영해야 하는 탭만. keyed 업서트(위 KEY_CONFIG)는
// 그대로 두고 별개 모드로 라우팅한다 — 8탭의 비파괴 계약은 불변.
var LIST_REPLACE_TABS = {
  Presets: true,
};

function doPost(e) {
  try {
    if (!e || !e.postData || !e.postData.contents) throw new Error('empty POST body');
    var body = JSON.parse(e.postData.contents);
    var ss = SpreadsheetApp.getActiveSpreadsheet();
    var results = {};
    Object.keys(body).forEach(function (tabName) {
      if (tabName.charAt(0) === '_') return;          // _note 등 메타 키 무시
      var rows = body[tabName];
      if (!Array.isArray(rows)) return;
      results[tabName] = LIST_REPLACE_TABS[tabName]
        ? replaceTab(ss, tabName, rows)
        : upsertTab(ss, tabName, rows);
    });
    return jsonOut({ success: true, data: { results: results }, errorDetail: null });
  } catch (err) {
    return jsonOut({
      success: false, data: null,
      errorDetail: { errorMessage: String((err && err.message) || err) },
    });
  }
}

function upsertTab(ss, tabName, rows) {
  var keyCols = KEY_CONFIG[tabName];
  if (!keyCols) throw new Error('unknown tab (no key config): ' + tabName);

  var sheet = ss.getSheetByName(tabName) || ss.insertSheet(tabName);
  var lastCol = sheet.getLastColumn();
  var lastRow = sheet.getLastRow();

  // 1) 헤더(1행) 순서 유지 + JSON/키 신규 컬럼을 오른쪽에 append.
  var header = lastCol > 0
    ? sheet.getRange(1, 1, 1, lastCol).getValues()[0].map(function (h) { return String(h); })
    : [];
  var colOf = {};
  header.forEach(function (h, i) { colOf[h] = i; });
  var dataCount = Math.max(0, lastRow - 1);

  // 방어(sheet-export-push): 기존 데이터가 있는데 키 컬럼이 원래 헤더에 없으면, 키를
  // 못 잡아 전량 "신규"로 오인 → 중복 append 로 시트를 오염시킨다. 추측하지 말고 스킵.
  // (실사고: Defenders 탭 키 컬럼이 'id' 가 아니라 '공' 으로 잘못 라벨돼 20행 중복 추가.)
  var keyMissing = keyCols.filter(function (kc) { return !(kc in colOf); });
  if (dataCount > 0 && keyMissing.length > 0) {
    return {
      updated: 0, added: 0, orphans: [],
      error: '기존 ' + dataCount + '행이 있는데 키 컬럼(' + keyMissing.join(',') +
             ')이 헤더에 없음 — 시트 1행 헤더를 확인하세요. 중복 방지를 위해 이 탭은 스킵(추가 안 함).',
    };
  }

  function ensureCol(name) {
    if (!(name in colOf)) { colOf[name] = header.length; header.push(name); }
  }
  keyCols.forEach(ensureCol);
  rows.forEach(function (row) { Object.keys(row).forEach(ensureCol); });
  var width = header.length;

  // 2) 기존 데이터(2행~) → 헤더폭 배열 리스트 + 키 인덱스 + 표시용 키.
  var raw = (dataCount > 0 && lastCol > 0)
    ? sheet.getRange(2, 1, dataCount, lastCol).getValues()
    : [];
  var records = [];
  var indexByKey = {};
  var displayByKey = {};
  raw.forEach(function (r) {
    var rec = new Array(width).fill('');
    for (var c = 0; c < r.length && c < width; c++) rec[c] = r[c];
    var key = keyOf(rec, keyCols, colOf);
    if (key !== null && !(key in indexByKey)) {
      indexByKey[key] = records.length;
      displayByKey[key] = displayKey(rec, keyCols, colOf);
    }
    records.push(rec);
  });

  // 3) 업서트.
  var updated = 0, added = 0, seen = {};
  rows.forEach(function (row) {
    var key = keyOfObj(row, keyCols);
    if (key === null) return;                          // 키 결측 행은 스킵
    seen[key] = true;
    var rec;
    if (key in indexByKey) {
      rec = records[indexByKey[key]];
      updated++;
    } else {
      rec = new Array(width).fill('');
      indexByKey[key] = records.length;
      records.push(rec);
      added++;
    }
    Object.keys(row).forEach(function (col) { rec[colOf[col]] = row[col]; }); // blank=keep
    keyCols.forEach(function (kc) {                    // 신규 행 키 컬럼 보장
      if (rec[colOf[kc]] === '' || rec[colOf[kc]] === undefined) rec[colOf[kc]] = row[kc];
    });
  });

  // 4) 고아: 기존 키 중 이번 push 에 없던 것 (삭제하지 않음).
  var orphans = [];
  Object.keys(indexByKey).forEach(function (key) {
    if (!seen[key] && key in displayByKey) orphans.push(displayByKey[key]);
  });

  // 5) 매트릭스 재기록. 신규 범위 ⊇ 기존 범위라 잔여 셀 없음.
  var matrix = [header].concat(records);
  sheet.getRange(1, 1, matrix.length, width).setValues(matrix);

  return { updated: updated, added: added, orphans: orphans };
}

// list-SoT 전체 교체: 기존 데이터를 전부 지우고 payload 행으로 재작성한다. 키·고아 개념이
// 없다 — 시트 = Unity 리스트의 정확한 미러라 삭제/재정렬이 그대로 반영된다. 헤더는 행 키의
// 등장 순서(`_` 접두 제외). keyed upsertTab 과 달리 파괴적이지만, 이 탭은 Unity 가 리스트
// 전체를 소유한다는 계약(list-SoT)에서만 쓴다.
function replaceTab(ss, tabName, rows) {
  // 빈 payload 로 시트를 통째 비우는 사고 방지(keyed 탭의 키 컬럼 결측 가드와 같은 정신).
  // 의도적으로 프리셋을 0개로 만들려면 시트에서 수동으로 지운다.
  if (!rows || rows.length === 0) {
    return { replaced: 0, error: 'payload 행이 0 — 전체 교체가 시트를 비울 수 있어 스킵(보호).' };
  }

  var sheet = ss.getSheetByName(tabName) || ss.insertSheet(tabName);

  // 헤더 = 행 키 등장순. upsertTab 과 동일하게 모든 컬럼을 쓴다 — `_` 접두 행 필드도
  // 정보성 컬럼이라 보존(`_note` 같은 메타 탭 키는 doPost 의 top-level 스킵이 이미 처리).
  var header = [];
  var seen = {};
  rows.forEach(function (row) {
    Object.keys(row).forEach(function (k) {
      if (!(k in seen)) { seen[k] = true; header.push(k); }
    });
  });
  if (header.length === 0) return { replaced: 0, error: 'payload 행에 컬럼이 없음 — 스킵.' };

  // 매트릭스를 clearContents 전에 완성한다 — setValues 가 실패해도 시트가 빈 채로 남지
  // 않도록 파괴(clear)와 재작성(write) 사이의 창을 최소화한다.
  var matrix = [header];
  rows.forEach(function (row) {
    matrix.push(header.map(function (h) { return (h in row) ? row[h] : ''; }));
  });

  sheet.clearContents();
  sheet.getRange(1, 1, matrix.length, header.length).setValues(matrix);

  return { replaced: rows.length };
}

function keyOf(rec, keyCols, colOf) {
  var parts = [];
  for (var i = 0; i < keyCols.length; i++) {
    var v = rec[colOf[keyCols[i]]];
    if (v === '' || v === null || v === undefined) return null;
    parts.push(String(v));
  }
  return parts.join(' ');
}

function keyOfObj(obj, keyCols) {
  var parts = [];
  for (var i = 0; i < keyCols.length; i++) {
    var v = obj[keyCols[i]];
    if (v === null || v === undefined || v === '') return null;
    parts.push(String(v));
  }
  return parts.join(' ');
}

function displayKey(rec, keyCols, colOf) {
  return keyCols.map(function (c) { return String(rec[colOf[c]]); }).join(':');
}

function jsonOut(obj) {
  return ContentService
    .createTextOutput(JSON.stringify(obj))
    .setMimeType(ContentService.MimeType.JSON);
}
