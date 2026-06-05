import { PresentationFile, FileBlob } from '@oai/artifact-tool';
const pres=await PresentationFile.importPptx(await FileBlob.load(process.argv[2]));
const sh=pres.slides.getItem(0).shapes.getItem(1);
console.log('text obj proto', Object.getOwnPropertyNames(Object.getPrototypeOf(sh.text)));
console.log('text keys', Object.keys(sh.text));
console.log('plain', sh.text.plainText, 'string', String(sh.text));
console.log('descriptor text prop', Object.getOwnPropertyDescriptor(Object.getPrototypeOf(sh),'text'), Object.getOwnPropertyDescriptor(sh,'text'));
try { sh.text = 'NEW TITLE'; console.log('assigned', sh.toSnapshot().text); } catch(e) { console.error('assign fail',e.message); }
try { sh.text.plainText = 'PLAIN'; console.log('plain assigned', sh.toSnapshot().text); } catch(e) { console.error('plain fail',e.message); }
