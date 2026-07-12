using System;
using UnityEngine;

namespace Wassup.Data
{
    // unit-status-fx Unit 0 — 상태 종류별 연출 프리팹 매핑(SO). 하드코딩 금지:
    // 프리팹/오프셋/스케일/빌보드/폴백틴트를 전부 여기서. 상태마다 다른 프리팹을 끼운다.
    // prefab 이 비면 절차적 "!" 폴백(현 어그로 외형 유지).
    [CreateAssetMenu(fileName = "StatusFxRegistry", menuName = "Wassup/Status FX Registry")]
    public class StatusFxRegistry : ScriptableObject
    {
        // unit-status-fx 5 — 절차 폴백 글리프. 기본 0(Exclamation)이라 기존 Aggro
        // 에셋 직렬화 하위호환(재저장 불필요). 새 글리프는 끝에 추가.
        public enum FallbackGlyph : byte
        {
            Exclamation = 0,
            Zz = 1,
        }

        [Serializable]
        public struct Entry
        {
            public StatusFxKind kind;
            [Tooltip("이 상태의 연출 프리팹. 비우면 절차적 글리프 폴백")]
            public GameObject prefab;
            [Tooltip("유닛 발치(anchor) 기준 로컬 오프셋 (머리 위 = +Y)")]
            public Vector3 localOffset;
            public float scale;
            [Tooltip("카메라 대면 빌보드 여부")]
            public bool billboard;
            [Tooltip("prefab 없을 때 절차 글리프 틴트")]
            public Color fallbackTint;
            [Tooltip("prefab 없을 때 그릴 절차 글리프")]
            public FallbackGlyph fallbackGlyph;
        }

        public Entry[] entries;

        public bool TryGet(StatusFxKind kind, out Entry entry)
        {
            if (entries != null)
            {
                for (int i = 0; i < entries.Length; i++)
                    if (entries[i].kind == kind) { entry = entries[i]; return true; }
            }
            entry = default;
            return false;
        }
    }
}
