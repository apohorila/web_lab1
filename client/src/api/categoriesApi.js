import { request } from "./client";

export const getCategories = () => request("/categories");

export const getCategoryById = (id) => request(`/categories/${id}`);

export const createCategory = (categoryData) =>
  request("/categories", {
    method: "POST",
    body: categoryData,
  });

export const updateCategory = (id, categoryData) =>
  request(`/categories/${id}`, {
    method: "PUT",
    body: categoryData,
  });

export const deleteCategory = (id) =>
  request(`/categories/${id}`, {
    method: "DELETE",
  });
