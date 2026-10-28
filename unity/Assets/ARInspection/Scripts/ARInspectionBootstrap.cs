using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

/// <summary>
/// 场景入口：补齐 AR 组件、创建 UI 与上报逻辑。
/// 用法：在 AR 场景中新建空物体，挂载本脚本，填写后端地址后直接 Build &amp; Run。
/// </summary>
[DefaultExecutionOrder(-50)]
public class ARInspectionBootstrap : MonoBehaviour
{
    [Tooltip("Go 后端地址。真机不能用 localhost，请填运行后端电脑的局域网 IP，例如 http://192.168.1.20:8080")]
    public string backendBaseUrl = "http://192.168.1.100:8080";

    [Tooltip("可选：自定义标记 Prefab。留空则自动生成一个 12cm 的球体标记")]
    public GameObject markerPrefab;

    ARPlacementController placement;
    ARPlaneManager planeManager;
    InspectionFormUI form;
    IssueApiClient api;
    bool submitting;

    void Start()
    {
        TapInput.EnableTouchSupport();

        ARCameraManager cameraManager = FindObjectOfType<ARCameraManager>();
        if (cameraManager == null)
        {
            Debug.LogError("[ARInspection] 未找到 ARCameraManager。请使用 AR 模板场景（AR Session + XR Origin），或手动添加 AR Session 与 XR Origin。");
            enabled = false;
            return;
        }

        GameObject rig = cameraManager.transform.root.gameObject;
        ARRaycastManager raycast = FindObjectOfType<ARRaycastManager>();
        if (raycast == null)
        {
            raycast = rig.AddComponent<ARRaycastManager>();
        }

        planeManager = FindObjectOfType<ARPlaneManager>();
        if (planeManager == null)
        {
            planeManager = rig.AddComponent<ARPlaneManager>();
        }
        if (planeManager.planePrefab == null)
        {
            planeManager.planePrefab = CreateRuntimePlanePrefab();
        }

        api = new IssueApiClient(backendBaseUrl);

        form = new GameObject("InspectionUI").AddComponent<InspectionFormUI>();
        form.SetBackendLabel(backendBaseUrl);
        form.SubmitRequested += HandleSubmitRequested;
        form.CloseRequested += HandleCloseRequested;

        placement = new GameObject("PlacementController").AddComponent<ARPlacementController>();
        placement.Init(raycast, form, markerPrefab);

        Debug.Log("[ARInspection] 初始化完成，后端地址：" + backendBaseUrl);
    }

    void Update()
    {
        // 运行时生成的平面 Prefab 是隐藏模板；这里兜底确保已识别的平面可见。
        if (planeManager == null)
        {
            return;
        }
        foreach (ARPlane plane in planeManager.trackables)
        {
            if (!plane.gameObject.activeSelf)
            {
                plane.gameObject.SetActive(true);
            }
        }
    }

    void OnDestroy()
    {
        if (form != null)
        {
            form.SubmitRequested -= HandleSubmitRequested;
            form.CloseRequested -= HandleCloseRequested;
        }
    }

    void HandleSubmitRequested(IssueDraft draft)
    {
        if (submitting)
        {
            return;
        }
        submitting = true;
        form.SetSubmitting(true);
        StartCoroutine(api.CreateIssue(draft, HandleCreateSuccess, HandleCreateFailed));
    }

    void HandleCreateSuccess(IssueDto issue)
    {
        submitting = false;
        form.SetSubmitting(false);
        form.ShowSuccess(issue);
        placement.ApplyPriorityStyle(issue.priority);
    }

    void HandleCreateFailed(string message)
    {
        submitting = false;
        form.SetSubmitting(false);
        form.ShowError("提交失败：" + message);
    }

    void HandleCloseRequested()
    {
        if (submitting)
        {
            return;
        }
        form.HidePanel();
        placement.ClearMarker();
    }

    // 没有指定平面 Prefab 时，运行时拼一个最小的平面可视化模板。
    static GameObject CreateRuntimePlanePrefab()
    {
        GameObject template = new GameObject("RuntimeARPlane");
        template.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = template.AddComponent<MeshRenderer>();
        template.AddComponent<ARPlane>();
        template.AddComponent<ARPlaneMeshVisualizer>();

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }
        if (shader == null)
        {
            shader = Shader.Find("Unlit/Color");
        }
        if (shader != null)
        {
            var material = new Material(shader);
            Color tint = new Color(0.2f, 0.62f, 1f, 0.22f);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", tint);
            }
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", tint);
            }
            meshRenderer.material = material;
        }

        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        template.SetActive(false);
        return template;
    }
}
