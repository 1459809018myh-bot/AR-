using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// 负责：点击水平平面 → 在命中点生成标记 → 通知表单。
/// 表单打开时不接受放置，避免点击输入框/按钮时误放标记。
/// </summary>
public class ARPlacementController : MonoBehaviour
{
    static readonly Color PendingColor = new Color(1f, 0.82f, 0.25f, 1f);
    static readonly Color HighColor = new Color(0.93f, 0.27f, 0.25f, 1f);
    static readonly Color MediumColor = new Color(0.96f, 0.65f, 0.14f, 1f);
    static readonly Color LowColor = new Color(0.13f, 0.6f, 0.95f, 1f);

    readonly List<ARRaycastHit> hitResults = new List<ARRaycastHit>();

    ARRaycastManager raycastManager;
    InspectionFormUI formUI;
    GameObject markerPrefab;
    GameObject currentMarker;
    Renderer currentRenderer;

    public void Init(ARRaycastManager raycast, InspectionFormUI form, GameObject markerTemplate)
    {
        raycastManager = raycast;
        formUI = form;
        markerPrefab = markerTemplate;
    }

    void Update()
    {
        if (raycastManager == null || formUI == null)
        {
            return;
        }

        // 表单打开时不做任何放置，点击只交给 UI
        if (formUI.IsVisible)
        {
            return;
        }

        if (!TapInput.TryGetTap(out Vector2 screenPosition, out int pointerId))
        {
            return;
        }

        // 点到 UI（按钮/输入框）时不放置
        if (TapInput.IsPointerOverUI(pointerId))
        {
            return;
        }

        TryPlaceAt(screenPosition);
    }

    void TryPlaceAt(Vector2 screenPosition)
    {
        hitResults.Clear();
        if (!raycastManager.Raycast(screenPosition, hitResults, TrackableType.PlaneWithinPolygon))
        {
            formUI.ShowHint("没有命中已识别的平面，请对准平面再点一次");
            return;
        }

        // 只接受水平平面（本任务要求识别水平面），避免把标记放到墙面上
        bool found = false;
        ARRaycastHit selected = default;
        foreach (ARRaycastHit hit in hitResults)
        {
            if (hit.trackable is ARPlane plane &&
                (plane.alignment == PlaneAlignment.HorizontalUp || plane.alignment == PlaneAlignment.HorizontalDown))
            {
                selected = hit;
                found = true;
                break;
            }
        }

        if (!found)
        {
            formUI.ShowHint("请点击水平地面（当前命中的不是水平平面）");
            return;
        }

        PlaceMarker(selected);
    }

    void PlaceMarker(ARRaycastHit hit)
    {
        ClearMarker();

        GameObject marker = markerPrefab != null ? Instantiate(markerPrefab) : CreateDefaultMarker();
        marker.name = "IssueMarker";
        marker.transform.SetPositionAndRotation(hit.pose.position + Vector3.up * 0.03f, hit.pose.rotation);

        // 跟随被命中的平面一起移动，作为原型阶段的位置稳定方案
        if (hit.trackable is ARPlane plane)
        {
            marker.transform.SetParent(plane.transform, true);
        }

        currentMarker = marker;
        currentRenderer = marker.GetComponentInChildren<Renderer>();
        ApplyMarkerColor(PendingColor);

        formUI.ShowFor(marker.transform.position);
    }

    /// <summary>提交成功后按优先级给标记上色（高=红 / 中=橙 / 低=蓝）。</summary>
    public void ApplyPriorityStyle(string priority)
    {
        switch (priority)
        {
            case IssueEnums.PriorityHigh:
                ApplyMarkerColor(HighColor);
                break;
            case IssueEnums.PriorityLow:
                ApplyMarkerColor(LowColor);
                break;
            default:
                ApplyMarkerColor(MediumColor);
                break;
        }
    }

    public void ClearMarker()
    {
        if (currentMarker != null)
        {
            Destroy(currentMarker);
        }
        currentMarker = null;
        currentRenderer = null;
    }

    void ApplyMarkerColor(Color color)
    {
        if (currentRenderer == null)
        {
            return;
        }
        Material material = currentRenderer.material;
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", color);
        }
    }

    GameObject CreateDefaultMarker()
    {
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.transform.localScale = Vector3.one * 0.12f;
        Collider collider = marker.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }
        return marker;
    }
}
