using System.Collections.Generic;
using UnityEngine;

// Multi 전용 PopupDisplay.
// Single PopupDisplay와 구조는 동일하지만,
// BattleManager / GameManager 참조만 _Multi 버전으로 바꾼다.
public class PopupDisplay_Multi : MonoBehaviour
{
    private readonly List<PopupComponent> _slots =
        new List<PopupComponent>();

    private bool _slotsFound;
    private bool _clipChecked;

    public void SetEffects(
        IReadOnlyList<EffectType> effectTypes)
    {
        EnsureSlots();

        bool hasContent =
            effectTypes != null &&
            effectTypes.Count > 0;

        if (hasContent && !_clipChecked)
        {
            _clipChecked = true;
            CheckClipAndFlip();
        }

        gameObject.SetActive(true);

        for (int i = 0; i < _slots.Count; i++)
        {
            bool active =
                effectTypes != null &&
                i < effectTypes.Count;

            _slots[i].gameObject.SetActive(active);

            if (active)
            {
                _slots[i].SetEffect(
                    BattleManager_Multi.GetEmoji(
                        effectTypes[i]),
                    GameManager_Multi.GetEffectSummary(
                        effectTypes[i]));
            }
        }
    }

    private void EnsureSlots()
    {
        if (_slotsFound)
            return;

        _slots.AddRange(
            GetComponentsInChildren<
                PopupComponent>(true));

        _slotsFound = true;
    }

    private void CheckClipAndFlip()
    {
        Camera camera = Camera.main;

        bool anyClipped = false;

        foreach (PopupComponent slot in _slots)
        {
            if (slot.IsClipped(camera))
            {
                anyClipped = true;
                break;
            }
        }

        if (anyClipped)
        {
            Vector3 pos =
                transform.localPosition;

            pos.x = -pos.x;

            transform.localPosition = pos;
        }
    }
}
