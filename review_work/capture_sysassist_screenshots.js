const { chromium } = require('playwright-core');
const fs = require('fs');
const path = require('path');
(async () => {
  const out = path.resolve('screenshots');
  fs.mkdirSync(out, { recursive: true });
  const browser = await chromium.launch({ headless: true, executablePath: 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe' });
  const page = await browser.newPage({ viewport: { width: 1440, height: 980 }, deviceScaleFactor: 1 });
  page.setDefaultTimeout(25000);
  const loginResp = await page.request.post('http://34.107.8.65/api/auth/login', { data: { login: 'admin', password: 'SysAssist.Demo@2026!' } });
  if (!loginResp.ok()) throw new Error('login failed ' + loginResp.status() + ' ' + await loginResp.text());
  const session = await loginResp.json();
  await page.goto('http://34.107.8.65/', { waitUntil: 'domcontentloaded', timeout: 45000 });
  await page.evaluate((session) => localStorage.setItem('sysassist.session', JSON.stringify({ token: session.accessToken, user: session.user })), session);
  await page.reload({ waitUntil: 'networkidle', timeout: 45000 });
  await page.waitForTimeout(2500);
  async function shot(name) {
    await page.screenshot({ path: path.join(out, name + '.png'), fullPage: true });
    const h1 = await page.locator('h1,h2').first().innerText().catch(()=>page.url());
    console.log('SHOT', name, h1);
  }
  async function clickLabel(labels) {
    for (const label of labels) {
      const re = new RegExp(label, 'i');
      const button = page.getByRole('button', { name: re }).first();
      if (await button.count().catch(()=>0)) { await button.click(); return true; }
      const text = page.getByText(re).first();
      if (await text.count().catch(()=>0)) { await text.click(); return true; }
    }
    return false;
  }
  console.log('VISIBLE TEXT SAMPLE:', (await page.locator('body').innerText()).slice(0,1000).replace(/\s+/g,' '));
  await shot('01_dashboard');
  const targets = [
    ['02_modules', ['Modules']],
    ['03_events', ['Events', 'Incidents']],
    ['04_approvals', ['Approvals']],
    ['05_diagnostics', ['Diagnostics', 'Production readiness', 'Readiness']]
  ];
  for (const [name, labels] of targets) {
    const ok = await clickLabel(labels);
    console.log('NAV', name, ok);
    await page.waitForLoadState('networkidle').catch(()=>{});
    await page.waitForTimeout(1800);
    await shot(name);
  }
  await browser.close();
})().catch(err => { console.error(err); process.exit(1); });
