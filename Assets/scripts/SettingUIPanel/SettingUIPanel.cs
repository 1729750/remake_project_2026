using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
    // [변경 - Background Music ControlButton Hover 이미지]
    // Bar 자체가 아니라 움직이는 ControlButton의 이미지를 변경한다.
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
    // [변경 - Effect Sound ControlButton Hover 이미지]
    // Bar 자체가 아니라 움직이는 ControlButton의 이미지를 변경한다.
    // =========================================================

    [SerializeField]
    private Image effectSoundControlButtonImage;

    [SerializeField]
    private Sprite effectSoundControlButtonNormalSprite;

    [SerializeField]
    private Sprite effectSoundControlButtonHoverSprite;


    // =========================================================
    // 현재 선택된 항목
    //
    // 0 = BackgroundMusic
    // 1 = SoundEffect
    // =========================================================

    private int currentIndex = 0;

    private bool inputLoaded = false;


    private void Awake()
    {
        if (settingPanel == null)
            settingPanel = gameObject;


        // =====================================================
        // [추가 - ControlButton Image 자동 연결]
        //
        // Inspector에서 Image를 따로 넣지 않았더라도
        // ControlButton 오브젝트에 Image 컴포넌트가 있으면 자동으로 가져온다.
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
    }


    private void OnEnable()
    {
        // 설정창을 열었을 때 첫 선택은 BackgroundMusic
        currentIndex = 0;


        // 현재 SoundManager의 음량에 맞춰
        // ControlButton 위치 갱신
        RefreshVolumeBars();


        // [변경 - ControlButton Hover]
        // index 0이므로 처음에는 BackgroundMusic 버튼이 Hover
        UpdateCurrentSelection();


        // Setting 전용 입력 등록
        LoadInput();
    }


    private void OnDisable()
    {
        // Setting 입력 제거
        // 아래에 있던 MainMenu 입력이 다시 활성화됨
        UnloadInput();
    }


    // =========================================================
    // Input 등록
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
    // 위 항목으로 이동
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
    //
    // 아래 항목으로 이동
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
    // 현재 선택된 음량 -1
    // =========================================================

    private void OnInputLeft()
    {
        ChangeCurrentVolume(-1);
    }


    // =========================================================
    // Select / Right
    //
    // 현재 선택된 음량 +1
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


        // -----------------------------------------------------
        // index 0
        // Background Music
        // -----------------------------------------------------

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


        // -----------------------------------------------------
        // index 1
        // Effect Sound
        // -----------------------------------------------------

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
    // Bar를 30등분해서 ControlButton을 이동시킨다.
    // =========================================================

    private void UpdateBarPosition(
        RectTransform bar,
        RectTransform controlButton,
        int volume)
    {
        if (bar == null || controlButton == null)
            return;


        volume = Mathf.Clamp(volume, 0, 30);


        // 0 ~ 30
        // ↓
        // 0.0 ~ 1.0
        float normalized =
            volume / 30f;


        // Bar의 실제 왼쪽 위치
        float left =
            -bar.rect.width * bar.pivot.x;


        // Bar의 실제 오른쪽 위치
        float right =
            bar.rect.width * (1f - bar.pivot.x);


        // 현재 음량에 해당하는 위치
        float targetX =
            Mathf.Lerp(
                left,
                right,
                normalized
            );


        Vector2 position =
            controlButton.anchoredPosition;


        position.x = targetX;


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
    // [변경 - ControlButton Hover]
    //
    // currentIndex == 0
    // BackgroundMusic ControlButton = Hover
    // Effect ControlButton          = Normal
    //
    // currentIndex == 1
    // BackgroundMusic ControlButton = Normal
    // Effect ControlButton          = Hover
    // =========================================================

    private void UpdateCurrentSelection()
    {
        // -----------------------------------------------------
        // BackgroundMusic ControlButton
        // -----------------------------------------------------

        if (backgroundMusicControlButtonImage != null)
        {
            if (currentIndex == 0)
            {
                if (backgroundMusicControlButtonHoverSprite != null)
                {
                    backgroundMusicControlButtonImage.sprite =
                        backgroundMusicControlButtonHoverSprite;
                }
            }
            else
            {
                if (backgroundMusicControlButtonNormalSprite != null)
                {
                    backgroundMusicControlButtonImage.sprite =
                        backgroundMusicControlButtonNormalSprite;
                }
            }
        }


        // -----------------------------------------------------
        // EffectSound ControlButton
        // -----------------------------------------------------

        if (effectSoundControlButtonImage != null)
        {
            if (currentIndex == 1)
            {
                if (effectSoundControlButtonHoverSprite != null)
                {
                    effectSoundControlButtonImage.sprite =
                        effectSoundControlButtonHoverSprite;
                }
            }
            else
            {
                if (effectSoundControlButtonNormalSprite != null)
                {
                    effectSoundControlButtonImage.sprite =
                        effectSoundControlButtonNormalSprite;
                }
            }
        }
    }


    // =========================================================
    // 마우스 - BackgroundMusic Bar 클릭 / 드래그
    // =========================================================

    public void OnBackgroundBarPointer(BaseEventData data)
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

    public void OnEffectBarPointer(BaseEventData data)
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
            -bar.rect.width * bar.pivot.x;


        float right =
            bar.rect.width * (1f - bar.pivot.x);


        float normalized =
            Mathf.InverseLerp(
                left,
                right,
                localPoint.x
            );


        // 마우스 위치를
        // 가장 가까운 0~30 정수 위치로 변환
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


        // -----------------------------------------------------
        // Background Music
        // -----------------------------------------------------

        if (isBgm)
        {
            // 마우스로 BGM Bar를 선택했으므로
            // currentIndex도 0으로 변경
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


        // -----------------------------------------------------
        // Effect Sound
        // -----------------------------------------------------

        else
        {
            // 마우스로 Effect Bar를 선택했으므로
            // currentIndex도 1로 변경
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


        // 마우스로 선택한 ControlButton도
        // Hover Sprite로 변경
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