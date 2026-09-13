using UnityEngine;

// turnDisplay의 턴 숫자를 대신해, 큐에 쌓인 카드들이 몇 턴 뒤에 발동하는지 보여주는 눈금 게이지.
// scaleArea를 중심으로 왼쪽에 10칸, 오른쪽에 3칸이 있고, 각 칸은 위/아래 눈금이 대칭으로 존재한다.
// 왼쪽 눈금만 실제 큐 데이터에 연동된다(위=적 큐, 아래=내 큐, 칸 번호=카드의 남은 쿨다운) —
// 오른쪽은 장식용으로 항상 기본 반투명 상태이며, 중심에서 멀어질수록 왼쪽보다 더 급격히 옅어진다.
public class TurnScaleDisplay
{
    private const int LeftCount = 10;
    private const int RightCount = 3;

    private readonly Color _defaultColor;
    private readonly float _leftFadePerStep;
    private readonly float _rightFadePerStep;
    private readonly Color _enemyNearColor;
    private readonly Color _enemyFarColor;
    private readonly Color _playerNearColor;
    private readonly Color _playerFarColor;

    // 각 배열의 index 0 = 중심에서 가장 가까운 칸(거리 1)
    private readonly SpriteRenderer[] _leftTop;
    private readonly SpriteRenderer[] _leftBottom;
    private readonly SpriteRenderer[] _rightTop;
    private readonly SpriteRenderer[] _rightBottom;

    public TurnScaleDisplay(
        Transform scaleArea, GameObject scalePrefab, float horizontalSpacing, float verticalOffset,
        Color defaultColor, float leftFadePerStep, float rightFadePerStep,
        Color enemyNearColor, Color enemyFarColor, Color playerNearColor, Color playerFarColor)
    {
        _defaultColor = defaultColor;
        _leftFadePerStep = leftFadePerStep;
        _rightFadePerStep = rightFadePerStep;
        _enemyNearColor = enemyNearColor;
        _enemyFarColor = enemyFarColor;
        _playerNearColor = playerNearColor;
        _playerFarColor = playerFarColor;

        if (scaleArea == null || scalePrefab == null)
        {
            Debug.LogWarning("[TurnScaleDisplay] scaleArea/scalePrefab가 지정되지 않아 눈금을 만들지 않습니다.");
            _leftTop = _leftBottom = new SpriteRenderer[0];
            _rightTop = _rightBottom = new SpriteRenderer[0];
            return;
        }

        _leftTop = BuildRow(scaleArea, scalePrefab, LeftCount, -1, 1, horizontalSpacing, verticalOffset);
        _leftBottom = BuildRow(scaleArea, scalePrefab, LeftCount, -1, -1, horizontalSpacing, verticalOffset);
        _rightTop = BuildRow(scaleArea, scalePrefab, RightCount, 1, 1, horizontalSpacing, verticalOffset);
        _rightBottom = BuildRow(scaleArea, scalePrefab, RightCount, 1, -1, horizontalSpacing, verticalOffset);
    }

    private static SpriteRenderer[] BuildRow(Transform parent, GameObject prefab, int count, int xSign, int ySign, float spacing, float verticalOffset)
    {
        var row = new SpriteRenderer[count];
        for (int i = 0; i < count; i++)
        {
            int distance = i + 1;
            GameObject go = Object.Instantiate(prefab, parent);
            go.transform.localPosition = new Vector3(xSign * distance * spacing, ySign * verticalOffset, 0f);
            row[i] = go.GetComponent<SpriteRenderer>();
        }
        return row;
    }

    // BattleManager.Tick에서 매번 호출된다. 두 CharacterManager의 큐를 보고 왼쪽 눈금 색을 갱신하고,
    // 나머지는 거리 기반 기본 반투명 색으로 되돌린다.
    public void Refresh(CharacterManager player, CharacterManager enemy)
    {
        ResetRow(_leftTop, _leftFadePerStep);
        ResetRow(_leftBottom, _leftFadePerStep);
        ResetRow(_rightTop, _rightFadePerStep);
        ResetRow(_rightBottom, _rightFadePerStep);

        ApplyQueue(_leftTop, enemy, _enemyNearColor, _enemyFarColor);
        ApplyQueue(_leftBottom, player, _playerNearColor, _playerFarColor);
    }

    private void ResetRow(SpriteRenderer[] row, float fadePerStep)
    {
        for (int i = 0; i < row.Length; i++)
        {
            if (row[i] == null) continue;
            Color c = _defaultColor;
            c.a = _defaultColor.a * Mathf.Pow(fadePerStep, i);
            row[i].color = c;
        }
    }

    // owner의 큐에 있는 카드마다, 남은 쿨다운(GetCooldownLeft) 칸의 눈금을 해당 진영 색으로 칠한다.
    // 남은 턴이 적을수록(칸이 중심에 가까울수록) nearColor에 가깝게, 멀수록 farColor에 가깝게 —
    // 색이 칠해진 눈금은 항상 완전 불투명하다(반투명 처리 대상에서 제외).
    private static void ApplyQueue(SpriteRenderer[] row, CharacterManager owner, Color nearColor, Color farColor)
    {
        if (owner == null || row.Length == 0) return;

        foreach (CardInstance card in owner.GetQueue())
        {
            if (card == null) continue;
            int index = card.GetCooldownLeft() - 1;
            if (index < 0 || index >= row.Length || row[index] == null) continue;

            float intensity = row.Length > 1 ? 1f - (float)index / (row.Length - 1) : 1f;
            Color c = Color.Lerp(farColor, nearColor, intensity);
            c.a = 1f;
            row[index].color = c;
        }
    }
}
