// Clean-room Plane / Ray / GeometryUtility (frustum planes for the orthographic 2D camera).
namespace UnityEngine
{
    public partial struct Plane
    {
        Vector3 m_Normal; float m_Distance;
        public Plane(Vector3 inNormal, Vector3 inPoint) { m_Normal = inNormal.normalized; m_Distance = -Vector3.Dot(m_Normal, inPoint); }
        public Plane(Vector3 inNormal, float d) { m_Normal = inNormal.normalized; m_Distance = d; }
        public Vector3 normal { get { return m_Normal; } set { m_Normal = value; } }
        public float distance { get { return m_Distance; } set { m_Distance = value; } }
        public void SetNormalAndPosition(Vector3 n, Vector3 p) { m_Normal = n.normalized; m_Distance = -Vector3.Dot(m_Normal, p); }
        public float GetDistanceToPoint(Vector3 p) { return Vector3.Dot(m_Normal, p) + m_Distance; }
        public bool GetSide(Vector3 p) { return GetDistanceToPoint(p) > 0f; }
        public bool Raycast(Ray ray, ref float enter)
        {
            float a = Vector3.Dot(ray.direction, m_Normal), num = -Vector3.Dot(ray.origin, m_Normal) - m_Distance;
            if (Mathf.Approximately(a, 0f)) { enter = 0f; return false; }
            enter = num / a; return enter > 0f;
        }
    }

    public partial struct Ray
    {
        Vector3 m_Origin, m_Direction;
        public Ray(Vector3 origin, Vector3 direction) { m_Origin = origin; m_Direction = direction.normalized; }
        public Vector3 origin { get { return m_Origin; } set { m_Origin = value; } }
        public Vector3 direction { get { return m_Direction; } set { m_Direction = value.normalized; } }
        public Vector3 GetPoint(float d) { return m_Origin + m_Direction * d; }
    }

    public sealed partial class GeometryUtility
    {
        public static Plane[] CalculateFrustumPlanes(Camera cam)
        {
            var planes = new Plane[6];
            var p = cam != null ? cam.transform.position : Vector3.zero;
            float h = cam != null ? KNL.RenderSystem.OrthoSizeOverride : 3.75f, w = h * KNL.RenderSystem.Width / KNL.RenderSystem.Height;
            float near = cam != null ? cam.nearClipPlane : 0.3f, far = cam != null ? cam.farClipPlane : 1000f;
            planes[0] = new Plane(Vector3.right, new Vector3(p.x - w, 0, 0));
            planes[1] = new Plane(Vector3.left, new Vector3(p.x + w, 0, 0));
            planes[2] = new Plane(Vector3.up, new Vector3(0, p.y - h, 0));
            planes[3] = new Plane(Vector3.down, new Vector3(0, p.y + h, 0));
            planes[4] = new Plane(Vector3.forward, new Vector3(0, 0, p.z + near));
            planes[5] = new Plane(Vector3.back, new Vector3(0, 0, p.z + far));
            return planes;
        }
        // Particle Playground binds this private member by reflection (name + signature as in Unity).
        static void Internal_ExtractPlanes(Plane[] planes, Matrix4x4 worldToProjection)
        {
            var src = CalculateFrustumPlanes(Camera.main);
            for (int i = 0; i < planes.Length && i < src.Length; i++) planes[i] = src[i];
        }
        public static bool TestPlanesAABB(Plane[] planes, Bounds b)
        {
            foreach (var pl in planes)
            {
                var n = pl.normal; var e = b.extents;
                float r = e.x * Mathf.Abs(n.x) + e.y * Mathf.Abs(n.y) + e.z * Mathf.Abs(n.z);
                if (pl.GetDistanceToPoint(b.center) < -r) return false;
            }
            return true;
        }
    }
}
