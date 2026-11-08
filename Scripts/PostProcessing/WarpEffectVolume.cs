using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 【核心架构 1：数据层】
// 这个脚本不负责画图，只负责在 Unity 的 Volume 面板里“长”出滑条，供美术和程序调参。
// [System.Serializable] 保证它能在 Inspector 面板中被序列化显示。
// [VolumeComponentMenu] 决定了它在面板 Add Override 菜单里的路径。
[System.Serializable, VolumeComponentMenu("Custom/Warp Effect")]
public class WarpEffectVolume : VolumeComponent, IPostProcessComponent
{
    // 【ClampedFloatParameter】带限位的浮点数滑条。
    // (默认值, 最小值, 最大值)。如果不用这个包装，普通的 float 无法参与 Volume 的权重混合与平滑过渡。
    public ClampedFloatParameter distortionIntensity = new ClampedFloatParameter(0f, 0f, 1f);
    public ClampedFloatParameter chromaticAberration = new ClampedFloatParameter(0f, 0f, 1f);

    // 【IPostProcessComponent 接口实现】
    // IsActive() 是给 URP 引擎看的“通行证”。如果这里返回 false（比如数值都是0），
    // 引擎在调度时就会彻底忽略这个后处理，从而做到“零开销”。
    public bool IsActive() => distortionIntensity.value > 0f || chromaticAberration.value > 0f;

    // 用于 VR 或分屏渲染，一般填 false 即可。
    public bool IsTileCompatible() => false;
}