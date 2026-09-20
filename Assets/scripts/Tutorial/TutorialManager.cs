using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.SceneManagement;

// 튜토리얼 대본(Script)을 한 줄씩 해석해 "페이지" 목록으로 미리 변환해두고, A/D(Select 맵의
// Left/Right)로 그 목록을 앞뒤로 이동하며 popupManager에 보여준다.
//
// 대본 문법(한 줄 = 한 명령):
//  - 숫자(또는 "2-1"처럼 "숫자-숫자") 한 줄만 있으면: tutorialObjectsRoot 밑에서 그 이름과 정확히
//    일치하는 오브젝트를 켜고 나머지 숫자 이름 오브젝트를 전부 끈다.
//  - 숫자 뒤에 "(이름 sprite)"/"(이름 스프라이트)"가 붙으면: 위 동작에 더해 그 이름의 스프라이트를
//    popupManager 아이콘으로 쓴다.
//  - 숫자 없이 "(이름 sprite)"만 있으면: tutorialBackground의 스프라이트를 그것으로 바꾼다.
//  - 그 외의 줄은 실제로 보여줄 문단이다.
// 숫자/괄호 줄은 화면에 그 자체로 표시되지 않고, 처리된 뒤 바로 다음 줄로 자동으로 넘어간다 —
// 그래서 사용자가 실제로 페이지를 넘길 때 보게 되는 것은 항상 문단(텍스트) 줄뿐이다.
// 숫자·배경 상태는 다음 숫자/배경 줄이 나오기 전까지 계속 유지된다(같은 구간의 문단 여러 줄에
// 그대로 적용됨). 아이콘은 숫자 줄이 나올 때마다 초기화되고, 그 줄에 괄호가 있을 때만 다시 채워진다.
public class TutorialManager : MonoBehaviour
{
    [Serializable]
    private class NamedSprite
    {
        public string name;
        public Sprite sprite;
    }

    // 대본에서 "(이름 sprite)"/"(이름 스프라이트)" 형태로 참조하는 스프라이트를 이름으로 찾기 위한
    // 목록. 아이콘(팝업 아이콘)과 배경(tutorial_background) 스프라이트를 구분하지 않고 이름으로만
    // 찾는다 — 인스펙터에서 대본에 나오는 이름(cardBackground, cost, cooldown, icon attack, shield,
    // turntimer)에 맞는 스프라이트를 채워 넣어야 한다.
    [SerializeField] private List<NamedSprite> icons = new List<NamedSprite>();

    [SerializeField] private Transform tutorialObjectsRoot;
    [SerializeField] private SpriteRenderer tutorialBackground;
    [SerializeField] private PopupManager popupManager;
    [SerializeField] private string beforeGameSceneName = "BeforeGame";

    private static readonly Regex HeaderRegex = new Regex(@"^(\d+(?:-\d+)?)\s*(?:\((.+)\))?$");
    private static readonly Regex BackgroundOnlyRegex = new Regex(@"^\((.+)\)$");
    private static readonly Regex TokenNameRegex = new Regex(@"^\d+(-\d+)?$");

    private readonly struct TutorialPage
    {
        public readonly string Token;
        public readonly Sprite Background;
        public readonly Sprite Icon;
        public readonly string Text;

        public TutorialPage(string token, Sprite background, Sprite icon, string text)
        {
            Token = token;
            Background = background;
            Icon = icon;
            Text = text;
        }
    }

    private readonly Dictionary<string, GameObject> _tokenObjects = new Dictionary<string, GameObject>();
    private readonly List<TutorialPage> _pages = new List<TutorialPage>();
    private int _pageIndex;

    private void Start()
    {
        BuildTokenMap();
        ParseScript();
        if (_pages.Count > 0) ShowPage(0);
    }

    private void OnEnable()
    {
        PlayerInputManager.Instance?.Load("Select", new Dictionary<string, Action>
        {
            ["Left"] = PrevPage,
            ["Right"] = NextPage,
        });
    }

    private void OnDisable()
    {
        PlayerInputManager.Instance?.Unload();
    }

    private void BuildTokenMap()
    {
        _tokenObjects.Clear();
        if (tutorialObjectsRoot == null) return;

        foreach (Transform t in tutorialObjectsRoot.GetComponentsInChildren<Transform>(true))
        {
            if (t == tutorialObjectsRoot) continue;
            if (TokenNameRegex.IsMatch(t.name))
                _tokenObjects[t.name] = t.gameObject;
        }
    }

    // 대본을 한 번에 끝까지 훑으며 문단(텍스트) 줄마다 그 시점까지 누적된 숫자/배경/아이콘 상태를
    // 그대로 스냅샷으로 저장한다 — 그래서 페이지 이동은 이 목록의 인덱스만 앞뒤로 옮기면 된다.
    private void ParseScript()
    {
        _pages.Clear();
        string currentToken = null;
        Sprite currentIcon = null;
        Sprite currentBackground = null;

        foreach (string rawLine in Script.Split('\n'))
        {
            string line = rawLine.Trim();
            if (line.Length == 0) continue;

            Match header = HeaderRegex.Match(line);
            if (header.Success)
            {
                currentToken = header.Groups[1].Value;
                currentIcon = header.Groups[2].Success
                    ? ResolveSprite(StripSpriteSuffix(header.Groups[2].Value))
                    : null;
                continue;
            }

            Match backgroundOnly = BackgroundOnlyRegex.Match(line);
            if (backgroundOnly.Success)
            {
                string content = backgroundOnly.Groups[1].Value.Trim();
                Sprite resolved = ResolveSprite(StripSpriteSuffix(content));
                if (resolved != null) currentBackground = resolved;
                continue;
            }

            _pages.Add(new TutorialPage(currentToken, currentBackground, currentIcon, line));
        }
    }

    private void NextPage()
    {
        if (_pageIndex >= _pages.Count - 1)
        {
            SceneManager.LoadScene(beforeGameSceneName);
            return;
        }
        ShowPage(_pageIndex + 1);
    }

    private void PrevPage()
    {
        if (_pageIndex <= 0) return;
        ShowPage(_pageIndex - 1);
    }

    private void ShowPage(int index)
    {
        _pageIndex = Mathf.Clamp(index, 0, _pages.Count - 1);
        TutorialPage page = _pages[_pageIndex];

        ApplyToken(page.Token);
        if (page.Background != null && tutorialBackground != null)
            tutorialBackground.sprite = page.Background;
        popupManager?.ShowRaw(page.Icon, page.Text);
    }

    // token 오브젝트와 그 조상(2-1의 "2"처럼 숫자 이름을 가진 상위 오브젝트)만 켜고, 그 외 숫자 이름
    // 오브젝트는 전부 끈다. 화살표 등 숫자 이름이 아닌 장식 자식은 직접 건드리지 않고, 부모의
    // 활성 상태를 그대로 물려받게 둔다.
    private void ApplyToken(string token)
    {
        var keep = new HashSet<GameObject>();
        if (token != null && _tokenObjects.TryGetValue(token, out GameObject target))
        {
            for (Transform t = target.transform; t != null && t != tutorialObjectsRoot; t = t.parent)
                keep.Add(t.gameObject);
        }

        foreach (var pair in _tokenObjects)
            pair.Value.SetActive(keep.Contains(pair.Value));
    }

    private Sprite ResolveSprite(string rawName)
    {
        string key = Normalize(rawName);
        foreach (NamedSprite entry in icons)
        {
            if (entry != null && Normalize(entry.name) == key)
                return entry.sprite;
        }
        Debug.LogWarning($"[TutorialManager] '{rawName}'에 해당하는 스프라이트를 icons 목록에서 찾지 못했습니다.");
        return null;
    }

    private static string StripSpriteSuffix(string s)
    {
        s = s.Trim();
        if (s.EndsWith("스프라이트")) return s.Substring(0, s.Length - "스프라이트".Length).Trim();
        if (s.EndsWith("sprite", StringComparison.OrdinalIgnoreCase)) return s.Substring(0, s.Length - "sprite".Length).Trim();
        return s;
    }

    private static string Normalize(string s) => s == null ? "" : s.Replace(" ", "").ToLowerInvariant();

    private const string Script = @"
(tutorial1)
0
실시간 카드게임, 실카에 오신것을 환영합니다
이 튜토리얼에서는 실카의 전투 방법에 대해 설명해드리도록 하겠습니다

1
이 바는 ""당신(파랑)""과 ""상대방(빨강)""의 체력 바 입니다. 상대의 체력을 먼저 0으로 만든 쪽이 승리합니다
상대의 체력은 내 패에 있는 카드를 통해 감소시킬 수 있습니다

2-1 (cardBackground sprite)
카드는 코스트, 쿨다운, 효과를 가집니다
2-2 (cost 스프라이트)
코스트는 당신이 카드를 사용하기 위해 필요한 비용입니다.
코스트는 한 턴에 2씩 증가하지만, 지금 가진 코스트가 10 이상이라면 1만 증가합니다.
2-3 (cooldown sprite)
쿨다운은 카드를 사용한 뒤 카드가 큐에 남아있는 시간입니다
2-4
사용한 카드는 큐로 이동하며, 쿨다운만큼의 시간이 지난 다음 덱으로 되돌아갑니다
2-5 (icon attack sprite)
효과는 카드의 효과입니다
카드는 최대 3개의 효과를 가질 수 있습니다
전투중이 아닐 때, 팝업을 통해 카드의 자세한 효과를 확인할 수 있습니다. 팝업은 Q,R로 페이지를 넘길 수 있습니다.

3
효과는 카드가 덱으로 되돌아갈 때 발동하는 효과와 큐에 남아있는 동안 효능을 발휘하는 효과로 나뉩니다
카드는 둘 중 하나의 효과들만 가질 수 있습니다. 카드의 배경을 통해 해당 카드가 가질 수 있는 효과의 종류를 확인할 수 있습니다

4
패에 있는 카드는 q,w,e,r을 통해 사용할 수 있습니다.
d를 통해 코스트를 1 지불하고 패를 언제나 다시 뽑을 수 있습니다. 턴의 종료시 자동으로 패를 다시 뽑습니다.


(tutorial2)
5 (shield sprite)
적과 나의 큐에 배정된 카드는 스케일을 통해 남은 쿨다운을 확인할 수 있습니다.  또한, 큐에서 카드들은 쿨다운이 낮은 순대로 가운데에 가까이 배치됩니다.

6
f를 통해 적의 공격을 방어할 수 있습니다.
방어는 공격의 피해를 절반으로 줄이고, 그 이외의 특정한 효과들을 저해합니다.
그러나, 턴 종료시 회복하는 코스트가 1 줄어듭니다.
방어는 카드 사용 등의 행동을 취할 시 자동으로 해제됩니다.

7
(turntimer sprite)
턴은 N초 이후, 플레이어의 의사와 상관 없이 자동으로 진행됩니다. 빠른 판단과 결정으로 승리를 쟁취하세요!


(tutorial6)
8
전투에서 승리한다면, 카드를 획득하거나, 제거하거나, 강화할 수 있습니다.
카드를 획득한다면 이미 정해진 카드들 중 하나를 고르게 되지만, 강화보다 카드의 효과의 효율이 좋습니다
카드를 강화한다면 더 유동적인 덱을 구성할 수 있지만, 강화 시 카드의 코스트와 쿨다운이 증가하게 됩니다.
필요없는 카드가 있다면, 대신 그 카드를 덱에서 제거할 수 있습니다.


(tutorial4)
9
당신은 제시되는 3명의 적 중 하나를 골라 싸우게 될 것입니다.

(tutorial5)
10
상대방의 덱을 잘 보고, 적을 전략적으로 선택하세요.
이상으로 실카의 설명을 마치겠습니다.
";
}
