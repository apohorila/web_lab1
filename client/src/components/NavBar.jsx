import React, { useState, useEffect } from "react";
import { Link } from "react-router-dom";
import { getCategories } from "../api/categoriesApi";
import searchIcon from '../assets/icons/search.svg';
import userIcon from '../assets/icons/profile.svg';
import cartIcon from '../assets/icons/cart.svg';

export default function Navbar() {
  const [isCatalogOpen, setIsCatalogOpen] = useState(false);
  const [categories, setCategories] = useState([]);
  const [isLoading, setIsLoading] = useState(false);

  const isAdmin = false;

  useEffect(() => {
    async function fetchCategoriesData() {
      try {
        setIsLoading(true);
        const data = await getCategories();
        setCategories(Array.isArray(data) ? data : []);
      } catch (error) {
        console.error("Не вдалося завантажити категорії:", error);
      } finally {
        setIsLoading(false);
      }
    }

    fetchCategoriesData();
  }, []);

  return (
    <header className="sticky top-0 z-50 bg-white border-b border-neutral-100 font-[Halvar_Breitschrift] text-black tracking-wide">
      <div className="max-w-7xl mx-auto px-[30px] h-[80px] flex items-center justify-between">
        {/* ЛІВА ЧАСТИНА: Каталог з ховером */}
        <div className="flex items-center gap-8">
          <div
            className="relative py-6 cursor-pointer"
            onMouseEnter={() => setIsCatalogOpen(true)}
            onMouseLeave={() => setIsCatalogOpen(false)}
          >
            <Link
              to="/"
              className="hover:opacity-60 transition-opacity tracking-widest no-underline text-inherit"
            >
              Каталог
            </Link>

            {isCatalogOpen && (
              <div className="absolute top-full left-0 w-[384px] bg-white border border-neutral-100 shadow-lg p-[24px] normal-case no-underline text-inherit text-sm font-normal tracking-normal z-50"
              style={{ backgroundColor: '#ffffff' }}>
                {isLoading ? (
                  <div className="text-neutral-400 text-xs py-2">
                    Завантаження категорій...
                  </div>
                ) : categories.length === 0 ? (
                  <div className="text-neutral-400 text-xs py-2">
                    Немає доступних категорій
                  </div>
                ) : (
                  <div className="grid grid-cols-2 gap-x-6 gap-y-3">
                    <Link
                      to="/"
                      className="font-medium text-neutral-950 hover:opacity-60 col-span-2 pb-1 border-b border-neutral-100"
                    >
                      Дивитись все
                    </Link>
                    {categories.map((category) => (
                      <Link
                        key={category.id}
                        to={`/?categoryId=${category.id}`}
                        className="hover:opacity-60 transition-opacity truncate"
                        title={category.name}
                      >
                        {category.name}
                      </Link>
                    ))}
                  </div>
                )}
              </div>
            )}
          </div>

          {isAdmin && (
            <Link
              to="/add"
              className="text-inherit hover:text-neutral-900 transition-colors tracking-widest text-[11px]"
            >
              + Додати виріб
            </Link>
          )}
        </div>

        <div className="absolute left-1/2 -translate-x-1/2">
          <Link
            to="/"
            className="text-[36px] md:text-3xl font-bold tracking-[0.25em] no-underline text-inherit uppercase font-sans hover:opacity-80 transition-opacity"
          >
            Bagelle
          </Link>
        </div>

        <div className="flex items-center gap-[24px]">
          <Link
            to="/map"
            className="hover:opacity-60 transition-opacity tracking-widest text-inherit no-underline"
          >
            Магазини
          </Link>

          <Link
            to="/search"
            className="hover:opacity-60 transition-opacity p-1"
            title="Пошук"
          >
            <img src={searchIcon} alt="Пошук" className="w-[24px] h-[24px]"/>
          </Link>

          <Link
            to="/profile"
            className="hover:opacity-60 transition-opacity p-1"
            title="Особистий кабінет"
          >
            <img src={userIcon} alt="Профіль" className="w-[24px] h-[24px]"/>
          </Link>

          <Link
            to="/cart"
            className="hover:opacity-60 transition-opacity p-1 relative"
            title="Кошик"
          >
            <img src={cartIcon} alt="Кошик" className="w-[24px] h-[24px]"/>
          </Link>
        </div>
      </div>
    </header>
  );
}
