// Raw Persian gameplay captures from the browser preview (run from store-listing/fa).
const { chromium } = require('playwright');
const fs = require('fs');
(async () => {
  const b = await chromium.launch();
  const html = fs.readFileSync('/home/user/cargo-sort/web-preview/index.html','utf8');
  const p = await b.newPage({ viewport: { width: 400, height: 711 }, deviceScaleFactor: 2.7 });
  await p.setContent('<!doctype html><html><head><meta charset="utf-8"><meta name="viewport" content="width=device-width"><style>[hidden]{display:none!important}</style></head><body>'+html+'</body></html>');
  await p.waitForTimeout(1200);
  await p.evaluate(()=>{ lang='fa'; applyLanguage(); $('daily').hidden=true; pendingDaily=0; coins=240; });
  const snap = async (n) => { await p.screenshot({ path: `raw${n}.png` }); };
  const tapCol = async (c) => { const pt = await p.evaluate(c=>{const [x,y]=colPos(c,0);const r=cv.getBoundingClientRect();return {x:r.left+sx(x)/dpr,y:r.top+sy(y)/dpr}},c); await p.mouse.click(pt.x,pt.y); };
  const solveSteps = async (n) => { for (let i=0;i<n;i++){ const m = await p.evaluate(()=>game.status==='playing'?pickMove(game.clone()):-1); if(m<0)break; await tapCol(m); await p.waitForTimeout(900);} };
  const hideToast = () => p.evaluate(()=>{ $('toast').hidden=true; });
  // 1: level 1 with the tutorial hand
  await p.evaluate(()=>start(1)); await p.waitForTimeout(800); await snap(1);
  // 2: level 9 mid-play, crate in flight
  await p.evaluate(()=>{ store.set('tutDone', true); start(9); }); await p.waitForTimeout(700); await solveSteps(9);
  { const m = await p.evaluate(()=>pickMove(game.clone())); await tapCol(m); await p.waitForTimeout(90); await hideToast(); await snap(2); await p.waitForTimeout(900); }
  // 3: hard level with hidden crates and ice
  await p.evaluate(()=>start(45)); await p.waitForTimeout(700); await solveSteps(1); await hideToast(); await snap(3);
  // 4: win with confetti, then the panel
  await p.evaluate(()=>start(3)); await p.waitForTimeout(700); await solveSteps(60); await p.waitForTimeout(1100); await snap(4);
  await p.evaluate(()=>{ $('win').hidden=true; pendingWin=0; });
  // 5: lose panel
  await p.evaluate(()=>{ start(10); let r=1; while(game.status==='playing'){ const cs=[...game.columns.keys()].filter(c=>game.canTap(c)); const c=cs[(r=(r*16807)%2147483647)%cs.length]; history.push(game.clone()); game.tap(c);} view=cloneView(game); afterMove(); });
  await p.waitForTimeout(900); await hideToast(); await snap(5);
  await b.close();
})();
