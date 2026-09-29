import React from "react";
import { Link } from "react-router-dom";

export default function ProductCard({ product }) {
  if (!product) return null;

  const formattedPrice = `₴${Number(product.price || 0).toLocaleString("uk-UA")}`;

  const defaultImage =
    "https://images.unsplash.com/photo-1590874103328-eac38a683ce7?auto=format&fit=crop&w=800&q=80";

  return (
    <div className="group flex flex-col items-center text-center bg-white cursor-pointer font-[Halvar_Breitschrift] no-underline text-black  select-none">
      <Link
        to={`/products/${product.id}`}
        className="w-full aspect-4/3 flex items-center justify-center overflow-hidden mb-[24px] bg-white"
      >
        <img
          src={product.imageUrl || defaultImage}
          alt={product.name}
          className="h-full w-full object-contain object-center transition-transform duration-500 ease-out group-hover:scale-105"
          onError={(e) => {
            e.currentTarget.src = defaultImage;
          }}
        />
      </Link>

      <Link to={`/products/${product.id}`} className="block">
        <h3 className="text-xs text-inherit md:text-sm font-normal tracking-[0.18em]   no-underline uppercase transition-opacity group-hover:opacity-70">
          {product.name}
        </h3>
      </Link>

      <p className="mt-2 text-xs md:text-sm font-normal tracking-wide text-neutral-900">
        {formattedPrice}
      </p>
    </div>
  );
}
