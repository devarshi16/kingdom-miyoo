#!/usr/bin/env python3
"""Developer-only: export generic Unity class layouts, never read game files.

Input: UnityPy 1.25.4's generic Unity 2017 class database (MIT package).
The result contains no Kingdom scripts, objects, asset IDs, names or payloads.
"""
import argparse,struct
from UnityPy.enums import ClassIDType
from UnityPy.helpers.Tpk import get_typetree,get_typetree_node
from UnityPy.helpers.UnityVersion import UnityVersion
p=argparse.ArgumentParser(description=__doc__);p.add_argument('output');a=p.parse_args()
version=UnityVersion.from_str('2017.1.2p1');trees=[]
for cid in sorted(get_typetree().ClassInformation):
 try:n=get_typetree_node(cid,version)
 except (ValueError,AssertionError):continue
 trees.append((cid,list(n.traverse())))
def string(f,s):
 b=s.encode();f.write(struct.pack('<i',len(b)));f.write(b)
with open(a.output,'wb') as f:
 f.write(b'KNLT'+struct.pack('<II',1,len(trees)))
 for cid,nodes in trees:
  f.write(struct.pack('<ii',cid,len(nodes)))
  for n in nodes:
   string(f,ClassIDType(cid).name if n.m_Level==0 else n.m_Type);string(f,n.m_Name);f.write(struct.pack('<iii',n.m_Level,n.m_MetaFlag or 0,n.m_ByteSize))
print('Generic schema classes:',len(trees))
