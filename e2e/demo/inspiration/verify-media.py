"""Decode the complete video and create chapter frames for visual inspection."""
import json, subprocess, re
from pathlib import Path
import imageio_ffmpeg
ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'docs/demo/inspiration'
SRC=OUT/'source'
ff=imageio_ffmpeg.get_ffmpeg_exe()
video=OUT/'inspiration-tour.mp4'
def run(args):
 result=subprocess.run([ff,'-hide_banner',*args],capture_output=True,check=True)
 return result.stderr.decode('utf-8',errors='replace')
log=run(['-v','error','-i',str(video),'-f','null','-'])
assert not log.strip(),log
volume=run(['-i',str(video),'-vn','-af','volumedetect','-f','null','-'])
mean=float(re.search(r'mean_volume: ([-\d.]+) dB',volume)[1])
peak=float(re.search(r'max_volume: ([-\d.]+) dB',volume)[1])
assert -30<mean<-10 and -5<peak<0,(mean,peak)
chapters=json.loads((OUT/'chapters.json').read_text(encoding='utf-8'))
for chapter in chapters:
 run(['-v','error','-y','-ss',str(chapter['start']+min(5,chapter['duration']/2)),'-i',str(video),'-frames:v','1',str(SRC/('final-'+chapter['id']+'.jpg'))])
(SRC/'media-check.json').write_text(json.dumps({'fullDecodeErrors':log,'meanVolumeDb':mean,'peakVolumeDb':peak,'chapterFrames':len(chapters)},indent=2),encoding='utf-8')
print('Full video decode and audio levels verified:',mean,peak)
