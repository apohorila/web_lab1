import React from "react";
import MiniDoughnut from "./MiniDoughnut";
import { colorAt } from "./chartSetup";

// Міні-статистика шоуруму для popup на мапі
export default function ShowroomMiniChart({ stat }) {
  if (!stat) return null;

  if (stat.productCount === 0) {
    return (
      <p className="text-[11px] text-neutral-400 mt-3 pt-3 border-t border-neutral-200">
        У цьому шоурумі поки немає товарів
      </p>
    );
  }

  return (
    <div className="mt-3 pt-3 border-t border-neutral-200">
      <p className="text-[10px] tracking-widest uppercase text-neutral-400 mb-2">
        Асортимент · {stat.productCount} шт.
      </p>
      <div className="flex items-center gap-3">
        <MiniDoughnut items={stat.byCategory} size={96} />
        <ul className="text-[11px] text-neutral-600 space-y-1 m-0 p-0 list-none">
          {stat.byCategory.slice(0, 5).map((c, idx) => (
            <li key={c.name} className="flex items-center gap-1.5">
              <span
                className="inline-block w-2 h-2 rounded-full"
                style={{ backgroundColor: colorAt(idx) }}
              />
              {c.name}: <b>{c.count}</b>
            </li>
          ))}
        </ul>
      </div>
    </div>
  );
}
