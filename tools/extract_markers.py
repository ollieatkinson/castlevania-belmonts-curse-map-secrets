#!/usr/bin/env python3
"""Rebuild all-area factual markers from an installed game and the attributed reference snapshot."""
import argparse,subprocess,sys,hashlib
from pathlib import Path

def main():
 root=Path(__file__).resolve().parent;parser=argparse.ArgumentParser(description=__doc__)
 parser.add_argument('game',type=Path);parser.add_argument('output',type=Path);parser.add_argument('--cache',type=Path,default=root.parent/'.build/catalog')
 parser.add_argument('--reference',type=Path,default=root.parent/'data/reference-source.json');args=parser.parse_args();args.output.parent.mkdir(parents=True,exist_ok=True)
 fingerprint=hashlib.sha256()
 for path in [args.game/'GameAssembly.dll',args.game/"Castlevania Belmont's Curse_Data/StreamingAssets/aa/catalog.json"]:
  with path.open('rb') as source:
   while chunk:=source.read(1024*1024):fingerprint.update(chunk)
 args.cache=args.cache/fingerprint.hexdigest()[:16]
 args.cache.mkdir(parents=True,exist_ok=True)
 def run(name,*arguments):subprocess.run([sys.executable,str(root/name),*(str(a) for a in arguments)],check=True)
 run('extract_assets.py',args.game,args.cache)
 run('audit_rooms.py',args.game,args.cache/'map-assist-audit')
 run('calibrate_reference.py',args.cache,args.reference)
 run('match_reference.py',args.cache,args.reference)
 run('build_catalog.py',args.cache,args.game,args.output,'--reference',args.reference)
if __name__=='__main__':main()
