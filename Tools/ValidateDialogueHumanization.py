"""Focused content-only audit against the captured pre-polish assets; no Unity state changes."""
import json
import re
from pathlib import Path

root = Path(__file__).resolve().parents[1]
evidence = root / 'docs/evidence/dialogue-humanization'
before = json.loads((evidence / 'before.json').read_text(encoding='utf-8'))
before = {key.replace('\\', '/'): value for key, value in before.items()}
results = []


def check(label, condition):
    results.append(('PASS' if condition else 'FAIL') + ': ' + label)


def erase_fields(src, fields):
    for indent, field in fields:
        # Unity plain/quoted scalars can wrap on more-indented lines.
        src = re.sub(r'(?m)^' + ' ' * indent + re.escape(field)
                     + r': .*\n(?:' + ' ' * (indent + 2) + r'[^\n]*\n|\n)*',
                     ' ' * indent + field + ': <TEXT>\n', src)
    return src


catalog_path = 'Assets/Resources/ViCSIDialogueCatalog.asset'
old_catalog = before[catalog_path]
catalog = (root / catalog_path).read_text(encoding='utf-8-sig')
old_blocks = dict(re.findall(r'(?ms)^  - id: ([^\n]+)\n(.*?)(?=^  - id: |\Z)', old_catalog))
blocks = dict(re.findall(r'(?ms)^  - id: ([^\n]+)\n(.*?)(?=^  - id: |\Z)', catalog))
check('Original catalog IDs, channels, speakers, durations, priorities and queue/skip settings unchanged',
      all(k in blocks and erase_fields(v, [(4, 'text')]) == erase_fields(blocks[k], [(4, 'text')])
          for k, v in old_blocks.items()))
check('Only three catalog-backed closing radio entries added',
      set(blocks) - set(old_blocks) == {'ending-radio-opening', 'ending-radio-closing', 'ending-radio-farewell'})

for path, fields, label in [
    ('Assets/_Project/Data/Ending/Endings_CSI_Environment.asset', [(4, 'endingText')], 'Ending requirements, priorities, IDs and post-session explanation'),
    ('Assets/_Project/Data/Scenarios/HypothesisCheckpoints_CSI_Environment.asset', [(4, 'promptText'), (6, 'text')], 'HYP IDs, correctness, prerequisites and timing'),
    ('Assets/_Project/Scenes/Scenarios/CSI_Environment.unity', [(2, 'displayText')], 'Scene components, interaction ranges, statement gates, utility wiring and phase data')]:
    now = (root / path).read_text(encoding='utf-8-sig')
    check(label + ' unchanged', erase_fields(before[path], fields) == erase_fields(now, fields))

path = 'Assets/_Project/Scripts/Deduction/CompletionScreenUI.cs'
expected = before[path]
for name, key in [('OpeningLine', 'ending-radio-opening'), ('ClosingLine', 'ending-radio-closing'), ('FarewellLine', 'ending-radio-farewell')]:
    expected = re.sub(r'private const string ' + name + r' = .*?;',
                      f'private static string {name} => GameDialogue.Text("{key}");', expected)
check('Completion logic unchanged apart from three text lookups', expected == (root / path).read_text(encoding='utf-8-sig'))


def scalar(block):
    value = re.search(r'(?ms)^    text: (.*?)(?=^    duration:)', block)[1].strip()
    if value.startswith('"'):
        return json.loads(value)
    if value.startswith("'"):
        return re.sub(r'\s*\n\s*', ' ', value[1:-1]).replace("''", "'")
    return re.sub(r'\s*\n\s*', ' ', value)


texts = {key: scalar(block) for key, block in blocks.items()}
check('All catalog entries non-empty', all(t.strip() for t in texts.values()))
spoken = {k: t for k, t in texts.items() if k.startswith(('narr-', 'team-', 'FR_TURNOVER_', 'IOC_SOCO_', 'statement-', 'ioc-observation-', 'ending-radio-'))
          or k in ('ioc-assessment-guidance', 'soco-decision-reconsider')}
banned = re.compile(r'has not been processed|procedure has not|\brequirement\b|assessment condition|evidence state|\bsystem\b', re.I)
check('Active spoken catalog wording has no software-state phrases', all(not banned.search(t) for t in spoken.values()))
check('Vendor question unchanged', texts['vendor-followup-anyone-else'] == scalar(old_blocks['vendor-followup-anyone-else']))
check('No prepared/unused Peter account rewrites', all(texts[k] == scalar(old_blocks[k]) for k in ('statement-peter-appearance', 'statement-peter-money')))
failures = sum(line.startswith('FAIL') for line in results)
results.append(f'=== {"PASS" if not failures else "FAIL"}: {len(results)} focused content/structure checks, {failures} failures ===')
transcript = '\n'.join(results) + '\n'
(evidence / 'content-structure-checks.txt').write_text(transcript, encoding='utf-8')
print(transcript)
raise SystemExit(1 if failures else 0)
