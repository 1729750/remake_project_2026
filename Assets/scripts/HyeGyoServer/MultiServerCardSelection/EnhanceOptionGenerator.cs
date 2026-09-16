using UnityEngine;

public sealed class EnhanceOptionGenerator
{
    private readonly EffectPriceDatabase
        database;

    public EnhanceOptionGenerator(
        EffectPriceDatabase database)
    {
        this.database = database;
    }

    public CardUpgrade RollEnhanceOption()
    {
        if (database == null)
        {
            Debug.LogError(
                "[EnhanceOptionGenerator] " +
                "EffectPriceDatabase가 없습니다."
            );

            return default;
        }

        if (database.Count <= 0)
        {
            Debug.LogError(
                "[EnhanceOptionGenerator] " +
                "강화 옵션 데이터가 없습니다."
            );

            return default;
        }

        int randomIndex =
            Random.Range(
                0,
                database.Count
            );

        if (!database.TryCreateUpgrade(
                randomIndex,
                out CardUpgrade upgrade))
        {
            Debug.LogError(
                "[EnhanceOptionGenerator] " +
                $"강화 옵션 생성 실패 | " +
                $"Index: {randomIndex}"
            );

            return default;
        }

        Debug.Log(
            "[EnhanceOptionGenerator] " +
            $"강화 옵션 생성 | " +
            $"Index: {randomIndex}"
        );

        return upgrade;
    }
}