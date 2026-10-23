import React, { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../context/AuthContext";
import { useFeatures } from "../context/FeatureContext";
import { getPremiumLounge } from "../api/featuresApi";

const money = (n) => `₴${Number(n || 0).toLocaleString("uk-UA")}`;

export default function PremiumPage() {
  const { user } = useAuth();
  const { plan, spent, threshold, loaded, isEnabled, isAvailable } = useFeatures();

  const hasAccess = isEnabled("PremiumLounge");

  const [lounge, setLounge] = useState(null);
  const [error, setError] = useState("");


  useEffect(() => {
    if (!hasAccess) return undefined;

    let cancelled = false;
    getPremiumLounge()
      .then((data) => {
        if (!cancelled) setLounge(data);
      })
      .catch((err) => {
        if (!cancelled) setError(err?.message || "Не вдалося завантажити сторінку");
      });

    return () => {
      cancelled = true;
    };
  }, [hasAccess]);

  const shell = "max-w-[920px] mx-auto py-10 font-[Halvar_Breitschrift]";
  const title = "text-3xl tracking-widest uppercase font-light mb-4";

  if (!user) {
    return (
      <div className={shell}>
        <h1 className={title}>Bagelle Club</h1>
        <p className="text-sm text-neutral-600 mb-6">
          Закритий клуб для постійних клієнтів. Увійдіть, щоб побачити свій статус.
        </p>
        <Link
          to="/login"
          className="inline-block px-8 py-3 text-xs tracking-widest uppercase bg-black text-white no-underline"
        >
          Увійти
        </Link>
      </div>
    );
  }

  if (!loaded) return null;

  if (!isAvailable("PremiumLounge")) {
    return (
      <div className={shell}>
        <h1 className={title}>Bagelle Club</h1>
        <p className="text-sm text-neutral-600">Ця сторінка зараз недоступна.</p>
      </div>
    );
  }

  if (!hasAccess) {
    const percent = threshold > 0 ? Math.min(100, Math.round((spent / threshold) * 100)) : 0;
    const left = Math.max(0, threshold - spent);

    return (
      <div className={shell}>
        <span className="text-[11px] tracking-[0.25em] uppercase text-neutral-400 block mb-1">
          Ваш тариф: {plan || "Free"}
        </span>
        <h1 className={title}>Bagelle Club ✦</h1>
        <p className="text-sm text-neutral-600 mb-6">
          Premium відкривається автоматично, коли сума ваших замовлень досягає {money(threshold)}.
          Тоді вам стануть доступні ранній доступ до колекцій, персональний промокод, безкоштовна
          доставка та закриті покази.
        </p>

        <div className="mb-2 flex justify-between text-xs tracking-wider uppercase">
          <span>Ваші покупки: {money(spent)}</span>
          <span>{percent}%</span>
        </div>
        <div className="h-2 bg-neutral-200 mb-3" role="progressbar" aria-valuenow={percent} aria-valuemin={0} aria-valuemax={100}>
          <div className="h-2 bg-black" style={{ width: `${percent}%` }} />
        </div>
        <p className="text-xs text-neutral-500 mb-8">Ще {money(left)} до Premium</p>

        <Link
          to="/"
          className="inline-block px-8 py-3 text-xs tracking-widest uppercase bg-black text-white no-underline"
        >
          До каталогу
        </Link>
      </div>
    );
  }

  return (
    <div className={shell}>
      <span className="text-[11px] tracking-[0.25em] uppercase text-neutral-400 block mb-1">
        Ваш тариф: {plan || "Premium"}
      </span>

      {error && (
        <div className="mb-4 p-3 text-xs text-red-600 bg-red-50 border border-red-200">{error}</div>
      )}

      {!lounge && !error && (
        <p className="text-xs tracking-widest uppercase text-neutral-400">Завантаження...</p>
      )}

      {lounge && (
        <>
          <h1 className={title}>{lounge.title} ✦</h1>
          <p className="text-sm text-neutral-600 mb-8">{lounge.welcome}</p>

          <div className="bg-neutral-900 text-white p-8 mb-8">
            <p className="text-[10px] tracking-[0.25em] uppercase text-neutral-400 mb-2">
              Ваш промокод · −{lounge.discountPercent}%
            </p>
            <p className="text-3xl tracking-[0.3em] font-light">{lounge.promoCode}</p>
          </div>

          <h2 className="text-sm tracking-[0.2em] uppercase mb-4">Привілеї</h2>
          <ul className="text-sm text-neutral-700 space-y-2 mb-10 list-none p-0">
            {lounge.perks.map((p) => (
              <li key={p}>✦ {p}</li>
            ))}
          </ul>

          <h2 className="text-sm tracking-[0.2em] uppercase mb-4">Незабаром у клубі</h2>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            {lounge.upcoming.map((u) => (
              <div key={u.name} className="border border-neutral-200 p-5">
                <p className="text-xs tracking-widest uppercase mb-1">{u.name}</p>
                <p className="text-[11px] text-neutral-500">
                  {u.date} · {u.note}
                </p>
              </div>
            ))}
          </div>
        </>
      )}
    </div>
  );
}
