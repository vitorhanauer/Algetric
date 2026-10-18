import {desiredMode, acceptState, objectsInScreenArea, keyboardCommand} from './bridge.mjs';
const notice=document.querySelector('#notice');
let viewer,state,sequence=0,pending=false,morphing=false,draft=[],drag=null,area=null,providerId,mode='3D',mapMissing=false,pointerHeld=false;
const C=globalThis.Cesium;
function report(message){notice.textContent=message;}
function geos(points){return points.map(p=>C.Cartesian3.fromDegrees(p.longitude,p.latitude));}
function positionAt(screen){
  const hit=viewer.camera.pickEllipsoid(screen,viewer.scene.globe.ellipsoid);
  if(!hit)return null;
  const c=C.Cartographic.fromCartesian(hit);
  return {latitude:C.Math.toDegrees(c.latitude),longitude:C.Math.toDegrees(c.longitude)};
}
async function send(type,payload={}){
  if(!state || pending || morphing)return null;
  pending=true;
  try{
    const response=await fetch('bridge',{method:'POST',headers:{'Content-Type':'application/json'},
      body:JSON.stringify({type,session:state.session,sequence:++sequence,revision:state.revision,...payload})});
    if(!response.ok)throw new Error('Falha na comunicação com o editor.');
    const next=await response.json(); sync(next);return next;
  }catch(error){report(error.message);return null;}finally{pending=false;}
}
function sync(next){
  if(!acceptState(state,next))return;
  const reset=!state || next.session!==state.session;
  if(reset){clearAreaSelection();sequence=Date.now()*1000;draft=[];drag=null;viewer.scene.completeMorph();viewer.scene.morphTo3D(0);mode='3D';morphing=false;}
  if(state?.tool!==next.tool){draft=[];closePolygonMenu();clearAreaSelection();}
  state=next;
  viewer.scene.screenSpaceCameraController.enableRotate=next.tool!==0;
  viewer.scene.screenSpaceCameraController.enableTranslate=next.tool!==0;
  if(reset)viewer.camera.setView({destination:C.Cartesian3.fromDegrees(next.view.longitude,next.view.latitude,next.view.height)});
  configureProvider();render();
  report(`${mode==='3D'?'Globo 3D':'Mapa 2D'} • ${next.status}${!next.provider?' • Configure um provedor de mapas.':mapMissing?' • Mapa base indisponível nesta área.':''}`);
}
function configureProvider(){
  const id=state.provider?.id??null;if(id===providerId)return;providerId=id;mapMissing=false;
  viewer.imageryLayers.removeAll();
  if(!id)return;
  const provider=new C.UrlTemplateImageryProvider({url:new URL(`tiles/${id}/{z}/{x}/{y}`,location.href).href,
    minimumLevel:state.provider.minZoom,maximumLevel:state.provider.maxZoom,
    credit:state.provider.attribution,tilingScheme:new C.WebMercatorTilingScheme()});
  provider.errorEvent.addEventListener(()=>{mapMissing=true;report('Mapa base indisponível nesta área. Geometrias e regiões baixadas continuam acessíveis.');});
  viewer.imageryLayers.addImageryProvider(provider);
}
function render(){
  if(!viewer||!state)return;viewer.entities.removeAll();
  for(const object of state.objects){
    const selected=(state.selectedIds??[state.selectedId]).includes(object.id);
    const color=C.Color.fromCssColorString(object.color);
    const vertices=geos(drag?.id===object.id?drag.vertices:object.vertices);
    if(object.kind===0)viewer.entities.add({id:object.id,position:vertices[0],point:{pixelSize:selected?14:10,color,outlineColor:selected?C.Color.ROYALBLUE:C.Color.WHITE,outlineWidth:2}});
    else if(object.kind===3)viewer.entities.add({id:object.id,polygon:{hierarchy:vertices,material:color.withAlpha(.22)},
      polyline:{positions:[...vertices,vertices[0]],width:selected?4:2,material:color}});
    else viewer.entities.add({id:object.id,polyline:{positions:vertices,width:selected?4:2,material:color}});
    if(selected && object.kind!==0)vertices.forEach((position,index)=>viewer.entities.add({id:`vertex:${object.id}:${index}`,position,
      point:{pixelSize:10,color,outlineColor:C.Color.WHITE,outlineWidth:2}}));
  }
  if(draft.length){
    const positions=geos(draft);
    positions.forEach((position,index)=>viewer.entities.add({id:`draft:${index}`,position,point:{pixelSize:7,color:C.Color.ROYALBLUE}}));
    if(positions.length>1)viewer.entities.add({id:'draft',polyline:{positions,width:2,material:C.Color.ROYALBLUE}});
  }
  const origin=state.origin;
  viewer.entities.add({id:'origin',position:C.Cartesian3.fromDegrees(origin.longitude,origin.latitude),
    point:{pixelSize:6,color:C.Color.DARKSLATEGRAY},label:{text:'Origem',font:'12px sans-serif',pixelOffset:new C.Cartesian2(0,16),fillColor:C.Color.DARKSLATEGRAY}});
  viewer.scene.requestRender();
}
async function finish(){
  if(!state || draft.length===0)return;
  const minimum=state.tool===4?3:state.tool===3?2:0;
  if(!minimum || draft.length<minimum){report('Adicione mais vértices para concluir.');return;}
  const before=state.revision;const next=await send('create',{vertices:draft});
  if(next && next.revision>before){draft=[];render();}
}
function cancel(){closePolygonMenu();clearAreaSelection();draft=[];drag=null;viewer.scene.screenSpaceCameraController.enableInputs=true;render();}
function centerAndHeight(){
  const center=positionAt(new C.Cartesian2(viewer.canvas.clientWidth/2,viewer.canvas.clientHeight/2));
  return {...(center??{longitude:state.view.longitude,latitude:state.view.latitude}),height:viewer.camera.positionCartographic.height};
}
function transition(){
  if(morphing||pending||!state)return;
  const next=desiredMode(mode,viewer.camera.positionCartographic.height);if(next===mode)return;
  const saved=centerAndHeight();viewer.camera.cancelFlight();morphing=true;cancel();viewer.scene.screenSpaceCameraController.enableInputs=false;
  const unregister=viewer.scene.morphComplete.addEventListener(()=>{
    unregister();mode=next;viewer.camera.setView({destination:C.Cartesian3.fromDegrees(saved.longitude,saved.latitude,saved.height),
      orientation:{heading:0,pitch:-Math.PI/2,roll:0}});
    morphing=false;viewer.scene.screenSpaceCameraController.enableInputs=true;report(mode==='2D'?'Mapa 2D':'Globo 3D');
  });
  if(next==='2D')viewer.scene.morphTo2D(.8);else viewer.scene.morphTo3D(.8);
}
function clearAreaSelection(){
  area=null;document.querySelector('#selection-area')?.remove();
  if(viewer)viewer.scene.screenSpaceCameraController.enableInputs=true;
}
function beginAreaSelection(position){
  area={start:C.Cartesian2.clone(position),end:C.Cartesian2.clone(position)};
  const rectangle=document.createElement('div');rectangle.id='selection-area';
  rectangle.style.cssText='position:absolute;pointer-events:none;border:1.5px solid #1e90ff;background:#1e90ff26;box-sizing:border-box;z-index:5';
  viewer.container.append(rectangle);
  viewer.scene.screenSpaceCameraController.enableInputs=false;
}
function updateAreaSelection(position){
  area.end=C.Cartesian2.clone(position);
  const style=document.querySelector('#selection-area').style;
  style.left=`${Math.min(area.start.x,position.x)}px`;style.top=`${Math.min(area.start.y,position.y)}px`;
  style.width=`${Math.abs(area.start.x-position.x)}px`;style.height=`${Math.abs(area.start.y-position.y)}px`;
}
async function finishAreaSelection(position){
  const selection=area;clearAreaSelection();
  const occluder=new C.EllipsoidalOccluder(viewer.scene.globe.ellipsoid,viewer.camera.positionWC);
  const ids=C.Cartesian2.distance(selection.start,position)<4?[]:
    objectsInScreenArea(state.objects,selection.start,position,vertex=>{
      const world=C.Cartesian3.fromDegrees(vertex.longitude,vertex.latitude);
      if(viewer.scene.mode===C.SceneMode.SCENE3D && !occluder.isPointVisible(world))return null;
      return C.SceneTransforms.worldToWindowCoordinates(viewer.scene,world);
    });
  await send('selectMany',{ids});
}
function closePolygonMenu(){document.querySelector('#polygon-menu')?.remove();}
function openPolygonMenu(event){
  event.preventDefault();closePolygonMenu();if(!state||pending||morphing)return;
  const bounds=viewer.canvas.getBoundingClientRect();
  const picked=viewer.scene.drillPick(new C.Cartesian2(event.clientX-bounds.left,event.clientY-bounds.top));
  const polygon=state.objects.find(object=>object.kind===3 && picked.some(hit=>hit.id?.id===object.id));
  if(!polygon)return;
  const menu=document.createElement('div');menu.id='polygon-menu';menu.setAttribute('role','menu');
  menu.style.cssText=`position:fixed;z-index:10;left:${Math.max(8,Math.min(event.clientX,innerWidth-250))}px;top:${Math.max(8,Math.min(event.clientY,innerHeight-50))}px;padding:4px;background:white;border:1px solid #bccddd;border-radius:5px;box-shadow:0 4px 16px #0003`;
  const button=document.createElement('button');button.textContent='Gerar caminho de pontos…';button.setAttribute('role','menuitem');
  button.onclick=()=>{closePolygonMenu();void send('generatePath',{id:polygon.id});};
  menu.addEventListener('keydown',key=>{key.stopPropagation();if(key.key==='Escape'){closePolygonMenu();key.preventDefault();}});
  menu.append(button);document.body.append(menu);button.focus();
}
function wireEditing(){
  const handler=new C.ScreenSpaceEventHandler(viewer.canvas);
  viewer.canvas.addEventListener('contextmenu',openPolygonMenu);
  window.addEventListener('pointerdown',event=>{if(!event.target.closest('#polygon-menu'))closePolygonMenu();});
  viewer.screenSpaceEventHandler.removeInputAction(C.ScreenSpaceEventType.LEFT_DOUBLE_CLICK);
  handler.setInputAction(async event=>{
    if(pending||morphing||!state)return;
    if(state.tool>=1 && state.tool<=4 && !state.canCreateObject){await send('requireLayer');return;}
    const point=positionAt(event.position);if(!point)return;
    if(state.tool===5){await send('origin',{vertices:[point]});return;}
    if(state.tool===1){await send('create',{vertices:[point]});return;}
    if(state.tool>=2 && state.tool<=4){
      const previous=draft.at(-1);
      if(previous && Math.abs(previous.latitude-point.latitude)+Math.abs(previous.longitude-point.longitude)<1e-10)return;
      draft.push(point);render();if(state.tool===2 && draft.length===2){const before=state.revision;const next=await send('create',{vertices:draft});if(next?.revision>before){draft=[];render();}}
    }
  },C.ScreenSpaceEventType.LEFT_CLICK);
  handler.setInputAction(()=>{if(state?.tool===3||state?.tool===4)void finish();},C.ScreenSpaceEventType.LEFT_DOUBLE_CLICK);
  handler.setInputAction(async event=>{
    if(pending||morphing||!state||state.tool!==0)return;
    const picked=viewer.scene.pick(event.position)?.id?.id;
    let id=typeof picked==='string'?picked:null,vertex=-1;
    if(id?.startsWith('vertex:')){const parts=id.split(':');id=parts[1];vertex=Number(parts[2]);}
    const object=state.objects.find(o=>o.id===id);if(!object){beginAreaSelection(event.position);return;}
    const start=positionAt(event.position);if(!start)return;
    pointerHeld=true;viewer.scene.screenSpaceCameraController.enableInputs=false;
    await send('select',{id:object.id,vertex:Math.max(0,vertex)});
    if(!pointerHeld){viewer.scene.screenSpaceCameraController.enableInputs=true;return;}
    drag={id:object.id,vertex,start,original:object.vertices.map(p=>({...p})),vertices:object.vertices.map(p=>({...p})),changed:false};
  },C.ScreenSpaceEventType.LEFT_DOWN);
  handler.setInputAction(event=>{
    if(area){updateAreaSelection(event.endPosition);return;}
    if(!drag||morphing)return;const point=positionAt(event.endPosition);if(!point)return;
    drag.vertices=drag.original.map((p,i)=>drag.vertex<0||drag.vertex===i?{
      latitude:p.latitude+point.latitude-drag.start.latitude,longitude:p.longitude+point.longitude-drag.start.longitude}:p);
    drag.changed=true;render();
  },C.ScreenSpaceEventType.MOUSE_MOVE);
  handler.setInputAction(async event=>{
    if(area){await finishAreaSelection(event.position);return;}
    pointerHeld=false;viewer.scene.screenSpaceCameraController.enableInputs=true;
    const edited=drag;drag=null;if(edited?.changed)await send('replace',{id:edited.id,vertices:edited.vertices});render();
  },C.ScreenSpaceEventType.LEFT_UP);
  window.addEventListener('blur',()=>{pointerHeld=false;cancel();});
  window.addEventListener('keydown',event=>{
    if(event.target instanceof Element && event.target.closest('input,textarea,select,[contenteditable="true"]'))return;
    const command=keyboardCommand(event);if(!command)return;
    event.preventDefault();
    if(command.type==='cancel'){cancel();return;}
    if(command.type==='finish'){void finish();return;}
    if(command.type==='home'){document.querySelector('#home').click();return;}
    void send(command.type,command.tool===undefined?{}:{tool:command.tool});
  });
}
try{
  if(!C)throw new Error('Recursos Cesium ausentes. Execute npm ci e npm run build na pasta Web.');
  C.Ion.defaultAccessToken='';
  viewer=new C.Viewer('globe',{baseLayer:false,terrainProvider:new C.EllipsoidTerrainProvider(),
    geocoder:false,baseLayerPicker:false,animation:false,timeline:false,homeButton:false,
    navigationHelpButton:false,sceneModePicker:false,fullscreenButton:false,infoBox:false,selectionIndicator:false,
    skyBox:false,skyAtmosphere:false,requestRenderMode:true,maximumRenderTimeChange:Infinity});
  viewer.scene.globe.baseColor=C.Color.fromCssColorString('#DDE7EC');
  viewer.scene.backgroundColor=C.Color.fromCssColorString('#EEF3F8');
  viewer.scene.globe.enableLighting=false;
  globalThis.algetric={sync,finish,cancel,diagnostics:()=>({mode,morphing,pending,tool:state?.tool,providerId,mapMissing,tilesLoaded:viewer.scene.globe.tilesLoaded,objects:state?.objects.length,selectedIds:state?.selectedIds,areaSelecting:!!area,height:viewer.camera.positionCartographic.height,webgl:viewer.scene.context.webgl2,notice:notice.textContent})};
  wireEditing();sync(await(await fetch('state')).json());
  viewer.camera.changed.addEventListener(transition);viewer.camera.percentageChanged=.05;
  viewer.camera.moveEnd.addEventListener(()=>{if(!morphing&&state)void send('camera',centerAndHeight());});
  document.querySelector('#home').onclick=()=>viewer.camera.flyTo({destination:C.Cartesian3.fromDegrees(state.origin.longitude,state.origin.latitude,30_000)});
  document.querySelector('#cancel').onclick=cancel;
}catch(error){const box=document.querySelector('#error');box.style.display='block';box.textContent=`Não foi possível inicializar o globo.\n${error.message}\nVerifique WebGL e as dependências nativas do WebView.`;report('Globo indisponível');}
