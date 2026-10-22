"""Resolve clue rewards to scene interaction objects, rather than room centres."""


def references(data):
    return {r['rid']: r for r in data.get('references', {}).get('RefIds', [])}


def clue_rewards(resolver, assets):
    """Bind the item and trigger parameters of actual clue-grant subresolvers.

    A clue mentioned in a condition is not a reward. Only GiveItem nodes in
    the called graph establish the reward; ActivateTrigger identifies its
    interaction. Bindings belong to that call, not the whole resolver.
    """
    refs = references(resolver)
    for node in refs.values():
        if node.get('type', {}).get('class') != 'ResolverPlaySubResolverNodeData':
            continue
        data = node.get('data', {})
        sub = assets.get(str(data.get('SubResolverData', {}).get('m_PathID')), {}).get('data', {})
        variables = {}
        for ref in data.get('VariablesContainer', {}).get('_variables', []):
            variable = refs.get(ref['rid'], {}).get('data', {})
            variables[variable.get('_guid')] = variable.get('Value')
        items, triggers = set(), set()
        for instruction in references(sub).values():
            kind = instruction.get('type', {}).get('class')
            field = 'ItemData' if kind == 'ResolverGiveItemNodeData' else 'Trigger' if kind == 'ResolverActivateTriggerNodeData' else None
            if field is None:
                continue
            value = instruction.get('data', {}).get(field, {})
            value = variables.get(value.get('ValueRef', {}).get('_guid')) if value.get('UseRef') else value.get('Value')
            if field == 'Trigger' and isinstance(value, str) and value:
                triggers.add(value)
            elif field == 'ItemData' and isinstance(value, dict):
                item = assets.get(str(value.get('m_PathID')), {}).get('data', {})
                if item.get('itemType') == 5 and not item.get('abstractItem') and not item.get('isDeprecated'):
                    items.add(item['itemID'])
        # Multiple triggers in a generic graph cannot establish an exact pickup.
        if len(triggers) == 1:
            for item in sorted(items):
                yield item, next(iter(triggers))


def clue_bindings(components, assets):
    """Link grant triggers through the scene's UniqueIDs registry to GameObjects."""
    targets = {}
    for component in components:
        ids = component.get('UniqueIDs', {})
        for guid, ref in zip(ids.get('_items1', []), ids.get('_items2', [])):
            if ref.get('m_FileID') == 0 and ref.get('m_PathID'):
                targets.setdefault(guid, set()).add(ref['m_PathID'])
    result = set()
    for component in components:
        resolver = assets.get(str(component.get('_resolverData', {}).get('m_PathID')), {}).get('data', {})
        for item, trigger in clue_rewards(resolver, assets):
            objects = targets.get(trigger, set())
            if len(objects) == 1:
                result.add((item, next(iter(objects))))
    return sorted(result)
