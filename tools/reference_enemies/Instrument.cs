using System;using System.Linq;using System.IO;using Mono.Cecil;using Mono.Cecil.Cil;
class Instrument {
 static void Main(string[] a) {
  if(a.Length!=3)throw new ArgumentException("Expected original managed directory, harness DLL, new output DLL path");
  if(Path.GetFullPath(a[2])==Path.GetFullPath(Path.Combine(a[0],"Assembly-CSharp.dll")))throw new ArgumentException("Output must differ from original assembly path; use an isolated copied game");
  var resolver=new DefaultAssemblyResolver();resolver.AddSearchDirectory(a[0]);
  var p=new ReaderParameters {AssemblyResolver=resolver};
  using(var asm=AssemblyDefinition.ReadAssembly(a[0]+"/Assembly-CSharp.dll",p))
  using(var hook=AssemblyDefinition.ReadAssembly(a[1],p)) {
   var helper=hook.MainModule.Types.Single(t=>t.Name=="OriginalEnemyVisibility");
   var loader=asm.MainModule.Types.Single(t=>t.Name=="Loader").Methods.Single(m=>m.Name=="Awake");
   loader.Body.GetILProcessor().InsertBefore(loader.Body.Instructions[0],Instruction.Create(OpCodes.Call,asm.MainModule.ImportReference(helper.Methods.Single(m=>m.Name=="Ensure"))));
   asm.Write(a[2]);
  }
 }
}
