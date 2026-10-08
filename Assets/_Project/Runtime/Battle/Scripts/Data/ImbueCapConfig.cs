using System;
using UnityEngine;
using Somnia.Battle.BattleCore;
using Somnia.Battle.BattleCore.Effects;

namespace Somnia.Battle.Data
{
    // battle-core-rebuild unit 6a2 — **한 발이 나를 수 있는 세기의 상한**(사용자 결정 ②).
    //
    // 「겹치면 합」의 짝이다. 카드 넷이 같은 불 부여를 걸면 합이 4가 되는데, 그 합을 막는
    // 것이 이 표다. 스택 종류의 최대 중첩(`StackModifierSO.maxStack`)과 **다른 축**이다 —
    // 저쪽은 「피해자에게 몇 개까지 쌓이나」이고 이쪽은 「한 발에 얼마까지 실리나」다.
    //
    // ⚠ **줄이 없는 키는 부여가 거절된다**(제약 6). 「저작이 없다」가 「상한이 없다」로
    // 읽히면 그 순간 근거 없는 무한 부여가 조용히 성립한다. 그래서 생산자(카드·스킬,
    // unit 7)를 추가할 때 **같은 커밋에서** 여기 줄을 저작한다.
    //
    // ⚠ **뷰 설정이 아니다.** 화면이 아니라 판이 읽는 값이라 `Data/BattleView/` 가 아니라
    // `Data/Config/` 에 있고, 소비처는 `MatchDefinitionBuilder` 다(`configHash` 에 든다).
    [CreateAssetMenu(menuName = "Somnia/Battle/Imbue Cap Config", fileName = "ImbueCapConfig")]
    public sealed class ImbueCapConfig : ScriptableObject
    {
        [Serializable]
        public struct Row
        {
            [Tooltip("무엇을 얹나. 피해·회복은 부여 대상이 아니다.")]
            public ImbueKind kind;

            [Tooltip("스탯 부여일 때의 스탯. 다른 종류에서는 무시된다.")]
            public StatKind stat;

            [Tooltip("스탯 부여일 때의 결합 방식. 다른 종류에서는 무시된다.")]
            public CombineOp op;

            [Tooltip("스택 부여일 때의 스택 종류. 다른 종류에서는 무시된다.")]
            public StackKind stackKind;

            [Tooltip("군중 제어 부여일 때의 종류. 다른 종류에서는 무시된다.")]
            public CcRequestKind cc;

            [Tooltip("한 발이 나르는 크기의 상한. **양수여야 한다** — 0 이하는 빌더가 거절한다.")]
            public float cap;

            /// <summary>이 줄이 가리키는 키. 종류가 «어느 칸을 읽나»를 정한다.</summary>
            public ImbueKey Key()
            {
                switch (kind)
                {
                    case ImbueKind.ApplyStat: return ImbueKey.Stat(stat, op);
                    case ImbueKind.ApplyStack: return ImbueKey.Stack(stackKind);
                    case ImbueKind.Cc: return ImbueKey.Cc(cc);
                    default: return default;
                }
            }
        }

        [Tooltip("부여 상한 줄. 키 하나에 한 줄이고, 중복·비정상 값은 빌더가 loud 하게 거절한다.")]
        [SerializeField] private Row[] rows = Array.Empty<Row>();

        public Row[] Rows => rows ?? Array.Empty<Row>();
    }
}
