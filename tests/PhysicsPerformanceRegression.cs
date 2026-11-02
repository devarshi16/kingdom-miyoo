using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KNL;
using UnityEngine;

class PhysicsNeighborTracker : MonoBehaviour
{
    public readonly HashSet<int> near = new HashSet<int>();
    void OnTriggerEnter2D(Collider2D c) { near.Add(c.GetInstanceID()); }
    void OnTriggerStay2D(Collider2D c) { near.Add(c.GetInstanceID()); }
    void OnTriggerExit2D(Collider2D c) { near.Remove(c.GetInstanceID()); }
}
class PhysicsPerformanceRegression
{
    const BindingFlags Hidden = BindingFlags.Static | BindingFlags.NonPublic;
    static int checks;
    static void Check(bool ok, string description) { if (!ok) throw new Exception(description); Console.WriteLine("PASS: " + description); checks++; }
    static readonly List<BoxCollider2D> colliders = new List<BoxCollider2D>();
    static readonly List<Rigidbody2D> movers = new List<Rigidbody2D>();
    static readonly List<PhysicsNeighborTracker> trackers = new List<PhysicsNeighborTracker>();
    static readonly MethodInfo gather = typeof(Physics2DSystem).GetMethod("Gather", Hidden);
    static void Gather() { gather.Invoke(null, null); Physics2DSystem.Invalidate(); }
    static bool Eligible(Collider2D c) { return c != null && c.enabled && c.gameObject.activeInHierarchy; }
    static Aabb Box(Collider2D c) { return Physics2DSystem.AabbOf(c); }
    static bool Same(IList<Collider2D> a, IList<Collider2D> b) { return a.Count == b.Count && a.Zip(b, (x, y) => ReferenceEquals(x, y)).All(v => v); }
    static bool ExactOriginalSort()
    {
        var entries = (IList)typeof(Physics2DSystem).GetField("entries", Hidden).GetValue(null);
        var pool = (IList)typeof(Physics2DSystem).GetField("entryPool", Hidden).GetValue(null);
        var comparison = typeof(Physics2DSystem).GetField("s_EntryComparison", Hidden).GetValue(null);
        var reference = (IList)Activator.CreateInstance(entries.GetType());
        for (int i = 0; i < entries.Count; i++) reference.Add(pool[i]);
        reference.GetType().GetMethod("Sort", new[] { comparison.GetType() }).Invoke(reference, new[] { comparison });
        for (int i = 0; i < entries.Count; i++) if (!ReferenceEquals(entries[i], reference[i])) return false;
        return true;
    }
    static void Main()
    {
        World.ActiveScene = new SceneTag("physics performance regression");
        for (int i = 0; i < 320; i++)
        {
            var go = new GameObject("forest trigger " + i); go.layer = i % 4;
            go.transform.position = new Vector3((i % 80) / 2f, (i / 80) * 3, 0);
            var c = go.AddComponent<BoxCollider2D>(); c.isTrigger = true; c.size = new Vector2(1, 2); colliders.Add(c);
        }
        for (int i = 0; i < 24; i++)
        {
            var go = new GameObject("moving debris " + i); go.layer = i % 4;
            go.transform.position = new Vector3((i % 8) / 2f, (i / 8) * 3, 0);
            var body = go.AddComponent<Rigidbody2D>(); body.gravityScale = 0; movers.Add(body);
            var c = go.AddComponent<BoxCollider2D>(); c.isTrigger = true; c.size = new Vector2(1, 2); colliders.Add(c);
            trackers.Add(go.AddComponent<PhysicsNeighborTracker>());
        }
        bool sortOk = true, contactsOk = true, bodyListOk = true, maskChangesOk = true;
        for (int step = 0; step < 100; step++)
        {
            if (step % 3 == 0) for (int i = 0; i < movers.Count; i++) movers[i].position = new Vector2(((i + step) % 18) / 2f, (i % 3) * 3);
            if (step % 7 == 0) colliders[step].enabled = !colliders[step].enabled;
            if (step % 9 == 0) colliders[step].gameObject.layer = (colliders[step].gameObject.layer + 1) % 4;
            if (step % 11 == 0) colliders[step + 5].gameObject.SetActive(!colliders[step + 5].gameObject.activeSelf);
            if (step == 10) movers[0].simulated = false;
            if (step == 20) movers[0].simulated = true;
            Gather(); sortOk &= ExactOriginalSort();
            var expectedBodies = ((IList)typeof(Rigidbody2D).GetField("s_All", Hidden).GetValue(null)).Cast<Rigidbody2D>().Where(b => b != null && b.simulated && b.gameObject.activeInHierarchy).ToList();
            var actualBodies = ((IList)typeof(Physics2DSystem).GetField("bodies", Hidden).GetValue(null)).Cast<Rigidbody2D>().ToList();
            bodyListOk &= expectedBodies.Count == actualBodies.Count && expectedBodies.Zip(actualBodies, (a, b) => ReferenceEquals(a, b)).All(v => v);
            Physics2DSystem.Step();
            for (int i = 0; i < trackers.Count; i++)
            {
                var mine = colliders[320 + i];
                var expected = new HashSet<int>(colliders.Where(c => Eligible(c) && !ReferenceEquals(c, mine) && Box(c).Overlaps(Box(mine))).Select(c => c.GetInstanceID()));
                contactsOk &= expected.SetEquals(trackers[i].near);
            }
            var expectedMask = colliders.Where(c => Eligible(c)).Cast<Collider2D>().ToList(); expectedMask.Sort((a, b) => Box(a).x0.CompareTo(Box(b).x0));
            int changingMask = 1 << (step % 4);
            expectedMask = expectedMask.Where(c => ((1 << c.gameObject.layer) & changingMask) != 0).ToList();
            maskChangesOk &= Same(expectedMask, Physics2DSystem.QueryArea(new Vector2(-10, -10), new Vector2(100, 100), changingMask));
        }
        Check(bodyListOk, "persistent body snapshot retains exact active/simulated registry order across changes");
        Check(sortOk, "100 moving/tied/disabled/reactivated registries retain exact original framework-sort order");
        Check(maskChangesOk, "cached query masks retain exact order after movement, layer, activation and collider membership changes");
        Check(contactsOk, "all 24 moving bodies retain brute-force trigger neighbors across 100 dense-forest steps");
        Lifecycle.Frame(.02f); Gather();
        var ordered = colliders.Where(c => Eligible(c)).Cast<Collider2D>().ToList(); ordered.Sort((a, b) => Box(a).x0.CompareTo(Box(b).x0));
        var rng = new System.Random(918); bool queriesOk = true, buffersOk = true, circlesOk = true, raysOk = true;
        var buffer = new Collider2D[5];
        for (int sample = 0; sample < 200; sample++)
        {
            var point = new Vector2((float)rng.NextDouble() * 45, (float)rng.NextDouble() * 12 - 1);
            var end = point + new Vector2(2, 2); int mask = 1 << (sample % 4);
            var expectedPoint = ordered.Where(c => ((1 << c.gameObject.layer) & mask) != 0 && c.OverlapPoint(point)).ToList();
            var expectedArea = ordered.Where(c => ((1 << c.gameObject.layer) & mask) != 0 && Box(c).Overlaps(new Aabb { x0 = point.x, y0 = point.y, x1 = end.x, y1 = end.y })).ToList();
            queriesOk &= Same(expectedPoint, Physics2DSystem.QueryPoint(point, mask)) && Same(expectedArea, Physics2DSystem.QueryArea(point, end, mask));

            float radius = .25f + (sample % 7);
            var expectedCircle = ordered.Where(c => { var b = Box(c); float x = Mathf.Clamp(point.x, b.x0, b.x1), y = Mathf.Clamp(point.y, b.y0, b.y1); return ((1 << c.gameObject.layer) & mask) != 0 && (x - point.x) * (x - point.x) + (y - point.y) * (y - point.y) <= radius * radius; }).ToList();
            circlesOk &= Same(expectedCircle, Physics2DSystem.QueryCircle(point, radius, mask));
            var origin = new Vector2(-2, point.y);
            var expectedRay = ordered.Where(c => { var b = Box(c); return ((1 << c.gameObject.layer) & mask) != 0 && b.y0 <= origin.y && b.y1 >= origin.y && b.x1 >= origin.x && b.x0 <= origin.x + 50; }).ToList();
            expectedRay.Sort((a, b) => Mathf.Max(0, Box(a).x0 - origin.x).CompareTo(Mathf.Max(0, Box(b).x0 - origin.x)));
            raysOk &= Same(expectedRay, Physics2DSystem.RayAll(origin, Vector2.right, 50, mask, float.NegativeInfinity, float.PositiveInfinity).Select(hit => hit.collider).ToList());
            int n = Physics2D.OverlapAreaNonAlloc(point, end, buffer, mask);
            buffersOk &= n == Math.Min(buffer.Length, expectedArea.Count) && expectedArea.Take(n).Zip(buffer.Take(n), (a, b) => ReferenceEquals(a, b)).All(v => v);
            n = Physics2D.OverlapPointNonAlloc(point, buffer, mask);
            buffersOk &= n == Math.Min(buffer.Length, expectedPoint.Count) && expectedPoint.Take(n).Zip(buffer.Take(n), (a, b) => ReferenceEquals(a, b)).All(v => v);
        }
        Check(queriesOk, "200 bounded point/area queries preserve collider masks, coverage and result order");
        Check(circlesOk, "200 cached-mask circle queries retain exact coverage and order");
        Check(raysOk, "200 cached-mask ray queries retain exact hit ordering");
        Check(buffersOk, "NonAlloc queries preserve exact truncated result prefixes");
        bool manyMasksOk = true;
        for (int i = 0; i < 90; i++) { int mask = (i % 16) | (1 << (4 + i % 28)); var expected = ordered.Where(c => ((1 << c.gameObject.layer) & mask) != 0).ToList(); manyMasksOk &= Same(expected, Physics2DSystem.QueryArea(new Vector2(-10, -10), new Vector2(100, 100), mask)); }
        Check(manyMasksOk, "more than 32 distinct query masks preserve results through bounded-cache fallback");
        Check(Physics2D.OverlapAreaNonAlloc(Vector2.zero, Vector2.one, new Collider2D[0]) == 0, "empty NonAlloc output is accepted");
        var moved = new GameObject("same-frame moved point collider"); moved.transform.position = new Vector3(1000, 0, 0);
        var movedCollider = moved.AddComponent<BoxCollider2D>();
        Physics2DSystem.Invalidate(); Physics2DSystem.QueryPoint(new Vector2(200, 0), -1);
        moved.transform.position = new Vector3(200, 0, 0);
        Check(Physics2DSystem.QueryPoint(new Vector2(200, 0), -1).Contains(movedCollider), "point query observes same-frame movement after an earlier gather");
        Check(Physics2D.OverlapPointNonAlloc(new Vector2(200, 0), buffer) == 1 && ReferenceEquals(buffer[0], movedCollider), "NonAlloc point query also observes same-frame movement");
        for (int i = 0; i < 10; i++) { Physics2D.OverlapAreaNonAlloc(Vector2.zero, Vector2.one, buffer); Physics2D.OverlapPointNonAlloc(Vector2.zero, buffer); }
        long bytes = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) { Physics2D.OverlapAreaNonAlloc(Vector2.zero, Vector2.one, buffer); Physics2D.OverlapPointNonAlloc(Vector2.zero, buffer); }
        Check(GC.GetAllocatedBytesForCurrentThread() == bytes, "2000 warm NonAlloc queries allocate no managed memory");
        Console.WriteLine("Physics performance regressions passed: " + checks);
    }
}
