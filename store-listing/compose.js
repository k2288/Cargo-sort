const { chromium } = require('playwright'); const fs=require('fs');
const caps=[["Tap to load","the trucks!"],["Plan ahead.","Don't fill the dock!"],["Endless levels","with more colors"],["Ship every","truck to win"],["Stuck?","Grab an extra slot"]];
const accents=["#ffcc33","#5bd16a","#3e8ef7","#ff8a2b","#f04e4e"];
(async()=>{ const b=await chromium.launch(); const p=await b.newPage({viewport:{width:1080,height:1920}});
 for(let i=0;i<5;i++){ const img=fs.readFileSync(`raw${i+1}.png`).toString('base64');
  await p.setContent(`<html><head><link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Lilita+One&family=Nunito:wght@800&display=swap"><style>
  body{margin:0;width:1080px;height:1920px;background:linear-gradient(#2c4670,#182638);overflow:hidden;font-family:'Lilita One';color:#fff;text-align:center}
  h1{margin:0;padding-top:110px;font-size:120px;line-height:1;font-weight:400}
  h2{margin:18px 0 0;font:800 64px Nunito;color:${accents[i]}}
  .ph{position:absolute;left:50%;top:420px;transform:translateX(-50%);width:810px;height:1440px;border-radius:64px;overflow:hidden;box-shadow:0 30px 80px rgba(0,0,0,.5);border:14px solid #0e1622}
  .ph img{width:100%;height:100%;display:block}</style></head><body><h1>${caps[i][0]}</h1><h2>${caps[i][1]}</h2><div class="ph"><img src="data:image/png;base64,${img}"></div></body></html>`);
  await p.evaluate(()=>document.fonts.ready); await p.waitForTimeout(300);
  await p.screenshot({path:`screenshot-${i+1}.png`}); }
 await b.close(); })();
