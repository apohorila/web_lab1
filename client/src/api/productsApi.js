import { request } from './client';

export const getProducts = (categoryId = null) => {
  const endpoint = categoryId ? `/products?categoryId=${categoryId}` : '/products';
  return request(endpoint);
};

export const getProductById = (id) => request(`/products/${id}`);

export const createProduct = (productData) =>
  request('/products', {
    method: 'POST',
    body: productData,
  });

export const updateProduct = (id, productData) =>
  request(`/products/${id}`, {
    method: 'PUT',
    body: productData,
  });

export const deleteProduct = (id) =>
  request(`/products/${id}`, {
    method: 'DELETE',
  });