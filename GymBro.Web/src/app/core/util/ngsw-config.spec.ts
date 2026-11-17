import config from '../../../../ngsw-config.json';

/**
 * Decisión de la Etapa 8: las respuestas de la API NO se guardan en la caché del service worker (datos de salud).
 * El service worker de Angular no tiene una estrategia "network-only": cualquier dataGroup que coincida con /api
 * guarda al menos la última respuesta (aun con maxSize 0). Las requests que no coinciden con ningún grupo van
 * directo a la red sin cachearse. Por eso la regla es: ningún grupo de ngsw-config.json puede coincidir con /api.
 */
interface Grupo {
  name: string;
  urls?: string[];
  resources?: { files?: string[]; urls?: string[] };
}

const patrones = (g: Grupo): string[] => [...(g.urls ?? []), ...(g.resources?.files ?? []), ...(g.resources?.urls ?? [])];

describe('ngsw-config.json', () => {
  const cfg = config as unknown as { assetGroups?: Grupo[]; dataGroups?: Grupo[] };

  it('no declara dataGroups (la API no se cachea)', () => {
    expect(cfg.dataGroups ?? []).toEqual([]);
  });

  it('ningún grupo de recursos apunta a la API', () => {
    const todos = [...(cfg.assetGroups ?? []), ...(cfg.dataGroups ?? [])].flatMap(patrones);
    expect(todos.filter((p) => p.toLowerCase().includes('/api'))).toEqual([]);
  });

  it('cachea el cascarón de la aplicación', () => {
    const app = (cfg.assetGroups ?? []).find((g) => g.name === 'app');
    expect(app?.resources?.files).toContain('/index.html');
  });
});
