using System;
using System.Reflection;
using KNL;
using UnityEngine;

class DisableAnimationWriter : MonoBehaviour {
    void OnDisable() { GetComponent<Animator>().Play("Die", 0, 1); }
}
class AnimatorPoolingRegression {
    static int passed;
    static void Check(bool ok,string name) {if(!ok)throw new Exception(name);passed++;Console.WriteLine("PASS: "+name);}
    static AnimationClip Clip(Sprite sprite) {
        var data=new ClipData {length=1,stop=1,constCount=1,constants=new[]{0f},pptrs=new UnityEngine.Object[]{sprite}};
        typeof(ClipData).GetField("totalCurves",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(data,1);
        data.bindings.Add(new ClipBinding {typeID=212,pptr=true,curveIndex=0,curveCount=1});
        return new AnimationClip {m_Data=data};
    }
    static GameObject Actor(Sprite upright,Sprite dead) {
        var data=new AnimControllerData();data.clips.Add(Clip(upright));data.clips.Add(Clip(dead));
        var layer=new AnimLayer {defaultState=0};
        layer.states.Add(new AnimState {nameId=Animator.StringToHash("Stand"),fullPathId=Animator.StringToHash("Stand"),clipIndex=0});
        layer.states.Add(new AnimState {nameId=Animator.StringToHash("Die"),fullPathId=Animator.StringToHash("Die"),clipIndex=1});data.layers.Add(layer);
        var controller=new AnimControllerAsset();typeof(AnimControllerAsset).GetField("data",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(controller,data);
        var go=new GameObject("pooled actor");go.AddComponent<SpriteRenderer>().sprite=upright;
        go.AddComponent<Animator>().runtimeAnimatorController=controller;return go;
    }
    static void Main() {
        World.ActiveScene=new SceneTag("animator pooling test");Lifecycle.AnimatorStep=AnimationSystem.Step;
        var upright=new Sprite {name="upright"};var terminal=new Sprite {name="terminal death pose"};
        var go=Actor(upright,terminal);var animator=go.GetComponent<Animator>();var renderer=go.GetComponent<SpriteRenderer>();
        animator.Play("Die",0,4);Check(renderer.sprite==terminal,"death animation selects its terminal sprite before pooling");
        go.SetActive(false);Check(animator.m_Rt==null,"object deactivation discards evaluated animation state");
        go.SetActive(true);Lifecycle.Frame(1f/30);Check(animator.GetCurrentAnimatorStateInfo(0).shortNameHash==Animator.StringToHash("Stand")&&renderer.sprite==upright,"reactivated live actor evaluates default upright animation");
        bool reset=true;
        for(int i=0;i<20;i++){animator.Play("Die",0,4);go.SetActive(false);go.SetActive(true);Lifecycle.Frame(1f/30);reset &= renderer.sprite==upright&&animator.GetCurrentAnimatorStateInfo(0).shortNameHash==Animator.StringToHash("Stand");}
        Check(reset,"twenty death-pose pool cycles cannot retain a terminal sprite");
        animator.Play("Die",0,4);var pausedRuntime=animator.m_Rt;animator.enabled=false;
        Check(ReferenceEquals(pausedRuntime,animator.m_Rt),"pool repair leaves the separate component-disable path unchanged");
        animator.enabled=true;Lifecycle.Frame(1f/30);Check(renderer.sprite==terminal,"component reenable preserves the previous adapter behavior pending separate reference validation");
        var parent=new GameObject("pool parent");go.transform.SetParent(parent.transform,false);animator.Play("Die",0,4);parent.SetActive(false);
        Check(animator.m_Rt==null,"parent deactivation resets descendant Animator");parent.SetActive(true);Lifecycle.Frame(1f/30);
        go.AddComponent<DisableAnimationWriter>();go.SetActive(false);Check(animator.m_Rt==null,"reset occurs after OnDisable callbacks that write animation");go.SetActive(true);Lifecycle.Frame(1f/30);
        animator.Play("Die",0,4);var originalRuntime=animator.m_Rt;var clone=UnityEngine.Object.Instantiate(go);var clonedAnimator=clone.GetComponent<Animator>();
        Check(clonedAnimator.m_Rt==null,"cloning a warm Animator does not retain the source runtime and targets");
        Lifecycle.Frame(1f/30);Check(clone.GetComponent<SpriteRenderer>().sprite==upright&&renderer.sprite==terminal&&ReferenceEquals(originalRuntime,animator.m_Rt),"clone default animation changes only the clone while source retains its pose");
        Check(Lifecycle.Exceptions==0,"pool lifecycle fixtures have no runtime exceptions");Console.WriteLine("Animator pooling regressions passed: "+passed);
    }
}
