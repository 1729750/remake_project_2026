using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// 일회성 에디터 툴: "약한 적" WeakEnemyAI 에셋 하나와, 그 AI를 쓰는 CharacterData/CardCollection
// 5쌍을 Assets/scripts/Enemies/에 생성한다. 이미 존재하는 파일이 있으면 덮어쓰지 않고 건너뛴다.
public static class WeakEnemyGenerator
{
    private const string CardsDir = "Assets/cards/";
    private const string ClaudeDir = "Assets/card_claude/";
    private const string OutDir = "Assets/scripts/Enemies/";
    private const string WeakAiPath = OutDir + "WeakEnemyAI.asset";
    private const string DefaultAiPath = OutDir + "DefaultEnemyAI.asset";

    [MenuItem("Tools/Generate Weak Enemies")]
    public static void Generate()
    {
        GetOrCreateDefaultAi();
        WeakEnemyAI ai = GetOrCreateWeakAi();

        // (이름, 최대체력, 카드 경로 반복 목록)
        CreateWeakEnemy(ai, "WeakBrawler", 65, new[]
        {
            (ClaudeDir + "iron-resolve.asset", 2),
            (CardsDir + "attack.asset", 3),
            (ClaudeDir + "bastion-core.asset", 1),
            (ClaudeDir + "second-wind.asset", 1),
        });

        CreateWeakEnemy(ai, "WeakGuard", 75, new[]
        {
            (CardsDir + "defense.asset", 2),
            (ClaudeDir + "bastion-core.asset", 2),
            (ClaudeDir + "iron-resolve.asset", 2),
            (ClaudeDir + "second-wind.asset", 1),
        });

        CreateWeakEnemy(ai, "WeakBurner", 70, new[]
        {
            (ClaudeDir + "blight-bomb.asset", 3),
            (ClaudeDir + "slow-burn.asset", 2),
            (CardsDir + "attack.asset", 2),
        });

        CreateWeakEnemy(ai, "WeakGlass", 60, new[]
        {
            (ClaudeDir + "flicker-blade.asset", 2),
            (ClaudeDir + "rift-fang.asset", 2),
            (CardsDir + "attack.asset", 2),
            (ClaudeDir + "second-wind.asset", 1),
        });

        CreateWeakEnemy(ai, "WeakScrapper", 80, new[]
        {
            (CardsDir + "CostGet.asset", 1),
            (ClaudeDir + "overheal-core.asset", 1),
            (CardsDir + "attack.asset", 3),
            (ClaudeDir + "iron-resolve.asset", 2),
        });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[WeakEnemyGenerator] Done.");
    }

    private static DefaultEnemyAI GetOrCreateDefaultAi()
    {
        DefaultEnemyAI existing = AssetDatabase.LoadAssetAtPath<DefaultEnemyAI>(DefaultAiPath);
        if (existing != null) return existing;

        DefaultEnemyAI ai = ScriptableObject.CreateInstance<DefaultEnemyAI>();
        AssetDatabase.CreateAsset(ai, DefaultAiPath);
        return ai;
    }

    private static WeakEnemyAI GetOrCreateWeakAi()
    {
        WeakEnemyAI existing = AssetDatabase.LoadAssetAtPath<WeakEnemyAI>(WeakAiPath);
        if (existing != null) return existing;

        WeakEnemyAI ai = ScriptableObject.CreateInstance<WeakEnemyAI>();
        AssetDatabase.CreateAsset(ai, WeakAiPath);
        return ai;
    }

    private static void CreateWeakEnemy(WeakEnemyAI ai, string name, int maxHealth, (string path, int count)[] cardPaths)
    {
        string enemyPath = $"{OutDir}{name}Enemy.asset";
        if (AssetDatabase.LoadAssetAtPath<CharacterData>(enemyPath) != null)
        {
            Debug.Log($"[WeakEnemyGenerator] Skipping {name} (already exists)");
            return;
        }

        List<CardDefinition> cards = new List<CardDefinition>();
        foreach (var (path, count) in cardPaths)
        {
            CardDefinition def = AssetDatabase.LoadAssetAtPath<CardDefinition>(path);
            if (def == null)
            {
                Debug.LogError($"[WeakEnemyGenerator] Missing card asset: {path}");
                continue;
            }
            for (int i = 0; i < count; i++)
                cards.Add(def);
        }

        CardCollection collection = ScriptableObject.CreateInstance<CardCollection>();
        SerializedObject collectionSo = new SerializedObject(collection);
        SerializedProperty cardsProp = collectionSo.FindProperty("cards");
        cardsProp.arraySize = cards.Count;
        for (int i = 0; i < cards.Count; i++)
            cardsProp.GetArrayElementAtIndex(i).objectReferenceValue = cards[i];
        collectionSo.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(collection, $"{OutDir}{name}Deck.asset");

        CharacterData data = ScriptableObject.CreateInstance<CharacterData>();
        SerializedObject dataSo = new SerializedObject(data);
        dataSo.FindProperty("maxHealth").intValue = maxHealth;
        dataSo.FindProperty("deck").objectReferenceValue = collection;
        dataSo.FindProperty("enemyAI").objectReferenceValue = ai;
        dataSo.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(data, enemyPath);
    }
}
