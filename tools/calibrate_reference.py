import json,pathlib,re,collections,itertools,math,statistics,argparse
p=argparse.ArgumentParser();p.add_argument("cache",type=pathlib.Path);p.add_argument("reference",type=pathlib.Path);args=p.parse_args();BASE=args.cache
A=json.load(open(BASE/'map-assist-assets-cache.json'));C=json.load(open(BASE/'map-assist-room-configs.json'));T=json.load(open(BASE/'map-assist-english.json'))
strings={r['m_Id']:r['m_Localized'] for t in T.values() for r in t['m_TableData']};items={int(k):v['data'] for k,v in A.items() if 'itemID' in v['data']}
def norm(s):return ''.join(c.lower() for c in s if c.isalnum())
site=json.load(open(args.reference))['markers']['features'];web=collections.defaultdict(list)
for f in site:
 p=f['properties'];web[norm(p['displayTitle'])].append(p)
points=collections.defaultdict(list);seen=set()
for path in (BASE/'map-assist-audit').glob('*.json'):
 r=json.loads(path.read_text());rd=C[str(r['root']['worldRoomData']['roomData']['m_PathID'])];coord=r['root']['worldRoomData']['positionCoord'];size=rd['chunkSize']
 for o in r['objects']:
  for comp in o['components']:
   ref=comp.get('itemData') or comp.get('clueItem')
   if not ref:continue
   item=items.get(ref['m_PathID']);
   if not item:continue
   title=strings.get(item['itemDisplayName']['m_TableEntryReference']['m_KeyId'],'');matches=web[norm(title)]
   if len(matches)!=1:continue
   key=(r['room'],item['itemID'])
   if key in seen:continue
   seen.add(key);s=matches[0];points[r['world']].append(dict(title=title,room=r['room'],gx=coord['x']*size['x']+o['x'],gy=coord['y']*size['y']+o['y'],sx=s['origPoints'][0][0],sy=s['origPoints'][0][1],siteId=s['id']))
result={}
for world,ps in points.items():
 best=[];model=None
 for a,b in itertools.combinations(ps,2):
  if abs(a['gx']-b['gx'])<20 or abs(a['gy']-b['gy'])<20:continue
  ax=(a['sx']-b['sx'])/(a['gx']-b['gx']);ay=(a['sy']-b['sy'])/(a['gy']-b['gy'])
  if ax<=0 or ay>=0 or abs(abs(ax/ay)-1)>0.15:continue
  tx=a['sx']-ax*a['gx'];ty=a['sy']-ay*a['gy']
  good=[p for p in ps if math.hypot(p['sx']-(ax*p['gx']+tx),p['sy']-(ay*p['gy']+ty))<60]
  if len(good)>len(best):best=good;model=(ax,ay,tx,ty)
 print(world,'matched',len(ps),'inliers',len(best),'model',model)
 if len(best)>=3:result[world]=dict(model=model,anchors=best)
(BASE/'map-assist-calibration.json').write_text(json.dumps(result,indent=2))
