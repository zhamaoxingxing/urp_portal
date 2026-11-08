using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// 【核心架构 2：执行层】
// 这个脚本是真正的“底层打工人”，它负责向显卡提交 CommandBuffer（指令），执行全屏 Blit（画面拷贝）。
public class WarpEffectPass : ScriptableRenderPass
{
    private Material material;
    private float intensity;
    private float aberration;

    private RenderTargetIdentifier source; // 记录当前摄像机画好的原图
    private RenderTargetHandle tempTexture; // 申请的一块临时显存画布

    // 构造函数：在 Feature 创建时调用
    public WarpEffectPass(Material mat)
    {
        this.material = mat;
        // 【渲染时机】：
        // BeforeRenderingPostProcessing 代表在 URP 的 Bloom、色调映射等后处理之前执行。
        this.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        // 初始化临时画布的名字
        tempTexture.Init("_TempWarpTexture");
    }

    // 【数据接收接口】：由 Feature 调度层将 Volume 里的参数塞进来
    public void Setup(float intensity, float aberration)
    {
        this.intensity = intensity;
        this.aberration = aberration;
    }

    // 在每次渲染摄像机之前调用，用于获取当前摄像机的颜色缓冲（屏幕画面）
    public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
    {
        source = renderingData.cameraData.renderer.cameraColorTarget;
    }

    // 【最核心的方法】：这里才是真正执行全屏渲染的地方
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (material == null) return;

        // 1. 从对象池拿一个 CommandBuffer，避免 new 产生 GC 垃圾
        CommandBuffer cmd = CommandBufferPool.Get("WarpEffectPass");

        // 2. 把当前参数传给 Shader
        material.SetFloat("_Intensity", intensity);
        material.SetFloat("_Aberration", aberration);

        // 3. 描述临时画布的大小和格式（和当前屏幕一致，去掉深度缓冲省性能）
        RenderTextureDescriptor opaqueDesc = renderingData.cameraData.cameraTargetDescriptor;
        opaqueDesc.depthBufferBits = 0;

        // 4. 向显存申请一块临时画布
        cmd.GetTemporaryRT(tempTexture.id, opaqueDesc);

        // 5. 【双缓冲 Blit（重点）】：
        // 因为 GPU 不能同时读取和写入同一张图，所以必须先把 原图(source) 经过材质处理，写到 临时画布(tempTexture)，
        // 然后再把 临时画布 画回 原图(source)。相当于“左手倒右手”。
        Blit(cmd, source, tempTexture.Identifier(), material, 0);
        Blit(cmd, tempTexture.Identifier(), source);

        // 6. 执行指令，并释放 CommandBuffer
        context.ExecuteCommandBuffer(cmd);
        CommandBufferPool.Release(cmd);
    }

    // 渲染结束时调用，释放临时显存，防止内存泄漏
    public override void OnCameraCleanup(CommandBuffer cmd)
    {
        cmd.ReleaseTemporaryRT(tempTexture.id);
    }
}