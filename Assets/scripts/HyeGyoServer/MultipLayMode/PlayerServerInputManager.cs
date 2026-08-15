using UnityEngine;

/// <summary>
/// 멀티플레이에서 현재 PC의 플레이어 입력을 받아
/// NetworkMatchBridge에 네트워크 요청을 전달한다.
/// </summary>
public class PlayerServerInputManager : MonoBehaviour
{
    [SerializeField]
    private NetworkMatchBridge networkMatchBridge;

    /// <summary>
    /// 카드 UI Button에서 호출.
    /// </summary>
    public void ChooseCard(int cardId)
    {
        if (networkMatchBridge == null)
        {
            Debug.LogError(
                "[PlayerServerInputManager] NetworkMatchBridge가 없습니다."
            );

            return;
        }

        networkMatchBridge.RequestChooseCard(cardId);
    }
}