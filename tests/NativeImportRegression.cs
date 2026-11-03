// Synthetic transaction/recovery fixtures: no game files or extracted assets.
using System;using System.IO;using System.Linq;using System.Text;using System.Collections.Generic;
using KNL.Import;
class NativeImportRegression
{
    static int checks;
    static void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
    static void Put(string path, string value) { Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, value); }
    static void Tag(string folder, string id) { Put(Path.Combine(folder, ".native-install-id"), id); }
    static void Pack(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using (var w = new BinaryWriter(File.Create(path)))
        {
            var names = Importer.Serialized.Select(n => "objects/" + n).Concat(new[]{"sprites"}).ToArray();
            long offset = 12 + names.Sum(n => 2 + Encoding.UTF8.GetByteCount(n) + 12);
            w.Write(Encoding.ASCII.GetBytes("KNLP")); w.Write(1); w.Write(names.Length);
            foreach (string name in names) { byte[] b = Encoding.UTF8.GetBytes(name); w.Write((ushort)b.Length); w.Write(b); w.Write((ulong)offset); w.Write(name=="sprites"?12u:4u); offset += name=="sprites"?12:4; }
            foreach (string name in names) { w.Write(Encoding.ASCII.GetBytes(name=="sprites"?"KNLS":"KNLB")); if(name=="sprites"){w.Write(1);w.Write(2205);} }
        }
    }
    static void Stage(string stage)
    {
        Pack(Path.Combine(stage, "data", "kingdom.knlpack"));
        Put(Path.Combine(stage,"data","music","test.ogg"),"synthetic audio");
        foreach(string n in Importer.GameDlls) Put(Path.Combine(stage,"managed",n),"synthetic metadata "+n);
        foreach(string n in new[]{"UnityEngine.dll","Rewired_Core.dll","knl.dll"}) Put(Path.Combine(stage,"managed",n),"synthetic port "+n);
    }
    static void Main()
    {
        string test=Path.Combine(Path.GetTempPath(),"knl-import-regression-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(test);
        try
        {
            string lockPath=Path.Combine(test,".prepare.lock");
            using(var held=new PreparationLock(lockPath)) {
                bool busy=false;try{using(var other=new PreparationLock(lockPath)) { }}catch(IOException){busy=true;}
                Check(busy,"second preparation/check cannot acquire a live installation lock");
            }
            using(var released=new PreparationLock(lockPath)) Check(true,"closed preparation lock can be acquired again");
            string resource=Path.Combine(test,"clips.resource"),copy=Path.Combine(test,"copy.fsb");
            File.WriteAllBytes(resource,Enumerable.Range(0,100000).Select(i=>(byte)i).ToArray());
            var range=new Dictionary<string,object>{{"m_Source","archive:/clips.resource"},{"m_Offset",123L},{"m_Size",70000L}};
            Audio.knli_reset_cancel();Audio.CopyResource(test,range,copy);
            Check(File.ReadAllBytes(copy).SequenceEqual(File.ReadAllBytes(resource).Skip(123).Take(70000)),"audio range copy preserves exact bytes across buffer boundaries");
            range["m_Size"]=long.MaxValue;bool truncated=false;try{Audio.CopyResource(test,range,copy);}catch(InvalidDataException){truncated=true;}
            Check(truncated&&File.ReadAllBytes(copy).SequenceEqual(File.ReadAllBytes(resource).Skip(123).Take(70000)),"overflowing audio range fails before overwriting output");
            range["m_Size"]=70000L;Audio.knli_cancel();bool cancelled=false;try{Audio.CopyResource(test,range,copy);}catch(OperationCanceledException){cancelled=true;}
            Check(cancelled,"audio resource copying responds to cancellation");Audio.knli_reset_cancel();
            for(int step=0;step<7;step++)
            {
                string root=Path.Combine(test,"interrupt-"+step),id=new string('a',32),key=new string('b',64);
                string stage=Path.Combine(root,".prepare-v1",key),backup=Path.Combine(root,".install-rollback",id);
                Put(Path.Combine(root,"data","old"),"old data");Put(Path.Combine(root,"managed","old"),"old managed");Put(Path.Combine(root,"save","sentinel"),"precious save");
                Put(Path.Combine(root,".native-ready"),"prior receipt");Stage(stage);Tag(Path.Combine(stage,"data"),id);Tag(Path.Combine(stage,"managed"),id);Directory.CreateDirectory(backup);
                Put(Path.Combine(root,".install-journal"),id+"\n"+key);
                if(step>=1)File.Move(Path.Combine(root,".native-ready"),Path.Combine(backup,".native-ready"));
                if(step>=2)Directory.Move(Path.Combine(root,"data"),Path.Combine(backup,"data"));
                if(step>=3)Directory.Move(Path.Combine(stage,"data"),Path.Combine(root,"data"));
                if(step>=4)Directory.Move(Path.Combine(root,"managed"),Path.Combine(backup,"managed"));
                if(step>=5)Directory.Move(Path.Combine(stage,"managed"),Path.Combine(root,"managed"));
                if(step>=6)Put(Path.Combine(root,".native-ready"),id+"\n"+key+"\ninvalid incomplete receipt");
                Transaction.Recover(root);
                Check(File.ReadAllText(Path.Combine(root,"data","old"))=="old data"&&File.ReadAllText(Path.Combine(root,"managed","old"))=="old managed"&&File.ReadAllText(Path.Combine(root,".native-ready"))=="prior receipt", "interruption "+step+" rolls back both directories and the prior receipt");
                Check(File.ReadAllText(Path.Combine(root,"save","sentinel"))=="precious save"&&!File.Exists(Path.Combine(root,".install-journal")),"interruption "+step+" preserves save and clears recovered journal");
            }
            string committed=Path.Combine(test,"commit"),commitKey=new string('c',64),pending=Path.Combine(committed,".prepare-v1",commitKey);Stage(pending);Put(Path.Combine(committed,"save","sentinel"),"unchanged");
            Transaction.Commit(committed,pending,commitKey);Check(Transaction.IsReady(committed),"complete commit produces a validated owned-data receipt");
            Check(File.ReadAllText(Path.Combine(committed,"save","sentinel"))=="unchanged","successful commit preserves save");
            // Public runtime updates are independent of the owned-data receipt.
            Put(Path.Combine(committed,"managed","UnityEngine.dll"),"new public runtime with a different length");Check(Transaction.IsReady(committed),"public engine update does not require importing owned data again");
            string receipt=File.ReadAllText(Path.Combine(committed,".native-ready"));string currentId=receipt.Split('\n')[0];Put(Path.Combine(committed,".install-journal"),currentId+"\n"+commitKey);Transaction.Recover(committed);Check(Transaction.IsReady(committed),"durable completed receipt finishes recovery without rolling back");
            string owned=Path.Combine(committed,"managed",Importer.GameDlls[0]);var old=File.ReadAllText(owned);File.WriteAllText(owned,new string('x',old.Length));Check(!Transaction.IsReady(committed),"same-length owned assembly corruption is rejected");File.WriteAllText(owned,old);
            File.Delete(Path.Combine(committed,"data","music","test.ogg"));Check(!Transaction.IsReady(committed),"missing prepared music is rejected");
            string invalid=Path.Combine(test,"invalid-journal");Put(Path.Combine(invalid,".install-journal"),"../../not-a-transaction");Put(Path.Combine(invalid,"save","sentinel"),"safe");bool rejected=false;try{Transaction.Recover(invalid);}catch(InvalidDataException){rejected=true;}Check(rejected&&File.ReadAllText(Path.Combine(invalid,"save","sentinel"))=="safe","malformed journal is rejected without touching saves");
            Console.WriteLine("Native import recovery checks passed: "+checks);
        }
        finally { Directory.Delete(test,true); }
    }
}
