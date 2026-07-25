using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.Editor.UnitStatImport
{
    // dreamcatcher-attach-requirement unit 3 — 부착 제한 설정의 조기 검출.
    //
    // 왜 필요한가: 카드 정의에는 bake 단계가 없다. 런타임 경고(BattleBridge)는 플레이어가
    // 드롭을 **시도할 때만** 나오므로 잘못된 시트 값이 실제 플레이 세션까지 살아간다.
    // 게다가 무효 카드는 부착도 소모도 되지 않아 매치 내내 손패 슬롯을 점유하고,
    // 범위 밖 설정(Squad/Active/적표식)은 게이트 함수를 아예 안 타서 런타임 경고조차
    // 나지 않는다 — validator 만이 잡을 수 있다.
    //
    // 검증 코어는 순수 static(CollectWarnings)으로 분리해 EditMode 가 같은 규칙을 핀한다
    // (UnitVisualDataValidator 선례 — 인스펙터 HelpBox + 배치 스캔이 같은 함수를 공유).
    public static class DcAttachRequirementValidator
    {
        private const string DcFolder = "Assets/_Project/Data/Dreamcatcher";

        // knownUnitIds = DefenderUnitData.id 집합. null 이면 id 존재 검사를 건너뛴다
        // (카탈로그를 못 찾은 경우 — 없는 id 를 오탐으로 신고하지 않는다).
        public static List<string> CollectWarnings(DreamcatcherCard card, ICollection<string> knownUnitIds)
        {
            var warnings = new List<string>();
            if (card == null || card.attachType == DcAttachType.None) return warnings;

            // ① 범위 밖 설정 — 조용한 무효. 부착 제한은 type=Unit 의 defender 부착 경로만
            // 소비한다. BountyMark 카드는 Classify 가 적 타겟으로 라우팅해 게이트를 안 탄다.
            if (card.type != CardType.Unit)
                warnings.Add($"attachType 이 설정됐지만 카드 type={card.type} — 부착 제한은 type=Unit 만 소비한다(조용히 무효).");
            else if (card.HasBountyMark())
                warnings.Add("attachType 이 설정됐지만 적 지정(BountyMark) 카드 — 적에게 부착되므로 제한이 조용히 무효다.");

            // ② 무효 값 — fail-closed 라 어떤 유닛에도 붙지 않는다.
            // unit 7 rev — Class 는 "비었다" 와 "이름을 못 읽는다"(오타·숫자)를 구분해 알린다.
            // 구 3필드 설계에선 클래스 오타가 import 예외로 잡혔는데, 값 칸이 string 이 된
            // 뒤로는 이 검사가 그 역할을 대신한다.
            if (DreamcatcherAttachEval.HasInvalidAttachRequirement(card))
            {
                if (card.attachType == DcAttachType.Class)
                {
                    warnings.Add(string.IsNullOrEmpty(card.attachValue)
                        ? "attachType=Class 인데 attachValue 가 비어 있다 — 어떤 유닛에도 부착되지 않는다(fail-closed)."
                        : $"attachType=Class 인데 attachValue='{card.attachValue}' 를 클래스 이름으로 읽을 수 없다 — 어떤 유닛에도 부착되지 않는다(fail-closed). 허용: Ranger/Guardian/Fighter/Caster/Support.");
                }
                else
                {
                    warnings.Add("attachType=UnitId 인데 attachValue 가 비어 있다 — 어떤 유닛에도 부착되지 않는다(fail-closed).");
                }
                return warnings; // 값을 못 읽으면 아래 id 존재 검사는 무의미
            }

            // ③ 없는 유닛 id — 오타/리네임 잔재. 역시 fail-closed 로 귀결된다.
            if (card.attachType == DcAttachType.UnitId
                && knownUnitIds != null && !knownUnitIds.Contains(card.attachValue))
            {
                warnings.Add($"attachValue='{card.attachValue}' 가 어느 DefenderUnitData.id 와도 일치하지 않는다 — 어떤 유닛에도 부착되지 않는다.");
            }

            return warnings;
        }

        [MenuItem("Wassup/Tools/Validate Dreamcatcher Attach Requirements")]
        public static void ValidateAll()
        {
            var knownIds = CollectKnownUnitIds();
            var log = new StringBuilder();
            int cards = 0, flagged = 0, total = 0;

            foreach (var card in UnitAssetScan.Enumerate<DreamcatcherCard>(DcFolder))
            {
                cards++;
                var warnings = CollectWarnings(card, knownIds);
                if (warnings.Count == 0) continue;
                flagged++;
                total += warnings.Count;
                log.AppendLine($"[{card.id}] {card.name}");
                foreach (string w in warnings) log.AppendLine($"  - {w}");
            }

            if (knownIds == null)
                log.AppendLine("⚠ DefenderCatalog 를 찾지 못해 유닛 id 존재 검사를 건너뛰었다.");

            string head = $"부착 제한 검증 — 카드 {cards}장 중 {flagged}장에서 {total}건.";
            if (flagged == 0) Debug.Log($"[DcAttachRequirement] {head} 위반 없음.\n{log}");
            else Debug.LogWarning($"[DcAttachRequirement] {head}\n{log}");
        }

        // 카탈로그 우선, 없으면 폴더 스캔 폴백. 어느 쪽도 못 찾으면 null(검사 생략).
        private static ICollection<string> CollectKnownUnitIds()
        {
            var ids = new HashSet<string>();
            foreach (var cat in UnitAssetScan.Enumerate<DefenderCatalog>("Assets/_Project/Data"))
            {
                if (cat.units == null) continue;
                foreach (var u in cat.units)
                    if (u != null && !string.IsNullOrEmpty(u.id)) ids.Add(u.id);
            }
            if (ids.Count > 0) return ids;
            foreach (var u in UnitAssetScan.Enumerate<DefenderUnitData>("Assets/_Project/Data/Defenders"))
                if (!string.IsNullOrEmpty(u.id)) ids.Add(u.id);
            return ids.Count > 0 ? ids : null;
        }
    }

    // 저작 중 즉시 보이게 — 아트/기획이 콘솔을 안 봐도 되게 HelpBox 로 노출
    // (UnitVisualDataValidator 의 인스펙터 노출 선례와 동일한 의도).
    [CustomEditor(typeof(DreamcatcherCard))]
    public class DreamcatcherCardEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            // 인스펙터에서는 id 존재 검사를 생략한다(에셋 스캔을 매 OnInspectorGUI 마다
            // 돌리지 않기 위해) — 그 항목은 배치 검증 메뉴가 담당한다.
            foreach (string w in DcAttachRequirementValidator.CollectWarnings(target as DreamcatcherCard, null))
                EditorGUILayout.HelpBox(w, MessageType.Warning);
        }
    }
}
