import {cp, mkdir} from 'node:fs/promises';
await mkdir('vendor', {recursive:true});
await cp('node_modules/cesium/Build/Cesium', 'vendor/Cesium', {recursive:true});
await cp('node_modules/cesium/LICENSE.md', 'vendor/Cesium/LICENSE.md');

await cp('node_modules/cesium/ThirdParty.json', 'vendor/Cesium/ThirdParty.json');
await cp('node_modules/cesium/ThirdParty.extra.json', 'vendor/Cesium/ThirdParty.extra.json');
