import { request } from "./client";

export const getUserOrders = async () => {
  return await request("/orders/my", { method: "GET" });
};

export const getOrderById = async (id) => {
  return await request(`/orders/${id}`, { method: "GET" });
};

export const createOrder = async (orderData) => {
  return await request("/orders", {
    method: "POST",
    body: orderData,
  });
};
export const cancelOrder = (id) =>
  request(`/orders/${id}/cancel`, { method: "POST" });

export const ORDER_STATUS_LABELS = {
  Pending: "Очікує обробки",
  Cancelled: "Скасовано",
};

export const getStatusLabel = (status) => ORDER_STATUS_LABELS[status] ?? status;
