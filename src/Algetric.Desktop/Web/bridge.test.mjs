import { test } from 'node:test';
import assert from 'node:assert/strict';
import { desiredMode, acceptState } from './bridge.mjs';
test('zoom transition has hysteresis', () => {
  assert.equal(desiredMode('3D', 50_000), '2D');
  assert.equal(desiredMode('2D', 70_000), '2D');
  assert.equal(desiredMode('3D', 70_000), '3D');
  assert.equal(desiredMode('2D', 100_000), '3D');
  assert.equal(desiredMode('2D', NaN), '2D');
});
test('late snapshots do not overwrite new revisions', () => {
  assert.equal(acceptState({session:'a', revision:3}, {session:'a', revision:2}), false);
  assert.equal(acceptState({session:'a', revision:3}, {session:'b', revision:0}), true);
});

test('area selection contains complete objects in either drag direction', async () => {
  const {objectsInScreenArea} = await import('./bridge.mjs');
  assert.equal(typeof objectsInScreenArea, 'function');
  const objects = [
    {id:'point',vertices:[{x:0,y:0}]},
    {id:'line',vertices:[{x:2,y:2},{x:10,y:10}]},
    {id:'partial',vertices:[{x:5,y:5},{x:20,y:20}]},
    {id:'hidden',vertices:[null]},
  ];
  const start={x:0,y:0},end={x:10,y:10};
  assert.deepEqual(objectsInScreenArea(objects,start,end,p=>p), ['point','line']);
  assert.deepEqual(objectsInScreenArea(objects,end,start,p=>p), ['point','line']);
});

test('toolbar shortcuts select tools and preserve file and history commands', async () => {
  const {keyboardCommand} = await import('./bridge.mjs');
  assert.equal(typeof keyboardCommand, 'function');
  for(const [key,tool] of [['v',0],['m',6],['p',1],['l',3],['g',4]])
    assert.deepEqual(keyboardCommand({key}),{type:'tool',tool});
  assert.deepEqual(keyboardCommand({key:'N',ctrlKey:true,shiftKey:true}),{type:'addLayer'});
  assert.deepEqual(keyboardCommand({key:'n',ctrlKey:true}),{type:'new'});
  assert.deepEqual(keyboardCommand({key:'z',ctrlKey:true}),{type:'undo'});
  assert.deepEqual(keyboardCommand({key:'y',ctrlKey:true}),{type:'redo'});
  assert.deepEqual(keyboardCommand({key:'Enter'}),{type:'finish'});
  assert.deepEqual(keyboardCommand({key:'Escape'}),{type:'cancel'});
  assert.equal(keyboardCommand({key:'l',altKey:true}),null);
});
