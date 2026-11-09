const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const cp = require('child_process');
const target = 'c:\\Git-Directory\\UpnL_FirstGame\\remake_project_2026';
const run = path.join(target, '.agents', 'tasks', 'github-unity-import-2026');
const temp = path.join(run, 'temp-remote');
const backupRoot = path.join(run, 'local-backup');
function git(repo, args) { return cp.execFileSync('git', ['-C', repo, ...args], {encoding:'utf8'}); }
function norm(p) { p=p.trim(); if(p.startsWith('"')&&p.endsWith('"')) { try { p=JSON.parse(p); } catch {} } return p.replaceAll('\\','/'); }
function excluded(p) { const q=norm(p); return /(^|\/)(\.git|Library|obj|Binaries|Temp|Build|UserSettings|\.vs|\.kiro)(\/|$)/.test(q) || /(^|\/)[^/]+\.(csproj|user|vsconfig)$/.test(q) || /^\.agents\/tasks\/github-unity-import-2026(\/|$)/.test(q); }
function files(repo) { return git(repo,['-c','core.quotePath=false','ls-files']).split(/\r?\n/).map(norm).filter(p=>p&&!excluded(p)); }
function sha(file) { return crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex'); }
function full(root, rel) { return path.join(root, ...rel.split('/')); }
const remote=files(temp), local=files(target), rset=new Set(remote.map(p=>p.toLowerCase())), lset=new Set(local.map(p=>p.toLowerCase()));
const untracked=[];
for (const line of git(target,['status','--porcelain']).split(/\r?\n/)) { if(!line.startsWith('?? ')) continue; const rel=norm(line.slice(3)); const f=full(target,rel); if(fs.existsSync(f)&&fs.statSync(f).isFile()&&!excluded(rel)) untracked.push(rel); else if(fs.existsSync(f)&&fs.statSync(f).isDirectory()) for(const x of fs.readdirSync(f,{recursive:true})) { const a=norm(path.relative(target,path.join(f,x))); if(fs.statSync(path.join(f,x)).isFile()&&!excluded(a)) untracked.push(a); } }
const add=remote.filter(p=>!lset.has(p.toLowerCase())).sort(), replace=remote.filter(p=>lset.has(p.toLowerCase())).sort(), del=local.filter(p=>!rset.has(p.toLowerCase())).sort(), differs=replace.filter(p=>sha(full(target,p))!==sha(full(temp,p)));
const localOnly=[...new Set(untracked)].sort();
fs.writeFileSync(path.join(run,'classify.json'),JSON.stringify({add,replace,replaceDiffers:differs,delete:del,localOnlyPreserved:localOnly},null,2));
fs.rmSync(backupRoot,{recursive:true,force:true}); fs.mkdirSync(backupRoot,{recursive:true});
const entries=[];
for(const [arr,reason] of [[differs,'replaced'],[localOnly,'local-only']]) for(const rel of arr) { const src=full(target,rel), dst=full(backupRoot,rel); fs.mkdirSync(path.dirname(dst),{recursive:true}); fs.copyFileSync(src,dst); const a=sha(src), b=sha(dst); if(a!==b) throw new Error('Backup hash mismatch: '+rel); entries.push({path:rel,reason,bytes:fs.statSync(src).size,sha256:a}); }
fs.writeFileSync(path.join(backupRoot,'manifest.json'),JSON.stringify({backupRoot,createdAt:new Date().toISOString(),entries},null,2));
const summary=`# Unity import setup summary\n\n- Remote URL: https://github.com/muffinhead03/remake_project_2026_HyeGyoVersion\n- Default branch: develop\n- SHA: 68cacbb071658f0fcca2e3fce0efcd26a325b31e\n- Counts: add ${add.length}, replace ${replace.length}, replaceDiffers ${differs.length}, delete ${del.length}, localOnlyPreserved ${localOnly.length}\n- Backup entry count: ${entries.length}\n- Backup root: ${backupRoot}\n`;
fs.writeFileSync(path.join(run,'setup-summary.md'),summary); console.log(summary);
