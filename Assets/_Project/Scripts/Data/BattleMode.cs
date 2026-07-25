namespace Wassup.Data
{
    // endless-mode unit 0 — 배틀 모드 구분자. BattleBridge 만 이 값을 읽어 분기한다(간격/누수/시간축/리포트).
    // Main    = 기존 토너먼트 배틀(시간·스트레스·킬 점수, 웨이브수 의존 간격).
    // Endless = 스코어어택 무한 모드(고정 간격, 킬+스트레스 점수, 누수로 죽지 않음, 토너먼트 미리포트).
    public enum BattleMode { Main = 0, Endless = 1 }
}
