import React, { useState, useEffect } from "react";
import { useSearchParams } from "react-router-dom";
import ProductCard from "../components/ProductCard";
import { getProducts } from "../api/productsApi";
import bannerImg from "../assets/icons/banner.jpg";

export default function Catalog() {
  const [products, setProducts] = useState([]);
  const [isLoading, setIsLoading] = useState(true);
  const [searchParams] = useSearchParams();
  const categoryId = searchParams.get("categoryId");

  useEffect(() => {
    async function loadProducts() {
      try {
        setIsLoading(true);
        const data = await getProducts(categoryId);
        setProducts(Array.isArray(data) ? data : []);
      } catch (error) {
        console.error("Помилка завантаження товарів:", error);
      } finally {
        setIsLoading(false);
      }
    }

    loadProducts();
  }, [categoryId]);

  if (isLoading) {
    return (
      <div className="text-center py-20 text-xs tracking-widest uppercase">
        Завантаження виробів...
      </div>
    );
  }

  return (
    <div className="w-full">
      <section className="relative w-full h-[280px] sm:h-[360px] md:h-[440px] overflow-hidden bg-neutral-100 flex items-center justify-center">
        <img
          src={bannerImg}
          alt="New In Collection"
          className="absolute inset-0 w-full h-full object-cover object-center"
        />
        <div className="absolute inset-0 bg-black/25" />
      </section>
      <div className="relative z-10 text-center text-white px-4 select-none">
        <h1 className="text-3xl sm:text-5xl md:text-6xl font-light tracking-[0.25em] uppercase font-[Halvar_Breitschrift]">
          NEW IN
        </h1>
      </div>

      <main className="w-full max-w-[1280px] mx-auto px-4 sm:px-6 lg:px-8 py-10">
        <div
          className="grid gap-x-6 gap-y-12 sm:grid-cols-2 md:grid-cols-3"
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))", // Автоматично розбиває по 3 на звичайному екрані
          }}
        >
          {products.map((product) => (
            <div key={product.id} className="min-w-0 w-full">
              <ProductCard product={product} />
            </div>
          ))}
        </div>
      </main>
    </div>
  );
}
