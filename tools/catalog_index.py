"""Read the installed Unity Addressables catalog without game binaries or external services."""
import base64,json,struct
from pathlib import Path

class Catalog:
 def __init__(self,game):
  self.game=Path(game);self.base=self.game/"Castlevania Belmont's Curse_Data/StreamingAssets/aa/StandaloneWindows64"
  self.cat=json.loads((self.base.parent/'catalog.json').read_text())
  data=base64.b64decode(self.cat['m_EntryDataString']);bucket=base64.b64decode(self.cat['m_BucketDataString'])
  self.entries=[struct.unpack_from('<7i',data,4+28*i) for i in range(struct.unpack_from('<i',data)[0])]
  self.buckets=[];offset=4
  for _ in range(struct.unpack_from('<i',bucket)[0]):
   _,count=struct.unpack_from('<2i',bucket,offset);offset+=8
   self.buckets.append(struct.unpack_from('<'+'i'*count,bucket,offset));offset+=4*count
 def path(self,i):return self.cat['m_InternalIds'][self.entries[i][0]]
 def bundle(self,i):return self.path(self.buckets[self.entries[i][2]][0]).split('\\')[-1]
 def assets(self):
  seen=set()
  for i in range(len(self.entries)):
   path=self.path(i)
   if path not in seen:seen.add(path);yield i,path
