using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 실제 멀티플레이 전투에서
/// Client 입력과 기존 Battle 로직을 연결하는 Bridge.
///
/// 현재 1차 구현:
/// 1. Client 카드 선택 요청
/// 2. Server에서 SenderClientId 확인
/// 3. ClientId → CharacterManager 매핑
/// 4. Server에서 CharacterManager.SelectCard() 실행
///
/// 추후 구현:
/// - Redraw
/// - Guard
/// - HP / Defense / Cost 동기화
/// - Hand / Queue 동기화
/// - Turn 상태 동기화
/// - Battle 종료 동기화
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public sealed class NetworkBattleBridge : NetworkBehaviour
{
    private const int HandSize = 4;

    // =========================================================
    // Inspector
    // =========================================================

    [Header("Battle Characters")]

    [Tooltip("Host가 조작하는 CharacterManager")]
    [SerializeField]
    private CharacterManager hostCharacter;

    [Tooltip("Client가 조작하는 CharacterManager")]
    [SerializeField]
    private CharacterManager clientCharacter;

    // =========================================================
    // Events
    // =========================================================

    /// <summary>
    /// 현재 PC에 메시지를 보여주고 싶을 때 사용.
    /// UI에서 구독 가능.
    /// </summary>
    public event Action<string> LocalMessage;

    // =========================================================
    // Public Request
    // =========================================================

    /// <summary>
    /// 현재 PC의 플레이어가
    /// 손패의 카드를 선택할 때 호출한다.
    ///
    /// handIndex:
    /// 0 ~ 3
    /// </summary>
    public void RequestChooseCard(int handIndex)
    {
        // -----------------------------------------------------
        // NetworkObject Spawn 확인
        // -----------------------------------------------------

        if (!IsSpawned)
        {
            SendLocalMessage(
                "NetworkBattleBridge가 아직 Spawn되지 않았습니다."
            );

            return;
        }

        // -----------------------------------------------------
        // 기본적인 로컬 입력 검사
        //
        // 보안을 위한 검사는 Server에서도 다시 한다.
        // -----------------------------------------------------

        if (!IsValidHandIndex(handIndex))
        {
            SendLocalMessage(
                $"잘못된 손패 인덱스입니다: {handIndex}"
            );

            return;
        }

        // -----------------------------------------------------
        // Server에 요청
        // -----------------------------------------------------

        RequestChooseCardRpc(handIndex);
    }

    // =========================================================
    // Card RPC
    // =========================================================

    /// <summary>
    /// Client → Server 카드 선택 요청.
    ///
    /// ClientId는 Client가 파라미터로 보내지 않는다.
    /// Server가 실제 RPC SenderClientId를 사용한다.
    /// </summary>
    [Rpc(SendTo.Server)]
    private void RequestChooseCardRpc(
        int handIndex,
        RpcParams rpcParams = default)
    {
        ulong senderClientId =
            rpcParams.Receive.SenderClientId;

        Debug.Log(
            "[NetworkBattleBridge] " +
            "카드 선택 요청 | " +
            $"ClientId: {senderClientId} | " +
            $"HandIndex: {handIndex}"
        );

        // -----------------------------------------------------
        // 검증 1
        // 실제로 연결된 Client인가?
        // -----------------------------------------------------

        if (!IsConnectedClient(senderClientId))
        {
            RejectRequest(
                senderClientId,
                "현재 전투에 연결된 플레이어가 아닙니다."
            );

            return;
        }

        // -----------------------------------------------------
        // 검증 2
        // 손패 범위가 정상인가?
        // -----------------------------------------------------

        if (!IsValidHandIndex(handIndex))
        {
            RejectRequest(
                senderClientId,
                "잘못된 카드 위치입니다."
            );

            return;
        }

        // -----------------------------------------------------
        // 검증 3
        // 이 Client가 조작할 CharacterManager 찾기
        // -----------------------------------------------------

        if (!TryGetCharacter(
                senderClientId,
                out CharacterManager character))
        {
            RejectRequest(
                senderClientId,
                "플레이어 CharacterManager를 찾을 수 없습니다."
            );

            return;
        }

        // -----------------------------------------------------
        // 기존 게임 로직 호출
        //
        // CharacterManager.SelectCard()가
        // 기존 카드 선택 로직을 담당한다.
        // -----------------------------------------------------

        character.SelectCard(handIndex);

        Debug.Log(
            "[NetworkBattleBridge] " +
            "카드 선택 요청 처리 | " +
            $"ClientId: {senderClientId} | " +
            $"HandIndex: {handIndex} | " +
            $"Character: {character.name}"
        );

        /*
         * TODO:
         *
         * 현재 SelectCard()가 bool을 반환하지 않기 때문에
         * 실제 선택이 성공했는지 Bridge가 정확히 알 수 없다.
         *
         * 추후 CharacterManager에:
         *
         * bool TrySelectCard(int index)
         *
         * 같은 함수를 추가하는 것이 좋다.
         */
    }

    // =========================================================
    // ClientId → CharacterManager
    // =========================================================

    /// <summary>
    /// ClientId를 실제 전투 CharacterManager와 연결한다.
    ///
    /// 현재 2인 게임 전용:
    ///
    /// ServerClientId
    ///     → Host Character
    ///
    /// 나머지 Client
    ///     → Client Character
    /// </summary>
    private bool TryGetCharacter(
        ulong clientId,
        out CharacterManager character)
    {
        character = null;

        if (NetworkManager == null)
            return false;

        // -----------------------------------------------------
        // Host
        // -----------------------------------------------------

        if (clientId ==
            Unity.Netcode.NetworkManager.ServerClientId)
        {
            character =
                hostCharacter;

            return character != null;
        }

        // -----------------------------------------------------
        // Client
        //
        // 현재 게임은 2인 전용이므로
        // Host가 아닌 연결된 Client는
        // clientCharacter에 대응시킨다.
        // -----------------------------------------------------

        if (IsConnectedClient(clientId))
        {
            character =
                clientCharacter;

            return character != null;
        }

        return false;
    }

    // =========================================================
    // Validation
    // =========================================================

    /// <summary>
    /// 현재 NetworkManager에 실제 연결된 Client인지 확인.
    /// </summary>
    private bool IsConnectedClient(
        ulong clientId)
    {
        if (NetworkManager == null)
            return false;

        foreach (
            NetworkClient networkClient
            in NetworkManager.ConnectedClientsList)
        {
            if (networkClient.ClientId ==
                clientId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 현재 손패는 기존 HandManager 기준 4칸.
    /// </summary>
    private static bool IsValidHandIndex(
        int handIndex)
    {
        return
            handIndex >= 0 &&
            handIndex < HandSize;
    }

    // =========================================================
    // Reject
    // =========================================================

    /// <summary>
    /// Server에서 요청을 거절한다.
    /// </summary>
    private void RejectRequest(
        ulong targetClientId,
        string message)
    {
        if (!IsServer)
            return;

        Debug.LogWarning(
            "[NetworkBattleBridge] 요청 거절 | " +
            $"ClientId: {targetClientId} | " +
            message
        );

        RejectRequestRpc(
            targetClientId,
            new FixedString128Bytes(message)
        );
    }

    /// <summary>
    /// 모든 Client에게 RPC가 전달되지만
    /// targetClientId와 동일한 PC만 처리한다.
    ///
    /// 현재 NetworkMatchBridge와 같은 방식이다.
    /// </summary>
    [Rpc(SendTo.ClientsAndHost)]
    private void RejectRequestRpc(
        ulong targetClientId,
        FixedString128Bytes message)
    {
        if (NetworkManager == null)
            return;

        if (NetworkManager.LocalClientId !=
            targetClientId)
        {
            return;
        }

        SendLocalMessage(
            message.ToString()
        );
    }

    // =========================================================
    // Local
    // =========================================================

    private void SendLocalMessage(
        string message)
    {
        Debug.Log(
            $"[NetworkBattleBridge] {message}"
        );

        LocalMessage?.Invoke(message);
    }
}