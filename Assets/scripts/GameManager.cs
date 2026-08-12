using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    private GameState _currentState;

    [SerializeField] private BattleManager battleManager;
    [SerializeField] private RewardManager rewardManager;
    [SerializeField] private InputManager inputManager;
    [SerializeField] private MapManager mapManager;
    [SerializeField] private CharacterData firstEnemyData;
    // 적 후보 풀(인스펙터 원본). Awake에서 _enemyCandidatePool로 복제되고, 이후로는 이 배열 자체를
    // 직접 건드리지 않는다.
    [SerializeField] private CharacterData[] enemyCandidates;
    // 실제로 뽑아가는 풀. ShowEnemySelection이 매번 이 중 3개를 중복 없이 랜덤으로 뽑아 보여주고,
    // 선택 여부와 무관하게 뽑힌 캐릭터는 여기서 제거되어 다시는 후보로 나오지 않는다.
    private List<CharacterData> _enemyCandidatePool;

    // MonoBehaviour가 아닌 순수 C# 클래스라 그냥 인스턴스를 들고 있는다.
    private readonly CharacterScaler characterScaler = new CharacterScaler();

    private const string EffectPricesResourcePath = "Data/EffectPrices";
    private static Dictionary<EffectType, EffectPriceInfo> _effectPriceCache;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _enemyCandidatePool = new List<CharacterData>(enemyCandidates);

        BuildEffectPriceCache();
    }

    // Resources/Data/EffectPrices.json을 읽어 EffectType별 가격/magnitude 범위표를 채운다.
    // JSON 예시(하한 10, 상한 20, 유닛 2 → 10/12/14/16/18/20 중 하나가 뽑힐 수 있다):
    // {
    //   "prices": [
    //     { "effectType": "Attack", "price": 1.5, "magnitudeMin": 1, "magnitudeMax": 5, "magnitudeUnit": 1 },
    //     { "effectType": "Strength", "price": 3, "magnitudeMin": 10, "magnitudeMax": 20, "magnitudeUnit": 2 }
    //   ]
    // }
    private void BuildEffectPriceCache()
    {
        _effectPriceCache = new Dictionary<EffectType, EffectPriceInfo>();

        TextAsset json = Resources.Load<TextAsset>(EffectPricesResourcePath);
        if (json != null)
        {
            EffectPriceTable table = JsonUtility.FromJson<EffectPriceTable>(json.text);
            if (table?.prices != null)
            {
                foreach (EffectPriceJsonEntry entry in table.prices)
                {
                    if (Enum.TryParse(entry.effectType, true, out EffectType type))
                    {
                        _effectPriceCache[type] = new EffectPriceInfo
                        {
                            price = entry.price,
                            magnitudeMin = entry.magnitudeMin,
                            magnitudeMax = entry.magnitudeMax,
                            magnitudeUnit = entry.magnitudeUnit,
                        };
                    }
                    else
                    {
                        Debug.LogWarning($"[GameManager] EffectPrices.json에 알 수 없는 EffectType: {entry.effectType}");
                    }
                }
            }
        }
        else
        {
            Debug.LogWarning($"[GameManager] Resources/{EffectPricesResourcePath}.json을 찾지 못했습니다.");
        }
    }

    [Serializable]
    private class EffectPriceJsonEntry
    {
        public string effectType;
        public float price;
        public int magnitudeMin;
        public int magnitudeMax;
        public int magnitudeUnit;
    }

    [Serializable]
    private class EffectPriceTable
    {
        public List<EffectPriceJsonEntry> prices;
    }

    void Start()
    {
        _currentState = GameState.StartScreen;
        battleManager.Init();
        EndBattle();
        //ShowEnemySelection();
    }

    public void StartBattle(CharacterData enemyData)
    {
        SetGameState(GameState.Battle);
        inputManager.Unload();
        battleManager.StartBattle(PlayerManager.Instance.GetCharacterData(), enemyData);
    }


    public void EndBattle()
    {
        SetGameState(GameState.BattleEnd);
        inputManager.Unload();
        rewardManager.ShowRewardDisplay();
    }

    // RewardManager가 카드 획득을 확정할 때 부르는, PlayerManager 덱을 직접 건드리는 지점.
    public void AddCard(CardDefinition card)
    {
        PlayerManager.Instance.AddCard(card);
    }

    // RewardManager가 카드 삭제를 확정할 때 부르는, PlayerManager 덱을 직접 건드리는 지점.
    public void DiscardCard(int index)
    {
        PlayerManager.Instance.DiscardCard(index);
    }

    // RewardManager가 카드 강화를 확정할 때 부르는, PlayerManager 덱을 직접 건드리는 지점.
    public void EnhanceCard(int index, CardUpgrade upgrade)
    {
        PlayerManager.Instance.GetDeck()[index].ApplyUpgrade(upgrade);
    }

    // EffectType당 강화 가격(강화 예산 산출용 계수). JSON의 price는 소숫점을 가질 수 있어 버림해서 반환한다.
    // 보통 Awake에서 이미 채워진 캐시를 그대로 읽지만, (에디터 툴 등에서) Awake보다 먼저 호출된 경우를
    // 대비해 비어 있으면 그때 채운다.
    public static int GetEffectPrice(EffectType effectType)
    {
        return Mathf.FloorToInt(GetEffectPriceInfo(effectType).price);
    }

    // EffectType당 magnitude 하한/상한/유닛 단위. RewardManager가 강화 후보의 magnitude를 고를 때 쓴다.
    public static EffectPriceInfo GetEffectPriceInfo(EffectType effectType)
    {
        if (_effectPriceCache == null)
            Instance.BuildEffectPriceCache();
        _effectPriceCache.TryGetValue(effectType, out EffectPriceInfo info);
        return info;
    }

    // 마지막 후보 슬롯은 _enemyCandidatePool에서 뽑지 않고 GenerateRandomEnemy로 완전히 새로
    // 생성한다. 나머지 (EnemySelectionCount - 1)개만 풀에서 뽑아 소모한다.
    private const int EnemySelectionCount = 3;

    public void ShowEnemySelection()
    {
        SetGameState(GameState.SelectEnemy);
        inputManager.Load("Select", new Dictionary<string, Action>
        {
            ["Left"]   = () => mapManager.MoveSelection(-1),
            ["Right"]  = () => mapManager.MoveSelection(1),
            ["Select"] = mapManager.ConfirmSelection,
        });

        CharacterData[] picks = PickRandomDistinct(_enemyCandidatePool, EnemySelectionCount - 1);
        foreach (CharacterData pick in picks)
            _enemyCandidatePool.Remove(pick);

        CharacterData enemyA = picks.Length > 0 ? picks[0] : null;
        CharacterData enemyB = picks.Length > 1 ? picks[1] : null;
        CharacterData randomEnemy = GenerateRandomEnemy(mapManager.GetCurrentRound());

        // characterScaler가 셋 다 복사본으로 강화까지 마쳐서 돌려준다 — 원본(풀에서 뽑힌 에셋,
        // 방금 만든 randomEnemy)은 건드리지 않는다.
        CharacterData[] scaled = characterScaler.Scale(enemyA, enemyB, randomEnemy, mapManager.GetCurrentRound());
        CharacterData[] candidates = scaled.Where(c => c != null).ToArray();

        mapManager.ShowEnemySelection(candidates);
    }

    // 완전 무작위 적의 덱만 생성한다(강화는 CharacterScaler가 맡는다).
    // 1) RewardManager의 rewardCards 풀에서 8~12장을 중복 허용 랜덤으로 뽑아(각각 Clone) 덱을 구성한다.
    // 2) 80 ~ (120 + enemySelectionCount * 10) 사이의 체력을 골라 CharacterData로 감싼다.
    // enemySelectionCount: 지금까지 플레이어가 적을 선택(확정)한 횟수(MapManager.GetCurrentRound()).
    private CharacterData GenerateRandomEnemy(int enemySelectionCount)
    {
        CardDefinition[] rewardCards = rewardManager.GetRewardCards();
        if (rewardCards == null || rewardCards.Length == 0) return null;

        int cardCount = UnityEngine.Random.Range(8, 13); // 8~12
        List<CardDefinition> deck = new List<CardDefinition>(cardCount);
        for (int i = 0; i < cardCount; i++)
        {
            CardDefinition source = rewardCards[UnityEngine.Random.Range(0, rewardCards.Length)];
            deck.Add(source.Clone());
        }

        int maxHealth = UnityEngine.Random.Range(80, 121 + enemySelectionCount * 10);
        CardCollection collection = CardCollection.Create(deck);
        return CharacterData.Create(maxHealth, collection);
    }

    // source에서 최대 count개를 중복 없이 랜덤으로 뽑아 반환한다. source가 count보다 작으면 전부 반환한다.
    // 적 후보 풀(_enemyCandidatePool) 전용 — 카드 보상 쪽 랜덤 뽑기는 RewardManager가 자체적으로 갖는다.
    private static T[] PickRandomDistinct<T>(IReadOnlyList<T> source, int count)
    {
        if (source == null || source.Count == 0) return Array.Empty<T>();

        List<T> pool = new List<T>(source);
        int pickCount = Mathf.Min(count, pool.Count);
        T[] result = new T[pickCount];
        for (int i = 0; i < pickCount; i++)
        {
            int index = UnityEngine.Random.Range(0, pool.Count);
            result[i] = pool[index];
            pool.RemoveAt(index);
        }
        return result;
    }

    public GameState GetGameState()
    {
        return _currentState;
    }

    public void SetGameState(GameState newState)
    {
        _currentState = newState;
        LoadAppropriateManager();
    }

    // manager들의 enable/disable은 오직 이 지점을 통해서만 이뤄진다.
    // 현재 gameState를 담당하는 manager만 enable하고 나머지는 전부 disable한다.
    private void LoadAppropriateManager()
    {

        bool battleActive = false;
        bool mapActive = false;
        switch (_currentState)
        {
            case GameState.StartScreen:
                break;
            case GameState.BattleEnd:
                battleActive = true;
                break;
            case  GameState.Battle:
                battleActive = true;
                break;
            case GameState.SelectEnemy:
                mapActive = true;
                break;
            default:
                break;
        }

        battleManager.gameObject.SetActive(battleActive);
        mapManager.gameObject.SetActive(mapActive);

        // rewardPanel은 다른 오브젝트 위에 얹히는 패널이라 battleActive/mapActive와 상호배타적이지 않다.
        rewardManager.gameObject.SetActive(_currentState == GameState.BattleEnd);
    }

    static public DeckDisplay SummonDeck(Func<CardDefinition, bool> filter = null)
    {
        DeckDisplay deckDisplay = Instantiate(Resources.Load<GameObject>("Prefabs/DeckDisplay")).GetComponent<DeckDisplay>();
        Camera cam = Camera.main;
        float screenHeight = 2f * cam.orthographicSize;
        float screenWidth = screenHeight * cam.aspect;
        deckDisplay.transform.position = new Vector3(cam.transform.position.x, cam.transform.position.y, deckDisplay.transform.position.z);
        deckDisplay.SetSize(new Vector2(screenWidth/3*2, screenHeight/3*2));
        deckDisplay.SetDeck(PlayerManager.Instance.GetDeck(), filter, 1.2f);
        return deckDisplay;
    }
}

public enum GameState{
    StartScreen,
    Battle,
    BattleEnd,
    SelectEnemy,
}

// EffectPrices.json 한 항목이 담는 정보: 강화 예산 계수(price)와 magnitude를 고를 범위(min~max, unit 간격).
public struct EffectPriceInfo
{
    public float price;
    public int magnitudeMin;
    public int magnitudeMax;
    public int magnitudeUnit;
}