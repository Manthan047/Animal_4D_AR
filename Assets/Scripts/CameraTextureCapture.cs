using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if UNITY_6000_0_OR_NEWER
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
#endif

/// <summary>
/// Custom URP Render Pass that captures the camera color target (Vuforia background + 3D dinosaur augmentations)
/// at RenderPassEvent.AfterRenderingTransparents, BEFORE any ScreenSpace-Overlay UI or UICameras are rendered.
/// </summary>
public class ARVideoCapturePass : ScriptableRenderPass
{
    private Camera targetCamera;
    private RTHandle destinationHandle;
    private bool shouldCapture = false;
    private bool hasCapturedThisFrame = false;

    public bool HasCapturedFrame => hasCapturedThisFrame;

    public ARVideoCapturePass()
    {
        // Capture after 3D augmentations finish, but before UI Canvas (Screen Space Overlay) renders!
        renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        requiresIntermediateTexture = true;
    }

    public void Setup(Camera camera, RTHandle destHandle, bool capture)
    {
        targetCamera = camera;
        destinationHandle = destHandle;
        shouldCapture = capture;
    }

    public void ResetFrameFlag()
    {
        hasCapturedThisFrame = false;
    }

#if UNITY_6000_0_OR_NEWER
    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        if (!shouldCapture || destinationHandle == null) return;

        UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
        if (targetCamera != null && cameraData.camera != targetCamera) return;

        UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
        TextureHandle srcColor = resourceData.activeColorTexture;

        if (!srcColor.IsValid()) return;

        TextureHandle dstHandle = renderGraph.ImportTexture(destinationHandle);
        if (!dstHandle.IsValid()) return;

        renderGraph.AddBlitPass(srcColor, dstHandle, Vector2.one, Vector2.zero);
        hasCapturedThisFrame = true;
    }
#endif

#if !UNITY_6000_0_OR_NEWER
    [Obsolete]
    public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
    {
        if (!shouldCapture || destinationHandle == null) return;
        if (targetCamera != null && renderingData.cameraData.camera != targetCamera) return;

        RTHandle srcColor = renderingData.cameraData.renderer.cameraColorTargetHandle;
        if (srcColor != null)
        {
            CommandBuffer cmd = CommandBufferPool.Get("ARVideoCapturePass");
            Blitter.BlitCameraTexture(cmd, srcColor, destinationHandle);
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
            hasCapturedThisFrame = true;
        }
    }
#endif
}

/// <summary>
/// Captures the complete full-screen AR camera view (live camera background + 3D dinosaur augmentations)
/// without UI buttons or logos, scales it cleanly via GPU to the target video resolution,
/// and feeds raw RGBA frames to the native Android hardware encoder.
/// </summary>
public class CameraTextureCapture : MonoBehaviour
{
    [Header("Resolution & Frame Rate")]
    [Tooltip("Target capture width (aligned to multiple of 16 for H.264). 720p gives crisp, sharp video.")]
    [SerializeField] private int baseWidth = 720;

    [Tooltip("Target recording frame rate.")]
    [SerializeField] private int targetFps = 30;

    [Header("Camera Reference")]
    [Tooltip("ARCamera rendering the scene. Auto-detected if null.")]
    [SerializeField] private Camera targetCamera;

    private bool isCapturing = false;
    private int captureWidth;
    private int captureHeight;
    private float frameInterval;
    private long startTimestampUs;
    private Coroutine captureCoroutine;

    // Render pass & textures
    private ARVideoCapturePass videoCapturePass;
    private RenderTexture fullScreenRT;
    private RTHandle fullScreenRTHandle;
    private RenderTexture captureRT;
    private Texture2D readTex;

#if !UNITY_EDITOR && UNITY_ANDROID
    private AndroidJavaClass recorderClass;
#endif

    public bool IsCapturing => isCapturing;
    public int CaptureWidth => captureWidth;
    public int CaptureHeight => captureHeight;
    public int TargetFps => targetFps;

    private void Awake()
    {
        ResolveCamera();
        CalculateDimensions();
        videoCapturePass = new ARVideoCapturePass();

#if !UNITY_EDITOR && UNITY_ANDROID
        recorderClass = new AndroidJavaClass("com.animal4d.recorder.CameraVideoRecorder");
#endif
    }

    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
    }

    private void OnDestroy()
    {
        StopCapture();
        ReleaseTextures();
    }

    private void ResolveCamera()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }
        if (targetCamera == null)
        {
            var arObj = GameObject.Find("ARCamera");
            if (arObj != null) targetCamera = arObj.GetComponent<Camera>();
        }
        if (targetCamera == null)
        {
            targetCamera = Camera.main ?? FindAnyObjectByType<Camera>();
        }
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        if (targetCamera == null) ResolveCamera();
        if (camera != targetCamera) return;

        if (isCapturing && videoCapturePass != null && fullScreenRTHandle != null)
        {
            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            if (cameraData == null) cameraData = camera.GetComponent<UniversalAdditionalCameraData>();

            if (cameraData != null && cameraData.scriptableRenderer != null)
            {
                videoCapturePass.Setup(targetCamera, fullScreenRTHandle, true);
                cameraData.scriptableRenderer.EnqueuePass(videoCapturePass);
            }
        }
    }

    public void CalculateDimensions()
    {
        int screenW = Mathf.Max(16, Screen.width);
        int screenH = Mathf.Max(16, Screen.height);

        int w = Mathf.Min(baseWidth, screenW);
        w = Mathf.Max(16, (w / 16) * 16);

        int h = Mathf.RoundToInt((float)w * screenH / screenW);
        h = Mathf.Max(16, (h / 16) * 16);

        captureWidth = w;
        captureHeight = h;
        frameInterval = 1f / targetFps;

        Debug.Log($"[CameraTextureCapture] Full Screen: {screenW}x{screenH} -> Full Video Target: {captureWidth}x{captureHeight} @ {targetFps}fps");
    }

    public void StartCapture(int width = 0, int height = 0, int fps = 30)
    {
        if (isCapturing) return;

        ResolveCamera();
        CalculateDimensions();
        if (width > 0 && height > 0)
        {
            captureWidth = (width / 16) * 16;
            captureHeight = (height / 16) * 16;
        }

        if (fps > 0) targetFps = fps;
        frameInterval = 1f / targetFps;

        AllocateTextures();

        isCapturing = true;
        startTimestampUs = (long)(Time.realtimeSinceStartup * 1_000_000L);

        if (captureCoroutine != null)
        {
            StopCoroutine(captureCoroutine);
        }
        captureCoroutine = StartCoroutine(CaptureLoop());

        Debug.Log($"[CameraTextureCapture] Capture started: {captureWidth}x{captureHeight} @ {targetFps}fps (UI exclusion active)");
    }

    public void StopCapture()
    {
        if (!isCapturing) return;

        isCapturing = false;

        if (captureCoroutine != null)
        {
            StopCoroutine(captureCoroutine);
            captureCoroutine = null;
        }

        if (videoCapturePass != null)
        {
            videoCapturePass.Setup(null, null, false);
            videoCapturePass.ResetFrameFlag();
        }

        ReleaseTextures();
        Debug.Log("[CameraTextureCapture] Capture stopped.");
    }

    private void AllocateTextures()
    {
        ReleaseTextures();

        // 1. Full-screen buffer receiving clean AR camera view (without UI)
        fullScreenRT = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32)
        {
            filterMode = FilterMode.Bilinear,
            antiAliasing = 1,
            useMipMap = false
        };
        fullScreenRT.Create();
        fullScreenRTHandle = RTHandles.Alloc(fullScreenRT);

        // 2. Scaled target buffer for video encoding (e.g. 720 x 1600)
        captureRT = new RenderTexture(captureWidth, captureHeight, 0, RenderTextureFormat.ARGB32)
        {
            filterMode = FilterMode.Bilinear,
            antiAliasing = 1,
            useMipMap = false
        };
        captureRT.Create();

        readTex = new Texture2D(captureWidth, captureHeight, TextureFormat.RGBA32, false);
    }

    private void ReleaseTextures()
    {
        if (fullScreenRTHandle != null)
        {
            fullScreenRTHandle.Release();
            fullScreenRTHandle = null;
        }

        if (fullScreenRT != null)
        {
            if (fullScreenRT.IsCreated()) fullScreenRT.Release();
            Destroy(fullScreenRT);
            fullScreenRT = null;
        }

        if (captureRT != null)
        {
            if (captureRT.IsCreated()) captureRT.Release();
            Destroy(captureRT);
            captureRT = null;
        }

        if (readTex != null)
        {
            Destroy(readTex);
            readTex = null;
        }
    }

    private IEnumerator CaptureLoop()
    {
        var wait = new WaitForEndOfFrame();
        float lastCaptureTime = 0f;
        long frameIndex = 0;

        while (isCapturing)
        {
            // Wait for end of frame after URP has rendered the AR camera view
            yield return wait;

            if (!isCapturing) break;

            float now = Time.realtimeSinceStartup;
            if (now - lastCaptureTime >= frameInterval)
            {
                lastCaptureTime = now;
                // Guaranteed strictly increasing presentation timestamp (in microseconds)
                long timestampUs = frameIndex * (1_000_000L / (long)Mathf.Max(1, targetFps));
                frameIndex++;

                if (captureRT != null && readTex != null)
                {
                    if (videoCapturePass != null && videoCapturePass.HasCapturedFrame && fullScreenRT != null)
                    {
                        // Clean frame from URP pass (Vuforia background + 3D models, NO UI!)
                        Graphics.Blit(fullScreenRT, captureRT);
                        videoCapturePass.ResetFrameFlag();

                        RenderTexture previous = RenderTexture.active;
                        RenderTexture.active = captureRT;
                        readTex.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0, false);
                        RenderTexture.active = previous;
                    }
                    else
                    {
                        // 100% reliable fallback: reads the rendered display buffer (camera background + 3D augmentations)
                        RenderTexture previous = RenderTexture.active;
                        RenderTexture.active = null; // null represents the rendered display backbuffer

                        int screenW = Screen.width;
                        int screenH = Screen.height;
                        int srcW = Mathf.Min(captureWidth, screenW);
                        int srcH = Mathf.Min(captureHeight, screenH);
                        int srcX = Mathf.Max(0, (screenW - srcW) / 2);
                        int srcY = Mathf.Max(0, (screenH - srcH) / 2);

                        readTex.ReadPixels(new Rect(srcX, srcY, srcW, srcH), 0, 0, false);
                        RenderTexture.active = previous;
                    }

                    // Extract raw RGBA bytes
                    byte[] rawBytes = readTex.GetRawTextureData();

#if !UNITY_EDITOR && UNITY_ANDROID
                    if (recorderClass != null && rawBytes != null && rawBytes.Length > 0)
                    {
                        recorderClass.CallStatic("feedRGBAFrame", rawBytes, timestampUs);
                    }
#endif
                }
            }
        }
    }
}
