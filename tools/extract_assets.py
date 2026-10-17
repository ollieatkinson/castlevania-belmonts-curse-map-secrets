"""Cache local metadata for the factual marker audit; cache files are never packaged."""
import argparse,json,re
from pathlib import Path
import UnityPy
from catalog_index import Catalog

def main():
 parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('game',type=Path);parser.add_argument('cache',type=Path);args=parser.parse_args();args.cache.mkdir(parents=True,exist_ok=True)
 catalog=Catalog(args.game);bundles=set()
 for i,p in catalog.assets():
  if not p.endswith('.asset'):continue
  if any(w in p for w in ['/Items/','/Charms/','/Arcana/','/VaniaMoves/','/Weapons/','/Codex/','/CompletionItemTypes/','/ArcanaCodexData/','/Narration/','/GPE/','/Loot/','/RoomDesigner/Rooms/']) or re.search(r'(CloseUp|MC_Progression|MainObjective|ProgressionKey)',p) or ('/Localization/' in p and p.endswith('_en.asset')):bundles.add(catalog.bundle(i))
 assets={};rooms={};english={}
 for index,b in enumerate(sorted(bundles),1):
  env=UnityPy.load(str(catalog.base/b))
  for o in env.objects:
   if o.type.name!='MonoBehaviour':continue
   d=o.read_typetree();key=str(o.path_id)
   assets[key]=dict(data=d,externals=[v.path for v in o.assets_file.externals],bundle=b)
   if 'containedNonResetableSecretZonesIndexes' in d or ('chunks' in d and 'configuration' in d):rooms[key]=d
   if 'm_TableData' in d and d.get('m_Name','').endswith('_en'):english[d['m_Name']]=d
  if index%25==0:print('Metadata bundles',index,'/',len(bundles),flush=True)
 for name,value in [('map-assist-assets-cache.json',assets),('map-assist-room-configs.json',rooms),('map-assist-english.json',english)]: (args.cache/name).write_text(json.dumps(value))
if __name__=='__main__':main()
