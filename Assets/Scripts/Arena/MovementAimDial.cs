using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace WelpTimeToDie.RectanglePrototype
{
    internal sealed class MovementAimDial : VisualElement
    {
        private static readonly Color Grid = new Color32(67, 99, 119, 255);
        private static readonly Color Accent = new Color32(116, 228, 246, 255);
        private Vector2 aim;
        private int dragPointer = -1;

        public event Action<Vector2> AimChanged;
        private float Radius => Mathf.Max(0, Mathf.Min(contentRect.width, contentRect.height) * 0.5f - 7f);

        public MovementAimDial()
        {
            name = "movementDial";
            tooltip = "Drag to set direction and power. The inner ring marks the old launch strength.";
            AddToClassList("movement-dial");
            generateVisualContent += Draw;
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
            RegisterCallback<PointerCaptureOutEvent>(e =>
            {
                if (e.pointerId == dragPointer) dragPointer = -1;
            });
        }

        public void SetAim(Vector2 value)
        {
            if (aim == value) return;
            aim = value;
            MarkDirtyRepaint();
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || dragPointer >= 0) return;
            dragPointer = evt.pointerId;
            this.CapturePointer(dragPointer);
            ReadPointer(evt.localPosition);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != dragPointer) return;
            ReadPointer(evt.localPosition);
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != dragPointer || evt.button != 0) return;
            ReadPointer(evt.localPosition);
            this.ReleasePointer(dragPointer);
            dragPointer = -1;
            evt.StopPropagation();
        }

        private void ReadPointer(Vector2 point)
        {
            if (!enabledInHierarchy || Radius <= 0) return;
            Vector2 offset = (point - contentRect.center) / Radius;
            offset.y = -offset.y;
            Vector2 direction = offset.sqrMagnitude < 0.0001f ? aim.normalized : offset.normalized;
            float power = Mathf.Clamp(offset.magnitude, RectangleCombat.MinMovementPower, 1f);
            AimChanged?.Invoke(direction * power);
        }

        private void Draw(MeshGenerationContext context)
        {
            float radius = Radius;
            if (radius <= 0) return;

            Vector2 center = contentRect.center;
            var painter = context.painter2D;
            painter.lineWidth = 1;
            painter.strokeColor = Grid;
            painter.fillColor = new Color32(24, 40, 52, 255);
            Circle(painter, center, radius);
            painter.Fill();
            painter.Stroke();

            Circle(painter, center, radius * RectangleCombat.LegacyMovementPower);
            painter.Stroke();
            painter.BeginPath();
            painter.MoveTo(center - Vector2.right * radius);
            painter.LineTo(center + Vector2.right * radius);
            painter.MoveTo(center - Vector2.up * radius);
            painter.LineTo(center + Vector2.up * radius);
            painter.Stroke();

            Vector2 tip = center + new Vector2(aim.x, -aim.y) * radius;
            painter.lineWidth = 2;
            painter.strokeColor = Accent;
            painter.BeginPath();
            painter.MoveTo(center);
            painter.LineTo(tip);
            painter.Stroke();
            painter.fillColor = Accent;
            Circle(painter, tip, 5);
            painter.Fill();
        }

        private static void Circle(Painter2D painter, Vector2 center, float radius)
        {
            painter.BeginPath();
            painter.Arc(center, radius, Angle.Degrees(0), Angle.Degrees(360));
            painter.ClosePath();
        }
    }
}
