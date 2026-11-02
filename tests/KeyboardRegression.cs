using System;
using System.Reflection;
using KNL;
using UnityEngine;
using UnityEngine.UI;
class KeyboardRegression {
    static int count;
    static void Check(bool ok,string message) { if(!ok)throw new Exception("FAIL: "+message);count++;Console.WriteLine("PASS: "+message); }
    static void Main() {
        Check(!TouchScreenKeyboard.isSupported,"Unsupported OS touch keyboard reports its actual platform capability");
        World.ActiveScene=new SceneTag("keyboard regression");var go=new GameObject("active InputField");go.SetActive(false);
        // Serialized UI objects use RectTransform. Construct that exact fixture without
        // requiring proprietary scenes, fonts or a visible native window.
        var rect=go.AddComponent<RectTransform>();typeof(GameObject).GetField("m_Transform",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(go,rect);
        var input=go.AddComponent<InputField>();go.SetActive(true);input.text="Shield";
        typeof(InputField).GetField("m_AllowInput",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(input,true);
        Check(input.isFocused&&input.isActiveAndEnabled,"Real UI.InputField fixture is active and focused");
        int before=Lifecycle.Exceptions;for(int i=0;i<50;i++)Lifecycle.Frame(.02f);
        Check(Lifecycle.Exceptions==before,"Focused InputField LateUpdate runs repeatedly without touch-keyboard stub exceptions");
        Check(input.text=="Shield"&&input.isFocused,"Unsupported touch keyboard cannot clear text or close hardware-input focus");
        go.SetActive(false);for(int i=0;i<3;i++)Lifecycle.Frame(.02f);
        Check(Lifecycle.Exceptions==before,"Leaving the input field does not enter unsupported keyboard APIs");
        Console.WriteLine("Keyboard regressions passed: "+count);
    }
}
