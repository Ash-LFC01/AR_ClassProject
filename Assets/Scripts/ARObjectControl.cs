using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ARObjectControl : MonoBehaviour
{
    [Header("AR References")]
    [SerializeField] private Camera arCamera;
    [SerializeField] private ARRaycastManager raycastManager;

    [Header("Settings")]
    [SerializeField] private float minScale = 0.2f;
    [SerializeField] private float maxScale = 3.0f;
    [SerializeField] private float rotationSpeed = 1.0f;

    private GameObject selectedObject;
    private static readonly List<ARRaycastHit> hits = new List<ARRaycastHit>();

    // Gesture tracking for touch
    private float previousTouchDistance;
    private float previousTouchAngle;
    private bool isTwoFingerGesture;

    private void Awake()
    {
        if (arCamera == null) arCamera = Camera.main;
        if (raycastManager == null) raycastManager = FindAnyObjectByType<ARRaycastManager>();
    }

    private void Update()
    {
        // 1. Mobile Touch Input (runs only when screen is actively being touched)
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            HandleTouchInput();
            return;
        }

        // 2. Editor / PC Simulation Input (Mouse & Keyboard)
        HandleEditorInput();
    }

    private void HandleTouchInput()
    {
        var touches = Touchscreen.current.touches;
        int activeTouchCount = 0;
        for (int i = 0; i < touches.Count; i++)
        {
            if (touches[i].isInProgress) activeTouchCount++;
        }

        // --- TWO FINGERS: Scale (Pinch) & Rotate (Twist) ---
        if (activeTouchCount >= 2)
        {
            Vector2 pos0 = touches[0].position.ReadValue();
            Vector2 pos1 = touches[1].position.ReadValue();

            float currentDistance = Vector2.Distance(pos0, pos1);
            Vector2 direction = pos1 - pos0;
            float currentAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            if (!isTwoFingerGesture)
            {
                isTwoFingerGesture = true;
                previousTouchDistance = currentDistance;
                previousTouchAngle = currentAngle;
                return;
            }

            if (selectedObject != null)
            {
                // Pinch to Scale
                if (previousTouchDistance > 0)
                {
                    float factor = currentDistance / previousTouchDistance;
                    Vector3 newScale = selectedObject.transform.localScale * factor;
                    float clampedX = Mathf.Clamp(newScale.x, minScale, maxScale);
                    selectedObject.transform.localScale = Vector3.one * clampedX;
                }

                // Twist to Rotate
                float angleDelta = Mathf.DeltaAngle(previousTouchAngle, currentAngle);
                selectedObject.transform.Rotate(Vector3.up, -angleDelta * rotationSpeed, Space.World);
            }

            previousTouchDistance = currentDistance;
            previousTouchAngle = currentAngle;
            return;
        }

        isTwoFingerGesture = false;

        // --- ONE FINGER: Select & Move along AR Plane ---
        if (activeTouchCount == 1)
        {
            var touch = touches[0];
            Vector2 touchPos = touch.position.ReadValue();

            if (IsPointerOverUI(touchPos)) return;

            // Tap down -> Select
            if (touch.press.wasPressedThisFrame)
            {
                Ray ray = arCamera.ScreenPointToRay(touchPos);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    selectedObject = hit.transform.root.gameObject;
                    Debug.Log($"[ARObjectControl] Selected: {selectedObject.name}");
                }
            }
            // Dragging -> Move selected object across detected planes
            else if (touch.press.isPressed && selectedObject != null)
            {
                if (raycastManager.Raycast(touchPos, hits, TrackableType.PlaneWithinPolygon | TrackableType.PlaneEstimated))
                {
                    selectedObject.transform.position = hits[0].pose.position;
                }
            }
        }
    }

    private void HandleEditorInput()
    {
        if (Mouse.current == null) return;
        Vector2 mousePos = Mouse.current.position.ReadValue();

        if (IsPointerOverUI(mousePos)) return;

        // Click to Select
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Ray ray = arCamera.ScreenPointToRay(mousePos);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                selectedObject = hit.transform.root.gameObject;
                Debug.Log($"[ARObjectControl] Selected: {selectedObject.name}");
            }
        }
        // Left-drag to Move across planes
        else if (Mouse.current.leftButton.isPressed && selectedObject != null)
        {
            if (raycastManager.Raycast(mousePos, hits, TrackableType.PlaneWithinPolygon | TrackableType.PlaneEstimated))
            {
                selectedObject.transform.position = hits[0].pose.position;
            }
        }

        if (selectedObject == null) return;

        // Right-click drag to Rotate horizontally
        if (Mouse.current.rightButton.isPressed)
        {
            float mouseDeltaX = Mouse.current.delta.ReadValue().x;
            selectedObject.transform.Rotate(Vector3.up, -mouseDeltaX * rotationSpeed * 2f, Space.World);
        }

        // Mouse Wheel to Scale
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            float scaleFactor = 1f + Mathf.Sign(scroll) * 0.1f;
            Vector3 newScale = selectedObject.transform.localScale * scaleFactor;
            float clamped = Mathf.Clamp(newScale.x, minScale, maxScale);
            selectedObject.transform.localScale = Vector3.one * clamped;
        }

        // Q / E or Left/Right Arrow keys to Rotate
        if (Keyboard.current != null)
        {
            if (Keyboard.current.qKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                selectedObject.transform.Rotate(Vector3.up, -90f * Time.deltaTime, Space.World);
            if (Keyboard.current.eKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                selectedObject.transform.Rotate(Vector3.up, 90f * Time.deltaTime, Space.World);
        }
    }

    private bool IsPointerOverUI(Vector2 screenPosition)
    {
        if (EventSystem.current == null) return false;
        PointerEventData eventData = new PointerEventData(EventSystem.current) { position = screenPosition };
        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);
        return results.Count > 0;
    }

    public void Select(GameObject target) => selectedObject = target;
    public void Deselect() => selectedObject = null;
}
