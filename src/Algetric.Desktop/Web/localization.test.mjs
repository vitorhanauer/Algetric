import test from 'node:test';
import assert from 'node:assert/strict';
import {mapText} from './localization.mjs';

test('map labels follow the selected language in both directions',()=>{
  assert.equal(mapText('Origem','en'),'Origin');
  assert.equal(mapText('Gerar caminho de pontos…','en'),'Generate point path…');
  assert.equal(mapText('Gerar caminho de pontos…','pt-BR'),'Gerar caminho de pontos…');
  assert.equal(mapText('Original polygon name','en'),'Original polygon name');
});
