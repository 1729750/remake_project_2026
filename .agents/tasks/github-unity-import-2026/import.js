const fs=require('fs'), path=require('path'), cp=require('child_process');
const target='c:\\Git-Directory\\UpnL_FirstGame\\remake_project_2026';
const run=path.join(target,'.agents','tasks','github-unity-import-2026');
const temp=path.join(run,'temp-remote');
const excluded=p=>{const q=p.replaceAll('\\','/');return /(^|\/)(\.git|Library|obj|Binaries|Temp|Build|UserSettings|\.vs|\.kiro)(\/|$)/.test(q)||/(^|\/)[^/]+\.(csproj|user|vsconfig)$/.test(q)||/^\.agents\/tasks\/github-unity-import-2026(\/|$)/.test(q)};
const rels=repo=>cp.execFileSync('git',['-C',repo,'-c','core.quotePath=false','ls-files'],{encoding:'utf8'}).split(/\r?\n/).map(x=>x.trim()).filter(x=>x&&!excluded(x));
const remote=rels(temp), local=rels(target); const localSet=new Set(local.map(x=>x.toLowerCase()));
for(const p of remote){const src=path.join(temp,...p.split('/')), dst=path.join(target,...p.split('/'));fs.mkdirSync(path.dirname(dst),{recursive:true});fs.copyFileSync(src,dst)}
const remoteSet=new Set(remote.map(x=>x.toLowerCase()));
for(const p of local)if(!remoteSet.has(p.toLowerCase())){const f=path.join(target,...p.split('/'));if(fs.existsSync(f)&&fs.statSync(f).isFile())fs.rmSync(f)}
console.log(JSON.stringify({copied:remote.length,deleted:local.filter(p=>!remoteSet.has(p.toLowerCase())).length}));
