import { requestWithMeta } from "./client";


export const getCatalogStats = () => requestWithMeta("/stats/catalog");


export const getSalesStats = (days = 30) =>
  requestWithMeta(`/stats/sales?days=${days}`);
