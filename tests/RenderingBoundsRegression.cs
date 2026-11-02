using System;
using KNL;
using UnityEngine;

class RenderingBoundsRegression
{
    static int passed;
    static void Check(bool value, string label)
    {
        if (!value) throw new Exception("FAIL: " + label);
        Console.WriteLine("PASS: " + label); passed++;
    }
    static bool Near(float a, float b) { return Math.Abs(a - b) < .0001f; }
    static void Main()
    {
        World.ActiveScene = new SceneTag("rendering-regression");
        var water = new GameObject("Water");
        water.transform.position = new Vector3(0, -.75f, -.5f);
        water.transform.localScale = new Vector3(16, -2, 1);
        var filter = water.AddComponent<MeshFilter>();
        filter.sharedMesh = new Mesh { vertices = new[] {
            new Vector3(-.5f,-.5f,0), new Vector3(.5f,.5f,0),
            new Vector3(.5f,-.5f,0), new Vector3(-.5f,.5f,0) } };
        var renderer = water.AddComponent<MeshRenderer>();
        Check(Near(renderer.bounds.max.y, .25f), "Original inverted water quad supplies its top edge to ReflectedLight");
        Check(Near(renderer.bounds.min.y, -1.75f), "Water bounds retain the lower edge under negative scale");
        Check(Near(renderer.bounds.size.x, 16), "Mesh bounds include the quad width");
        filter.sharedMesh.vertices = new[] { new Vector3(-2,-1,-3), new Vector3(1,3,2), new Vector3(5,-4,1) };
        var parent = new GameObject("Mirrored rotated parent");
        parent.transform.rotation = Quaternion.Euler(21, 13, 47);
        parent.transform.localScale = new Vector3(-2, 3, 4);
        water.transform.SetParent(parent.transform, false);
        var min = new Vector3(float.MaxValue,float.MaxValue,float.MaxValue);
        var max = new Vector3(float.MinValue,float.MinValue,float.MinValue);
        foreach (var vertex in filter.sharedMesh.vertices)
        {
            var point = water.transform.TransformPoint(vertex);
            min = new Vector3(Math.Min(min.x,point.x),Math.Min(min.y,point.y),Math.Min(min.z,point.z));
            max = new Vector3(Math.Max(max.x,point.x),Math.Max(max.y,point.y),Math.Max(max.z,point.z));
        }
        var bounds = renderer.bounds;
        Check(Near(bounds.min.x,min.x) && Near(bounds.min.y,min.y) && Near(bounds.min.z,min.z)
            && Near(bounds.max.x,max.x) && Near(bounds.max.y,max.y) && Near(bounds.max.z,max.z),
            "All mesh vertices participate in rotated mirrored world bounds");
        filter.sharedMesh.Clear();
        Check(Near(renderer.bounds.size.magnitude,0), "Empty meshes have empty bounds");
        var main = new GameObject("Main Camera").AddComponent<Camera>();
        main.cullingMask = 0x1fffffe9; // Original level2 Main Camera.
        var reflection = new GameObject("Reflection Camera").AddComponent<Camera>();
        reflection.cullingMask = 1 << 29;
        Check(CameraIncludes(main,0) && CameraIncludes(main,8) && CameraIncludes(main,14), "Main camera retains world and enemy layers");
        Check(!CameraIncludes(main,29), "Reflection-only sprites cannot leak into the main camera");
        Check(CameraIncludes(reflection,29) && !CameraIncludes(reflection,0), "Reflection camera honors its separate layer mask");
        main.cullingMask = unchecked((int)0x80000000);
        Check(CameraIncludes(main,31) && !CameraIncludes(main,0), "Signed camera masks preserve layer 31");
        Check(!CameraIncludes(main,-1) && !CameraIncludes(main,32), "Invalid layer indices do not wrap their bit shift");
        Console.WriteLine("Rendering bounds regressions passed: " + passed);
    }
    static bool CameraIncludes(Camera camera,int layer) { return RenderSystem.CameraIncludesLayer(camera,layer); }
}
