import json,pathlib,collections,math,statistics,argparse
p=argparse.ArgumentParser();p.add_argument("cache",type=pathlib.Path);p.add_argument("reference",type=pathlib.Path);args=p.parse_args();BASE=args.cache
C=json.load(open(BASE/'map-assist-room-configs.json'));cal=json.load(open(BASE/'map-assist-calibration.json'));rooms=[]
for p in (BASE/'map-assist-audit').glob('*.json'):
 r=json.loads(p.read_text());rd=C[str(r['root']['worldRoomData']['roomData']['m_PathID'])];cfg=C[str(rd['configuration']['m_PathID'])];coord=r['root']['worldRoomData']['positionCoord'];cx=coord['x']*24;cy=coord['y']*13
 chunks=[(i%rd['_width'],i//rd['_width']) for i,c in enumerate(rd['chunks']) if c['enabled']]
 rooms.append(dict(room=r['room'],world=r['world'],cx=cx,cy=cy,width=rd['_width']*24,height=rd['_height']*13,chunks=chunks,config=cfg))
# Refine each reference transform from its independently matched equipment anchors.
for w,c in cal.items():
 ps=c['anchors'];mx=statistics.mean(p['gx'] for p in ps);my=statistics.mean(p['gy'] for p in ps);sx=statistics.mean(p['sx'] for p in ps);sy=statistics.mean(p['sy'] for p in ps)
 ax=sum((p['gx']-mx)*(p['sx']-sx) for p in ps)/sum((p['gx']-mx)**2 for p in ps);ay=sum((p['gy']-my)*(p['sy']-sy) for p in ps)/sum((p['gy']-my)**2 for p in ps)
 c['model']=[ax,ay,sx-ax*mx,sy-ay*my]
# The final area has only one unique equipment anchor. It uses the same capture scale.
# Keep this separately marked so coverage reports cannot imply multi-anchor verification.
# Source marker position: WeaponFistTier4, using the known website item title.
A=json.load(open(BASE/'map-assist-assets-cache.json'));I={int(k):v['data'] for k,v in A.items() if 'itemID' in v['data']};strings={t['m_Id']:t['m_Localized'] for v in json.load(open(BASE/'map-assist-english.json')).values() for t in v['m_TableData']};web=json.load(open(args.reference))['markers']['features']
for rr in rooms:
 if rr['world']!='World_Biome_11_MasterQuarter':continue
 raw=json.load(open(BASE/'map-assist-audit'/(rr['room'].split(rr['world']+'_',1)[1]+'.json')))
 for o in raw['objects']:
  for c in o['components']:
   i=I.get(c.get('itemData',{}).get('m_PathID'))
   if not i:continue
   title=strings.get(i['itemDisplayName']['m_TableEntryReference']['m_KeyId'],'')
   matches=[f for f in web if f['properties']['displayTitle'].lower()==title.lower()]
   if len(matches)==1:
    sx,sy=matches[0]['properties']['origPoints'][0];cal[rr['world']]=dict(model=[12,-12,sx-12*(rr['cx']+o['x']),sy+12*(rr['cy']+o['y'])],anchors=[],singleAnchor=True)
catids={'db-117843':'challenge','db-117787':'objective','db-117784':'upgrade','db-117779':'keepsake','db-117778':'ability','db-117782':'arcana','db-117783':'relic','db-117781':'weapon','db-117789':'clue','map-8242':'puzzle','map-8243':'lore','db-117770':'secret','db-117769':'wall'}
result=[];unmapped=[]
for f in web:
 p=f['properties'];kind=catids.get(p['catId'])
 if not kind:continue
 sx,sy=p['origPoints'][0];candidates=[]
 for r in rooms:
  if r['world'] not in cal:continue
  ax,ay,tx,ty=cal[r['world']]['model'];x=(sx-tx)/ax-r['cx'];y=(sy-ty)/ay-r['cy']
  dist=min((math.hypot(max(24*cx-x,0,x-24*(cx+1)),max(13*cy-y,0,y-13*(cy+1))) for cx,cy in r['chunks']),default=1e9)
  if dist<100:candidates.append((dist,r,x,y))
 if not candidates:unmapped.append(dict(siteId=p['id'],kind=kind,title=p['displayTitle'],sx=sx,sy=sy));continue
 candidates.sort(key=lambda a:a[0]);dist,r,x,y=candidates[0]
 result.append(dict(siteId=p['id'],kind=kind,title=p['displayTitle'],world=r['world'],room=r['room'],x=x,y=y,distance=dist,ambiguous=len([v for v in candidates if abs(v[0]-dist)<0.1])>1))
(BASE/'map-assist-reference-matched.json').write_text(json.dumps(result,indent=2));(BASE/'map-assist-reference-unmapped.json').write_text(json.dumps(unmapped,indent=2));(BASE/'map-assist-reference-transforms.json').write_text(json.dumps(cal,indent=2))
print('Matched',len(result),dict(collections.Counter(m['kind'] for m in result)));print('Unmapped',len(unmapped),dict(collections.Counter(m['kind'] for m in unmapped)));print('Ambiguous',sum(m['ambiguous'] for m in result))
