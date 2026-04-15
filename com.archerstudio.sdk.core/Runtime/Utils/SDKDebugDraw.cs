using UnityEngine;

namespace ArcherStudio.SDK.Core {

    /// <summary>
    /// Debug drawing utilities for visual debugging in Scene/Game view.
    /// Provides gizmo-free alternatives using Debug.DrawLine/DrawRay,
    /// plus Gizmos.* wrappers for OnDrawGizmos usage.
    /// Ported from DebugExtension.cs for SDK-wide reuse.
    /// </summary>
    public static class SDKDebugDraw {

        // ════════════════════════════════════════════════════════════
        //  Debug.DrawLine/DrawRay methods (work in Play mode)
        // ════════════════════════════════════════════════════════════

        #region Debug Draw (Play Mode)

        public static void Point(Vector3 position, Color color, float scale = 1f, float duration = 0, bool depthTest = true) {
            color = color == default ? Color.white : color;
            Debug.DrawRay(position + Vector3.up * (scale * 0.5f), -Vector3.up * scale, color, duration, depthTest);
            Debug.DrawRay(position + Vector3.right * (scale * 0.5f), -Vector3.right * scale, color, duration, depthTest);
            Debug.DrawRay(position + Vector3.forward * (scale * 0.5f), -Vector3.forward * scale, color, duration, depthTest);
        }

        public static void Point(Vector3 position, float scale = 1f, float duration = 0) {
            Point(position, Color.white, scale, duration);
        }

        public static void Bounds(Bounds bounds, Color color, float duration = 0, bool depthTest = true) {
            Vector3 c = bounds.center;
            float x = bounds.extents.x, y = bounds.extents.y, z = bounds.extents.z;

            Vector3 ruf = c + new Vector3(x, y, z), rub = c + new Vector3(x, y, -z);
            Vector3 luf = c + new Vector3(-x, y, z), lub = c + new Vector3(-x, y, -z);
            Vector3 rdf = c + new Vector3(x, -y, z), rdb = c + new Vector3(x, -y, -z);
            Vector3 lfd = c + new Vector3(-x, -y, z), lbd = c + new Vector3(-x, -y, -z);

            // Top
            Debug.DrawLine(ruf, luf, color, duration, depthTest);
            Debug.DrawLine(ruf, rub, color, duration, depthTest);
            Debug.DrawLine(luf, lub, color, duration, depthTest);
            Debug.DrawLine(rub, lub, color, duration, depthTest);
            // Vertical
            Debug.DrawLine(ruf, rdf, color, duration, depthTest);
            Debug.DrawLine(rub, rdb, color, duration, depthTest);
            Debug.DrawLine(luf, lfd, color, duration, depthTest);
            Debug.DrawLine(lub, lbd, color, duration, depthTest);
            // Bottom
            Debug.DrawLine(rdf, lfd, color, duration, depthTest);
            Debug.DrawLine(rdf, rdb, color, duration, depthTest);
            Debug.DrawLine(lfd, lbd, color, duration, depthTest);
            Debug.DrawLine(lbd, rdb, color, duration, depthTest);
        }

        public static void Bounds(Bounds bounds, float duration = 0) {
            Bounds(bounds, Color.white, duration);
        }

        public static void LocalCube(Transform transform, Vector3 size, Color color, Vector3 center = default, float duration = 0, bool depthTest = true) {
            Vector3 half = size * 0.5f;
            Vector3 lbb = transform.TransformPoint(center - half);
            Vector3 rbb = transform.TransformPoint(center + new Vector3(half.x, -half.y, -half.z));
            Vector3 lbf = transform.TransformPoint(center + new Vector3(half.x, -half.y, half.z));
            Vector3 rbf = transform.TransformPoint(center + new Vector3(-half.x, -half.y, half.z));
            Vector3 lub = transform.TransformPoint(center + new Vector3(-half.x, half.y, -half.z));
            Vector3 rub = transform.TransformPoint(center + new Vector3(half.x, half.y, -half.z));
            Vector3 luf = transform.TransformPoint(center + half);
            Vector3 ruf = transform.TransformPoint(center + new Vector3(-half.x, half.y, half.z));

            Debug.DrawLine(lbb, rbb, color, duration, depthTest); Debug.DrawLine(rbb, lbf, color, duration, depthTest);
            Debug.DrawLine(lbf, rbf, color, duration, depthTest); Debug.DrawLine(rbf, lbb, color, duration, depthTest);
            Debug.DrawLine(lub, rub, color, duration, depthTest); Debug.DrawLine(rub, luf, color, duration, depthTest);
            Debug.DrawLine(luf, ruf, color, duration, depthTest); Debug.DrawLine(ruf, lub, color, duration, depthTest);
            Debug.DrawLine(lbb, lub, color, duration, depthTest); Debug.DrawLine(rbb, rub, color, duration, depthTest);
            Debug.DrawLine(lbf, luf, color, duration, depthTest); Debug.DrawLine(rbf, ruf, color, duration, depthTest);
        }

        public static void Circle(Vector3 position, Vector3 up, Color color, float radius = 1f, float duration = 0, bool depthTest = true) {
            Vector3 upNorm = up.normalized * radius;
            Vector3 forward = Vector3.Slerp(upNorm, -upNorm, 0.5f);
            Vector3 right = Vector3.Cross(upNorm, forward).normalized * radius;

            var matrix = new Matrix4x4();
            matrix[0] = right.x; matrix[1] = right.y; matrix[2] = right.z;
            matrix[4] = upNorm.x; matrix[5] = upNorm.y; matrix[6] = upNorm.z;
            matrix[8] = forward.x; matrix[9] = forward.y; matrix[10] = forward.z;

            Vector3 last = position + matrix.MultiplyPoint3x4(new Vector3(Mathf.Cos(0), 0, Mathf.Sin(0)));
            color = color == default ? Color.white : color;

            for (int i = 0; i < 91; i++) {
                Vector3 next = new Vector3(Mathf.Cos(i * 4 * Mathf.Deg2Rad), 0, Mathf.Sin(i * 4 * Mathf.Deg2Rad));
                next = position + matrix.MultiplyPoint3x4(next);
                Debug.DrawLine(last, next, color, duration, depthTest);
                last = next;
            }
        }

        public static void Circle(Vector3 position, Color color, float radius = 1f, float duration = 0) {
            Circle(position, Vector3.up, color, radius, duration);
        }

        public static void WireSphere(Vector3 position, Color color, float radius = 1f, float duration = 0, bool depthTest = true) {
            float angle = 10f;
            Vector3 x = new Vector3(position.x, position.y + radius * Mathf.Sin(0), position.z + radius * Mathf.Cos(0));
            Vector3 y = new Vector3(position.x + radius * Mathf.Cos(0), position.y, position.z + radius * Mathf.Sin(0));
            Vector3 z = new Vector3(position.x + radius * Mathf.Cos(0), position.y + radius * Mathf.Sin(0), position.z);

            for (int i = 1; i < 37; i++) {
                float rad = angle * i * Mathf.Deg2Rad;
                Vector3 nx = new Vector3(position.x, position.y + radius * Mathf.Sin(rad), position.z + radius * Mathf.Cos(rad));
                Vector3 ny = new Vector3(position.x + radius * Mathf.Cos(rad), position.y, position.z + radius * Mathf.Sin(rad));
                Vector3 nz = new Vector3(position.x + radius * Mathf.Cos(rad), position.y + radius * Mathf.Sin(rad), position.z);

                Debug.DrawLine(x, nx, color, duration, depthTest);
                Debug.DrawLine(y, ny, color, duration, depthTest);
                Debug.DrawLine(z, nz, color, duration, depthTest);
                x = nx; y = ny; z = nz;
            }
        }

        public static void WireSphere(Vector3 position, float radius = 1f, float duration = 0) {
            WireSphere(position, Color.white, radius, duration);
        }

        public static void Cylinder(Vector3 start, Vector3 end, Color color, float radius = 1f, float duration = 0, bool depthTest = true) {
            Vector3 up = (end - start).normalized * radius;
            Vector3 forward = Vector3.Slerp(up, -up, 0.5f);
            Vector3 right = Vector3.Cross(up, forward).normalized * radius;

            Circle(start, up, color, radius, duration, depthTest);
            Circle(end, -up, color, radius, duration, depthTest);
            Circle((start + end) * 0.5f, up, color, radius, duration, depthTest);

            Debug.DrawLine(start + right, end + right, color, duration, depthTest);
            Debug.DrawLine(start - right, end - right, color, duration, depthTest);
            Debug.DrawLine(start + forward, end + forward, color, duration, depthTest);
            Debug.DrawLine(start - forward, end - forward, color, duration, depthTest);

            Debug.DrawLine(start - right, start + right, color, duration, depthTest);
            Debug.DrawLine(start - forward, start + forward, color, duration, depthTest);
            Debug.DrawLine(end - right, end + right, color, duration, depthTest);
            Debug.DrawLine(end - forward, end + forward, color, duration, depthTest);
        }

        public static void Cone(Vector3 position, Vector3 direction, Color color, float angle = 45f, float duration = 0, bool depthTest = true) {
            float length = direction.magnitude;
            Vector3 fwd = direction;
            Vector3 up = Vector3.Slerp(fwd, -fwd, 0.5f);
            Vector3 right = Vector3.Cross(fwd, up).normalized * length;
            Vector3 dir = direction.normalized;

            Vector3 slerped = Vector3.Slerp(fwd, up, angle / 90f);
            var farPlane = new Plane(-dir, position + fwd);
            var ray = new Ray(position, slerped);
            farPlane.Raycast(ray, out float dist);

            Debug.DrawRay(position, slerped.normalized * dist, color, duration, depthTest);
            Debug.DrawRay(position, Vector3.Slerp(fwd, -up, angle / 90f).normalized * dist, color, duration, depthTest);
            Debug.DrawRay(position, Vector3.Slerp(fwd, right, angle / 90f).normalized * dist, color, duration, depthTest);
            Debug.DrawRay(position, Vector3.Slerp(fwd, -right, angle / 90f).normalized * dist, color, duration, depthTest);

            Circle(position + fwd, dir, color, (fwd - slerped.normalized * dist).magnitude, duration, depthTest);
            Circle(position + fwd * 0.5f, dir, color, (fwd * 0.5f - slerped.normalized * (dist * 0.5f)).magnitude, duration, depthTest);
        }

        public static void Arrow(Vector3 position, Vector3 direction, Color color, float duration = 0, bool depthTest = true) {
            Debug.DrawRay(position, direction, color, duration, depthTest);
            Cone(position + direction, -direction * 0.333f, color, 15, duration, depthTest);
        }

        public static void Arrow(Vector3 position, Vector3 direction, float duration = 0) {
            Arrow(position, direction, Color.white, duration);
        }

        public static void Capsule(Vector3 start, Vector3 end, Color color, float radius = 1f, float duration = 0, bool depthTest = true) {
            Vector3 up = (end - start).normalized * radius;
            Vector3 forward = Vector3.Slerp(up, -up, 0.5f);
            Vector3 right = Vector3.Cross(up, forward).normalized * radius;

            float height = (start - end).magnitude;
            float sideLength = Mathf.Max(0, height * 0.5f - radius);
            Vector3 middle = (end + start) * 0.5f;
            Vector3 startSphere = middle + (start - middle).normalized * sideLength;
            Vector3 endSphere = middle + (end - middle).normalized * sideLength;

            Circle(startSphere, up, color, radius, duration, depthTest);
            Circle(endSphere, -up, color, radius, duration, depthTest);

            Debug.DrawLine(startSphere + right, endSphere + right, color, duration, depthTest);
            Debug.DrawLine(startSphere - right, endSphere - right, color, duration, depthTest);
            Debug.DrawLine(startSphere + forward, endSphere + forward, color, duration, depthTest);
            Debug.DrawLine(startSphere - forward, endSphere - forward, color, duration, depthTest);

            // Hemisphere caps
            for (int i = 1; i < 26; i++) {
                float rad = i * 7 * Mathf.Deg2Rad;
                Debug.DrawLine(Vector3.Slerp(right, -up, (float)(i - 1) / 25) * radius + startSphere,
                    Vector3.Slerp(right, -up, (float)i / 25) * radius + startSphere, color, duration, depthTest);
                Debug.DrawLine(Vector3.Slerp(-right, -up, (float)(i - 1) / 25) * radius + startSphere,
                    Vector3.Slerp(-right, -up, (float)i / 25) * radius + startSphere, color, duration, depthTest);
                Debug.DrawLine(Vector3.Slerp(forward, -up, (float)(i - 1) / 25) * radius + startSphere,
                    Vector3.Slerp(forward, -up, (float)i / 25) * radius + startSphere, color, duration, depthTest);
                Debug.DrawLine(Vector3.Slerp(-forward, -up, (float)(i - 1) / 25) * radius + startSphere,
                    Vector3.Slerp(-forward, -up, (float)i / 25) * radius + startSphere, color, duration, depthTest);

                Debug.DrawLine(Vector3.Slerp(right, up, (float)(i - 1) / 25) * radius + endSphere,
                    Vector3.Slerp(right, up, (float)i / 25) * radius + endSphere, color, duration, depthTest);
                Debug.DrawLine(Vector3.Slerp(-right, up, (float)(i - 1) / 25) * radius + endSphere,
                    Vector3.Slerp(-right, up, (float)i / 25) * radius + endSphere, color, duration, depthTest);
                Debug.DrawLine(Vector3.Slerp(forward, up, (float)(i - 1) / 25) * radius + endSphere,
                    Vector3.Slerp(forward, up, (float)i / 25) * radius + endSphere, color, duration, depthTest);
                Debug.DrawLine(Vector3.Slerp(-forward, up, (float)(i - 1) / 25) * radius + endSphere,
                    Vector3.Slerp(-forward, up, (float)i / 25) * radius + endSphere, color, duration, depthTest);
            }
        }

        #endregion

        // ════════════════════════════════════════════════════════════
        //  Gizmos.* methods (for OnDrawGizmos/OnDrawGizmosSelected)
        // ════════════════════════════════════════════════════════════

        #region Gizmos Draw (Editor OnDrawGizmos)

        public static void GizmoPoint(Vector3 position, Color color, float scale = 1f) {
            Gizmos.color = color == default ? Color.white : color;
            Gizmos.DrawRay(position + Vector3.up * (scale * 0.5f), -Vector3.up * scale);
            Gizmos.DrawRay(position + Vector3.right * (scale * 0.5f), -Vector3.right * scale);
            Gizmos.DrawRay(position + Vector3.forward * (scale * 0.5f), -Vector3.forward * scale);
        }

        public static void GizmoBounds(Bounds bounds, Color color) {
            Vector3 c = bounds.center;
            float x = bounds.extents.x, y = bounds.extents.y, z = bounds.extents.z;
            Gizmos.color = color == default ? Color.white : color;

            Vector3 ruf = c + new Vector3(x, y, z), rub = c + new Vector3(x, y, -z);
            Vector3 luf = c + new Vector3(-x, y, z), lub = c + new Vector3(-x, y, -z);
            Vector3 rdf = c + new Vector3(x, -y, z), rdb = c + new Vector3(x, -y, -z);
            Vector3 lfd = c + new Vector3(-x, -y, z), lbd = c + new Vector3(-x, -y, -z);

            Gizmos.DrawLine(ruf, luf); Gizmos.DrawLine(ruf, rub);
            Gizmos.DrawLine(luf, lub); Gizmos.DrawLine(rub, lub);
            Gizmos.DrawLine(ruf, rdf); Gizmos.DrawLine(rub, rdb);
            Gizmos.DrawLine(luf, lfd); Gizmos.DrawLine(lub, lbd);
            Gizmos.DrawLine(rdf, lfd); Gizmos.DrawLine(rdf, rdb);
            Gizmos.DrawLine(lfd, lbd); Gizmos.DrawLine(lbd, rdb);
        }

        public static void GizmoCircle(Vector3 position, Vector3 up, Color color, float radius = 1f) {
            Gizmos.color = color == default ? Color.white : color;
            Vector3 upNorm = up.normalized * radius;
            Vector3 forward = Vector3.Slerp(upNorm, -upNorm, 0.5f);
            Vector3 right = Vector3.Cross(upNorm, forward).normalized * radius;

            var matrix = new Matrix4x4();
            matrix[0] = right.x; matrix[1] = right.y; matrix[2] = right.z;
            matrix[4] = upNorm.x; matrix[5] = upNorm.y; matrix[6] = upNorm.z;
            matrix[8] = forward.x; matrix[9] = forward.y; matrix[10] = forward.z;

            Vector3 last = position + matrix.MultiplyPoint3x4(new Vector3(1, 0, 0));
            for (int i = 0; i < 91; i++) {
                Vector3 next = new Vector3(Mathf.Cos(i * 4 * Mathf.Deg2Rad), 0, Mathf.Sin(i * 4 * Mathf.Deg2Rad));
                next = position + matrix.MultiplyPoint3x4(next);
                Gizmos.DrawLine(last, next);
                last = next;
            }
        }

        public static void GizmoArrow(Vector3 position, Vector3 direction, Color color) {
            Gizmos.color = color == default ? Color.white : color;
            Gizmos.DrawRay(position, direction);
            GizmoCone(position + direction, -direction * 0.333f, color, 15);
        }

        public static void GizmoCone(Vector3 position, Vector3 direction, Color color, float angle = 45f) {
            Gizmos.color = color == default ? Color.white : color;
            float length = direction.magnitude;
            Vector3 fwd = direction;
            Vector3 up = Vector3.Slerp(fwd, -fwd, 0.5f);
            Vector3 right = Vector3.Cross(fwd, up).normalized * length;
            Vector3 dir = direction.normalized;

            Vector3 slerped = Vector3.Slerp(fwd, up, angle / 90f);
            var farPlane = new Plane(-dir, position + fwd);
            var ray = new Ray(position, slerped);
            farPlane.Raycast(ray, out float dist);

            Gizmos.DrawRay(position, slerped.normalized * dist);
            Gizmos.DrawRay(position, Vector3.Slerp(fwd, -up, angle / 90f).normalized * dist);
            Gizmos.DrawRay(position, Vector3.Slerp(fwd, right, angle / 90f).normalized * dist);
            Gizmos.DrawRay(position, Vector3.Slerp(fwd, -right, angle / 90f).normalized * dist);

            GizmoCircle(position + fwd, dir, color, (fwd - slerped.normalized * dist).magnitude);
        }

        public static void GizmoWireSphere(Vector3 position, Color color, float radius = 1f) {
            Gizmos.color = color == default ? Color.white : color;
            float angle = 10f;
            Vector3 x = new Vector3(position.x, position.y + radius, position.z + radius);
            Vector3 y = new Vector3(position.x + radius, position.y, position.z + radius);
            Vector3 z = new Vector3(position.x + radius, position.y + radius, position.z);

            for (int i = 1; i < 37; i++) {
                float rad = angle * i * Mathf.Deg2Rad;
                Vector3 nx = new Vector3(position.x, position.y + radius * Mathf.Sin(rad), position.z + radius * Mathf.Cos(rad));
                Vector3 ny = new Vector3(position.x + radius * Mathf.Cos(rad), position.y, position.z + radius * Mathf.Sin(rad));
                Vector3 nz = new Vector3(position.x + radius * Mathf.Cos(rad), position.y + radius * Mathf.Sin(rad), position.z);
                Gizmos.DrawLine(x, nx); Gizmos.DrawLine(y, ny); Gizmos.DrawLine(z, nz);
                x = nx; y = ny; z = nz;
            }
        }

        #endregion

        // ════════════════════════════════════════════════════════════
        //  Reflection utilities
        // ════════════════════════════════════════════════════════════

        #region Reflection

        public static string MethodsOfObject(object obj, bool includeInfo = false) {
            var methods = obj.GetType().GetMethods();
            var sb = new System.Text.StringBuilder();
            foreach (var m in methods) {
                sb.AppendLine(includeInfo ? m.ToString() : m.Name);
            }
            return sb.ToString();
        }

        public static string MethodsOfType(System.Type type, bool includeInfo = false) {
            var methods = type.GetMethods();
            var sb = new System.Text.StringBuilder();
            foreach (var m in methods) {
                sb.AppendLine(includeInfo ? m.ToString() : m.Name);
            }
            return sb.ToString();
        }

        #endregion
    }
}
