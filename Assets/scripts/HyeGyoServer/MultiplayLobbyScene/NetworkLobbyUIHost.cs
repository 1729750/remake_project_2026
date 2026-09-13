using TMPro;
using UnityEngine;

public sealed class NetworkLobbyUIHost : MonoBehaviour
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
    /// Host 방 생성 버튼에서 호출.
    /// </summary>
    public void CreateSession()
    {
        string nickname = Nickname;
        string code = Code;

        if (string.IsNullOrWhiteSpace(nickname))
        {
            Debug.LogWarning(
                "[Host UI] 닉네임을 입력하세요."
            );

            return;
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            Debug.LogWarning(
                "[Host UI] Code를 입력하세요."
            );

            return;
        }

        Debug.Log(
            $"[Host UI] Nickname: {nickname} | Code: {code}"
        );

        // 현재 NetworkLauncher의 CreateSession()은
        // code를 전달받지 않는다.
        //
        // 닉네임 / Code를 실제 Server 데이터로 등록하는 것은
        // 이후 별도로 연결해야 한다.

        networkLauncher.CreateSession();
    }


    private char ValidateNicknameCharacter(
        string text,
        int charIndex,
        char addedChar)
    {
        // 영어 대문자
        if (addedChar >= 'A' &&
            addedChar <= 'Z')
        {
            return addedChar;
        }

        // 영어 소문자
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

        // Code는 자동으로 대문자로
        return char.ToUpperInvariant(
            addedChar
        );
    }
}