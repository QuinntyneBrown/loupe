import { readFileSync } from 'node:fs';
import { PhotographLibrary } from '../../fixtures/photograph-library.js';
import { critiqueOperation } from '../../fixtures/critique-operation.js';

export const firstTitle = 'Shoreline at first light';
export const secondTitle = 'Shoreline, second attempt';
export const rockStatement = 'Layered foreground rocks create a clear path toward the water.';
export const horizonStatement = 'The quiet horizon leaves space around the foreground shapes.';
export const rockRegion = { x: 0.28, y: 0.72, size: 0.22 };
const visible = (statement, region = null) => ({ kind: 'VisibleObservation', statement, region, exifField: null, exifValue: null });
const observation = (explanation, region, statement = explanation) => ({ assessable: true, explanation, uncertaintyReason: null, evidence: [visible(statement, region)] });
export function makeLibrary() {
  const library = new PhotographLibrary(2);
  const imageUrl = 'data:image/jpeg;base64,' + readFileSync(new URL('./reference-photo.jpg', import.meta.url)).toString('base64');
  library.photos.forEach((photo, index) => {
    Object.assign(photo, { title: index ? secondTitle : firstTitle, width: 1200, height: 800, imageUrl, previewUrl: imageUrl,
      createdAt: index ? '2026-09-08T07:12:00Z' : '2026-09-06T07:12:00Z',
      exif: { focalLength: '24 mm', aperture: 'f/8', shutterSpeed: '1/250 s', iso: '100' },
      brief: { intent: 'A quiet study of the rocks in first light. I wanted the texture and not much else.', genre: 'Landscape', experience: 'Intermediate', requestedFeedback: 'Technical, composition' },
      notes: 'Next time: bracket the sky and compare a tighter frame.' });
    const content = {
      strengths: [
        { explanation: 'The foreground texture gives the frame a strong point of entry.', evidence: [visible(rockStatement, rockRegion)] },
        { explanation: 'The open water and restrained palette support the intended quiet mood.', evidence: [visible(horizonStatement, {x:0.62,y:0.36,size:0.26})] },
      ],
      exposure: observation('The pale sky is much brighter than the rocks. Bracket a darker frame to compare highlight detail.', {x:0.5,y:0.14,size:0.24}),
      focus: observation('The foreground edges read clearly in this preview.', {x:0.16,y:0.84,size:0.16}),
      depthOfField: observation('Near and middle-distance rock shapes remain easy to distinguish.', rockRegion),
      motion: observation('Small ripples preserve the feeling of still water.', {x:0.52,y:0.56,size:0.2}),
      lighting: observation('Soft light reveals the surface texture without hard shadows.', rockRegion),
      color: observation('Muted blue-green water balances the warm grey rocks.', {x:0.65,y:0.48,size:0.2}),
      processing: {assessable:false, explanation:'The preview alone cannot establish how much editing was applied.', uncertaintyReason:'No editing history was supplied.', evidence:[]},
      framing: observation('The diagonal rock edge leads the eye into the scene.', rockRegion),
      subjectSeparation: observation('The darker foreground separates from the lighter water.', rockRegion),
      balance: observation('Open water gives the heavier foreground room to breathe.', {x:0.7,y:0.4,size:0.25}),
      visualHierarchy: observation('The bright upper frame competes with the textured rocks.', {x:0.5,y:0.14,size:0.24}),
      mood: observation('The low contrast and open horizon support a calm reading.', {x:0.62,y:0.36,size:0.26}),
      improvements: [
        { observation:'The pale sky pulls attention upward.', effect:'The texture you wanted becomes a secondary stop.', action:'Bracket one darker exposure and compare the highlights.', evidence:[visible('The brightest area sits above the horizon.',{x:0.5,y:0.14,size:0.24})] },
        { observation:'A small rock touches the right edge.', effect:'It can become an unintended exit from the frame.', action:'Try a slight shift left, then compare a tighter crop.', evidence:[visible('A small isolated rock sits near the right edge.',{x:0.9,y:0.78,size:0.16})] },
        { observation:'The nearest dark rock carries substantial visual weight.', effect:'It anchors the scene but may overpower the quieter water.', action:'Raise the camera slightly and compare the balance.', evidence:[visible('The nearest dark shape occupies the lower left.',{x:0.16,y:0.84,size:0.16})] },
      ],
      exercise: {action:'Make three frames: the original view, a darker exposure, and a slightly higher viewpoint.', comparison:'Compare which version keeps the rocks as the first point of attention.'},
    };
    if (index) content.strengths[0].explanation = 'Fixture variation: a higher viewpoint gives the water more space.';
    const saved = { operationId: `00000000-0000-4000-8000-88888888888${index}`, generatedAt:'2026-09-08T08:00:00Z', mode:'Live', model:'recording-fixture (no provider call)', promptVersion:'tour-fixture-v1', brief:photo.brief, content };
    library.critiques.set(photo.id, saved);
    library.critiqueOperations.set(photo.id,{...critiqueOperation(photo.id,'Succeeded'),id:saved.operationId,completedAt:saved.generatedAt,message:'Critique saved.'});
  });
  return library;
}
