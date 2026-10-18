export const ENTER_2D = 60_000;
export const EXIT_2D = 90_000;
export function desiredMode(current, height) {
  if (!Number.isFinite(height) || height <= 0) return current;
  if (current === '3D' && height < ENTER_2D) return '2D';
  if (current === '2D' && height > EXIT_2D) return '3D';
  return current;
}
export function acceptState(previous, next) {
  return !previous || previous.session !== next.session || next.revision >= previous.revision;
}
export function objectsInScreenArea(objects, start, end, project) {
  const left=Math.min(start.x,end.x),right=Math.max(start.x,end.x);
  const top=Math.min(start.y,end.y),bottom=Math.max(start.y,end.y);
  return objects.filter(object=>object.vertices.length>0 && object.vertices.every(vertex=>{
    const point=project(vertex);
    return point && point.x>=left && point.x<=right && point.y>=top && point.y<=bottom;
  })).map(object=>object.id);
}
export function keyboardCommand({key,ctrlKey=false,shiftKey=false,altKey=false,metaKey=false}) {
  if(altKey||metaKey)return null;
  if(ctrlKey){
    if(key.toLowerCase()==='n' && shiftKey)return {type:'addLayer'};
    const type={s:'save',o:'open',n:'new',z:'undo',y:'redo'}[key.toLowerCase()];
    return type?{type}:null;
  }
  const type={Escape:'cancel',Enter:'finish',Delete:'delete',Home:'home'}[key];
  if(type)return {type};
  if(shiftKey)return null;
  const tool={v:0,m:6,p:1,l:3,g:4}[key.toLowerCase()];
  return tool===undefined?null:{type:'tool',tool};
}
