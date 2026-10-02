import React, { useState, useEffect, useMemo } from "react";
import { request } from "../api/client";
import ShowroomMap from "../components/ShowroomMap";

const getCityFromAddress = (address = "") => {
  if (!address) return "Інше";
  const clean = address.replace(/^(м\.|смт\.|село|м\s)/i, "").trim();
  const city = clean.split(",")[0].trim();
  return city || "Інше";
};

export default function ShowroomsPage() {
  const [showrooms, setShowrooms] = useState([]);
  const [loading, setLoading] = useState(true);
  const [selectedCity, setSelectedCity] = useState("all");
  const [searchQuery, setSearchQuery] = useState("");
  const [activeShowroom, setActiveShowroom] = useState(null);

  useEffect(() => {
    async function fetchShowrooms() {
      try {
        setLoading(true);
        const data = await request("/showrooms", { method: "GET" });
        setShowrooms(data || []);
      } catch (err) {
        console.error("Не вдалося завантажити шоуруми:", err);
      } finally {
        setLoading(false);
      }
    }
    fetchShowrooms();
  }, []);

  const cities = useMemo(() => {
    const unique = Array.from(
      new Set(showrooms.map((s) => getCityFromAddress(s.address))),
    );
    return ["all", ...unique];
  }, [showrooms]);

  const filteredShowrooms = useMemo(() => {
    return showrooms.filter((s) => {
      const city = getCityFromAddress(s.address);
      const matchesCity = selectedCity === "all" || city === selectedCity;
      const matchesSearch =
        s.name?.toLowerCase().includes(searchQuery.toLowerCase()) ||
        s.address?.toLowerCase().includes(searchQuery.toLowerCase());
      return matchesCity && matchesSearch;
    });
  }, [showrooms, selectedCity, searchQuery]);

  return (
    <div className="max-w-[1380px] mx-auto px-6 py-12 font-[Halvar_Breitschrift]">
      <div className="text-center mb-10">
        <span className="text-[11px] tracking-[0.25em] uppercase text-neutral-400 block mb-2">
          Простори Bagelle
        </span>
        <h1 className="text-3xl sm:text-4xl tracking-widest uppercase font-light">
          Наші шоуруми
        </h1>
        <p className="text-xs text-neutral-500 max-w-md mx-auto mt-2">
          Завітайте до наших фізичних просторів, щоб приміряти вироби та відчути
          фактури шкіри
        </p>
      </div>

      <div className="flex flex-col md:flex-row items-center justify-between gap-4 mb-8 pb-6 border-b border-neutral-200">
        <div className="flex flex-wrap gap-[8px] mb-[20px]">
          {cities.map((city) => (
            <button
              key={city}
              onClick={() => {
                setSelectedCity(city);
                setActiveShowroom(null);
              }}
              className={`px-[16px] py-[8px] bg-[white] text-xs tracking-widest uppercase border transition-all rounded-[50px] font-[Halvar_Breitschrift] ${
                selectedCity === city
                  ? "bg-black text-white border-black"
                  : "border-neutral-300 text-neutral-600 hover:border-black"
              }`}
            >
              {city === "all" ? "Всі міста" : city}
            </button>
          ))}
        </div>

        <div className="w-full md:w-72 bg-white">
          <input
            type="text"
            placeholder="Пошук за адресою чи назвою..."
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            className="w-full border text-xs  tracking-wider outline-none focus:border-black py-[10px] rounded-[50px] font-[Halvar_Breitschrift] mb-[20px]"
          />
        </div>
      </div>

      {loading ? (
        <div className="text-center py-20 text-xs tracking-widest uppercase text-neutral-400">
          Завантаження просторів...
        </div>
      ) : (
        <div className="grid grid-cols-1 lg:grid-cols-12 gap-8 items-start">
          <div className="lg:col-span-5 space-y-4 max-h-[550px] overflow-y-auto pr-2">
            <p className="text-[11px] uppercase tracking-wider text-neutral-400 mb-2">
              Знайдено локацій: {filteredShowrooms.length}
            </p>

            {filteredShowrooms.length === 0 ? (
              <div className="p-8 border border-neutral-200 text-center text-xs text-neutral-400 tracking-wider">
                За обраними фільтрами шоурумів не знайдено
              </div>
            ) : (
              filteredShowrooms.map((showroom) => (
                <div
                  key={showroom.id}
                  onClick={() => setActiveShowroom(showroom)}
                  className={`p-5 border cursor-pointer transition-all ${
                    activeShowroom?.id === showroom.id
                      ? "border-black bg-neutral-50 shadow-sm"
                      : "border-neutral-200 hover:border-neutral-400"
                  }`}
                >
                  <div className="flex justify-between items-start mb-2">
                    <span className="text-[10px] tracking-[0.2em] uppercase text-neutral-400">
                      {showroom.city}
                    </span>
                    <span className="text-[10px] tracking-wider text-neutral-400 underline">
                      Показати на мапі →
                    </span>
                  </div>
                  <h3 className="text-sm uppercase tracking-wide font-medium text-neutral-900 mb-1">
                    {showroom.name || `Bagelle ${showroom.city}`}
                  </h3>
                  <p className="text-xs text-neutral-600 mb-2">
                    {showroom.address}
                  </p>
                  {showroom.phone && (
                    <p className="text-xs text-neutral-500 mb-1">
                      Тел: {showroom.phone}
                    </p>
                  )}
                  {showroom.workingHours && (
                    <p className="text-[11px] text-neutral-400">
                      Графік: {showroom.workingHours}
                    </p>
                  )}
                </div>
              ))
            )}
          </div>

          <div className="lg:col-span-7">
            <ShowroomMap
              showrooms={filteredShowrooms}
              selectedShowroom={activeShowroom}
              onSelectShowroom={(showroom) => setActiveShowroom(showroom)}
            />
          </div>
        </div>
      )}
    </div>
  );
}
