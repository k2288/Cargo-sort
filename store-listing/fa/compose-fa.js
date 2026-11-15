// Persian store screenshots (1080x1920) from raw captures.
const { chromium } = require('playwright'); const fs=require('fs');
const caps=[["بزن و بار بزن!","جعبه را به کامیون هم‌رنگ برسان"],["از قبل فکر کن","نگذار اسکله پر شود!"],["چالش‌های تازه","جعبه‌های مخفی و ستون یخ‌زده"],["همه را راهی کن","و سکه جایزه بگیر"],["گیر کردی؟","یک جای اضافه بگیر"]];
const accents=["#ffcc33","#5bd16a","#3e8ef7","#ff8a2b","#f04e4e"];
(async()=>{ const b=await chromium.launch(); const p=await b.newPage({viewport:{width:1080,height:1920}});
 for(let i=0;i<5;i++){ const img=fs.readFileSync(`raw${i+1}.png`).toString('base64');
  await p.setContent(`<html dir="rtl" lang="fa"><head><meta charset="utf-8"><link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Vazirmatn:wght@800;900&display=swap"><style>
  body{margin:0;width:1080px;height:1920px;background:linear-gradient(#2c4670,#182638);overflow:hidden;font-family:Vazirmatn;color:#fff;text-align:center}
  h1{margin:0;padding-top:100px;font-size:118px;line-height:1.15;font-weight:900}
  h2{margin:10px 0 0;font:800 62px Vazirmatn;color:${accents[i]}}
  .ph{position:absolute;left:50%;top:420px;transform:translateX(-50%);width:810px;height:1440px;border-radius:64px;overflow:hidden;box-shadow:0 30px 80px rgba(0,0,0,.5);border:14px solid #0e1622}
  .ph img{width:100%;height:100%;display:block}</style></head><body><h1>${caps[i][0]}</h1><h2>${caps[i][1]}</h2><div class="ph"><img src="data:image/png;base64,${img}"></div></body></html>`);
  await p.evaluate(()=>document.fonts.ready); await p.waitForTimeout(400);
  await p.screenshot({path:`screenshot-fa-${i+1}.png`}); }
 // Feature graphic 1024x500: same art as the English one, mirrored, with Persian title.
 const art=fs.readFileSync('../art.html','utf8')
   .replace('family=Lilita+One&family=Nunito:wght@800','family=Lilita+One&family=Nunito:wght@800&family=Vazirmatn:wght@800;900')
   .replace(`Promise.all([document.fonts.load("80px 'Lilita One'"),document.fonts.load("800 40px Nunito")])`,`Promise.all([document.fonts.load("900 80px Vazirmatn"),document.fonts.load("800 40px Vazirmatn")])`)
   .replace(`ctx.fillStyle="#fff";ctx.font="104px 'Lilita One'";ctx.fillText("Cargo Sort",56,130);`,`ctx.save();ctx.translate(1024,0);ctx.scale(-1,1);`)
   .replace(`ctx.fillStyle="#ffcc33";ctx.font="800 38px Nunito";ctx.fillText("Tap. Sort. Ship it!",62,186);`,``)
   .replace(`ctx.quadraticCurveTo(330,190,440,280);ctx.stroke();`,`ctx.quadraticCurveTo(330,190,440,280);ctx.stroke();ctx.restore();ctx.setLineDash([]);ctx.textAlign="right";ctx.direction="rtl";ctx.fillStyle="#fff";ctx.font="900 112px Vazirmatn";ctx.fillText("بار بزن!",968,150);ctx.fillStyle="#ffcc33";ctx.font="800 40px Vazirmatn";ctx.fillText("بزن، بچین، راهی کن!",964,215);`);
 const fp=await b.newPage({viewport:{width:1024,height:500}});
 await fp.setContent(art.replace('location.hash.slice(1)','"feature"')); await fp.waitForFunction(()=>document.title==='ready'); await fp.waitForTimeout(300);
 await fp.locator('#c').screenshot({path:'feature-fa.png'});
 await b.close(); })();
