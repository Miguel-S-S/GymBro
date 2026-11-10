import { MatPaginatorIntl } from '@angular/material/paginator';

/** Textos del paginador de Angular Material en español. */
export function paginadorEnEspaniol(): MatPaginatorIntl {
  const intl = new MatPaginatorIntl();
  intl.itemsPerPageLabel = 'Por página';
  intl.nextPageLabel = 'Página siguiente';
  intl.previousPageLabel = 'Página anterior';
  intl.firstPageLabel = 'Primera página';
  intl.lastPageLabel = 'Última página';
  intl.getRangeLabel = (pagina, tamanio, total) => {
    if (total === 0 || tamanio === 0) return `0 de ${total}`;
    const desde = pagina * tamanio;
    const hasta = Math.min(desde + tamanio, total);
    return `${desde + 1}–${hasta} de ${total}`;
  };
  return intl;
}
