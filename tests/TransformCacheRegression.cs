using System;
using System.Collections.Generic;
using KNL;
using UnityEngine;
class TransformCacheRegression
{
    static int checks;
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS: " + name); checks++; }
    static Matrix4x4 Reference(Transform t)
    {
        var local = Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale);
        return t.parent == null ? local : Reference(t.parent) * local;
    }
    static bool Same(Matrix4x4 a, Matrix4x4 b) { for (int i = 0; i < 16; i++) if (a[i] != b[i]) return false; return true; }
    static float F(System.Random r) { return (float)(r.NextDouble() * 6 - 3); }
    static IEnumerable<Transform> Walk(Transform t) { yield return t; for (int i = 0; i < t.childCount; i++) foreach (var c in Walk(t.GetChild(i))) yield return c; }
    static void Main()
    {
        World.ActiveScene = new SceneTag("transform cache");
        var random = new System.Random(927);
        var nodes = new List<Transform>();
        for (int i = 0; i < 160; i++)
        {
            var t = new GameObject("matrix node " + i).transform;
            if (i > 3) t.SetParent(nodes[random.Next(i)], false);
            t.localPosition = new Vector3(F(random), F(random), F(random));
            t.localRotation = Quaternion.Euler(F(random) * 10, F(random) * 10, F(random) * 60);
            t.localScale = new Vector3(1 + F(random) * .1f, 1 + F(random) * .1f, 1 + F(random) * .1f);
            nodes.Add(t); var warm = t.localToWorldMatrix; var inverse = t.worldToLocalMatrix;
        }
        bool localOk = true, parentOk = true, inverseOk = true;
        for (int sample = 0; sample < 1200; sample++)
        {
            int index = random.Next(nodes.Count); var t = nodes[index];
            switch (sample % 6)
            {
                case 0: t.localPosition = new Vector3(F(random), F(random), F(random)); break;
                case 1: t.localRotation = Quaternion.Euler(F(random) * 10, F(random) * 10, F(random) * 60); break;
                case 2: t.localScale = new Vector3(1 + F(random) * .1f, 1 + F(random) * .1f, 1 + F(random) * .1f); break;
                case 3: t.SetParent(index > 0 ? nodes[random.Next(index)] : null, sample % 12 == 3); break;
                case 4: t.position = new Vector3(F(random), F(random), F(random)); break;
                case 5: t.localPosition = t.localPosition; t.localRotation = t.localRotation; t.localScale = t.localScale; break;
            }
            localOk &= Same(t.localToWorldMatrix, Reference(t));
            for (int j = 0; j < 4; j++) { var other = nodes[random.Next(nodes.Count)]; parentOk &= Same(other.localToWorldMatrix, Reference(other)); }
            inverseOk &= Same(t.worldToLocalMatrix, Reference(t).inverse);
        }
        Check(localOk, "1200 local/world/reparent/identical writes exactly match freshly composed matrices");
        Check(parentOk, "4800 descendant and unrelated hierarchy reads match fresh composition");
        Check(inverseOk, "inverse caches refresh exactly after local and ancestor mutations");
        var root = new GameObject("warm original"); var child = new GameObject("warm child"); child.transform.SetParent(root.transform, false);
        var leaf = new GameObject("warm leaf"); leaf.transform.SetParent(child.transform, false); leaf.transform.localPosition = new Vector3(1, 2, 3);
        foreach (var node in Walk(root.transform)) { var matrix = node.localToWorldMatrix; var inverse = node.worldToLocalMatrix; }
        var clone = UnityEngine.Object.Instantiate(root); var cloneLeaf = clone.transform.GetChild(0).GetChild(0);
        Check(Same(cloneLeaf.localToWorldMatrix, Reference(cloneLeaf)), "cloning a warm hierarchy discards inherited world cache state");
        clone.transform.localPosition = new Vector3(4, 5, 6); var first = cloneLeaf.localToWorldMatrix;
        clone.transform.localPosition = new Vector3(9, 8, 7); clone.transform.localScale = new Vector3(2, 3, 4);
        Check(Same(cloneLeaf.localToWorldMatrix, Reference(cloneLeaf)) && leaf.transform.position == new Vector3(1, 2, 3), "repeated clone ancestor writes propagate while original remains unchanged");
        child.transform.SetParent(clone.transform, false); clone.transform.localRotation = Quaternion.Euler(0, 0, 37);
        Check(Same(leaf.transform.localToWorldMatrix, Reference(leaf.transform)), "reparented descendants follow later writes to their new ancestor");
        Console.WriteLine("Transform cache regressions passed: " + checks);
    }
}
