import { requestWithMeta } from "./client";

// Публічна статистика каталогу (міні-діаграми на мапі та дашборд)
export const getCatalogStats = () => requestWithMeta("/stats/catalog");

// Статистика продажів — лише для адміністратора (потрібен токен)
export const getSalesStats = (days = 30) =>
  requestWithMeta(`/stats/sales?days=${days}`);
