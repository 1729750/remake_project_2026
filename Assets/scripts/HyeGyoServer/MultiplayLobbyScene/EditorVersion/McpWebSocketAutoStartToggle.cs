#if UNITY_EDITOR

using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// MCP Unity의 ProjectSettings/McpUnitySettings.json 안에 있는
/// AutoStartServer 값을 Unity 메뉴에서 ON/OFF한다.
///
/// 이것은 게임 Relay/NGO가 아니라 Unity Editor용 MCP WebSocket 설정이다.
/// 현재 이미 실행 중인 서버의 즉시 Start/Stop은
/// Tools > MCP Unity > Server Window에서 처리한다.
/// </summary>
[InitializeOnLoad]
public static class McpWebSocketAutoStartToggle
{
    private const string SettingsPath =
        "ProjectSettings/McpUnitySettings.json";

    private const string MenuPath =
        "Tools/Project Network/MCP WebSocket Auto Start";

    private static readonly Regex AutoStartRegex =
        new Regex(
            "\"AutoStartServer\"\\s*:\\s*(true|false)",
            RegexOptions.IgnoreCase |
            RegexOptions.Compiled
        );

    static McpWebSocketAutoStartToggle()
    {
        EditorApplication.delayCall += RefreshMenuCheck;
    }

    [MenuItem(MenuPath)]
    private static void ToggleAutoStart()
    {
        if (!TryReadAutoStart(out string json, out bool current))
        {
            Debug.LogWarning(
                "[MCP Toggle] McpUnitySettings.json 또는 " +
                "AutoStartServer 항목을 찾지 못했습니다.\n" +
                "Tools > MCP Unity > Server Window에서 설정을 확인하세요."
            );

            return;
        }

        bool next = !current;

        string replacement =
            $"\"AutoStartServer\": {next.ToString().ToLowerInvariant()}";

        string updated =
            AutoStartRegex.Replace(json, replacement, 1);

        File.WriteAllText(SettingsPath, updated);
        Menu.SetChecked(MenuPath, next);

        Debug.Log(
            $"[MCP Toggle] WebSocket Auto Start: {(next ? "ON" : "OFF")}\n" +
            $"설정 파일: {SettingsPath}\n" +
            "현재 실행 중인 서버의 Start/Stop은 " +
            "Tools > MCP Unity > Server Window에서 제어하세요."
        );
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateToggle()
    {
        RefreshMenuCheck();
        return File.Exists(SettingsPath);
    }

    private static void RefreshMenuCheck()
    {
        if (TryReadAutoStart(out _, out bool enabled))
        {
            Menu.SetChecked(MenuPath, enabled);
        }
        else
        {
            Menu.SetChecked(MenuPath, false);
        }
    }

    private static bool TryReadAutoStart(
        out string json,
        out bool enabled)
    {
        json = string.Empty;
        enabled = false;

        if (!File.Exists(SettingsPath))
            return false;

        json = File.ReadAllText(SettingsPath);

        Match match = AutoStartRegex.Match(json);

        if (!match.Success)
            return false;

        return bool.TryParse(
            match.Groups[1].Value,
            out enabled
        );
    }
}

#endif
