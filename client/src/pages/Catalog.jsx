import React, { useState, useEffect } from 'react';
import { useSearchParams } from 'react-router-dom';
import ProductCard from '../components/ProductCard';
import { getProducts } from '../api/productsApi';

export default function Catalog() {
  const [products, setProducts] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [searchParams] = useSearchParams();
  const categoryId = searchParams.get('categoryId');

  useEffect(() => {
    async function loadProducts() {
      try {
        setIsLoading(true);
        const data = await getProducts(categoryId);
        setProducts(Array.isArray(data) ? data : []);
      } catch (error) {
        console.error('Помилка завантаження товарів:', error);
      } finally {
        setIsLoading(false);
      }
    }

    loadProducts();
  }, [categoryId]);

  if (isLoading) {
    return <div className="text-center py-20 text-xs tracking-widest uppercase">Завантаження виробів...</div>;
  }

  return (
    <div className="max-w-7xl mx-auto px-6 py-12">
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-x-8 gap-y-16">
        {products.map((product) => (
          <ProductCard key={product.id} product={product} />
        ))}
      </div>
    </div>
  );
}