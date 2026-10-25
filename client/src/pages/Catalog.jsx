import React, { useState, useEffect, useRef } from "react";
import { useSearchParams } from "react-router-dom";
import ProductCard from "../components/ProductCard";
import { getProductsPage } from "../api/productsApi";
import bannerImg from "../assets/icons/banner.jpg";

const PAGE_SIZE = 12;

const nextSkipOf = (page) =>
  page.nextLink
    ? Number(new URL(page.nextLink).searchParams.get("skip"))
    : null;

export default function Catalog() {
  const [products, setProducts] = useState([]);
  const [total, setTotal] = useState(0);
  const [nextSkip, setNextSkip] = useState(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isLoadingMore, setIsLoadingMore] = useState(false);
  const [searchParams] = useSearchParams();
  const categoryId = searchParams.get("categoryId");
  const activeCategory = useRef(categoryId);

  useEffect(() => {
    let cancelled = false;
    activeCategory.current = categoryId;
    async function loadFirstPage() {
      try {
        setIsLoading(true);
        const page = await getProductsPage({
          categoryId,
          skip: 0,
          limit: PAGE_SIZE,
        });
        if (cancelled) return;
        setProducts(page.items);
        setTotal(page.total);
        setNextSkip(nextSkipOf(page));
      } catch (error) {
        console.error("Помилка завантаження товарів:", error);
      } finally {
        if (!cancelled) setIsLoading(false);
      }
    }

    loadFirstPage();
    return () => {
      cancelled = true;
    };
  }, [categoryId]);

  const loadMore = async () => {
    if (nextSkip === null || isLoadingMore) return;
    const requestedCategory = categoryId;

    try {
      setIsLoadingMore(true);
      const page = await getProductsPage({
        categoryId,
        skip: nextSkip,
        limit: PAGE_SIZE,
      });
      if (activeCategory.current !== requestedCategory) return; // користувач уже змінив категорію
      setProducts((prev) => [...prev, ...page.items]);
      setTotal(page.total);
      setNextSkip(nextSkipOf(page));
    } catch (error) {
      console.error("Помилка завантаження наступної сторінки:", error);
    } finally {
      setIsLoadingMore(false);
    }
  };

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
        {products.length === 0 && (
          <p className="text-center text-xs tracking-widest uppercase text-neutral-400 py-10">
            Товарів поки немає
          </p>
        )}

        {products.length > 0 && (
          <div className="mt-14 flex flex-col items-center gap-4 font-[Halvar_Breitschrift]">
            <p className="text-[11px] tracking-widest uppercase text-neutral-400">
              Показано {products.length} з {total}
            </p>
            {nextSkip !== null && (
              <button
                type="button"
                onClick={loadMore}
                disabled={isLoadingMore}
                className="px-10 py-3 text-xs tracking-[0.2em] uppercase border border-black bg-white hover:bg-black hover:text-white transition-colors disabled:opacity-50 cursor-pointer"
              >
                {isLoadingMore ? "Завантаження..." : "Показати ще"}
              </button>
            )}
          </div>
        )}
      </main>
    </div>
  );
}
