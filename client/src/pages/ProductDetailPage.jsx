import React, { useState, useEffect } from "react";
import { useParams, Link } from "react-router-dom";
import { request } from "../api/client";
import { useCart } from "../context/CartContext";

export default function ProductDetailPage() {
  const { id } = useParams();
  const { addToCart } = useCart();

  const [product, setProduct] = useState(null);
  const [loading, setLoading] = useState(true);
  const [isAdding, setIsAdding] = useState(false);
  const [added, setAdded] = useState(false);

  useEffect(() => {
    async function fetchProduct() {
      try {
        setLoading(true);
        const data = await request(`/products/${id}`, { method: "GET" });
        setProduct(data);
      } catch (err) {
        console.error("Не вдалося завантажити товар:", err);
      } finally {
        setLoading(false);
      }
    }
    fetchProduct();
  }, [id]);

  const handleAddToCart = async () => {
    if (!product || isAdding) return;
    try {
      setIsAdding(true);
      await addToCart(product.id, 1);
      setAdded(true);
      setTimeout(() => setAdded(false), 2500);
    } catch (err) {
      console.error(err);
    } finally {
      setIsAdding(false);
    }
  };

  if (loading) {
    return (
      <div className="min-h-[60vh] flex items-center justify-center font-[Halvar_Breitschrift] text-xs uppercase tracking-widest text-neutral-400">
        Завантаження виробу...
      </div>
    );
  }

  if (!product) {
    return (
      <div className="min-h-[60vh] flex flex-col items-center justify-center font-[Halvar_Breitschrift] text-center">
        <p className="text-sm uppercase tracking-wider text-neutral-500 mb-4">
          Виріб не знайдено
        </p>
        <Link to="/" className="text-xs uppercase tracking-widest underline">
          Повернутися до каталогу
        </Link>
      </div>
    );
  }

  return (
    <div className="max-w-[1340px] mx-auto px-6 py-12 font-[Halvar_Breitschrift]">
      <div className="grid grid-cols-1 md:grid-cols-2 gap-12 lg:gap-20 items-center">
        <div className="flex items-center justify-center bg-white p-4 sm:p-8">
          <img
            src={
              product.imageUrl || "https://placehold.co/600x600?text=Bagllet"
            }
            alt={product.name}
            className="w-full max-h-[600px] object-contain"
          />
        </div>
        <div className="flex flex-col max-w-lg">
          {/* Назва товару */}
          <h1 className="text-2xl sm:text-3xl font-normal tracking-wide text-neutral-900 mb-4">
            {product.name}
          </h1>

          <div className="text-xl tracking-wider text-neutral-900 mb-6 font-light">
            ₴{Number(product.price).toLocaleString()}
          </div>

          <p className="text-xs tracking-wider text-neutral-500 mb-4">
            Відправлення: 2-3 робочих дні
          </p>

          <p className="text-xs tracking-wider text-neutral-900 mb-6 font-medium">
            В наявності онлайн
            {product.showroom?.name &&
              ` та у просторі «${product.showroom.name}»`}
          </p>

          <button
            onClick={handleAddToCart}
            disabled={isAdding}
            className="w-full bg-black text-white py-4 text-xs font-medium tracking-[0.2em] uppercase hover:bg-neutral-800 transition-colors disabled:opacity-50 mb-4 cursor-pointer"
          >
            {isAdding
              ? "Додаємо..."
              : added
                ? "Додано в кошик ✓"
                : "ДОДАТИ В КОШИК"}
          </button>

          {product.description && (
            <p className="text-xs leading-relaxed text-neutral-600 mb-8 whitespace-pre-line">
              {product.description}
            </p>
          )}

          <div className="border-t border-neutral-100 pt-6 text-[11px] text-neutral-400 space-y-1 tracking-wider leading-relaxed">
            {product.category?.name && (
              <p>
                <span className="uppercase text-neutral-500">Категорія:</span>{" "}
                {product.category.name}
              </p>
            )}
            {product.showroom?.name && (
              <p>
                <span className="uppercase text-neutral-500">
                  Шоурум представлення:
                </span>{" "}
                {product.showroom.name} ({product.showroom.address})
              </p>
            )}
            <p>
              <span className="uppercase text-neutral-500">Артикул:</span> BG-0
              {product.id}
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}
