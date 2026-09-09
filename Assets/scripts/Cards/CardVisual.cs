using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class CardVisual : MonoBehaviour
{
    private SpriteRenderer _background;
    private SpriteRenderer _sprite;
    private SpriteRenderer _spriteBackground;
    private Transform _effectArea;
    private TextMeshPro _costText;
    private TextMeshPro _cooltimeText;
    private GameObject _selectHighlight;
    private GameObject _isUnplayable;
    private GameObject _front;
    private GameObject _back;
    // CardType(Instant/Continuous/Mix) 순서대로 인덱싱되는 배경 스프라이트. 더 이상 카드(정의)별로
    // 배경을 고르지 않고, cardType에 따라 일괄 적용한다 — 인스펙터에서 3개(Instant/Continuous/Mix
    // 순서) 모두 채워야 한다.
    [SerializeField] private Sprite[] cardTypeBackgrounds;
    private Coroutine _moveCoroutine;
    private readonly List<EffectDisplay> _effectDisplays = new List<EffectDisplay>();
    private PopupDisplay _popupDisplay;

    // SetLayer("UI")로 표시된 카드가 select된 동안에만 effect 팝업을 띄우기 위한 상태.
    // SetCardDefinition이 SetLayer/SetSelected보다 먼저 불리는 경우(DeckDisplay/RewardManager 둘 다
    // 그렇다)가 있어서, effect 목록은 일단 저장해뒀다가 RefreshPopupVisibility가 조건이 맞을 때 적용한다.
    private bool _popupTrigger;
    private bool _selected;
    private List<EffectType> _pendingEffectTypes = new List<EffectType>();

    private void Awake()
    {
        _front              = transform.Find("Front").gameObject;
        _back               = transform.Find("Back").gameObject;
        _background         = transform.Find("Front/background").GetComponent<SpriteRenderer>();
        _sprite             = transform.Find("Front/sprite").GetComponent<SpriteRenderer>();
        _spriteBackground    = transform.Find("Front/sprite/background").GetComponent<SpriteRenderer>();
        _effectArea         = transform.Find("Front/effect");
        _costText           = transform.Find("Front/cost/CostText").GetComponent<TextMeshPro>();
        _cooltimeText       = transform.Find("Front/cooltime/cooltimeText").GetComponent<TextMeshPro>();
        _selectHighlight    = transform.Find("Front/SelectHighlight").gameObject;
        _selectHighlight.SetActive(false);
        _isUnplayable       = transform.Find("Front/IsUnplayable").gameObject;
        _isUnplayable.SetActive(false);
        _popupDisplay       = transform.Find("PopUpDisplay").GetComponent<PopupDisplay>();

        // 카드 프리팹이 effect 아래에 고정 개수의 EffectDisplay 슬롯을 미리 자식으로 가지고 있다.
        _effectDisplays.AddRange(_effectArea.GetComponentsInChildren<EffectDisplay>(true));
    }

    public void SetSelected(bool selected)
    {
        _selectHighlight.SetActive(selected);
        _selected = selected;
        RefreshPopupVisibility();
    }

    public void SetFace(bool front)
    {
        _front.SetActive(front);
        _back.SetActive(!front);
    }

    // CardVisual은 CardInstance를 갖지 않는다 — 표시할 데이터는 전부 CardInstance가 밀어넣어 준다.
    // 여기서는 카드의 정적인 부분(그림)만 다룬다 — cost/cooldown/effect는 CardInstance가 상황에 따라
    // (최초 표시 시 기본값, 매 턴 종료 시 버프 반영값) 별도로 SetCostText/SetCooldownText/
    // RefreshEffectDisplays를 통해 갱신한다.
    public void SetCardDefinition(CardDefinition def)
    {
        ApplyCardTypeBackground(def.GetCardType());
        _sprite.sprite = def.GetSprite();

        CardEffect[] effects = def.GetEffects();
        _pendingEffectTypes = new List<EffectType>(effects.Length);
        foreach (CardEffect cardEffect in effects)
            _pendingEffectTypes.Add(cardEffect.GetEffect().GetEffectType());

        RefreshPopupVisibility();
    }

    private void ApplyCardTypeBackground(CardType cardType)
    {
        int index = (int)cardType;
        if (cardTypeBackgrounds == null || index < 0 || index >= cardTypeBackgrounds.Length) return;

        Sprite background = cardTypeBackgrounds[index];
        if (background != null) _spriteBackground.sprite = background;
    }

    // 현재 코스트로는 낼 수 없는 카드임을 나타내는 오버레이. 기본은 꺼져 있고,
    // CardInstance.RefreshDisplay가 매 턴 시작/종료마다 preview cost를 다시 계산해 갱신한다.
    public void SetUnplayable(bool unplayable) => _isUnplayable.SetActive(unplayable);

    public void SetCostText(string text) => _costText.text = text;

    public void SetCooldownText(string text) => _cooltimeText.text = text;

    // effect 아래에 미리 배치되어 있는 고정 개수의 EffectDisplay 슬롯 내용물(아이콘+수치)만 갈아 끼운다.
    // cardEffects는 이미 최종 계산이 끝난(버프 반영 여부와 무관하게 GetMagnitude()가 바로 표시값인) 목록이다.
    // cardEffects보다 슬롯이 남으면 해당 슬롯은 비워둔다.
    public void RefreshEffectDisplays(List<CardEffect> cardEffects)
    {
        for (int i = 0; i < _effectDisplays.Count; i++)
        {
            if (i < cardEffects.Count)
            {
                CardEffect cardEffect = cardEffects[i];
                int magnitude = cardEffect.GetMagnitude();
                _effectDisplays[i].SetEffect(cardEffect.GetEffect(), magnitude <= -1 ? "" : magnitude.ToString());
            }
            else
            {
                _effectDisplays[i].Clear();
            }
        }
    }

    public Vector2 GetBackgroundSize() => _background.bounds.size;

    // 자식에 있는 모든 Renderer(SpriteRenderer, TextMeshPro 내부 MeshRenderer 등)의 sortingLayer를
    // 한 번에 옮긴다. 자주 호출되는 경로가 아니라 필드별로 캐싱하지 않고 그때그때 순회한다.
    public void SetLayer(string sortingLayerName)
    {
        _popupTrigger = sortingLayerName == "UI";
        RefreshPopupVisibility();

        int sortingLayerID = SortingLayer.NameToID(sortingLayerName);
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            renderer.sortingLayerID = sortingLayerID;
    }

    // UI 레이어 카드이면서(trigger) 동시에 select된(_selected) 동안에만 팝업을 보여준다.
    // 셋 중 하나라도 바뀌는 지점(SetLayer/SetSelected/SetCardDefinition)에서 공통으로 호출한다.
    private void RefreshPopupVisibility()
    {
        _popupDisplay.SetEffects(_popupTrigger && _selected ? _pendingEffectTypes : null);
    }

    // 카드 배경(SpriteRenderer)의 월드 크기가 targetSize가 되도록 균등하지 않게(가로/세로 개별) 스케일한다.
    public void SetSize(Vector2 targetSize)
    {
        Vector2 currentSize = _background.bounds.size;
        if (currentSize.x == 0f || currentSize.y == 0f) return;

        transform.localScale = new Vector3(
            transform.localScale.x * targetSize.x / currentSize.x,
            transform.localScale.y * targetSize.y / currentSize.y,
            transform.localScale.z
        );
    }

    public void MoveTo(Vector3 targetPosition, float duration = 0.3f)
    {
        if (_moveCoroutine != null)
            StopCoroutine(_moveCoroutine);
        _moveCoroutine = StartCoroutine(MoveRoutine(targetPosition, duration));
    }

    private IEnumerator MoveRoutine(Vector3 targetPosition, float duration)
    {
        Vector3 startPosition = transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            yield return null;
        }

        transform.position = targetPosition;
        _moveCoroutine = null;
    }
}
