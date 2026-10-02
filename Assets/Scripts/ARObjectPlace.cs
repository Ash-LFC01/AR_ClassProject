using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARObjectPlace : MonoBehaviour
{
    [SerializeField] private ARRaycastManager raycastManager;
    [SerializeField] private Camera arCamera;

    [Header("Placement Alignment")]
    [Tooltip("Automatically calculates bounds so the bottom of the object rests on the plane.")]
    [SerializeField] private bool snapBottomToFloor = true;

    [Tooltip("Optional manual vertical offset (in meters) to fine-tune height.")]
    [SerializeField] private float extraHeightOffset = 0f;

    private readonly List<GameObject> spawnedObjects = new List<GameObject>();
    public Ease ease;

    void Awake()
    {
        if (arCamera == null) arCamera = Camera.main;
    }

    void Update()
    {
        // Check if any pointer was pressed down this frame
        if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
        {
            Vector2 screenPosition = Pointer.current.position.ReadValue();

            // Ignore if clicking on an already spawned object
            Ray ray = arCamera.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (IsSpawnedObject(hit.collider.gameObject))
                    return;
            }

            // Otherwise, place on detected AR plane
            PlaceObj(screenPosition);
        }
    }

    private bool IsSpawnedObject(GameObject target)
    {
        foreach (var obj in spawnedObjects)
        {
            if (obj != null && (obj == target || target.transform.IsChildOf(obj.transform)))
                return true;
        }
        return false;
    }

    void PlaceObj(Vector2 screenPosition)
    {   
        var rayHits = new List<ARRaycastHit>();
        if (raycastManager.Raycast(screenPosition, rayHits, TrackableType.PlaneWithinPolygon))
        {
            Pose hitPose = rayHits[0].pose;
            GameObject placed = Instantiate(raycastManager.raycastPrefab, hitPose.position, hitPose.rotation);

            placed.transform.localScale = Vector3.zero; // Start with zero scale for animation
            placed.transform.DOScale(1f,1f).SetEase(ease); // Animate to full size

            // Offset the object so its bottom boundary sits exactly on the floor
            if (snapBottomToFloor)
            {
                float bottomOffset = GetBottomOffset(placed);
                placed.transform.position += Vector3.up * (bottomOffset + extraHeightOffset);
            }
            else if (extraHeightOffset != 0f)
            {
                placed.transform.position += Vector3.up * extraHeightOffset;
            }

            spawnedObjects.Add(placed);
        }
    }

    /// <summary>
    /// Calculates the vertical distance from the object's lowest boundary up to its pivot point.
    /// </summary>
    private float GetBottomOffset(GameObject obj)
    {
        // 1. Check Colliders first
        Collider[] colliders = obj.GetComponentsInChildren<Collider>();
        if (colliders.Length > 0)
        {
            Bounds bounds = colliders[0].bounds;
            for (int i = 1; i < colliders.Length; i++)
            {
                bounds.Encapsulate(colliders[i].bounds);
            }
            return obj.transform.position.y - bounds.min.y;
        }

        // 2. Fallback to Mesh / Sprite Renderers
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
            return obj.transform.position.y - bounds.min.y;
        }

        return 0f;
    }
}