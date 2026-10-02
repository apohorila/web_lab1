import React, { useState, useEffect } from "react";
import { Link } from "react-router-dom";
import { getCategories } from "../api/categoriesApi";
import searchIcon from "../assets/icons/search.svg";
import userIcon from "../assets/icons/profile.svg";
import cartIcon from "../assets/icons/cart.svg";
import { useAuth } from "../context/AuthContext";

export default function Navbar() {
  const { user, isAdmin, logout } = useAuth();
  const [isCatalogOpen, setIsCatalogOpen] = useState(false);
  const [categories, setCategories] = useState([]);
  const [isLoading, setIsLoading] = useState(false);

  const token = localStorage.getItem("token") || localStorage.getItem("jwt");

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
        <div className="flex items-center gap-[32px]">
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
              <div
                className="absolute top-full left-0 w-[384px] bg-white border border-neutral-100 shadow-lg p-[24px] normal-case no-underline text-inherit text-sm font-normal tracking-normal z-50"
                style={{ backgroundColor: "#ffffff" }}
              >
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
            to="/showrooms"
            className="hover:opacity-60 transition-opacity tracking-widest text-inherit no-underline"
          >
            Магазини
          </Link>

          <Link
            to="/search"
            className="hover:opacity-60 transition-opacity p-1"
            title="Пошук"
          >
            <img src={searchIcon} alt="Пошук" className="w-[24px] h-[24px]" />
          </Link>

          <Link
            to={token ? "/profile" : "/login"}
            className="hover:opacity-60 transition-opacity p-1"
            title={user && user.email ? "Особистий кабінет" : "Увійти"}
          >
            <img src={userIcon} alt="Профіль" className="w-[24px] h-[24px]" />
          </Link>

          <Link
            to="/cart"
            className="hover:opacity-60 transition-opacity p-1 relative"
            title="Кошик"
          >
            <img src={cartIcon} alt="Кошик" className="w-[24px] h-[24px]" />
          </Link>
        </div>
      </div>
    </header>
  );
}
