import React, { useEffect, useState } from "react";
import { Navigate } from "react-router-dom";
import { Bar, Line, Doughnut } from "react-chartjs-2";
import { useAuth } from "../context/AuthContext";
import { getSalesStats, getCatalogStats } from "../api/statsApi";
import { getStatusLabel } from "../api/orders";
import CacheBadge from "../components/charts/CacheBadge";
import { colorAt, money } from "../components/charts/chartSetup";

const PERIODS = [7, 30, 90];

const shortDate = (iso) => `${iso.slice(8, 10)}.${iso.slice(5, 7)}`;

function Kpi({ label, value }) {
  return (
    <div className="border border-neutral-200 p-5 bg-white">
      <p className="text-[10px] tracking-widest uppercase text-neutral-400 mb-2">
        {label}
      </p>
      <p className="text-2xl font-light tracking-wide">{value}</p>
    </div>
  );
}

function ChartCard({ title, children, empty }) {
  return (
    <div className="border border-neutral-200 p-5 bg-white">
      <h3 className="text-xs tracking-widest uppercase text-neutral-900 mb-4">
        {title}
      </h3>
      <div className="h-[260px] relative">
        {empty ? (
          <div className="h-full flex items-center justify-center text-xs text-neutral-400 tracking-wider">
            Поки немає даних
          </div>
        ) : (
          children
        )}
      </div>
    </div>
  );
}

const baseOptions = {
  responsive: true,
  maintainAspectRatio: false,
  plugins: { legend: { display: false } },
};

export default function StatsPage() {
  const { isAdmin, loading: authLoading } = useAuth();

  const [days, setDays] = useState(30);
  const [sales, setSales] = useState(null); 
  const [catalog, setCatalog] = useState(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);

  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    if (!isAdmin) return undefined;

    let cancelled = false; 

    (async () => {
      try {
        const [salesRes, catalogRes] = await Promise.all([
          getSalesStats(days),
          getCatalogStats(),
        ]);
        if (cancelled) return;
        setSales(salesRes);
        setCatalog(catalogRes);
        setError("");
      } catch (err) {
        if (!cancelled) {
          setError(err?.message || "Не вдалося завантажити статистику");
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [isAdmin, days, reloadKey]);

  const changePeriod = (p) => {
    setLoading(true);
    setDays(p);
  };

  const refresh = () => {
    setLoading(true);
    setReloadKey((k) => k + 1);
  };

  if (authLoading) return null;
  if (!isAdmin) return <Navigate to="/" replace />;

  const s = sales?.data;
  const c = catalog?.data;

  return (
    <div className="max-w-[1180px] mx-auto py-8 font-[Halvar_Breitschrift]">
      <div className="flex flex-wrap items-end justify-between gap-4 mb-8">
        <div>
          <span className="text-[11px] tracking-[0.25em] uppercase text-neutral-400 block mb-1">
            Панель адміністратора
          </span>
          <h1 className="text-3xl tracking-widest uppercase font-light">
            Статистика
          </h1>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          {PERIODS.map((p) => (
            <button
              key={p}
              type="button"
              onClick={() => changePeriod(p)}
              className={`px-4 py-2 text-xs tracking-widest uppercase border rounded-[50px] cursor-pointer transition-all ${
                days === p
                  ? "bg-black text-white border-black"
                  : "bg-white border-neutral-300 text-neutral-600 hover:border-black"
              }`}
            >
              {p} днів
            </button>
          ))}
          <button
            type="button"
            onClick={refresh}
            disabled={loading}
            className="px-4 py-2 text-xs tracking-widest uppercase border border-neutral-300 rounded-[50px] bg-white hover:border-black disabled:opacity-50 cursor-pointer"
          >
            {loading ? "Оновлення..." : "Оновити"}
          </button>
        </div>
      </div>

      {error && (
        <div className="mb-6 p-3 text-xs text-red-600 bg-red-50 border border-red-200">
          {error}
        </div>
      )}

      {!s && !error && (
        <div className="py-20 text-center text-xs tracking-widest uppercase text-neutral-400">
          Завантаження статистики...
        </div>
      )}

      {s && c && (
        <>
          <div className="flex flex-wrap items-center gap-2 mb-4">
            <span className="text-[10px] tracking-widest uppercase text-neutral-400">
              Продажі
            </span>
            <CacheBadge meta={sales} />
            <span className="text-[10px] tracking-widest uppercase text-neutral-400 ml-3">
              Каталог
            </span>
            <CacheBadge meta={catalog} />
          </div>

          <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-6">
            <Kpi label="Замовлень" value={s.totalOrders - s.cancelledOrders} />
            <Kpi label="Виручка" value={money(s.revenue)} />
            <Kpi label="Середній чек" value={money(s.averageOrderValue)} />
            <Kpi label="Скасовано" value={s.cancelledOrders} />
          </div>

          <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 mb-6">
            <ChartCard title={`Виручка по днях · ${s.days} дн.`}>
              <Line
                options={{
                  ...baseOptions,
                  scales: {
                    y: {
                      beginAtZero: true,
                      ticks: { callback: (v) => money(v) },
                    },
                    x: { ticks: { maxTicksLimit: 10 } },
                  },
                }}
                data={{
                  labels: s.daily.map((d) => shortDate(d.date)),
                  datasets: [
                    {
                      data: s.daily.map((d) => d.revenue),
                      borderColor: "#111111",
                      backgroundColor: "rgba(17,17,17,0.08)",
                      fill: true,
                      tension: 0.3,
                      pointRadius: 2,
                    },
                  ],
                }}
              />
            </ChartCard>

            <ChartCard title={`Замовлення по днях · ${s.days} дн.`}>
              <Bar
                options={{
                  ...baseOptions,
                  scales: {
                    y: { beginAtZero: true, ticks: { precision: 0 } },
                    x: { ticks: { maxTicksLimit: 10 } },
                  },
                }}
                data={{
                  labels: s.daily.map((d) => shortDate(d.date)),
                  datasets: [
                    {
                      data: s.daily.map((d) => d.orders),
                      backgroundColor: "#111111",
                    },
                  ],
                }}
              />
            </ChartCard>
          </div>

          <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 mb-6">
            <ChartCard
              title="Топ-5 товарів (штук)"
              empty={s.topProducts.length === 0}
            >
              <Bar
                options={{
                  ...baseOptions,
                  indexAxis: "y",
                  scales: { x: { beginAtZero: true, ticks: { precision: 0 } } },
                }}
                data={{
                  labels: s.topProducts.map((p) => p.name),
                  datasets: [
                    {
                      data: s.topProducts.map((p) => p.quantity),
                      backgroundColor: s.topProducts.map((_, i) => colorAt(i)),
                    },
                  ],
                }}
              />
            </ChartCard>

            <ChartCard
              title="Статуси замовлень"
              empty={s.ordersByStatus.length === 0}
            >
              <Doughnut
                options={{
                  ...baseOptions,
                  cutout: "58%",
                  plugins: { legend: { display: true, position: "bottom" } },
                }}
                data={{
                  labels: s.ordersByStatus.map((o) => getStatusLabel(o.name)),
                  datasets: [
                    {
                      data: s.ordersByStatus.map((o) => o.count),
                      backgroundColor: s.ordersByStatus.map((_, i) =>
                        colorAt(i),
                      ),
                      borderColor: "#fff",
                      borderWidth: 2,
                    },
                  ],
                }}
              />
            </ChartCard>

            <ChartCard
              title="Товарів за категоріями"
              empty={c.productsPerCategory.length === 0}
            >
              <Doughnut
                options={{
                  ...baseOptions,
                  cutout: "58%",
                  plugins: { legend: { display: true, position: "bottom" } },
                }}
                data={{
                  labels: c.productsPerCategory.map((x) => x.name),
                  datasets: [
                    {
                      data: c.productsPerCategory.map((x) => x.count),
                      backgroundColor: c.productsPerCategory.map((_, i) =>
                        colorAt(i),
                      ),
                      borderColor: "#fff",
                      borderWidth: 2,
                    },
                  ],
                }}
              />
            </ChartCard>
          </div>

          <ChartCard
            title={`Товарів за шоурумами · всього ${c.totalProducts} · середня ціна ${money(c.averagePrice)}`}
            empty={c.showrooms.length === 0}
          >
            <Bar
              options={{
                ...baseOptions,
                scales: { y: { beginAtZero: true, ticks: { precision: 0 } } },
              }}
              data={{
                labels: c.showrooms.map((x) => x.name || x.address),
                datasets: [
                  {
                    data: c.showrooms.map((x) => x.productCount),
                    backgroundColor: "#8a8a8a",
                  },
                ],
              }}
            />
          </ChartCard>
        </>
      )}
    </div>
  );
}
