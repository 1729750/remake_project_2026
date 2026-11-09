using System.Collections.Generic;
using UnityEngine;

// turnDisplay의 턴 숫자를 대신해, 큐에 쌓인 카드들이 몇 턴 뒤에 발동하는지 보여주는 눈금 게이지.
// scaleArea를 중심으로 왼쪽에 10칸(실제 큐 데이터, 남은 쿨다운 1칸당 한 칸), 오른쪽에 3칸(장식/퇴장
// 트레일)이 있고, 각 칸은 위/아래 눈금이 대칭으로 존재한다(위=적 큐, 아래=내 큐).
// 아래쪽(내 큐) 줄은 위쪽(적 큐)과 같은 프리팹을 위아래로 구분하기 위해 스프라이트를 flipY해서 쓴다.
// 색이 배정되지 않은(카드가 없는) 칸은 검정으로, 거리가 멀수록 더 옅게 표시된다.
//
// 매 턴 모든 큐 카드의 남은 쿨다운이 동시에 1씩 줄어든다 — 즉 좌표계 전체가 한 칸씩 밀리는 것과
// 같으므로, 카드가 차지한 칸이든 빈 칸이든 왼쪽 줄 전체가 한 덩어리로 한 칸씩 미끄러지듯 이동한다.
// 남은 쿨다운이 1인 카드(index 0)는 x=0에 위치하다가, 다음 턴에 발동해 큐를 떠나면 그 칸은 더 이상
// 실제 데이터를 추적하지 않고 마지막 색(근접색)은 고정한 채 오른쪽 장식 줄을 통과하는 코스메틱
// 트레일로 넘어간다. 트레일은 같은 방식(같은 _shiftOffset)으로 계속 한 칸씩 이동하며 오른쪽 끝을
// 향해 갈수록 투명해지다가 끝을 벗어나면 사라진다. 같은 진영에서 카드가 겹쳐서 퇴장할 수 있으므로
// 트레일은 한 번에 하나가 아니라 목록(최대 오른쪽 칸 수만큼)으로 관리해, 먼저 나간 트레일이 아직
// 오른쪽 줄을 지나가는 중에 새 트레일이 시작돼도 서로를 덮어써 중간에 사라지지 않게 한다.
public class TurnScaleDisplay
{
    private const int LeftCount = 10;
    private const int RightCount = 3;
    // 한 칸을 이동하는 데 걸리는 시간 — CardInstance.MoveTo의 기본 이동 시간과 맞춘다.
    private const float ShiftDuration = 0.3f;

    private readonly Color _defaultColor;
    private readonly float _leftFadePerStep;
    private readonly float _rightFadePerStep;
    private readonly Color _enemyNearColor;
    private readonly Color _enemyFarColor;
    private readonly Color _playerNearColor;
    private readonly Color _playerFarColor;

    private readonly float _spacing;
    private readonly float _verticalOffset;
    private readonly float _shiftSpeed;
    // 칸 위에 띄우는 효과 아이콘 줄의 최대 가로 길이 = 눈금 스프라이트 가로 크기 × 이 배율.
    private readonly float _effectMaxWidthRatio;

    private readonly RowState _enemyRow;
    private readonly RowState _playerRow;

    // 왼쪽/오른쪽 줄 전체가 한 칸 밀려 들어오는 애니메이션에 쓰는 공용 오프셋 — 턴이 바뀌는 순간
    // -spacing으로 점프했다가 0으로 되돌아오며, 그동안 모든 칸(색이 배정됐든 기본색이든, 퇴장
    // 트레일이든)의 x에 똑같이 더해진다.
    private float _shiftOffset;
    private int _lastSeenTurn = -1;

    // 오른쪽 장식 줄을 지나가는 중인 퇴장 트레일 하나. Index는 -1, -2, -3 ...(가상 index,
    // x = -Index * spacing) 이며 턴마다 하나씩 줄어들고, Color는 퇴장 시점의 근접색으로 고정된다.
    private struct ExitTrail
    {
        public int Index;
        public readonly Color Color;

        public ExitTrail(int index, Color color)
        {
            Index = index;
            Color = color;
        }
    }

    // 진영(적/내) 한 쪽의 눈금 상태 전체를 묶은 것.
    private class RowState
    {
        public SpriteRenderer[] Left;
        public SpriteRenderer[] Right;
        public CardInstance PrevNearest;
        public readonly List<ExitTrail> Exits = new List<ExitTrail>();
        // 왼쪽 칸별로 지금 그 칸을 차지한 카드들(같은 남은 쿨다운이면 여러 장이 한 칸에 겹친다)과,
        // 아이콘을 마지막으로 만들 때 기준이 됐던 카드들. 둘이 다를 때만 아이콘을 다시 만든다.
        public List<CardInstance>[] Occupants;
        public List<CardInstance>[] ShownOccupants;
        public List<EffectDisplay>[] SlotEffects;
    }

    public TurnScaleDisplay(
        GameObjectPool scalePool, float horizontalSpacing, float verticalOffset,
        Color defaultColor, float leftFadePerStep, float rightFadePerStep,
        Color enemyNearColor, Color enemyFarColor, Color playerNearColor, Color playerFarColor,
        float effectMaxWidthRatio)
    {
        _effectMaxWidthRatio = effectMaxWidthRatio;
        _defaultColor = defaultColor;
        _leftFadePerStep = leftFadePerStep;
        _rightFadePerStep = rightFadePerStep;
        _enemyNearColor = enemyNearColor;
        _enemyFarColor = enemyFarColor;
        _playerNearColor = playerNearColor;
        _playerFarColor = playerFarColor;

        _spacing = horizontalSpacing;
        _verticalOffset = verticalOffset;
        _shiftSpeed = horizontalSpacing / ShiftDuration;

        if (scalePool == null)
        {
            Debug.LogWarning("[TurnScaleDisplay] scalePool이 지정되지 않아 눈금을 만들지 않습니다.");
            _enemyRow = new RowState { Left = new SpriteRenderer[0], Right = new SpriteRenderer[0] };
            _playerRow = new RowState { Left = new SpriteRenderer[0], Right = new SpriteRenderer[0] };
            InitSlotEffects(_enemyRow);
            InitSlotEffects(_playerRow);
            return;
        }

        _enemyRow = new RowState
        {
            Left = BuildLeftRow(scalePool, LeftCount, 1, horizontalSpacing, verticalOffset),
            Right = BuildRightRow(scalePool, RightCount, 1, horizontalSpacing, verticalOffset),
        };
        _playerRow = new RowState
        {
            Left = BuildLeftRow(scalePool, LeftCount, -1, horizontalSpacing, verticalOffset),
            Right = BuildRightRow(scalePool, RightCount, -1, horizontalSpacing, verticalOffset),
        };
        InitSlotEffects(_enemyRow);
        InitSlotEffects(_playerRow);
    }

    private static void InitSlotEffects(RowState state)
    {
        int count = state.Left.Length;
        state.Occupants = new List<CardInstance>[count];
        state.ShownOccupants = new List<CardInstance>[count];
        state.SlotEffects = new List<EffectDisplay>[count];
        for (int i = 0; i < count; i++)
        {
            state.Occupants[i] = new List<CardInstance>();
            state.ShownOccupants[i] = new List<CardInstance>();
            state.SlotEffects[i] = new List<EffectDisplay>();
        }
    }

    // 눈금 오브젝트는 전투가 바뀌어도 풀에서 재사용되므로, 이전 TurnScaleDisplay가 붙여둔 효과
    // 아이콘이 자식으로 남아 있을 수 있다 — 새로 가져올 때마다 비운다.
    private static void ClearEffectChildren(GameObject tick)
    {
        foreach (EffectDisplay display in tick.GetComponentsInChildren<EffectDisplay>(true))
            Object.Destroy(display.gameObject);
    }

    // 왼쪽(데이터) 줄: 슬롯 i(0..LeftCount-1)의 논리 index는 그대로 i다 — index 0(남은 쿨다운 1)이 x=0.
    // pool.Get()은 재활용된(이전 전투에서 다른 역할로 쓰였을 수 있는) 오브젝트를 돌려줄 수 있으므로
    // flipY는 조건부로 켜기만 하지 않고 매번 명시적으로 대입해서 이전 상태가 남지 않게 한다.
    private static SpriteRenderer[] BuildLeftRow(GameObjectPool pool, int count, int ySign, float spacing, float verticalOffset)
    {
        var row = new SpriteRenderer[count];
        for (int i = 0; i < count; i++)
        {
            GameObject go = pool.Get();
            ClearEffectChildren(go);
            go.transform.localPosition = new Vector3(-i * spacing, ySign * verticalOffset, 0f);
            row[i] = go.GetComponent<SpriteRenderer>();
            if (row[i] != null)
                row[i].flipY = ySign < 0;
        }
        return row;
    }

    // 오른쪽(장식/퇴장 트레일) 줄: 슬롯 j(0..RightCount-1)의 논리 index는 -(j+1) — 왼쪽과 같은
    // x = -index * spacing 공식을 그대로 따르므로 x=0에서 이어지는 양수 x 쪽에 자연스럽게 놓인다.
    private static SpriteRenderer[] BuildRightRow(GameObjectPool pool, int count, int ySign, float spacing, float verticalOffset)
    {
        var row = new SpriteRenderer[count];
        for (int j = 0; j < count; j++)
        {
            int virtualIndex = -(j + 1);
            GameObject go = pool.Get();
            ClearEffectChildren(go);
            go.transform.localPosition = new Vector3(-virtualIndex * spacing, ySign * verticalOffset, 0f);
            row[j] = go.GetComponent<SpriteRenderer>();
            if (row[j] != null)
                row[j].flipY = ySign < 0;
        }
        return row;
    }

    // BattleManager.Tick에서 매번 호출된다.
    public void Refresh(CharacterManager player, CharacterManager enemy, float deltaTime)
    {
        int currentTurn = BattleManager.Instance != null ? BattleManager.Instance.GetCurrentTurn() : _lastSeenTurn;
        bool turnJustChanged = _lastSeenTurn >= 0 && currentTurn != _lastSeenTurn;
        if (turnJustChanged)
            _shiftOffset = -_spacing;
        _lastSeenTurn = currentTurn;
        _shiftOffset = Mathf.MoveTowards(_shiftOffset, 0f, _shiftSpeed * deltaTime);

        UpdateSide(_enemyRow, enemy, 1, _enemyNearColor, _enemyFarColor, turnJustChanged);
        UpdateSide(_playerRow, player, -1, _playerNearColor, _playerFarColor, turnJustChanged);
    }

    private void UpdateSide(RowState state, CharacterManager owner, int ySign, Color nearColor, Color farColor, bool turnJustChanged)
    {
        if (turnJustChanged)
        {
            // 기존 트레일들을 먼저 한 칸씩 늙히고, 오른쪽 줄을 완전히 벗어난 것은 제거한다.
            for (int i = state.Exits.Count - 1; i >= 0; i--)
            {
                ExitTrail trail = state.Exits[i];
                trail.Index--;
                if (trail.Index < -RightCount)
                    state.Exits.RemoveAt(i);
                else
                    state.Exits[i] = trail;
            }

            // 지난 턴 x=0(index 0)에 있던 카드가 이번 턴 큐에서 사라졌다 = 발동되어 퇴장했다.
            // 그 칸의 마지막 근접색을 고정한 새 트레일을 오른쪽 장식 줄 맨 앞칸(-1)에서 시작한다.
            if (state.PrevNearest != null && !QueueContains(owner, state.PrevNearest))
                state.Exits.Add(new ExitTrail(-1, nearColor));
        }

        CardInstance nearest = null;

        for (int i = 0; i < state.Left.Length; i++)
        {
            if (state.Left[i] == null) continue;
            state.Left[i].transform.localPosition = new Vector3(-i * _spacing + _shiftOffset, ySign * _verticalOffset, 0f);
            Color c = Color.black;
            c.a = _defaultColor.a * Mathf.Pow(_leftFadePerStep, i);
            state.Left[i].color = c;
        }

        for (int i = 0; i < state.Left.Length; i++)
            state.Occupants[i].Clear();

        if (owner != null)
        {
            foreach (CardInstance card in owner.GetQueue())
            {
                if (card == null) continue;
                int index = card.GetCooldownLeft() - 1;
                if (index == 0) nearest = card;
                if (index < 0 || index >= state.Left.Length || state.Left[index] == null) continue;

                float intensity = state.Left.Length > 1 ? 1f - (float)index / (state.Left.Length - 1) : 1f;
                Color c = Color.Lerp(farColor, nearColor, intensity);
                c.a = 1f;
                state.Left[index].color = c;
                state.Occupants[index].Add(card);
            }
        }
        state.PrevNearest = nearest;

        // 칸을 차지한 카드 구성이 바뀐 칸만 아이콘을 다시 만든다 — 매 프레임 호출되므로 매번
        // 새로 만들지 않는다(카드 구성은 턴 경계나 카드를 낼 때만 바뀐다).
        for (int i = 0; i < state.Left.Length; i++)
        {
            if (state.Left[i] == null || SameCards(state.Occupants[i], state.ShownOccupants[i])) continue;
            state.ShownOccupants[i].Clear();
            state.ShownOccupants[i].AddRange(state.Occupants[i]);
            RebuildSlotEffects(state.Left[i], state.SlotEffects[i], state.Occupants[i], ySign);
        }

        for (int j = 0; j < state.Right.Length; j++)
        {
            if (state.Right[j] == null) continue;
            int virtualIndex = -(j + 1);
            state.Right[j].transform.localPosition = new Vector3(-virtualIndex * _spacing + _shiftOffset, ySign * _verticalOffset, 0f);

            int trailAt = state.Exits.FindIndex(e => e.Index == virtualIndex);
            if (trailAt >= 0)
            {
                // 색상(색조)은 퇴장 시점 그대로 고정하고, 오른쪽 끝으로 갈수록(j가 커질수록) 알파만 낮춘다.
                Color c = state.Exits[trailAt].Color;
                c.a = 1f - (float)j / RightCount;
                state.Right[j].color = c;
            }
            else
            {
                Color c = Color.black;
                c.a = _defaultColor.a * Mathf.Pow(_rightFadePerStep, j);
                state.Right[j].color = c;
            }
        }
    }

    private static bool QueueContains(CharacterManager owner, CardInstance card)
    {
        if (owner == null) return false;
        foreach (CardInstance queued in owner.GetQueue())
            if (queued == card) return true;
        return false;
    }

    private static bool SameCards(List<CardInstance> a, List<CardInstance> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++)
            if (a[i] != b[i]) return false;
        return true;
    }

    // 칸(tick)을 차지한 카드들의 효과를 EffectType별로 합쳐(같은 효과는 아이콘 하나에 수치 합산)
    // 눈금 바깥쪽 끝(적 줄은 위, 뒤집힌 내 줄은 아래) 너머에 가운데 정렬로 띄운다. 아이콘은 tick의
    // 자식이라 턴 전환 시 눈금이 미끄러지는 애니메이션을 그대로 따라간다.
    private void RebuildSlotEffects(SpriteRenderer tick, List<EffectDisplay> displays, List<CardInstance> cards, int ySign)
    {
        var mergedTypes = new List<EffectType>();
        var mergedMagnitudes = new List<int>();
        foreach (CardInstance card in cards)
        {
            foreach (CardEffect cardEffect in card.GetEffects())
            {
                EffectType type = cardEffect.GetEffect().GetEffectType();
                if (BattleManager.GetEmoji(type) == null) continue; // Preserve/Disposable처럼 아이콘이 없는 마커 타입
                int magnitude = cardEffect.GetEffect().GetMagnitude();
                int at = mergedTypes.IndexOf(type);
                if (at >= 0)
                {
                    mergedMagnitudes[at] += magnitude;
                }
                else
                {
                    mergedTypes.Add(type);
                    mergedMagnitudes.Add(magnitude);
                }
            }
        }

        while (displays.Count < mergedTypes.Count)
        {
            EffectDisplay spawned = EffectDisplay.Spawn(tick.transform);
            if (spawned == null) break;
            displays.Add(spawned);
        }

        int shown = Mathf.Min(mergedTypes.Count, displays.Count);
        for (int k = 0; k < displays.Count; k++)
            displays[k].gameObject.SetActive(k < shown);
        if (shown == 0 || tick.sprite == null) return;

        float totalNativeWidth = 0f;
        float maxNativeHeight = 0f;
        var nativeSizes = new Vector2[shown];
        for (int k = 0; k < shown; k++)
        {
            Effect representative = Effect.Create(mergedTypes[k], mergedMagnitudes[k]);
            displays[k].SetEffect(representative, mergedMagnitudes[k].ToString());
            displays[k].SetSortingLayer(tick.sortingLayerName);

            Vector2 size = displays[k].GetSpriteSize();
            if (size.x <= 0f || size.y <= 0f) size = Vector2.one;
            nativeSizes[k] = size;
            totalNativeWidth += size.x;
            maxNativeHeight = Mathf.Max(maxNativeHeight, size.y);
        }

        // 아이콘 하나일 때는 높이가 최대 폭과 같아지도록(대략 정사각형) 하고, 여러 개가 그 폭을
        // 넘으면 전체 줄이 최대 폭에 딱 맞도록 균일하게 줄인다.
        Bounds tickBounds = tick.sprite.bounds;
        float maxWidth = tickBounds.size.x * _effectMaxWidthRatio;
        float scale = maxWidth / Mathf.Max(totalNativeWidth, maxNativeHeight);
        float rowWidth = totalNativeWidth * scale;
        float rowHeight = maxNativeHeight * scale;

        // flipY는 피벗 기준으로 y를 뒤집으므로, 뒤집힌 줄의 바깥쪽 끝은 -bounds.max.y다.
        float gap = rowHeight * 0.25f;
        float centerY = ySign > 0
            ? tickBounds.max.y + gap + rowHeight / 2f
            : -tickBounds.max.y - gap - rowHeight / 2f;

        float cursor = -rowWidth / 2f;
        for (int k = 0; k < shown; k++)
        {
            float width = nativeSizes[k].x * scale;
            displays[k].SetScale(scale);
            displays[k].SetLocalPosition(new Vector3(cursor + width / 2f, centerY, 0f));
            cursor += width;
        }
    }
}
