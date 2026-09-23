// salvaged from Assets/_Project/Scripts/Battle/Combat/Projectile/{MovementKind, PayloadKind}.cs
//   + Emission/MovementBinding.cs (battle-core-rebuild unit 3)
// 이식 시 바뀐 것: 번호·의미·분류가 **한 글자도 안 바뀌었다**. 세 enum 을 한 파일에 모은 이유는
//   셋이 한 축의 세 면이기 때문이다 — 궤적(어떻게 나나) × 페이로드(닿으면 뭐 하나) × 바인딩
//   (무엇을 겨누나). 바인딩은 앞의 것에서 **파생**이라 저작 축이 아니다.

namespace Wassup.BattleCore.Combat.Projectile
{
    // 궤적 축. 「위치가 매 틱 어떻게 변하나」 + 「언제 도착인가」를 소유한다.
    //
    // ⚠ **도착 조건은 궤적이 소유한다.** 페이로드는 「도착했다」만 듣는다 — 그래서 경로 스윕
    // (`PathHit`)에게 그 신호는 「착탄」이 아니라 **「비행 종료」**(마지막 스윕 후 소멸)다.
    public enum MovementKind : byte
    {
        /// <summary>대상의 live 위치를 좇는다. 대상이 사라지면 소멸(재조준 반경이 있으면 다시 겨눈다).</summary>
        HomingToEntity = 0,

        /// <summary>발사 시점에 **칸으로 고정된** 착탄점까지 포물선. 대상의 죽음·이동은 무관하다.</summary>
        BallisticArcToPoint = 1,

        /// <summary>
        /// 착탄점에 머물다 `flightTime` 뒤 도착(운석의 예고). **자리(sim 위치)는 움직이지 않는다** —
        /// 떨어지는 그림은 뷰 공간이다. 예고 시간은 요청이 싣는다(이동 거리가 0 이라 속도로 못 만든다).
        /// </summary>
        SkyFall = 2,

        /// <summary>발사 축을 따라 `maxDistance` 까지 직선. 도착 판정이 없고 경로 스윕으로 맞힌다.</summary>
        DirectionalLinear = 3,

        /// <summary>칸까지 굴러가(`flightTime`) 도화선(`fuseSec`)을 태운 뒤 도착. 폭탄맨의 자다.</summary>
        GrenadeToCell = 4,

        /// <summary>곡선으로 날며 대상을 좇는다. 제어점 2개는 발사 시 결정론 생성, 종점은 live 위치.</summary>
        BezierHomingToEntity = 5,

        /// <summary>
        /// 한 점을 도는 원운동. **주인이 사라지면 구슬도 사라진다** — 궤도는 «누구 주위를 돈다»가
        /// 정의라 주인이 없으면 의미가 없다. 다른 궤적(던지면 제 갈 길)은 이 규칙을 공유하지 않는다.
        /// </summary>
        OrbitAroundPoint = 6,

        /// <summary>
        /// 발사 축을 따라 나갔다 **돌아오는** 직선 왕복.
        /// ⚠ **발사 축을 되먹이지 말 것** — 「지금 돌아오는 중이니 뒤집자」로 매 틱 갱신하면
        /// 다음 틱이 발사점 **뒤**를 계산한다(초판의 실제 결함). 「어느 다리인가」는 저장하지 않는다.
        /// </summary>
        BoomerangReturn = 7,

        /// <summary>
        /// 하늘에서 떨어지지만 **적을 겨눈다**. `SkyFall` 과 같은 그림, **다른 조준**이다.
        /// ⚠ 이 축이 없어서 났던 사고: 셀 조준 낙하탄에 임자를 실었더니 **한 탄에 조준이 둘**이
        /// 되어 예고 시간만큼 어긋났다(실측 예고 0.40s × 속도 2.00 = 0.80타일 &gt; 칸 유지 폭 0.50).
        /// **탄 하나에 조준은 하나다.**
        /// </summary>
        SkyFallOnEntity = 8,
    }

    // 페이로드 축. 「닿으면 무엇을 하나」.
    public enum PayloadKind : byte
    {
        /// <summary>직격 대상 + 부가 비산.</summary>
        SingleSplash = 0,
        /// <summary>착탄 칸 반경 안 전원. 직격 대상이 없다.</summary>
        TileAoe = 1,
        /// <summary>직전→현재 선분을 매 틱 훑어 길 위의 대상을 때린다. 피해자당 창 1회.</summary>
        PathHit = 2,
        /// <summary>착탄 칸에 **길막 설치물**을 세운다. 피해는 0 이다(터지는 것은 부서질 때다).</summary>
        SpawnBlocker = 3,
    }

    /// <summary>발사 시점에 궤적이 요구하는 바인딩. 발사기가 알아야 하는 것은 이 셋뿐이다.</summary>
    public enum BindingClass : byte { Entity = 0, Cell = 1, Direction = 2 }

    // 궤적 → 바인딩 **순수 분류**. 발사기의 분기 축이다.
    //
    // 개별 궤적으로 분기하면 새 이동 수학마다 발사기가 자란다. 기존 바인딩으로 분류되는 새 궤적은
    // 여기 한 줄 말고는 발사기를 안 건드린다 — 궤도·부메랑이 실제로 그렇게 들어왔다.
    public static class MovementBinding
    {
        /// <summary>
        /// 분류가 전수인지는 컴파일러가 못 본다. 그래서 테스트가 이 상수와 enum 길이를 대조한다 —
        /// 새 궤적을 더하면 테스트가 빨개지고 여기 분류를 갱신하게 된다.
        /// </summary>
        public const int KnownKindCount = 9;

        public static BindingClass Of(MovementKind kind)
        {
            switch (kind)
            {
                case MovementKind.HomingToEntity:
                case MovementKind.BezierHomingToEntity:
                case MovementKind.SkyFallOnEntity:
                    return BindingClass.Entity;

                case MovementKind.BallisticArcToPoint:
                case MovementKind.SkyFall:
                case MovementKind.GrenadeToCell:
                case MovementKind.OrbitAroundPoint:
                    return BindingClass.Cell;

                case MovementKind.DirectionalLinear:
                case MovementKind.BoomerangReturn:
                    return BindingClass.Direction;

                default:
                    // 미분류 = 미개통으로 흐른다(호출부가 경고 후 발사를 소비한다).
                    // 조용한 오발사보다 눈에 보이는 경고가 낫다.
                    return BindingClass.Direction;
            }
        }
    }
}
