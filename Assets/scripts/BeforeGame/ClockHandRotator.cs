using UnityEngine;

// BeforeGame 타이틀 화면의 Lines/Tick(시침)·Tock(분침)을 시계 바늘처럼 계속 회전시킨다.
// 실시간 1초 = 시계상 10분으로 흐른다(분침 한 바퀴 = 60분 = 실시간 6초).
public class ClockHandRotator : MonoBehaviour
{
    [SerializeField] private RectTransform hourHand;   // Tick
    [SerializeField] private RectTransform minuteHand; // Tock

    private const float ClockMinutesPerRealSecond = 10f;

    // 분침: 60분에 360도 = 분당 6도. 시침: 12시간(720분)에 360도 = 분당 0.5도.
    private const float MinuteHandDegreesPerClockMinute = 360f / 60f;
    private const float HourHandDegreesPerClockMinute = 360f / (12f * 60f);

    private float _clockMinutes;

    private void Update()
    {
        _clockMinutes += Time.deltaTime * ClockMinutesPerRealSecond;

        // 시계 방향 회전이라 Z축으로는 음의 방향으로 돈다.
        if (minuteHand != null)
            minuteHand.localRotation = Quaternion.Euler(0f, 0f, -_clockMinutes * MinuteHandDegreesPerClockMinute);

        if (hourHand != null)
            hourHand.localRotation = Quaternion.Euler(0f, 0f, -_clockMinutes * HourHandDegreesPerClockMinute);
    }
}
