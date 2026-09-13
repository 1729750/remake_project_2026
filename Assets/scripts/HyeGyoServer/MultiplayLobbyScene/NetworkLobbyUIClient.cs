using TMPro;
using UnityEngine;

public sealed class NetworkLobbyUIClient : MonoBehaviour
{
    [Header("Network")]
    [SerializeField]
    private NetworkLauncher networkLauncher;

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
            nicknameInput.onValidateInput +=
                ValidateNicknameCharacter;
        }

        if (codeInput != null)
        {
            codeInput.onValidateInput +=
                ValidateCodeCharacter;
        }
    }


    /// <summary>
    /// Client 참가 버튼에서 호출.
    /// </summary>
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

        // 참가 Code는 기존 네트워크 코드로 전달
        networkLauncher.JoinSession(code);

        // 닉네임은 NGO 연결 완료 후
        // Server에 등록하는 처리가 추가로 필요하다.
    }


    private char ValidateNicknameCharacter(
        string text,
        int charIndex,
        char addedChar)
    {
        if (addedChar >= 'A' &&
            addedChar <= 'Z')
        {
            return addedChar;
        }

        if (addedChar >= 'a' &&
            addedChar <= 'z')
        {
            return addedChar;
        }

        return '\0';
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