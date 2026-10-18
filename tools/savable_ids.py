"""Resolve serialized room-specific save IDs without using shared prefab IDs."""

def context_ids(components):
 result={};conflicts=set()
 for component in components:
  for entry in component.get('_savableIDs',[]):
   ref=entry.get('SavableEntity',{})
   if ref.get('m_FileID')!=0 or not ref.get('m_PathID') or not entry.get('Id'):continue
   target=ref['m_PathID'];value=entry['Id']
   if target in result and result[target]!=value:conflicts.add(target)
   result[target]=value
 # Stale nested contexts can disagree for unrelated scene objects. Omit those
 # assignments rather than guessing; the catalog requires all statue IDs.
 return {target:value for target,value in result.items() if target not in conflicts}
