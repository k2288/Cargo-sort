"""Builds web/index.html, the browser version of Cargo Sort, from web-preview/index.html.

The browser version has no store: no shop, no coin packs, no starter pack, no piggy bank, no
ads (the free daily gift opens without one) and no developer tools. Run: python3 web/build.py
"""
import os, re

here = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(here, "..", "web-preview", "index.html"), encoding="utf-8").read()

# The Old/New skin switcher is only for the preview: always use the new look.
src, n = re.subn(r"<script>\s*\(function\(\)\{var m=location\.search\.match\(/\[\?&\]skin=.*?</script>\s*",
                 '<script>document.body.dataset.skin="saga";</script>\n', src, count=1, flags=re.S)
assert n == 1, "skin switcher not found"

CSS = """
/* ---- Web version: nothing that needs a store, an ad or a payment ---- */
#coinPlus, .sbtn:has(#shopHome), #starterSide, #starterPop, #shop,
#claim3, #revive, #claim2, #winPiggy, #restoreBtn, #privacyBtn, #devForm, .skinbar { display: none !important; }
"""
JS = """
// ---- Web version ----
fakeAd = (label, done) => done();          // no ads: the free gift and rewards just open
store.set("noads", true);                  // no ads between levels
starterLeft = () => 0;                     // no starter pack offer
openShop = () => {};
renderHome();
"""
assert src.count("</style>") >= 1 and src.rstrip().endswith("</script>")
src = src.replace("</style>", CSS + "</style>", 1)
i = src.rstrip().rfind("</script>")
src = src[:i] + JS + src[i:]
src = src.replace("<title>Cargo Sort</title>", "<title>Cargo Sort</title>\n<meta name=\"description\" content=\"Sort the cargo, load the trucks and travel the world.\">", 1)
# App icon (180 px, embedded so the page is one file).
import base64, io
from PIL import Image
im = Image.open(os.path.join(here, "..", "store-listing", "icon.png")).convert("RGB").resize((180, 180), Image.LANCZOS)
buf = io.BytesIO(); im.save(buf, "PNG", optimize=True)
uri = "data:image/png;base64," + base64.b64encode(buf.getvalue()).decode()
src = src.replace("<title>Cargo Sort</title>", f'<title>Cargo Sort</title>\n<link rel="icon" href="{uri}">\n<link rel="apple-touch-icon" href="{uri}">', 1)
open(os.path.join(here, "index.html"), "w", encoding="utf-8").write(src)
print("web/index.html written,", len(src), "bytes")
