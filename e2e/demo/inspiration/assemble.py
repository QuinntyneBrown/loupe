"""Assemble the verified Chromium recording of the real stack, synthetic narration and timed captions."""
import json, subprocess, textwrap, math, re
from pathlib import Path
import imageio_ffmpeg
ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'docs/demo/inspiration'
SRC=OUT/'source'
FF=imageio_ffmpeg.get_ffmpeg_exe()
def run(args):
    return subprocess.run([FF,'-hide_banner','-loglevel','error',*args],cwd=SRC,check=True,capture_output=True)
def anchor(name):
    raw=run(['-i',name+'.webm','-t','5','-vf','fps=25,scale=1:1','-f','rawvideo','-pix_fmt','rgb24','-']).stdout
    pink=[i//3 for i in range(0,len(raw),3) if raw[i]>180 and raw[i+1]<60 and raw[i+2]>180]
    assert pink, 'Missing recording calibration'
    return (max(pink)+1)/25

def ass_time(s):
    c=round(s*100); return f'{c//360000}:{c//6000%60:02}:{c//100%60:02}.{c%100:02}'
def timestamp(s,sep=','):
    m=round(s*1000); return f'{m//3600000:02}:{m//60000%60:02}:{m//1000%60:02}{sep}{m%1000:03}'
def cues(scene):
    tokens=scene['text'].split(); words=scene['words']; assert len(tokens)==len(words)
    groups=[]; start=0
    for i,token in enumerate(tokens):
        length=len(' '.join(tokens[start:i+1]))
        elapsed=(words[i]['offset']+words[i]['duration']-words[start]['offset'])/1e7
        if token.endswith(('.', '?', '!')) or length>=65 or elapsed>=4.3 or i==len(tokens)-1:
            text=' '.join(tokens[start:i+1]); begin=words[start]['offset']/1e7
            end=(words[i]['offset']+words[i]['duration'])/1e7+.12
            if i+1<len(words): end=min(end,words[i+1]['offset']/1e7)
            groups.append((begin,end,text));start=i+1
    return groups
HEADER='''[Script Info]
ScriptType: v4.00+
PlayResX: 1440
PlayResY: 1040
WrapStyle: 0
[V4+ Styles]
Format: Name, Fontname, Fontsize, PrimaryColour, SecondaryColour, OutlineColour, BackColour, Bold, Italic, Underline, StrikeOut, ScaleX, ScaleY, Spacing, Angle, BorderStyle, Outline, Shadow, Alignment, MarginL, MarginR, MarginV, Encoding
Style: Caption,Segoe UI,28,&H00FFFFFF,&H00FFFFFF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,0,0,5,40,40,20,1
Style: Header,Segoe UI,18,&H00FFFFFF,&H00FFFFFF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,0,0,4,24,24,0,1
Style: Badge,Segoe UI,16,&H00D4E9EE,&H00FFFFFF,&H00000000,&H00000000,0,0,0,0,100,100,0,0,1,0,0,6,24,24,0,1
[Events]
Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
'''
def event(a,b,style,text):
    return f'Dialogue: 0,{ass_time(a)},{ass_time(b)},{style},,0,0,0,,{text}\n'
record=json.loads((SRC/'recording.json').read_text(encoding='utf-8'))
narration={s['id']:s for s in json.loads((SRC/'narration.json').read_text(encoding='utf-8'))}
assert record['errors']==[] and record['unexpectedRequests']==[] and record['apiFailures']==[]
assert record['persisted']['tagPersisted'] and record['persisted']['notesPersisted'] and record['persisted']['filteredSearchTotal']>0
anchors={item['name']:anchor(item['name']) for item in record['recordings']}
print('Calibration:',anchors,flush=True)
all_cues=[]; chapters=[]; clips=[]; offset=0
for index,scene in enumerate(record['timeline']):
    sid=scene['id']; speech=narration[sid]
    duration=math.ceil((scene['end']-scene['start'])*25)/25
    assert speech['speechDuration']<duration
    chapter={'id':sid,'title':scene['title'],'start':offset,'duration':duration,'text':speech['text']};chapters.append(chapter)
    ass=HEADER+event(0,duration,'Header',r'{\pos(24,22)}'+f'{index+1:02}  /  '+scene['title'])
    ass+=event(0,duration,'Badge',r'{\pos(1416,22)}REAL API  /  REAL DATABASE  /  NO AI CONFIGURED')
    for a,b,text in cues(speech):
        ass+=event(a,b,'Caption',r'{\pos(720,991)}'+r'\N'.join(textwrap.wrap(text,82)))
        all_cues.append((offset+a,offset+b,text))
    (SRC/(sid+'.ass')).write_text(ass,encoding='utf-8-sig')
    x=(1440-scene['width'])//2
    vf=f'setpts=PTS-STARTPTS,fps=25,pad=1440:1040:{x}:44:color=0x222a30,drawbox=x=0:y=0:w=iw:h=44:color=0x172027:t=fill,drawbox=x=0:y=944:w=iw:h=96:color=0x172027:t=fill,ass={sid}.ass'
    clip=sid+'-finished.mp4';clips.append(clip)
    run(['-y','-ss',str(anchors[scene['capture']]+scene['start']),'-i',scene['capture']+'.webm','-i',sid+'.wav','-t',str(duration),'-vf',vf,'-af',f'loudnorm=I=-16:TP=-1.5:LRA=11,apad,atrim=duration={duration}','-c:v','libx264','-preset','fast','-crf','20','-pix_fmt','yuv420p','-c:a','aac','-b:a','192k','-ar','48000',clip])
    print('Rendered',sid,flush=True);offset+=duration
(SRC/'concat.txt').write_text(''.join(f"file '{name}'\n" for name in clips))
run(['-y','-f','concat','-safe','0','-i','concat.txt','-c','copy','-movflags','+faststart',str(OUT/'inspiration-tour.mp4')])
(OUT/'captions.srt').write_text('\n\n'.join(f'{i+1}\n{timestamp(a)} --> {timestamp(b)}\n{text}' for i,(a,b,text) in enumerate(all_cues))+'\n',encoding='utf-8')
(OUT/'captions.vtt').write_text('WEBVTT\n\n'+'\n\n'.join(f'{timestamp(a,".")} --> {timestamp(b,".")}\n{text}' for a,b,text in all_cues)+'\n',encoding='utf-8')
(OUT/'chapters.json').write_text(json.dumps(chapters,indent=2),encoding='utf-8')
INTRO=('# Inspiration library and keyword search\n\n'
       'This recording drives the real Angular application against the real Loupe.Api, Loupe.Worker and PostgreSQL stack from `docs/demo/harness`. '
       'Every save, board, tag, note and search result shown is real persisted state; no AI provider is configured and no AI request is made. '
       'Narration is synthetic (Microsoft Edge Emma Multilingual).\n\n')
OUTRO=('Images are public Picsum placeholder photographs cached in `e2e/demo/inspiration/images` (original URLs in `images.json`). '
       'Titles, notes, tags, boards and photographer names are fictional demonstration labels from `library.json`, not verified credits.\n')
(OUT/'transcript.md').write_text(INTRO+''.join(f"## {timestamp(c['start'],'.')[:8]} — {c['title']}\n\n{c['text']}\n\n" for c in chapters)+OUTRO,encoding='utf-8')
run(['-y','-ss',str(chapters[1]['start']+12),'-i',str(OUT/'inspiration-tour.mp4'),'-frames:v','1',str(OUT/'poster.jpg')])
player=OUT/'index.html'
if player.exists():
    player.write_text(re.sub(r'const chapters=.*?;const video=', 'const chapters='+json.dumps(chapters)+';const video=', player.read_text(encoding='utf-8')),encoding='utf-8')
print('Finished',offset,'seconds',flush=True)
