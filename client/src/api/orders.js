import { request } from './client';

export const getUserOrders = async () => {
  return await request('/orders/my', { method: 'GET' });
};

export const getOrderById = async (id) => {
  return await request(`/orders/${id}`, { method: 'GET' });
};

export const createOrder = async (orderData) => {
  return await request('/orders', {
    method: 'POST',
    body: orderData,
  });
};