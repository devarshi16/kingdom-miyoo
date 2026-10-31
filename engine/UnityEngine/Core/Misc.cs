// Small clean-room pieces: Random, list-filling component queries.
using System.Collections.Generic;

namespace UnityEngine
{
    public sealed partial class Random
    {
        static System.Random s_Rng = new System.Random(12345);
        public static float Range(float min, float max) { return min + (float)s_Rng.NextDouble() * (max - min); }
        public static int Range(int min, int max) { return max > min ? s_Rng.Next(min, max) : min; }   // max exclusive
        public static float value { get { return (float)s_Rng.NextDouble(); } }
        public static int seed { get { return 0; } set { s_Rng = new System.Random(value); } }
        public static void InitState(int s) { s_Rng = new System.Random(s); }
        public static Vector2 insideUnitCircle
        {
            get { float a = value * Mathf.PI * 2f, r = Mathf.Sqrt(value); return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r); }
        }
    }

    public partial class Component
    {
        public Component[] GetComponentsInChildren(System.Type t) { return m_GameObject.GetComponentsInChildren(t, false); }
        public void GetComponents(System.Type type, List<Component> results)
        { results.Clear(); foreach (var c in m_GameObject.m_Components) if (type.IsInstanceOfType(c)) results.Add(c); }
        public void GetComponentsInParent<T>(bool includeInactive, List<T> results)
        { results.Clear(); results.AddRange(m_GameObject.GetComponentsInParent<T>(includeInactive)); }
    }

    public sealed partial class GameObject
    {
        public void GetComponents(System.Type type, List<Component> results)
        { results.Clear(); foreach (var c in m_Components) if (type.IsInstanceOfType(c)) results.Add(c); }
        public void GetComponentsInParent<T>(bool includeInactive, List<T> results)
        { results.Clear(); results.AddRange(GetComponentsInParent<T>(includeInactive)); }
    }
}
