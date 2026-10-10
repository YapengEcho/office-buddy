"""Check native renderer output. Requires Pillow only for visual verification."""
from pathlib import Path
from PIL import Image, ImageChops
import sys
import json

folder=Path(sys.argv[1])
images={p.stem:Image.open(p).convert('RGBA') for p in folder.glob('*.png')}
neutral=images['neutral']
eyes=(288,579,372,614)
left=(297,615,333,664)
right=(335,615,366,664)
if (folder/'regions.json').exists():
    areas=json.loads((folder/'regions.json').read_text(encoding='utf-8-sig'))
    eyes,left,right=(tuple(areas[key]) for key in ('eyes','left','right'))

def differs(a,b,area):
    return ImageChops.difference(a.crop(area).convert('RGB'),b.crop(area).convert('RGB')).getbbox() is not None

for name in ('look-left','look-right','look-up','look-down','blink'):
    assert differs(neutral,images[name],eyes), f'{name}: no eye motion'
    assert not differs(neutral,images[name],left), f'{name}: unexpectedly moved left hand'
    assert not differs(neutral,images[name],right), f'{name}: unexpectedly moved right hand'
assert differs(images['look-left'],images['look-right'],eyes), 'gaze directions identical'
assert differs(neutral,images['left-hand'],left), 'left hand stayed still'
assert not differs(neutral,images['left-hand'],right), 'left input moved right hand'
assert differs(neutral,images['right-hand'],right), 'right hand stayed still'
assert not differs(neutral,images['right-hand'],left), 'right input moved left hand'
assert differs(neutral,images['both-hands'],left) and differs(neutral,images['both-hands'],right), 'simultaneous hands did not move'
assert differs(neutral,images['rest'],eyes), 'rest eyes did not close'
for image in images.values():
    assert image.size==(640,720)
    assert image.getpixel((0,0))[3]==0
print('PASS: rendered eye tracking, blink, independent left/right hands, simultaneous hands, rest eyes and transparency')
