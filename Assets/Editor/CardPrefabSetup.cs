using TMPro;
using UnityEditor;
using UnityEngine;

public static class CardPrefabSetup
{
    private const string PrefabPath = "Assets/resources/preFabs/Card.prefab";

    [MenuItem("Tools/Add Card Text Boxes")]
    static void AddCardTextBoxes()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

        foreach (string name in new[] { "NameText", "effectText", "CostText", "cooltimeText" })
            RemoveExisting(root, name);

        // Center — effect list
        AddTextChild(root, "effectText",
            anchoredPos: new Vector2(0f, 0f),
            localZ: -0.1f,
            size: new Vector2(0.7f, 0.3f),
            fontSize: 0.35f,
            alignment: TextAlignmentOptions.Center,
            sortingOrder: 10);

        // Upper-right — cost
        AddTextChild(root, "CostText",
            anchoredPos: new Vector2(0.35f, 0.38f),
            localZ: -0.1f,
            size: new Vector2(0.2f, 0.2f),
            fontSize: 0.35f,
            alignment: TextAlignmentOptions.Center,
            sortingOrder: 10);

        // Upper-left — cooltime
        AddTextChild(root, "cooltimeText",
            anchoredPos: new Vector2(-0.35f, 0.38f),
            localZ: -0.1f,
            size: new Vector2(0.2f, 0.2f),
            fontSize: 0.35f,
            alignment: TextAlignmentOptions.Center,
            sortingOrder: 10);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
        AssetDatabase.Refresh();
        Debug.Log("Card text boxes updated.");
    }

    static void AddTextChild(GameObject parent, string name, Vector2 anchoredPos, float localZ,
        Vector2 size, float fontSize, TextAlignmentOptions alignment, int sortingOrder)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);

        RectTransform rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.localPosition = new Vector3(0f, 0f, localZ);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        TextMeshPro tmp = go.AddComponent<TextMeshPro>();
        tmp.text = "";
        tmp.fontSize = fontSize;
        tmp.alignment = alignment;
        tmp.sortingOrder = sortingOrder;
    }

    static void RemoveExisting(GameObject root, string childName)
    {
        Transform t = root.transform.Find(childName);
        if (t != null)
            Object.DestroyImmediate(t.gameObject);
    }
}
