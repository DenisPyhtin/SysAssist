import { PresentationFile, FileBlob } from '@oai/artifact-tool';
const pptx = process.argv[2];
const pres = await PresentationFile.importPptx(await FileBlob.load(pptx));
console.log('pres keys', Object.keys(pres));
console.log('slides keys', Object.keys(pres.slides), 'count', pres.slides.count);
const slide = pres.slides.getItem(0);
console.log('slide keys', Object.keys(slide));
console.log('slide proto', Object.getOwnPropertyNames(Object.getPrototypeOf(slide)));
console.log('shapes?', slide.shapes && Object.keys(slide.shapes), slide.shapes?.count);
if (slide.shapes) {
  console.log('shapes proto', Object.getOwnPropertyNames(Object.getPrototypeOf(slide.shapes)));
  const sh = slide.shapes.getItem ? slide.shapes.getItem(0) : null;
  console.log('shape', sh && Object.keys(sh), sh && Object.getOwnPropertyNames(Object.getPrototypeOf(sh)));
  if (sh) console.log('shape props text', sh.text, sh.name, sh.id, sh.left, sh.top, sh.width, sh.height);
}
