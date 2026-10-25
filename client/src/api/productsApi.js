import { request } from "./client";

export const getProducts = (categoryId = null) => {
  const endpoint = categoryId
    ? `/products?categoryId=${categoryId}`
    : "/products";
  return request(endpoint);
};

export const getProductsPage = ({
  categoryId = null,
  skip = 0,
  limit = 12,
} = {}) => {
  const params = new URLSearchParams();
  if (categoryId) params.set("categoryId", categoryId);
  params.set("skip", skip);
  params.set("limit", limit);
  return request(`/products?${params}`);
};

export const getProductById = (id) => request(`/products/${id}`);

export const createProduct = (productData) =>
  request("/products", {
    method: "POST",
    body: productData,
  });

export const updateProduct = (id, productData) =>
  request(`/products/${id}`, {
    method: "PUT",
    body: productData,
  });

export const deleteProduct = (id) =>
  request(`/products/${id}`, {
    method: "DELETE",
  });
