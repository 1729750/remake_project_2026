using UnityEngine;

// DefaultEnemyAI와 같은 채점 로직을 쓰되, 두 가지 방법으로 고의로 성능을 낮춘 AI.
// 1) decisionInterval 프레임마다 한 번만 실제로 판단하고, 그 사이에는 아무것도 하지 않는다(반응이 느리다).
// 2) 각 카드 점수에 무작위 노이즈를 더해 비교하므로, 점수 차가 노이즈보다 작은 카드들 사이에서
//    가끔 최선이 아닌(또는 원래라면 안 냈을) 카드를 고른다.
[CreateAssetMenu(fileName = "WeakEnemyAI", menuName = "Scriptable Objects/Enemy AI/Weak")]
public class WeakEnemyAI : DefaultEnemyAI
{
    [SerializeField, Tooltip("몇 프레임(Update 틱)마다 한 번만 실제로 판단할지. 클수록 반응이 느려진다.")]
    private int decisionInterval = 10;

    [SerializeField, Tooltip("카드 점수에 더해지는 무작위 노이즈의 최대 절대값. 클수록 더 자주 잘못된(최선이 아닌) 카드를 고른다.")]
    private float scoreNoise = 6f;

    public override int Decide(in BattleSnapshot snapshot)
    {
        // decisionInterval프레임 중 이번 프레임이 판단 차례가 아니면 아무것도 하지 않는다 — 지연 반응.
        if (decisionInterval > 1 && Time.frameCount % decisionInterval != 0)
            return ActionNone;

        return base.Decide(snapshot);
    }

    protected override int ChooseBestCardIndex(in BattleSnapshot snapshot)
    {
        CardInstance[] hand = snapshot.SelfHand;
        int bestIndex = -1;
        float bestScore = 0f;

        for (int i = 0; i < hand.Length; i++)
        {
            CardInstance card = hand[i];
            if (card == null) continue;
            if (PreviewCost(snapshot, card) > snapshot.SelfCost) continue;

            float score = ScoreCard(snapshot, card) + UnityEngine.Random.Range(-scoreNoise, scoreNoise);
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }
}
