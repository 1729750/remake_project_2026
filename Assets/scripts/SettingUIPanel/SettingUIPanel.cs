using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem; // [추가 - Left/Right 홀드 입력]

public class SettingUIPanel : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject settingPanel;


    // =========================================================
    // Background Music Bar
    // =========================================================

    [Header("Background Music")]

    [SerializeField]
    private RectTransform backgroundMusicBar;

    [SerializeField]
    private RectTransform backgroundMusicBarControlButton;


    // =========================================================
    // Background Music ControlButton Hover 이미지
    // =========================================================

    [SerializeField]
    private Image backgroundMusicControlButtonImage;

    [SerializeField]
    private Sprite backgroundMusicControlButtonNormalSprite;

    [SerializeField]
    private Sprite backgroundMusicControlButtonHoverSprite;


    // =========================================================
    // Effect Sound Bar
    // =========================================================

    [Header("Effect Sound")]

    [SerializeField]
    private RectTransform effectSoundBar;

    [SerializeField]
    private RectTransform effectSoundBarControlButton;


    // =========================================================
    // Effect Sound ControlButton Hover 이미지
    // =========================================================

    [SerializeField]
    private Image effectSoundControlButtonImage;

    [SerializeField]
    private Sprite effectSoundControlButtonNormalSprite;

    [SerializeField]
    private Sprite effectSoundControlButtonHoverSprite;


    // =========================================================
    // [추가 - Left / Right 홀드용 InputAction]
    //
    // 기존 InputManager는 건드리지 않는다.
    // 홀드 여부만 SettingUIPanel에서 직접 확인한다.
    // =========================================================

    [Header("Hold Input")]

    [SerializeField]
    private InputActionReference leftActionReference;

    [SerializeField]
    private InputActionReference rightActionReference;


    // =========================================================
    // 현재 선택된 항목
    //
    // 0 = BackgroundMusic
    // 1 = SoundEffect
    // =========================================================

    private int currentIndex = 0;


    // =========================================================
    // ControlButton Hover Scale
    // =========================================================

    private Vector3 backgroundMusicControlButtonBaseScale;
    private Vector3 effectSoundControlButtonBaseScale;

    private const float HoverScale = 1.2f;


    // =========================================================
    // [추가 - Left / Right 홀드 설정]
    // =========================================================

    // 0.7초 이상 누르면 연속 입력 시작
    private const float HoldStartTime = 0.7f;

    // 그 이후 0.5초마다 1씩 변경
    // = 초당 5
    private const float HoldRepeatInterval = 0.2f;

    private float leftHoldTime = 0f;
    private float rightHoldTime = 0f;

    private float leftRepeatTime = 0f;
    private float rightRepeatTime = 0f;

    private bool leftHoldStarted = false;
    private bool rightHoldStarted = false;


    private bool inputLoaded = false;


    private void Awake()
    {
        if (settingPanel == null)
            settingPanel = gameObject;


        // =====================================================
        // ControlButton Image 자동 연결
        // =====================================================

        if (backgroundMusicControlButtonImage == null &&
            backgroundMusicBarControlButton != null)
        {
            backgroundMusicControlButtonImage =
                backgroundMusicBarControlButton.GetComponent<Image>();
        }


        if (effectSoundControlButtonImage == null &&
            effectSoundBarControlButton != null)
        {
            effectSoundControlButtonImage =
                effectSoundBarControlButton.GetComponent<Image>();
        }


        // =====================================================
        // Hover 전 원래 Scale 저장
        // =====================================================

        if (backgroundMusicBarControlButton != null)
        {
            backgroundMusicControlButtonBaseScale =
                backgroundMusicBarControlButton.localScale;
        }


        if (effectSoundBarControlButton != null)
        {
            effectSoundControlButtonBaseScale =
                effectSoundBarControlButton.localScale;
        }
    }


    private void OnEnable()
    {
        // 설정창을 열면 BackgroundMusic부터 선택
        currentIndex = 0;


        // 현재 SoundManager 음량에 맞춰 버튼 위치 갱신
        RefreshVolumeBars();


        // BackgroundMusic 버튼 Hover
        UpdateCurrentSelection();


        // 홀드 상태 초기화
        ResetHoldInput();


        // Setting 전용 입력 등록
        LoadInput();
    }


    private void OnDisable()
    {
        // Setting 입력 제거
        // 아래 Stack의 MainMenu 입력이 다시 활성화됨
        UnloadInput();


        // 홀드 상태 초기화
        ResetHoldInput();
    }


    // =========================================================
    // [추가 - Left / Right 홀드 처리]
    //
    // 처음 누름:
    // Left  -> 즉시 -1
    // Right -> 즉시 +1
    //
    // 계속 누름:
    // 0.7초 도달 -> 한 번 더 ±1
    // 이후 0.5초마다 ±1
    // = 초당 2
    // =========================================================

    private void Update()
    {
        if (!inputLoaded)
            return;


        bool leftPressed =
            leftActionReference != null &&
            leftActionReference.action != null &&
            leftActionReference.action.IsPressed();


        bool rightPressed =
            rightActionReference != null &&
            rightActionReference.action != null &&
            rightActionReference.action.IsPressed();


        // =====================================================
        // Left 홀드
        // =====================================================

        if (leftPressed && !rightPressed)
        {
            leftHoldTime += Time.unscaledDeltaTime;


            // 0.7초가 되는 순간 바로 -1
            if (!leftHoldStarted &&
                leftHoldTime >= HoldStartTime)
            {
                leftHoldStarted = true;
                leftRepeatTime = 0f;

                ChangeCurrentVolume(-1);
            }


            // 이후 0.5초마다 -1
            if (leftHoldStarted)
            {
                leftRepeatTime += Time.unscaledDeltaTime;


                if (leftRepeatTime >= HoldRepeatInterval)
                {
                    leftRepeatTime -= HoldRepeatInterval;

                    ChangeCurrentVolume(-1);
                }
            }
        }
        else
        {
            leftHoldTime = 0f;
            leftRepeatTime = 0f;
            leftHoldStarted = false;
        }


        // =====================================================
        // Right 홀드
        // =====================================================

        if (rightPressed && !leftPressed)
        {
            rightHoldTime += Time.unscaledDeltaTime;


            // 0.7초가 되는 순간 바로 +1
            if (!rightHoldStarted &&
                rightHoldTime >= HoldStartTime)
            {
                rightHoldStarted = true;
                rightRepeatTime = 0f;

                ChangeCurrentVolume(1);
            }


            // 이후 0.5초마다 +1
            if (rightHoldStarted)
            {
                rightRepeatTime += Time.unscaledDeltaTime;


                if (rightRepeatTime >= HoldRepeatInterval)
                {
                    rightRepeatTime -= HoldRepeatInterval;

                    ChangeCurrentVolume(1);
                }
            }
        }
        else
        {
            rightHoldTime = 0f;
            rightRepeatTime = 0f;
            rightHoldStarted = false;
        }
    }


    // =========================================================
    // [추가 - 홀드 상태 초기화]
    // =========================================================

    private void ResetHoldInput()
    {
        leftHoldTime = 0f;
        rightHoldTime = 0f;

        leftRepeatTime = 0f;
        rightRepeatTime = 0f;

        leftHoldStarted = false;
        rightHoldStarted = false;
    }


    // =========================================================
    // Input 등록
    // 기존 InputManager 구조 그대로 사용
    // =========================================================

    private void LoadInput()
    {
        if (inputLoaded)
            return;


        if (PlayerInputManager.Instance == null)
        {
            Debug.LogError(
                "[SettingUIPanel] PlayerInputManager가 존재하지 않습니다."
            );

            return;
        }


        Dictionary<string, Action> bindings =
            new Dictionary<string, Action>
            {
                { "Up", OnInputUp },
                { "Down", OnInputDown },
                { "Left", OnInputLeft },
                { "Right", OnInputRight },
                { "Cancel", CloseSettingPanel }
            };


        PlayerInputManager.Instance.Load(
            "Select",
            bindings
        );


        inputLoaded = true;
    }


    private void UnloadInput()
    {
        if (!inputLoaded)
            return;


        if (PlayerInputManager.Instance != null)
        {
            PlayerInputManager.Instance.Unload();
        }


        inputLoaded = false;
    }


    // =========================================================
    // Select / Up
    //
    // 0 = BackgroundMusic
    // 1 = SoundEffect
    // =========================================================

    private void OnInputUp()
    {
        currentIndex--;


        if (currentIndex < 0)
            currentIndex = 1;


        UpdateCurrentSelection();
    }


    // =========================================================
    // Select / Down
    // =========================================================

    private void OnInputDown()
    {
        currentIndex++;


        if (currentIndex > 1)
            currentIndex = 0;


        UpdateCurrentSelection();
    }


    // =========================================================
    // Select / Left
    //
    // 처음 누른 순간 즉시 -1
    // =========================================================

    private void OnInputLeft()
    {
        ChangeCurrentVolume(-1);
    }


    // =========================================================
    // Select / Right
    //
    // 처음 누른 순간 즉시 +1
    // =========================================================

    private void OnInputRight()
    {
        ChangeCurrentVolume(1);
    }


    // =========================================================
    // 현재 선택된 항목의 음량 조절
    // =========================================================

    private void ChangeCurrentVolume(int amount)
    {
        if (SoundManager.Instance == null)
            return;


        // =====================================================
        // index 0
        // Background Music
        // =====================================================

        if (currentIndex == 0)
        {
            int currentVolume =
                SoundManager.Instance.GetBgmVolume();


            int newVolume =
                Mathf.Clamp(
                    currentVolume + amount,
                    0,
                    30
                );


            SoundManager.Instance.SetBgmVolume(
                newVolume
            );


            UpdateBarPosition(
                backgroundMusicBar,
                backgroundMusicBarControlButton,
                newVolume
            );
        }


        // =====================================================
        // index 1
        // Effect Sound
        // =====================================================

        else if (currentIndex == 1)
        {
            int currentVolume =
                SoundManager.Instance.GetEffectVolume();


            int newVolume =
                Mathf.Clamp(
                    currentVolume + amount,
                    0,
                    30
                );


            SoundManager.Instance.SetEffectVolume(
                newVolume
            );


            UpdateBarPosition(
                effectSoundBar,
                effectSoundBarControlButton,
                newVolume
            );
        }
    }


    // =========================================================
    // BarControlButton 위치 계산
    //
    // 0  -> Bar 제일 왼쪽
    // 15 -> Bar 가운데
    // 30 -> Bar 제일 오른쪽
    //
    // ControlButton은 해당 Bar의 자식이어야 한다.
    // =========================================================

    private void UpdateBarPosition(
        RectTransform bar,
        RectTransform controlButton,
        int volume)
    {
        if (bar == null || controlButton == null)
            return;


        volume = Mathf.Clamp(
            volume,
            0,
            30
        );


        // 0 ~ 30
        // ↓
        // 0.0 ~ 1.0
        float normalized =
            volume / 30f;


        // =====================================================
        // ControlButton의 Anchor 자체를
        // Bar 왼쪽 0 ~ 오른쪽 1 사이로 이동
        //
        // 이 방식이 UI RectTransform에서 가장 안정적이다.
        // =====================================================

        Vector2 anchorMin =
            controlButton.anchorMin;

        Vector2 anchorMax =
            controlButton.anchorMax;


        anchorMin.x = normalized;
        anchorMax.x = normalized;


        controlButton.anchorMin =
            anchorMin;

        controlButton.anchorMax =
            anchorMax;


        // Anchor 위치에 버튼 중심을 맞춘다.
        Vector2 position =
            controlButton.anchoredPosition;


        position.x = 0f;


        controlButton.anchoredPosition =
            position;
    }


    // =========================================================
    // SoundManager의 현재 값을
    // ControlButton 위치에 반영
    // =========================================================

    private void RefreshVolumeBars()
    {
        if (SoundManager.Instance == null)
            return;


        UpdateBarPosition(
            backgroundMusicBar,
            backgroundMusicBarControlButton,
            SoundManager.Instance.GetBgmVolume()
        );


        UpdateBarPosition(
            effectSoundBar,
            effectSoundBarControlButton,
            SoundManager.Instance.GetEffectVolume()
        );
    }


    // =========================================================
    // 현재 선택된 ControlButton 상태
    //
    // 선택됨:
    // Hover Sprite
    // X / Y Scale = 기존의 1.2배
    //
    // 선택 안 됨:
    // Normal Sprite
    // 원래 Scale
    // =========================================================

    private void UpdateCurrentSelection()
    {
        // =====================================================
        // BackgroundMusic ControlButton
        // =====================================================

        if (currentIndex == 0)
        {
            // Hover Sprite
            if (backgroundMusicControlButtonImage != null &&
                backgroundMusicControlButtonHoverSprite != null)
            {
                backgroundMusicControlButtonImage.sprite =
                    backgroundMusicControlButtonHoverSprite;
            }


            // X / Y만 1.2배
            if (backgroundMusicBarControlButton != null)
            {
                backgroundMusicBarControlButton.localScale =
                    new Vector3(
                        backgroundMusicControlButtonBaseScale.x * HoverScale,
                        backgroundMusicControlButtonBaseScale.y * HoverScale,
                        backgroundMusicControlButtonBaseScale.z
                    );
            }
        }
        else
        {
            // Normal Sprite
            if (backgroundMusicControlButtonImage != null &&
                backgroundMusicControlButtonNormalSprite != null)
            {
                backgroundMusicControlButtonImage.sprite =
                    backgroundMusicControlButtonNormalSprite;
            }


            // 원래 Scale
            if (backgroundMusicBarControlButton != null)
            {
                backgroundMusicBarControlButton.localScale =
                    backgroundMusicControlButtonBaseScale;
            }
        }


        // =====================================================
        // EffectSound ControlButton
        // =====================================================

        if (currentIndex == 1)
        {
            // Hover Sprite
            if (effectSoundControlButtonImage != null &&
                effectSoundControlButtonHoverSprite != null)
            {
                effectSoundControlButtonImage.sprite =
                    effectSoundControlButtonHoverSprite;
            }


            // X / Y만 1.2배
            if (effectSoundBarControlButton != null)
            {
                effectSoundBarControlButton.localScale =
                    new Vector3(
                        effectSoundControlButtonBaseScale.x * HoverScale,
                        effectSoundControlButtonBaseScale.y * HoverScale,
                        effectSoundControlButtonBaseScale.z
                    );
            }
        }
        else
        {
            // Normal Sprite
            if (effectSoundControlButtonImage != null &&
                effectSoundControlButtonNormalSprite != null)
            {
                effectSoundControlButtonImage.sprite =
                    effectSoundControlButtonNormalSprite;
            }


            // 원래 Scale
            if (effectSoundBarControlButton != null)
            {
                effectSoundBarControlButton.localScale =
                    effectSoundControlButtonBaseScale;
            }
        }
    }


    // =========================================================
    // 마우스 - BackgroundMusic Bar 클릭 / 드래그
    // =========================================================

    public void OnBackgroundBarPointer(
        BaseEventData data)
    {
        PointerEventData pointerData =
            data as PointerEventData;


        if (pointerData == null)
            return;


        SetVolumeFromMouse(
            backgroundMusicBar,
            true,
            pointerData
        );
    }


    // =========================================================
    // 마우스 - EffectSound Bar 클릭 / 드래그
    // =========================================================

    public void OnEffectBarPointer(
        BaseEventData data)
    {
        PointerEventData pointerData =
            data as PointerEventData;


        if (pointerData == null)
            return;


        SetVolumeFromMouse(
            effectSoundBar,
            false,
            pointerData
        );
    }


    // =========================================================
    // 마우스 위치를 0~30 음량으로 변환
    // =========================================================

    private void SetVolumeFromMouse(
        RectTransform bar,
        bool isBgm,
        PointerEventData pointerData)
    {
        if (bar == null ||
            SoundManager.Instance == null)
            return;


        Vector2 localPoint;


        bool success =
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                bar,
                pointerData.position,
                pointerData.pressEventCamera,
                out localPoint
            );


        if (!success)
            return;


        float left =
            bar.rect.xMin;

        float right =
            bar.rect.xMax;


        float normalized =
            Mathf.InverseLerp(
                left,
                right,
                localPoint.x
            );


        // 가장 가까운 0~30 정수 위치
        int volume =
            Mathf.RoundToInt(
                normalized * 30f
            );


        volume =
            Mathf.Clamp(
                volume,
                0,
                30
            );


        // =====================================================
        // Background Music
        // =====================================================

        if (isBgm)
        {
            // BGM을 클릭했으므로
            // 선택 index도 0
            currentIndex = 0;


            SoundManager.Instance.SetBgmVolume(
                volume
            );


            UpdateBarPosition(
                backgroundMusicBar,
                backgroundMusicBarControlButton,
                volume
            );
        }


        // =====================================================
        // Effect Sound
        // =====================================================

        else
        {
            // Effect를 클릭했으므로
            // 선택 index도 1
            currentIndex = 1;


            SoundManager.Instance.SetEffectVolume(
                volume
            );


            UpdateBarPosition(
                effectSoundBar,
                effectSoundBarControlButton,
                volume
            );
        }


        // 마우스로 선택한 버튼 Hover
        UpdateCurrentSelection();
    }


    // =========================================================
    // Select / Cancel
    //
    // SettingPanel 비활성화
    // =========================================================

    public void CloseSettingPanel()
    {
        if (settingPanel != null)
        {
            settingPanel.SetActive(false);
        }
    }
}