"""Generate narration and word timing; sends only the authored tour script to Edge TTS."""
import asyncio, json, subprocess, wave
from pathlib import Path
import edge_tts, imageio_ffmpeg
ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/'docs/demo/critique/source'
OUT.mkdir(parents=True,exist_ok=True)
scenes=json.loads((Path(__file__).parent/'scenes.json').read_text())
async def main():
    for scene in scenes:
        stem=OUT/scene['id']
        words=[]
        communicate=edge_tts.Communicate(scene['text'],voice='en-US-EmmaMultilingualNeural',rate='-5%',boundary='WordBoundary')
        with stem.with_suffix('.mp3').open('wb') as audio:
            async for part in communicate.stream():
                if part['type']=='audio': audio.write(part['data'])
                elif part['type']=='WordBoundary': words.append(part)
        subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(),'-y','-v','error','-i',str(stem.with_suffix('.mp3')),'-ar','48000','-ac','1',str(stem.with_suffix('.wav'))],check=True)
        with wave.open(str(stem.with_suffix('.wav'))) as f: scene['speechDuration']=f.getnframes()/f.getframerate()
        scene['duration']=max(scene['speechDuration']+1.2,10)
        scene['words']=words
        print(scene['id'],round(scene['duration'],2),flush=True)
    (OUT/'narration.json').write_text(json.dumps(scenes,indent=2))
asyncio.run(main())
