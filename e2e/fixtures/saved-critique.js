export function savedCritique(mode = 'Live') {
  const evidence = [
    { kind: 'VisibleObservation', statement: 'The subject outline is clearly separated.', exifField: null, exifValue: null },
    { kind: 'ExifFact', statement: 'The supplied capture setting is ISO 200.', exifField: 'Iso', exifValue: '200' },
    { kind: 'Hypothesis', statement: 'The darker edge may draw less attention.', exifField: null, exifValue: null },
    { kind: 'StylisticPreference', statement: 'A tighter frame is an optional alternative.', exifField: null, exifValue: null },
  ];
  const content = {
    strengths: [{ explanation: 'The shape communicates the intended quiet mood.', evidence }],
    improvements: [1, 2, 3].map(number => ({ observation: `Observed edge ${number}`, effect: `Attention effect ${number}`, action: `Try framing change ${number}`, evidence: [evidence[0]] })),
    exercise: { action: 'Make two frames with different subject positions.', comparison: 'Compare which silhouette is easier to distinguish.' },
  };
  for (const aspect of ['exposure', 'focus', 'depthOfField', 'motion', 'lighting', 'color', 'processing', 'framing', 'subjectSeparation', 'balance', 'visualHierarchy', 'mood'])
    content[aspect] = { assessable: false, explanation: `Recorded ${aspect} observation.`, uncertaintyReason: `The preview cannot establish ${aspect} with confidence.`, evidence: [] };
  return {
    operationId: '00000000-0000-4000-8000-888888888888', generatedAt: '2026-09-07T13:00:00Z', mode,
    model: mode === 'Demo' ? 'loupe-demo-critique-v1' : 'gpt-5.4-mini-2026-03-17', promptVersion: 'critique-v1',
    brief: { intent: 'Make deliberate silhouettes', genre: 'Street', experience: 'Intermediate', requestedFeedback: 'Preserve the strong shapes' }, content,
  };
}
