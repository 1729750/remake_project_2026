public enum MatchPhase
{
    WaitingForPlayers, // Host, Client 접속 기다리는중

    ChoosingCard, // Server가 준 랜덤 카드 3장 중 1장 선택

    ChoosingCondition, // 자기 덱 강화 및 최종 상태 결정

    ShowingResult, // 양쪽 선택이 모두 끝나고 서로 최종 조건을 잠시 10초 보여주는 상태

    Battle //실제 전투
}