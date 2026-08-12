using UnityEngine;

public class PlayerInputManager :
    MonoBehaviour
{
    [SerializeField]
    private NetworkMatchBridge
        networkMatchBridge;

    /// <summary>
    /// 카드 UI Button에서 호출.
    /// </summary>
    public void ChooseCard(int cardId)
    {
        if (networkMatchBridge == null)
        {
            Debug.LogError(
                "NetworkMatchBridge가 없습니다."
            );

            return;
        }

        networkMatchBridge
            .RequestChooseCard(cardId);
    }
}