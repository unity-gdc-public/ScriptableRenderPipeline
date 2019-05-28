using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEditor.ShortcutManagement;
using UnityEditorInternal;
using UnityEngine.UIElements;

namespace UnityEditor.Rendering.LookDev
{
    class CameraController : Manipulator
    { 
        float m_StartZoom = 0.0f;
        float m_ZoomSpeed = 0.0f;
        float m_TotalMotion = 0.0f;
        Vector3 m_Motion = new Vector3();
        float m_FlySpeedNormalized = .5f;
        float m_FlySpeed = 1f;
        const float m_FlySpeedMin = .01f;
        const float m_FlySpeedMax = 2f;
        const float kFlyAcceleration = 1.1f;
        bool m_IsDragging;
        ViewTool m_BehaviorState;
        static TimeHelper s_Timer = new TimeHelper();

        CameraState m_CameraState;
        DisplayWindow m_Window;
        
        Rect screen => target.contentRect;

        float flySpeedNormalized
        {
            get => m_FlySpeedNormalized;
            set
            {
                m_FlySpeedNormalized = Mathf.Clamp01(value);
                float speed = Mathf.Lerp(m_FlySpeedMin, m_FlySpeedMax, m_FlySpeedNormalized);
                // Round to nearest decimal: 2 decimal points when between [0.01, 0.1]; 1 decimal point when between [0.1, 10]; integral between [10, 99]
                speed = (float)(System.Math.Round((double)speed, speed < 0.1f ? 2 : speed < 10f ? 1 : 0));
                m_FlySpeed = Mathf.Clamp(speed, m_FlySpeedMin, m_FlySpeedMax);
            }
        }
        float flySpeed
        {
            get => m_FlySpeed;
            set => flySpeedNormalized = Mathf.InverseLerp(m_FlySpeedMin, m_FlySpeedMax, value);
        }

        bool isDragging
        {
            get => m_IsDragging;
            set
            {
                //As in scene view, stop dragging as first button is release in case of multiple button down
                if (value ^ m_IsDragging)
                {
                    if (value)
                        target.RegisterCallback<MouseMoveEvent>(OnMouseDrag);
                    else
                        target.UnregisterCallback<MouseMoveEvent>(OnMouseDrag);
                    m_IsDragging = value;
                }
            }
        }

        public CameraController(CameraState cameraState, DisplayWindow window)
        {
            m_CameraState = cameraState;
            m_Window = window;
        }
        
        private void ResetCameraControl()
        {
            isDragging = false;
            m_BehaviorState = ViewTool.None;
            m_Motion = Vector3.zero;
        }
        
        void OnCameraScrollWheel(WheelEvent evt)
        {
            // See UnityEditor.SceneViewMotion.HandleScrollWheel
            switch (m_BehaviorState)
            {
                case ViewTool.FPS:  OnChangeFPSCameraSpeed(evt);    break;
                default:            OnZoom(evt);                    break;
            }
        }

        void OnMouseDrag(MouseMoveEvent evt)
        {
            switch (m_BehaviorState)
            {
                case ViewTool.Orbit:    OnMouseDragOrbit(evt);  break;
                case ViewTool.FPS:      OnMouseDragFPS(evt);    break;
                case ViewTool.Pan:      OnMouseDragPan(evt);    break;
                case ViewTool.Zoom:     OnMouseDragZoom(evt);   break;
                default:                break;
            }
        }
        
        void OnChangeFPSCameraSpeed(WheelEvent evt)
        {
            float scrollWheelDelta = evt.delta.y;
            flySpeedNormalized -= scrollWheelDelta * .01f;
            string cameraSpeedDisplayValue = flySpeed.ToString(flySpeed < 0.1f ? "F2" : flySpeed < 10f ? "F1" : "F0");
            if (flySpeed < 0.1f)
                cameraSpeedDisplayValue = cameraSpeedDisplayValue.TrimStart(new Char[] { '0' });
            GUIContent cameraSpeedContent = EditorGUIUtility.TrTempContent(
                $"{cameraSpeedDisplayValue}x");
            //[TODO check acceleration]
            //$"{cameraSpeedDisplayValue}{(s_SceneView.cameraSettings.accelerationEnabled ? "x" : "")}");
            m_Window.ShowNotification(cameraSpeedContent, .5f);
            evt.StopPropagation();
        }

        void OnZoom(WheelEvent evt)
        {
            float scrollWheelDelta = evt.delta.y;
            float relativeDelta = m_CameraState.viewSize * scrollWheelDelta * .015f;
            const float deltaCutoff = .3f;
            if (relativeDelta > 0 && relativeDelta < deltaCutoff)
                relativeDelta = deltaCutoff;
            else if (relativeDelta < 0 && relativeDelta > -deltaCutoff)
                relativeDelta = -deltaCutoff;
            m_CameraState.viewSize += relativeDelta;
            evt.StopPropagation();
        }

        void OnMouseDragOrbit(MouseMoveEvent evt)
        {
            Quaternion rotation = m_CameraState.rotation;
            rotation = Quaternion.AngleAxis(evt.mouseDelta.y * .003f * Mathf.Rad2Deg, rotation * Vector3.right) * rotation;
            rotation = Quaternion.AngleAxis(evt.mouseDelta.x * .003f * Mathf.Rad2Deg, Vector3.up) * rotation;
            m_CameraState.rotation = rotation;
            Debug.Log(m_CameraState.rotation.eulerAngles);
            evt.StopPropagation();
        }

        void OnMouseDragFPS(MouseMoveEvent evt)
        {
            Vector3 camPos = m_CameraState.pivot - m_CameraState.rotation * Vector3.forward * m_CameraState.distanceFromPivot;
            Quaternion rotation = m_CameraState.rotation;
            rotation = Quaternion.AngleAxis(evt.mouseDelta.y * .003f * Mathf.Rad2Deg, rotation * Vector3.right) * rotation;
            rotation = Quaternion.AngleAxis(evt.mouseDelta.x * .003f * Mathf.Rad2Deg, Vector3.up) * rotation;
            m_CameraState.rotation = rotation;
            m_CameraState.pivot = camPos + rotation * Vector3.forward * m_CameraState.distanceFromPivot;
            evt.StopPropagation();
        }

        void OnMouseDragPan(MouseMoveEvent evt)
        {
            //[TODO: fix WorldToScreenPoint and ScreenToWorldPoint
            var screenPos = m_CameraState.QuickProjectPivotInScreen(screen);
            screenPos += new Vector3(evt.mouseDelta.x, -evt.mouseDelta.y, 0);
            //Vector3 newWorldPos = m_CameraState.ScreenToWorldPoint(screen, screenPos);
            Vector3 newWorldPos = m_CameraState.QuickReprojectionWithFixedFOVOnPivotPlane(screen, screenPos);
            Vector3 worldDelta = newWorldPos - m_CameraState.pivot;
            worldDelta *= EditorGUIUtility.pixelsPerPoint;
            Debug.Log($"Pan: screen:({screen.width},{screen.height}) pivotScreenPos:({screenPos.x,10:F3},{screenPos.y,10:F3},{screenPos.z,10:F3}) mouseDelta:({evt.mouseDelta.x,10:F3},{evt.mouseDelta.y,10:F3}) worldDelta:({worldDelta.x,15:F9},{worldDelta.y,15:F9},{worldDelta.z,15:F9})");
            if (evt.shiftKey)
                worldDelta *= 4;
            m_CameraState.pivot += worldDelta;
            evt.StopPropagation();
        }


        void OnMouseDragZoom(MouseMoveEvent evt)
        {
            float zoomDelta = HandleUtility.niceMouseDeltaZoom * (evt.shiftKey ? 9 : 3);
            m_TotalMotion += zoomDelta;
            if (m_TotalMotion < 0)
                m_CameraState.viewSize = m_StartZoom * (1 + m_TotalMotion * .001f);
            else
                m_CameraState.viewSize = m_CameraState.viewSize + zoomDelta * m_ZoomSpeed * .003f;
            evt.StopPropagation();
        }

        private void HandleCameraKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape)
                ResetCameraControl();
            evt.StopPropagation();
        }
        
        void OnMouseUp(MouseUpEvent evt)
        {
            ResetCameraControl();
            evt.StopPropagation();
        }

        void OnMouseDown(MouseDownEvent evt)
        {
            bool onMac = Application.platform == RuntimePlatform.OSXEditor;

            if (evt.button == 2)
                m_BehaviorState = ViewTool.Pan;
            else if (evt.button == 0 && evt.ctrlKey && onMac || evt.button == 1 && evt.altKey)
            {
                m_BehaviorState = ViewTool.Zoom;
                m_StartZoom = m_CameraState.viewSize;
                m_ZoomSpeed = Mathf.Max(Mathf.Abs(m_StartZoom), .3f);
                m_TotalMotion = 0;
            }
            else if (evt.button == 0)
                m_BehaviorState = ViewTool.Orbit;
            else if (evt.button == 1 && !evt.altKey)
                m_BehaviorState = ViewTool.FPS;

            // see also SceneView.HandleClickAndDragToFocus()
            if (evt.button == 1 && onMac)
                m_Window.Focus();

            isDragging = true;
            evt.StopPropagation();
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<MouseUpEvent>(OnMouseUp);
            target.RegisterCallback<MouseDownEvent>(OnMouseDown);
            target.RegisterCallback<WheelEvent>(OnCameraScrollWheel);
            target.RegisterCallback<KeyDownEvent>(HandleCameraKeyDown);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<MouseUpEvent>(OnMouseUp);
            target.UnregisterCallback<MouseDownEvent>(OnMouseDown);
            target.UnregisterCallback<WheelEvent>(OnCameraScrollWheel);
            target.UnregisterCallback<KeyDownEvent>(HandleCameraKeyDown);
        }
    }

    //[TODO: check to reuse legacy internal one]
    struct TimeHelper
    {
        public float deltaTime;
        long lastTime;

        public void Begin()
        {
            lastTime = System.DateTime.Now.Ticks;
        }

        public float Update()
        {
            deltaTime = (System.DateTime.Now.Ticks - lastTime) / 10000000.0f;
            lastTime = System.DateTime.Now.Ticks;
            return deltaTime;
        }
    }
}
