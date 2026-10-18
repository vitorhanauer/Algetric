const english = {
  'Origem':'Origin',
  'Cancelar':'Cancel',
  'Ir para origem (Home)':'Go to origin (Home)',
  'Cancelar (Esc)':'Cancel (Esc)',
  'Algetric — Mapa':'Algetric — Map',
  'Inicializando o globo…':'Initializing the globe…',
  'Globo 3D':'3D globe',
  'Mapa 2D':'2D map',
  'Configure um provedor de mapas.':'Configure a map provider.',
  'Mapa base indisponível nesta área.':'Base map unavailable in this area.',
  'Mapa base indisponível nesta área. Geometrias e regiões baixadas continuam acessíveis.':'Base map unavailable in this area. Geometries and downloaded regions remain accessible.',
  'Adicione mais vértices para concluir.':'Add more vertices to finish.',
  'Gerar caminho de pontos…':'Generate point path…',
  'Falha na comunicação com o editor.':'Failed to communicate with the editor.',
  'Recursos Cesium ausentes. Execute npm ci e npm run build na pasta Web.':'Cesium assets are missing. Run npm ci and npm run build in the Web directory.',
  'Não foi possível inicializar o globo.':'Unable to initialize the globe.',
  'Verifique WebGL e as dependências nativas do WebView.':'Check WebGL and the native WebView dependencies.',
  'Globo indisponível':'Globe unavailable'
};

export function mapText(text, language) { return language === 'en' ? english[text] ?? text : text; }
