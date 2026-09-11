import { readFileSync } from 'node:fs';
import { ReferenceLibrary } from '../../fixtures/reference-library.js';
export const images=JSON.parse(readFileSync(new URL('./images.json',import.meta.url)));
export function makeLibrary() {
 const library=new ReferenceLibrary(25);
 library.items.forEach((item,i)=>{const image=images[i%12];const url='data:image/jpeg;base64,'+readFileSync(new URL('./images/'+image.seed+'.jpg',import.meta.url)).toString('base64');Object.assign(item,{title:image.title+(i>=12?' — study '+(i+1):''),attribution:image.attribution+' (mock label)',width:480,height:600,imageUrl:url,previewUrl:url});});
 return library;
}
