import xml.etree.ElementTree as ET, sys, os
sys.stdout.reconfigure(encoding='utf-8')
G = r'C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\Modules'
files = [os.path.join(G, 'BannerKings.Redux', 'ModuleData', 'workshops.xml'), os.path.join(G, 'SandBox', 'ModuleData', 'spworkshops.xml')]
for mod in os.listdir(G):
    md = os.path.join(G, mod, 'ModuleData')
    if not os.path.isdir(md) or mod in ('BannerKings.Redux', 'SandBox'):
        continue
    for root, dirs, fs in os.walk(md):
        for f in fs:
            if 'workshop' in f.lower() and f.lower().endswith('.xml'):
                files.append(os.path.join(root, f))
food = {'grain', 'bread', 'beer', 'wine', 'grape', 'olives', 'oil', 'fish', 'meat', 'cheese', 'butter', 'honey', 'date_fruit', 'Egg', 'apple', 'carrot',
        'orange', 'salt', 'cow', 'hog', 'sheep', 'goose', 'chicken', 'WhaleMeat', 'mead', 'fruit', 'vegetables', 'milk', 'dates'}
for fp in files:
    if not os.path.isfile(fp):
        continue
    print('#####', os.path.relpath(fp, G))
    try:
        t = ET.parse(fp).getroot()
    except Exception as e:
        print('  blad XML', e); continue
    for wt in t.iter('WorkshopType'):
        wid = wt.get('id')
        for p in wt.iter('Production'):
            ins = [((i.get('input') or i.get('input_item') or '?').replace('ItemCategory.', ''), i.get('input_count', '1')) for i in p.iter('Input')]
            outs = [((o.get('output') or o.get('output_item') or '?').replace('ItemCategory.', ''), o.get('output_count', '1')) for o in p.iter('Output')]
            names = {a for a, _ in ins} | {a for a, _ in outs}
            if names & food:
                print(f"  {wid:16s} speed {p.get('conversion_speed', '?'):5s} | " + ' + '.join(f'{c}x{a}' for a, c in ins) + '  ->  ' + ' + '.join(f'{c}x{a}' for a, c in outs))
