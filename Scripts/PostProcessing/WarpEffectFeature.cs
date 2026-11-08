using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 【核心架构 3：调度层】
// 它是一个“大管家”，负责挂载到 URP Renderer 资产上，读取 Volume 数据，并决定什么时候把 Pass 塞进渲染队列。
public class WarpEffectFeature : ScriptableRendererFeature
{
    // 材质槽：你需要在 Unity 的 Inspector 面板里，把写好的 WarpEffect 材质拖到这里
    public Material warpMaterial;
    private WarpEffectPass m_Pass;

    // Feature 被创建（或修改属性）时调用，用于初始化 Pass
    public override void Create()
    {
        m_Pass = new WarpEffectPass(warpMaterial);
    }

    // 【每帧必调】：决定这一帧要不要执行特效
    private bool wasActiveLastFrame = false;

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        
        var volume = VolumeManager.instance.stack.GetComponent<WarpEffectVolume>();

        // 检查是否激活
        bool isCurrentlyActive = (volume != null && volume.IsActive());

        // 状态刚刚改变时，才打印一次日志
        if (isCurrentlyActive != wasActiveLastFrame)
        {
            wasActiveLastFrame = isCurrentlyActive;
            Debug.Log(isCurrentlyActive ? "【后处理】开启！" : "【后处理】关闭。");
        }

        // 如果没激活，直接退出
        if (!isCurrentlyActive) return;

        // 把参数传给 Pass
        m_Pass.Setup(volume.distortionIntensity.value, volume.chromaticAberration.value);
        renderer.EnqueuePass(m_Pass);
    }
}