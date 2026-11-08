using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal; // 需要引入 URP 命名空间
using System.Collections;

public class PlayerController : PortalableObject
{
    private CameraMove cameraMove;

    // 【新增】：缓存后处理 Volume 组件
    private WarpEffectVolume warpVolume;

    protected override void Awake()
    {
        base.Awake();
        cameraMove = GetComponent<CameraMove>();
    }

    private void Start()
    {
        // 【新增】：游戏开始时，找到场景里的 Volume，并提取出我们自定义的 WarpEffectVolume
        Volume volume = FindObjectOfType<Volume>();
        if (volume != null && volume.profile != null)
        {
            // TryGet 会自动去 Profile 里找有没有这个类型的后处理
            volume.profile.TryGet(out warpVolume);
        }
    }

    public override void Warp()
    {
        base.Warp(); // 先执行父类的位置转换逻辑

        cameraMove.ResetTargetRotation(); // 重置目标视角

        // 强行同步摄像机 Transform
        Camera cam = Camera.main;
        if (cam != null)
        {
            cam.transform.position = transform.position;
            cam.transform.rotation = transform.rotation;
        }

        // =========================================================
        // 【核心新增】：触发穿门后处理冲击
        // =========================================================
        if (warpVolume != null)
        {
            // 1. 瞬间把扭曲和色差拉满（你可以根据喜好调整 0.5f 和 1.0f）
            warpVolume.distortionIntensity.value = 0.4f;
            warpVolume.chromaticAberration.value = 0.2f;

            // 2. 启动平滑衰减协程
            StopAllCoroutines();
            StartCoroutine(FadeOutEffect());
        }
    }
    private IEnumerator FadeOutEffect()
    {
        float duration = 0.5f; // 消退持续 0.5 秒
        float elapsed = 0f;    // 计时器

        // 记录初始值
        float startDistort = warpVolume.distortionIntensity.value;
        float startAberration = warpVolume.chromaticAberration.value;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime; // 累加时间
            float t = elapsed / duration; // 计算进度 (0 到 1)

            // 平滑插值，降回 0
            warpVolume.distortionIntensity.value = Mathf.Lerp(startDistort, 0f, t);
            warpVolume.chromaticAberration.value = Mathf.Lerp(startAberration, 0f, t);

            yield return null; // 等待下一帧继续
        }

        // 确保最终归零
        warpVolume.distortionIntensity.value = 0f;
        warpVolume.chromaticAberration.value = 0f;
    }
    // 恢复后处理参数的辅助方法
    private void ResetWarpEffect()
    {
        if (warpVolume != null)
        {
            warpVolume.distortionIntensity.value = 0f;
            warpVolume.chromaticAberration.value = 0f;
        }
    }
}