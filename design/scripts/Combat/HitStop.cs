using UnityEngine;

/// <summary>
/// 打击停顿（Hit Stop）效果。受击时短暂暂停游戏时间。
/// 提升打击感的核心手段之一。
/// </summary>
public class HitStop : MonoBehaviour
{
    private static HitStop instance;

    [SerializeField] private float defaultStopDuration = 0.1f;

    private void Awake()
    {
        instance = this;
    }

    /// <summary>
    /// 触发打击停顿
    /// </summary>
    public static void Stop(float duration = -1f)
    {
        if (instance == null) return;
        float d = duration > 0 ? duration : instance.defaultStopDuration;
        instance.StartCoroutine(StopCoroutine(d));
    }

    private static System.Collections.IEnumerator StopCoroutine(float duration)
    {
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
    }
}
