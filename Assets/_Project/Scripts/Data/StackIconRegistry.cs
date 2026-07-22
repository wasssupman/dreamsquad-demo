using System;
using UnityEngine;

namespace Wassup.Data
{
    // unit-overhead-ui 확장(unit 6) — OverheadStackKind → 아이콘 sprite 매핑 SO.
    // 매핑 없는 kind 는 null 반환 → 뷰가 표시 생략(코드↔아트 디커플링: 아이콘(Codex, unit 9)
    // 도착 전에도 무크래시). 내용 채움은 unit 10 배선.
    [CreateAssetMenu(fileName = "StackIconRegistry", menuName = "Wassup/Stack Icon Registry", order = 25)]
    public class StackIconRegistry : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public OverheadStackKind kind;
            public Sprite icon;
        }

        [SerializeField] private Entry[] entries;

        // 미매핑/미할당 = null. 호출측(UnitOverheadView)은 null 이면 그 스택 아이콘을 생략한다.
        public Sprite IconFor(OverheadStackKind kind)
        {
            if (entries == null) return null;
            for (int i = 0; i < entries.Length; i++)
                if (entries[i].kind == kind) return entries[i].icon;
            return null;
        }
    }
}
