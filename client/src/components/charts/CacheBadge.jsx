import React from "react";

// Показує, чи прийшла відповідь із кешу API (заголовок X-Cache) і за скільки мілісекунд
export default function CacheBadge({ meta }) {
  if (!meta) return null;
  const hit = meta.cache === "HIT";

  return (
    <span
      className={`inline-block text-[10px] tracking-widest uppercase px-2 py-0.5 border ${
        hit
          ? "border-emerald-200 bg-emerald-50 text-emerald-700"
          : "border-neutral-300 bg-white text-neutral-500"
      }`}
      title="Відповідь API: чи взято з кешу сервера"
    >
      {meta.cache ? `кеш: ${meta.cache}` : "без кешу"}
      {meta.provider ? ` · ${meta.provider}` : ""} · {meta.ms} мс
    </span>
  );
}
