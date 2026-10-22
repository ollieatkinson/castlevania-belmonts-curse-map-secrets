import copy
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'tools'))
from clue_interactions import clue_bindings


def ref(rid, kind, **data):
    return dict(rid=rid, type={'class': kind}, data=data)


def graph(*refs):
    return {'references': {'RefIds': list(refs)}}


def variable(guid):
    return {'UseRef': 1, 'ValueRef': {'_guid': guid}}


class ClueInteractionsTest(unittest.TestCase):
    def setUp(self):
        self.assets = {
            '10': {'data': {'itemID': 'Clue_Ferryman_MissingCoins', 'itemType': 5}},
            '20': {'data': graph(
                ref(1, 'ResolverGiveItemNodeData', ItemData=variable('item')),
                ref(2, 'ResolverActivateTriggerNodeData', Trigger=variable('trigger')))},
            '30': {'data': graph(
                ref(3, 'ResolverPlaySubResolverNodeData',
                    SubResolverData={'m_PathID': 20},
                    VariablesContainer={'_variables': [{'rid': 4}, {'rid': 5}]}),
                ref(4, 'ItemDataVariable', _guid='item', Value={'m_PathID': 10}),
                ref(5, 'CutsceneTriggerVariable', _guid='trigger', Value='spirit'))},
        }
        self.components = [
            {'_resolverData': {'m_PathID': 30}},
            {'UniqueIDs': {'_items1': ['spirit'], '_items2': [{'m_FileID': 0, 'm_PathID': 99}]}},
        ]

    def test_spirit_reward_resolves_to_interaction_not_resolver_origin(self):
        self.assertEqual(clue_bindings(self.components, self.assets), [('Clue_Ferryman_MissingCoins', 99)])

    def test_condition_mentioning_a_clue_does_not_create_a_pickup(self):
        self.assets['20']['data']['references']['RefIds'][0]['type']['class'] = 'ResolverConditionHasItemNodeData'
        self.assertEqual(clue_bindings(self.components, self.assets), [])

    def test_two_calls_keep_their_own_item_and_trigger_bindings(self):
        self.assets['11'] = {'data': {'itemID': 'Clue_Other', 'itemType': 5}}
        second = copy.deepcopy(self.assets['30']['data']['references']['RefIds'])
        for node in second:
            node['rid'] += 10
        second[0]['data']['VariablesContainer']['_variables'] = [{'rid': 14}, {'rid': 15}]
        second[1]['data']['Value']['m_PathID'] = 11
        second[2]['data']['Value'] = 'other'
        self.assets['30']['data']['references']['RefIds'].extend(second)
        self.components.append({'UniqueIDs': {'_items1': ['other'], '_items2': [{'m_FileID': 0, 'm_PathID': 100}]}})
        self.assertEqual(clue_bindings(self.components, self.assets),
                         [('Clue_Ferryman_MissingCoins', 99), ('Clue_Other', 100)])

    def test_missing_or_ambiguous_scene_target_is_not_guessed(self):
        self.assertEqual(clue_bindings(self.components[:1], self.assets), [])
        self.components.append({'UniqueIDs': {'_items1': ['spirit'], '_items2': [{'m_FileID': 0, 'm_PathID': 100}]}})
        self.assertEqual(clue_bindings(self.components, self.assets), [])

    def test_non_clue_and_deprecated_items_are_ignored(self):
        self.assets['10']['data']['itemType'] = 1
        self.assertEqual(clue_bindings(self.components, self.assets), [])
        self.assets['10']['data'].update(itemType=5, isDeprecated=True)
        self.assertEqual(clue_bindings(self.components, self.assets), [])

    def test_duplicate_controller_does_not_duplicate_the_marker(self):
        self.components.append(self.components[0])
        self.assertEqual(clue_bindings(self.components, self.assets), [('Clue_Ferryman_MissingCoins', 99)])


if __name__ == '__main__':
    unittest.main()
