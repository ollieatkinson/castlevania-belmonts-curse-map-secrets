import runpy,pathlib,json,collections,re,UnityPy,sys
from savable_ids import context_ids
from clue_interactions import clue_bindings
x=runpy.run_path(str(pathlib.Path(__file__).with_name('native_util.py')))
g=pathlib.Path(sys.argv[1]);out=pathlib.Path(sys.argv[2]);out.mkdir(exist_ok=True)
b=g/"Castlevania Belmont's Curse_Data/StreamingAssets/aa/StandaloneWindows64"
counts=collections.Counter();scenes={s:b for s,b in x['catalog_entries'](g).items() if '/World_Biome_' in s}
assets=json.loads((out.parent/'map-assist-assets-cache.json').read_text())
for num,(scene,bundle) in enumerate(sorted(scenes.items()),1):
 target=out/(pathlib.Path(scene).stem+'.json')
 if target.exists() and json.loads(target.read_text()).get('schema')==5:continue
 e=UnityPy.load(str(b/bundle));gos=[];rootinfo=None
 for o in e.objects:
  if o.type.name=='MonoBehaviour':
   d=o.read_typetree()
   if 'worldRoomData' in d and '_root' in d:rootinfo=d;root=d['_root']['m_PathID'];file=o.assets_file
 # All gameplay objects in a room share its serialized scene file.
 if not rootinfo:raise ValueError(scene)
 # SavableIDContext assigns unique room IDs before the game loads entity state.
 # A prefab's serialized SavableEntityBase ID may be reused by every instance.
 components_by_id={o.path_id:o.read_typetree() for o in file.objects.values() if o.type.name=='MonoBehaviour'}
 assigned=context_ids(components_by_id.values())
 bindings=clue_bindings(list(components_by_id.values()),assets)
 interaction_ids={object_id for _,object_id in bindings};interaction_positions={}
 def pos(tid):
  p=(0.,0.,0.);seen=set()
  while tid!=root:
   if not tid or tid in seen:return None
   seen.add(tid);t=file.objects[tid].read_typetree();p=x['transformed'](p,t);tid=t['m_Father']['m_PathID']
  return p[:2]
 for o in list(file.objects.values()):
  if o.type.name!='GameObject':continue
  d=o.read_typetree();components=[];tid=None;context_id=''
  for c in d['m_Component']:
   co=file.objects[c['component']['m_PathID']]
   if co.type.name in ['Transform','RectTransform']:tid=co.path_id
   elif co.type.name=='MonoBehaviour':
    components.append(components_by_id[co.path_id])
    if co.path_id in assigned:context_id=assigned[co.path_id]
  save=next((c.get('Id') or c.get('m_SizeDelta') for c in components if 'saveStateTranslator' in c),None)
  interesting=any(any(k in c for k in ['resetableSecretZone','secretZoneToResetIndexes','itemData','closeUpData','clueItem','clueToGive','characterID','arcanaCodexData','virtueData','keyToSet','progressionKey']) for c in components)
  for comp in list(components):
   ref=comp.get('itemGiver')
   if ref and ref['m_FileID']==0 and ref['m_PathID']:
    components.append(components_by_id[ref['m_PathID']])
  if not save and not interesting and o.path_id not in interaction_ids:continue
  p=pos(tid)
  if p is None:continue
  if o.path_id in interaction_ids:interaction_positions[o.path_id]=p
  if not save and not interesting:continue
  name=re.sub(r'(\(Clone\)| \(\d+\))$','',d['m_Name']);counts[name]+=1
  gos.append(dict(name=d['m_Name'],id=save,contextId=context_id,x=p[0],y=p[1],components=components))
 interactions=[dict(itemId=item,x=interaction_positions[object_id][0],y=interaction_positions[object_id][1]) for item,object_id in bindings if object_id in interaction_positions]
 result=dict(schema=5,scene=scene,world=pathlib.Path(scene).parent.name,room=pathlib.Path(scene).parent.name+'_'+pathlib.Path(scene).stem,root=rootinfo,externals=[v.path for v in file.externals],objects=gos,clueInteractions=interactions)
 target.write_text(json.dumps(result))
 if num%25==0:print('Audited',num,'/',len(scenes),flush=True)
print('Cached',len(list(out.glob('*.json'))),'rooms',flush=True)
