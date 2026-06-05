import { PresentationFile, FileBlob } from '@oai/artifact-tool';
const pres = await PresentationFile.importPptx(await FileBlob.load(process.argv[2]));
for (let si=0; si<pres.slides.count; si++) {
  const slide=pres.slides.getItem(si);
  console.log('\nSLIDE', si, 'elements?', slide.elements?.count, 'shapes items', slide.shapes.items?.length);
  for (let i=0;i<(slide.shapes.items?.length||0);i++) {
    const sh=slide.shapes.getItem(i);
    const snap=sh.toSnapshot?.();
    const data=sh.data;
    let txt = '';
    try { txt = sh.text?.plainText || sh.text?.text || sh.text?.toString?.(); } catch {}
    console.log(i, 'id', sh.id, 'name', data?.name, 'type', sh.type, 'txt', txt, 'data keys', Object.keys(data||{}).slice(0,20));
    if (data?.text) console.log(' data.text', JSON.stringify(data.text).slice(0,500));
    if (snap) console.log(' snap keys', Object.keys(snap).slice(0,20), JSON.stringify(snap).slice(0,300));
  }
}
