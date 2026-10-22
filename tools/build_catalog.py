"""Build factual marker mappings from local room/asset audits and an optional guide cross-check."""
import json,pathlib,collections,math,re,hashlib,sys,argparse,base64
p=argparse.ArgumentParser();p.add_argument("cache",type=pathlib.Path);p.add_argument("game",type=pathlib.Path);p.add_argument("output",type=pathlib.Path)
p.add_argument('--reference',type=pathlib.Path,default=pathlib.Path(__file__).resolve().parent.parent/'data/reference-source.json');args=p.parse_args()
reference_source=json.loads(args.reference.read_text())
BASE=args.cache
A=json.load(open(BASE/'map-assist-assets-cache.json'));C=json.load(open(BASE/'map-assist-room-configs.json'));T=json.load(open(BASE/'map-assist-english.json'))
STR={r['m_Id']:r['m_Localized'] for t in T.values() for r in t['m_TableData']}
ROOMS={r['room']:r for r in [json.loads(p.read_text()) for p in (BASE/'map-assist-audit').glob('*.json')]}
ITEMS={int(k):v['data'] for k,v in A.items() if 'itemID' in v['data']};BYID={v['itemID']:v for v in ITEMS.values()}
KEYS=base64.b64decode(json.loads((args.game/"Castlevania Belmont's Curse_Data/StreamingAssets/aa/catalog.json").read_text())['m_KeyDataString'])
KINDS={1:'weapon',2:'arcana',3:'relic',4:'ability',5:'clue',6:'keepsake'}
CODEX={d['characterID']['m_PathID']:d for v in A.values() if 'arcanaSpell' in (d:=v['data']) and 'characterID' in d}
CHAR_FOR_KEY={}
for codex in CODEX.values():
 key={'Leon_CodexData':'CatacombsGateKeyLeon','JoanArc_CodexData':'CatacombsGateKeyJeanne','Medusa_CodexData':'CatacombsGateKeyMedusa'}.get(codex['m_Name'])
 if key:CHAR_FOR_KEY[key]=A[str(codex['characterID']['m_PathID'])]['data']['_guid']
markers=[];definitions={};native_keys=set();secrets=[];configs={}
def definition(item):
 id=item['itemID'];ref=item.get('itemDisplayIconAtlased',{})
 if not ref.get('m_AssetGUID'):ref=item.get('itemDisplayIconRef',{})
 if id=='WhipPendulum':ref=dict(m_AssetGUID='c3b03f3ba25cd4c4ea04a936f913539e',m_SubObjectName='PlayerStatus_VaniaIcons_Whip')
 if id=='UniqueItemID' and ref.get('m_AssetGUID')=='67e1a3e022b9adc419060f76672c0fce':ref=dict(ref,m_SubObjectName='T_UI_Items_GenericCharmIcon')
 if ref.get('m_AssetGUID','').encode() not in KEYS:ref={}
 definitions[id]=dict(kind=KINDS.get(item['itemType'],'clue'),iconGuid=ref.get('m_AssetGUID',''),iconName=ref.get('m_SubObjectName',''),name=STR.get(item['itemDisplayName']['m_TableEntryReference']['m_KeyId'],id))
 return id
for item in ITEMS.values():
 if item['itemType'] in KINDS:definition(item)
def add(r,kind,x,y,save='',item='',state='activated',secret=-1,keys=None,name='',source='game',verified=True,uid=None):
 m=dict(id=uid or f"{r['room']}:{kind}:{save or item}:{round(x,2)}:{round(y,2)}",world=r['world'],room=r['room'],kind=kind,x=round(x,4),y=round(y,4),saveId=save,itemId=item,state=state,secretIndex=secret,progressKeys=keys or [],name=name,source=source,verified=verified)
 if item in CHAR_FOR_KEY:m['characterGuid']=CHAR_FOR_KEY[item]
 markers.append(m);return m
for room,r in sorted(ROOMS.items()):
 rd=C[str(r['root']['worldRoomData']['roomData']['m_PathID'])];cfg=C[str(rd['configuration']['m_PathID'])];configs[room]=cfg
 chunks=[(i%rd['_width'],i//rd['_width']) for i,c in enumerate(rd['chunks']) if c['enabled']]
 center=min(chunks,key=lambda c:math.hypot(c[0]-rd['_width']/2,c[1]-rd['_height']/2)) if chunks else (0,0);center=(center[0]*24+12,center[1]*13+6.5)
 physical={};reset=set()
 for o in r['objects']:
  for c in o['components']:reset.update(c.get('secretZoneToResetIndexes',[]))
 for o in r['objects']:
  n=o['name'];save=o['id'] or ''
  if save and n.startswith(('GPE_UpgradeCollectibleHealth','GPE_UpgradeCollectibleMana','GPE_ConsumableAtkUpBook','GPE_ConsumableDefUpBook','GPE_DestructibleSourceExperience')):
   kind='health' if 'CollectibleHealth' in n else 'mana' if 'CollectibleMana' in n else 'experience' if 'SourceExperience' in n else 'upgrade'
   # Keep statue marker IDs stable, but read completion using the unique ID
   # assigned by the room's SavableIDContext, never the shared prefab value.
   if kind=='experience':
    actual=o.get('contextId','')
    if not actual or actual==save:raise ValueError(f"Missing unique experience-source ID in {room}")
    m=add(r,kind,o['x'],o['y'],save=actual,state='activated',name=n,
      uid=f"{r['room']}:{kind}::{round(o['x'],2)}:{round(o['y'],2)}")
   else:m=add(r,kind,o['x'],o['y'],save=save,state='activated',name=n)
   if 'AtkUp' in n:m['symbol']='attack'
   elif 'DefUp' in n:m['symbol']='defence'
  if save and n.startswith('GPE_EncounterGate_MainEntity'):add(r,'challenge',o['x'],o['y'],save=save,state='encounter',name='Combat room')
  if save and n.startswith('GPE_DestructibleWall'):
   actual=o.get('contextId','')
   if not actual or actual==save:raise ValueError(f"Missing unique breakable-wall ID in {room}")
   # Preserve marker IDs used by existing observation files, as for statues.
   add(r,'wall',o['x'],o['y'],save=actual,name='Breakable wall',
     uid=f"{room}:wall:{save}:{round(o['x'],2)}:{round(o['y'],2)}")
  for c in o['components']:
   if 'resetableSecretZone' in c:
    opt=not(c['resetableSecretZone'] or c['isSecretZoneNoSfx'] or c['secretZoneIndex'] in reset)
    secrets.append(dict(world=r['world'],room=room,index=c['secretZoneIndex'],optional=opt,x=o['x'],y=o['y'],size=c.get('size',{}),tiles=c.get('tilesLocalPositions',[])))
   ref=c.get('itemData') or c.get('clueItem')
   if ref and ref['m_PathID'] in ITEMS:
    item=ITEMS[ref['m_PathID']]
    if item['itemType'] not in KINDS or item.get('abstractItem') or item.get('isDeprecated'):continue
    id=item['itemID'];old=physical.get(id)
    if old is None or (save and not old['id']):physical[id]=o
   if 'closeUpData' in c and c['closeUpData'].get('m_PathID'):
    # Close-ups are not inventory items. Reading one is tracked by the mod per save.
    if n.startswith('GPE_Sign_CloseUp'):
     add(r,'lore',o['x'],o['y'],state='observed',name='Lore',uid=f"{room}:lore:{round(o['x'],2)}:{round(o['y'],2)}")
 for id,o in physical.items():
  if id not in cfg['containedItemsID']:continue
  item=BYID[id];add(r,KINDS[item['itemType']],o['x'],o['y'],save=o['id'] or '',item=id,state='item',name=definitions[id]['name']);native_keys.add((room,id))
 # The room configuration is authoritative for scripted item rewards and clues.
 for id in cfg['containedItemsID']:
  if (room,id) in native_keys or id not in BYID:continue
  item=BYID[id]
  if item['itemType'] not in KINDS or item.get('abstractItem') or item.get('isDeprecated'):continue
  add(r,KINDS[item['itemType']],*center,item=id,state='item',name=definitions[id]['name'],source='game-room');native_keys.add((room,id))
 for ref in cfg['containedBossArcana']+cfg['containedAbsorbableArcana']:
  codex=CODEX.get(ref['m_PathID'])
  if not codex:continue
  for field in ['arcanaSpell','arcanaVaniaMove']:
   item=ITEMS.get(codex[field]['m_PathID'])
   if not item or item['itemType'] not in KINDS or item['itemType']==4 or (room,item['itemID']) in native_keys:continue
   id=item['itemID'];add(r,KINDS[item['itemType']],*center,item=id,state='item',name=definitions[id]['name'],source='game-room');native_keys.add((room,id))
for room,r in sorted(ROOMS.items()):
 cfg=configs[room]
 for ref in cfg['containedBossArcana']:
  codex=CODEX.get(ref['m_PathID'])
  if not codex:continue
  key={'Leon_CodexData':'CatacombsGateKeyLeon','JoanArc_CodexData':'CatacombsGateKeyJeanne','Medusa_CodexData':'CatacombsGateKeyMedusa'}.get(codex['m_Name'])
  if not key or (room,key) in native_keys:continue
  rd=C[str(r['root']['worldRoomData']['roomData']['m_PathID'])]
  add(r,'keepsake',rd['_width']*12,rd['_height']*6.5,item=key,state='item',name=definitions[key]['name'],source='game-room')
  native_keys.add((room,key))
# Resolver-driven clues (including spirit dialogue) point to the actual
# interaction trigger. Preserve the old room marker's identity when moving it.
for room,r in sorted(ROOMS.items()):
 positioned=set()
 for interaction in r.get('clueInteractions',[]):
  item=interaction['itemId'];position=(item,round(interaction['x'],4),round(interaction['y'],4))
  if position in positioned or item not in configs[room]['containedItemsID']:continue
  positioned.add(position)
  existing=next((m for m in markers if m['room']==room and m['itemId']==item and m['kind']=='clue'),None)
  if existing is None:continue
  if existing['source']=='game-room':
   existing.update(x=position[1],y=position[2],source='game')
  elif existing['x']!=position[1] or existing['y']!=position[2]:
   add(r,'clue',position[1],position[2],item=item,state='item',name=definitions[item]['name'])
# Prefer native pickup positions; guide-only categories retain an explicit confidence flag.
reference=json.load(open(BASE/'map-assist-reference-matched.json'));all_reference=len(reference);linked=collections.Counter();uncertain=[]
SECRET_BY_ROOM=collections.defaultdict(list)
for s in secrets:
 if s['optional']:SECRET_BY_ROOM[s['room']].append(s)
# Only guide-confirmed hidden paths are marked. Do not turn every concealment volume into a POI.
for ref in reference:
 kind=ref['kind'];room=ref['room'];r=ROOMS[room];x,y=ref['x'],ref['y'];near=[m for m in markers if m['room']==room and m['kind']==kind]
 if kind in ['weapon','relic','ability','keepsake','arcana','clue','upgrade']:
  # Native definitions cover these categories independently of guide completeness.
  continue
 if kind=='lore':
  coord=r['root']['worldRoomData']['positionCoord'];gx=x+coord['x']*24;gy=y+coord['y']*13
  candidates=[m for m in markers if m['kind']=='lore' and m['source']=='game' and m['world']==r['world']]
  def lore_distance(m):
   p=ROOMS[m['room']]['root']['worldRoomData']['positionCoord'];return math.hypot(m['x']+p['x']*24-gx,m['y']+p['y']*13-gy)
  if candidates:
   native=min(candidates,key=lore_distance)
   if lore_distance(native)<5:
    native['corroboratedLore']=True;linked[kind]+=1;continue
 if kind in ['challenge','wall'] and near:
  best=min(near,key=lambda m:math.hypot(m['x']-x,m['y']-y))
  if math.hypot(best['x']-x,best['y']-y)<18:linked[kind]+=1;continue
 verified=not ref['ambiguous'] and ref['distance']<=5
 state='unknown';secret=-1;save='';keys=[]
 if kind=='secret':
  coord=r['root']['worldRoomData']['positionCoord'];gx=x+coord['x']*24;gy=y+coord['y']*13
  zones=[s for s in secrets if s['optional'] and s['world']==r['world']]
  def distance(s):
   p=ROOMS[s['room']]['root']['worldRoomData']['positionCoord'];ox=s['x']+p['x']*24;oy=s['y']+p['y']*13
   return min((math.hypot(max(ox+t['x']-gx,0,gx-ox-t['x']-1),max(oy+t['y']-gy,0,gy-oy-t['y']-1)) for t in s['tiles']),default=1e9)
  if zones:
   s=min(zones,key=distance)
   if distance(s)<5:
    secret=s['index'];state='secret';linked[kind]+=1
    r=ROOMS[s['room']];room=r['room'];p=r['root']['worldRoomData']['positionCoord'];x=gx-p['x']*24;y=gy-p['y']*13
    verified=True
  verified &= secret>=0
 if kind=='puzzle' and ref['siteId']==197125:
  keys=[v['data']['_guid'] for v in A.values() if v['data'].get('m_Name')=='GPE_StLouisScale_Progression_Done'];state='progression'
 elif kind=='puzzle' and ref['siteId']==196172:
  keys=[v['data']['_guid'] for v in A.values() if v['data'].get('m_Name')=='MC_Progression_LibraryRoom10_PlanetariumActivated'];state='progression'
 elif kind=='objective' and ref['siteId']==196525:
  keys=[v['data']['_guid'] for v in A.values() if v['data'].get('m_Name')=='MC_Narration_NotreDamePaintingPuzzle_Solved'];state='progression'
 if not verified:uncertain.append(dict(siteId=ref['siteId'],kind=kind,room=room,distance=ref['distance'],ambiguous=ref['ambiguous']))
 add(r,kind,x,y,state=state,secret=secret,keys=keys,name=ref['title'] or kind.title(),source='reference',verified=verified,uid='reference:'+str(ref['siteId']))
# Stable duplicate elimination for signs with multiple components and inherited pickup entities.
markers=list({m['id']:m for m in markers if m['kind']!='lore' or m['source']!='game' or m.pop('corroboratedLore',False)}.values())
# Native scene copies of the final area intentionally share pickup GUIDs. Each area retains its own marker.
result=dict(schema=2,gameAssemblySha256=hashlib.file_digest((args.game/'GameAssembly.dll').open('rb'),'sha256').hexdigest(),markers=markers,items=definitions,
 reference=dict(url=reference_source['source'],date=reference_source['retrieved'],crossChecked=all_reference),areas=sorted({r['world'] for r in ROOMS.values()}),roomsAudited=dict(collections.Counter(r['world'] for r in ROOMS.values())))
args.output.write_text(json.dumps(result,indent=2)+'\n')
report=dict(rooms=len(ROOMS),areas=result['areas'],markers=dict(collections.Counter(m['kind'] for m in markers)),native=sum(m['source'].startswith('game') for m in markers),referenceLinked=dict(linked),unverified=uncertain,unknownCompletion=dict(collections.Counter(m['kind'] for m in markers if m['state']=='unknown')))
args.output.with_name('coverage.json').write_text(json.dumps(report,indent=2)+'\n');print({k:v for k,v in report.items() if k not in ['areas','unverified']});print('Unverified marker mappings',len(uncertain))
