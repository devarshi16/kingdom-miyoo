using System;
using System.Collections.Generic;
using System.Reflection;
using KNL;
using UnityEngine;
interface ILookupMarker { }
class LookupBase : MonoBehaviour, ILookupMarker { }
class LookupDerived : LookupBase { }
class ComponentLookupRegression
{
    static int checks;
    static void Check(bool ok, string description) { if (!ok) throw new Exception(description); Console.WriteLine("PASS: " + description); checks++; }
    static bool Match<T>(GameObject g) { return ReferenceEquals((object)g.GetComponent<T>(), g.GetComponent(typeof(T))); }
    static bool ParentMatch<T>(GameObject g) { return ReferenceEquals((object)g.GetComponentInParent<T>(), g.GetComponentInParent(typeof(T))); }
    static void Main()
    {
        World.ActiveScene = new SceneTag("component lookup regression");
        var root = new GameObject("root"); root.AddComponent<LookupBase>(); root.AddComponent<Rigidbody2D>();
        var child = new GameObject("child"); child.transform.SetParent(root.transform, false); child.AddComponent<LookupDerived>(); child.AddComponent<LookupBase>(); child.AddComponent<BoxCollider2D>();
        Check(Match<Transform>(child) && Match<LookupBase>(child) && Match<LookupDerived>(child) && Match<ILookupMarker>(child) && Match<Rigidbody2D>(child) && Match<UnityEngine.Object>(child), "generic component lookup matches Type API for first/base/derived/interface/missing/object cases");
        Check(ParentMatch<LookupBase>(child) && ParentMatch<ILookupMarker>(child) && ParentMatch<Rigidbody2D>(child), "generic parent lookup matches nearest Type API result");
        child.SetActive(false);
        Check(ParentMatch<LookupBase>(child) && ParentMatch<ILookupMarker>(child) && ParentMatch<Rigidbody2D>(child), "inactive child and active ancestor follow the existing parent lookup rules");
        child.SetActive(true); var dead = child.GetComponent<LookupDerived>();
        typeof(UnityEngine.Object).GetField("m_Destroyed", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(dead, true);
        Check(Match<LookupBase>(child) && ParentMatch<LookupBase>(child) && ParentMatch<ILookupMarker>(child), "destroyed-first component preserves direct lookup and parent null semantics");
        var firstBody = child.AddComponent<Rigidbody2D>(); child.AddComponent<Rigidbody2D>();
        typeof(UnityEngine.Object).GetField("m_Destroyed", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(firstBody, true);
        Check(ReferenceEquals(child.GetComponent<BoxCollider2D>().attachedRigidbody, child.GetComponentInParent(typeof(Rigidbody2D))), "attached body skips an ancestor whose first body has been destroyed, matching Type API");
        var random = new System.Random(283); var nodes = new List<GameObject>(); var colliders = new List<Collider2D>();
        for (int i = 0; i < 140; i++)
        {
            var go = new GameObject("lookup hierarchy " + i); if (i > 0) go.transform.SetParent(nodes[random.Next(i)].transform, false);
            go.AddComponent<LookupBase>(); if (i % 4 == 0) go.AddComponent<Rigidbody2D>();
            colliders.Add(go.AddComponent<BoxCollider2D>()); nodes.Add(go);
        }
        bool equal = true;
        for (int sample = 0; sample < 800; sample++)
        {
            int n = random.Next(nodes.Count); var go = nodes[n];
            switch (sample % 4)
            {
                case 0: go.SetActive(!go.activeSelf); break;
                case 1: if (n > 0) go.transform.SetParent(nodes[random.Next(n)].transform, false); break;
                case 2: if (go.GetComponent<Rigidbody2D>() == null) go.AddComponent<Rigidbody2D>(); break;
                case 3: var rb = go.GetComponent<Rigidbody2D>(); if (rb != null) UnityEngine.Object.DestroyImmediate(rb); break;
            }
            for (int j = 0; j < 4; j++)
            {
                int i = random.Next(nodes.Count); var c = colliders[i];
                equal &= Match<ILookupMarker>(nodes[i]) && ParentMatch<Rigidbody2D>(nodes[i]) && ReferenceEquals(c.attachedRigidbody, nodes[i].GetComponentInParent(typeof(Rigidbody2D)));
            }
        }
        Check(equal, "3200 generic/attached-body lookups match Type API after random reparent, activation, body addition and removal");
        Console.WriteLine("Component lookup regressions passed: " + checks);
    }
}
