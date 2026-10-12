// Independently inspect generated PCM masters and make short listening excerpts.
// Usage: node Tools/Audio/InspectAfter333Music.mjs
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../..');
const dir = 'Assets/Project333/Resources/Project333/Audio/BGM';
const specs = [{id:'After333_Lobby', seconds:40*4*60/72, start:8*4*60/72},
    {id:'After333_Battle', seconds:120, start:30}];
function parse(file) {
    const b=fs.readFileSync(file);
    if(b.toString('ascii',0,4)!=='RIFF'||b.toString('ascii',8,12)!=='WAVE') throw Error('Not WAV: '+file);
    let fmt,data;
    for(let p=12;p+8<=b.length;){const n=b.readUInt32LE(p+4),id=b.toString('ascii',p,p+4);
        if(p+8+n>b.length)throw Error('Truncated WAV '+file);
        if(id==='fmt ')fmt={format:b.readUInt16LE(p+8), channels:b.readUInt16LE(p+10),rate:b.readUInt32LE(p+12),bits:b.readUInt16LE(p+22)};
        if(id==='data')data=b.subarray(p+8,p+8+n);p+=8+n+(n%2);}
    if(!fmt||!data||fmt.format!==1||fmt.channels!==2||fmt.bits!==16||fmt.rate!==44100)throw Error('Expected stereo PCM16 44.1kHz '+file);
    return {...fmt,data,frames:data.length/4};
}
function db(x){return Math.round(20*Math.log10(Math.max(x,1e-12))*100)/100;}
function inspect(w){let peak=0,sumL=0,sumR=0,sqL=0,sqR=0,cross=0,diffSq=0,clips=0,maxStep=0,prior=[0,0];
    let windowSq=0,windowN=0,minRms=Infinity,maxRms=0;
    for(let i=0;i<w.frames;i++){const l=w.data.readInt16LE(i*4)/32768,r=w.data.readInt16LE(i*4+2)/32768;
        peak=Math.max(peak,Math.abs(l),Math.abs(r));if(Math.abs(l)>=.9999||Math.abs(r)>=.9999)clips++;
        sumL+=l;sumR+=r;sqL+=l*l;sqR+=r*r;cross+=l*r;diffSq+=(l-r)*(l-r);
        if(i>0)maxStep=Math.max(maxStep,Math.abs(l-prior[0]),Math.abs(r-prior[1]));prior=[l,r];
        windowSq+=(l*l+r*r)*.5;windowN++;if(windowN===w.rate){const rms=Math.sqrt(windowSq/windowN);minRms=Math.min(minRms,rms);maxRms=Math.max(maxRms,rms);windowSq=0;windowN=0;}}
    const firstL=w.data.readInt16LE(0)/32768,firstR=w.data.readInt16LE(2)/32768;
    const seam=Math.max(Math.abs(firstL-prior[0]),Math.abs(firstR-prior[1]));
    return {seconds:w.frames/w.rate,frames:w.frames,peakDbfs:db(peak),rmsDbfs:db(Math.sqrt((sqL+sqR)/(2*w.frames))),
        clippedFrames:clips,dcLeft:sumL/w.frames,dcRight:sumR/w.frames,
        stereoDifferenceRms:Math.sqrt(diffSq/w.frames),stereoCorrelation:cross/Math.sqrt(sqL*sqR),
        maxAdjacentSampleStep:maxStep,loopBoundaryStep:seam,minOneSecondRmsDbfs:db(minRms),maxOneSecondRmsDbfs:db(maxRms)};
}
function excerpt(w,start,seconds){const offset=Math.round(start*w.rate),n=Math.min(Math.round(seconds*w.rate),w.frames-offset);
    const pcm=Buffer.from(w.data.subarray(offset*4,(offset+n)*4));
    for(let i=0;i<n;i++){const gain=Math.min(1,i/(.015*w.rate),(n-1-i)/(.04*w.rate));for(let c=0;c<2;c++){const p=i*4+c*2;pcm.writeInt16LE(Math.round(pcm.readInt16LE(p)*gain),p);}}
    const h=Buffer.alloc(44);h.write('RIFF');h.writeUInt32LE(36+pcm.length,4);h.write('WAVEfmt ',8);h.writeUInt32LE(16,16);h.writeUInt16LE(1,20);h.writeUInt16LE(2,22);h.writeUInt32LE(w.rate,24);h.writeUInt32LE(w.rate*4,28);h.writeUInt16LE(4,32);h.writeUInt16LE(16,34);h.write('data',36);h.writeUInt32LE(pcm.length,40);return Buffer.concat([h,pcm]);}
const reports=[];fs.mkdirSync(path.join(root,'docs/audio'),{recursive:true});
for(const spec of specs){const w=parse(path.join(root,dir,spec.id+'.wav')),r=inspect(w);
    if(Math.abs(r.seconds-spec.seconds)>1/w.rate)throw Error('Incorrect musical length '+spec.id);
    if(r.clippedFrames||r.peakDbfs>-.5||Math.abs(r.dcLeft)>.001||Math.abs(r.dcRight)>.001)throw Error('Master headroom/DC failure '+spec.id);
    if(spec.id==='After333_Lobby' && (r.peakDbfs > -8 || r.rmsDbfs > -22))throw Error('Revised quiet lobby master exceeds intended level');
    if(r.stereoDifferenceRms<.001)throw Error('Unexpected dual-mono master '+spec.id);
    if(r.loopBoundaryStep>Math.max(.02,r.maxAdjacentSampleStep*2))throw Error('Loop seam discontinuity '+spec.id);
    const preview='docs/audio/'+spec.id+'_Preview.wav';fs.writeFileSync(path.join(root,preview),excerpt(w,spec.start,20));
    reports.push({id:spec.id,...r,preview});}
fs.writeFileSync(path.join(root,'Tools/Audio/After333MusicIndependentValidation.json'),JSON.stringify({note:'Signal measurements do not establish subjective musical quality or replace device listening.',tracks:reports},null,2)+'\n');
for(const r of reports)console.log(r.id+': '+r.seconds.toFixed(3)+'s, peak '+r.peakDbfs+'dBFS, RMS '+r.rmsDbfs+'dBFS, loop step '+r.loopBoundaryStep.toFixed(6)+', clips '+r.clippedFrames);
