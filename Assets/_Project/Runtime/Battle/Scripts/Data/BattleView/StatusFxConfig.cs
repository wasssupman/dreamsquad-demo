using UnityEngine;

namespace Somnia.Battle.Data.BattleView
{
    // battle-core-rebuild unit 6c — **몸에 붙는 상태의 그림**. 새 Unity 층의 상태 표식·오라·
    // 오버헤드 스택 아이콘이 읽는 한 자리다.
    //
    // 옛 전투는 같은 두 자산을 **씬의 서로 다른 컴포넌트**에 따로 꽂았다(상태 스포너의
    // `registry` · 오버헤드 레이어의 `stackIcons`). 둘 다 「이 상태는 이렇게 보인다」이고, 한쪽만
    // 바꾸는 날 표식과 아이콘이 서로 다른 상태를 가리킨다. 그래서 새 층은 이 설정 하나를 본다.
    //
    // ⚠ **코어는 이것을 모른다.** 어느 상태가 어떤 표식인지(`StatusFxKind`)는 그림의 선택이고
    // 규칙이 아니다 — 정의표에 싣지 않는다(파이프라인 커버리지: 정의표 = N/A).
    //
    // 한 몸에 표식이 여럿일 때 **무엇이 이겨 보이나**는 여기 없다. 옛 코드의 암묵 순서를 베끼지
    // 않고 사용자 플레이에서 확인한 뒤 데이터로 굳힌다(6c 이식 제외 · 사용자 결정 기록).
    [CreateAssetMenu(menuName = "Somnia/Battle/BattleView/Status FX Config", fileName = "StatusFxConfig")]
    public sealed class StatusFxConfig : ScriptableObject
    {
        [Tooltip("상태 종류 → 표식 프리팹·오프셋·빌보드. 비어 있으면 표식이 안 뜬다(경고 1회).")]
        [SerializeField] private StatusFxRegistry registry;

        [Tooltip("오버헤드 스택 아이콘(피로·열기). 비어 있으면 아이콘을 생략한다.")]
        [SerializeField] private StackIconRegistry stackIcons;

        public StatusFxRegistry Registry => registry;
        public StackIconRegistry StackIcons => stackIcons;
    }
}
