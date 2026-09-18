using TMPro;
using UnityEngine;

public sealed class NetworkLobbyUIClient : MonoBehaviour
{
    [Header("Network")]
    [SerializeField]
    private NetworkLauncher networkLauncher;

    [Header("Panel")]
    [SerializeField]
    private GameObject selectionPanel;

    [Header("Input")]
    [SerializeField]
    private TMP_InputField nicknameInput;

    [SerializeField]
    private TMP_InputField codeInput;


    public string Nickname =>
        nicknameInput != null
            ? nicknameInput.text.Trim()
            : string.Empty;

    public string Code =>
        codeInput != null
            ? codeInput.text.Trim().ToUpperInvariant()
            : string.Empty;


    private void Awake()
    {
        if (nicknameInput != null)
        {
            nicknameInput.characterLimit = 10;
        }
    

        // 방 코드는 영어 + 숫자만 허용
        if (codeInput != null)
        {
            codeInput.onValidateInput +=
                ValidateCodeCharacter;
        }
    }


    public void JoinSession()
    {
        string nickname = Nickname;
        string code = Code;

        if (string.IsNullOrWhiteSpace(nickname))
        {
            Debug.LogWarning(
                "[Client UI] 닉네임을 입력하세요."
            );

            return;
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            Debug.LogWarning(
                "[Client UI] Code를 입력하세요."
            );

            return;
        }

        Debug.Log(
            $"[Client UI] Nickname: {nickname} | Code: {code}"
        );

        networkLauncher.JoinSession(code);
    }


    public void BackButton()
    {
        if (selectionPanel != null)
        {
            selectionPanel.SetActive(true);
        }

        gameObject.SetActive(false);
    }

    private bool IsValidNickname(string nickname)
{
    foreach (char c in nickname)
    {
        bool isEnglish =
            (c >= 'A' && c <= 'Z') ||
            (c >= 'a' && c <= 'z');

        bool isHangul =
            (c >= '\uAC00' && c <= '\uD7A3') || // 가~힣
            (c >= '\u3131' && c <= '\u318E') || // ㄱ~ㆎ
            (c >= '\u1100' && c <= '\u11FF');   // 한글 자모

        if (!isEnglish && !isHangul)
        {
            return false;
        }
    }

    return true;
}



    private char ValidateCodeCharacter(
        string text,
        int charIndex,
        char addedChar)
    {
        bool isEnglish =
            (addedChar >= 'A' &&
             addedChar <= 'Z') ||
            (addedChar >= 'a' &&
             addedChar <= 'z');

        bool isNumber =
            addedChar >= '0' &&
            addedChar <= '9';

        if (!isEnglish &&
            !isNumber)
        {
            return '\0';
        }

        return char.ToUpperInvariant(
            addedChar
        );
    }
}