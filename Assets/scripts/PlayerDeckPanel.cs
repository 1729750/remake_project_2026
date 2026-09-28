using System;
using System.Collections.Generic;
using UnityEngine;

// SinglePlayMode 씬에 미리 배치된 DeckPanel(DeckBackground + DeckDisplay 자식)을 담당한다.
// MapManager.LoadDeckDisplayInput(적 덱 보기)과 같은 패턴 — 지금 맨 위에 있는 Select context
// 위에 새 Select context를 쌓고, Cancel로 스택을 원래대로 되돌린다.
public class PlayerDeckPanel : MonoBehaviour
{
    [SerializeField] private DeckDisplay deckDisplay;
    // 이 패널 전용 팝업 인스턴스(씬 전역 static Instance 대신).
    [SerializeField] private PopupManager popupManager;

    private void Awake()
    {
        gameObject.SetActive(false);
        deckDisplay?.SetPopupManager(popupManager);
    }

    public void Open()
    {
        if (deckDisplay == null) return;

        gameObject.SetActive(true);
        deckDisplay.SetDeck(PlayerManager.Instance.GetDeck(), null);

        PlayerInputManager.Instance.Load("Select", new Dictionary<string, Action>
        {
            ["Left"]   = () => deckDisplay.MoveSelectionHorizontal(-1),
            ["Right"]  = () => deckDisplay.MoveSelectionHorizontal(1),
            ["Up"]     = () => deckDisplay.MoveSelectionVertical(-1),
            ["Down"]   = () => deckDisplay.MoveSelectionVertical(1),
            ["Cancel"] = Close,
        });
        deckDisplay.SelectFirst();
    }

    private void Close()
    {
        PlayerInputManager.Instance.Unload();
        deckDisplay.Deselect();
        gameObject.SetActive(false);
    }
}
