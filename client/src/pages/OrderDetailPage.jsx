import React, { useState, useEffect } from "react";
import { useParams, Link } from "react-router-dom";
import { getOrderById, cancelOrder, getStatusLabel } from "../api/orders";

export default function OrderDetailPage() {
  const { id } = useParams();

  const [order, setOrder] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [isCancelling, setIsCancelling] = useState(false);

  useEffect(() => {
    async function load() {
      try {
        setLoading(true);
        setError("");
        setOrder(await getOrderById(id));
      } catch (err) {
        setError(err?.message || "Не вдалося завантажити замовлення");
      } finally {
        setLoading(false);
      }
    }
    load();
  }, [id]);

  const handleCancel = async () => {
    if (!window.confirm("Скасувати це замовлення?")) return;
    try {
      setIsCancelling(true);
      setError("");
      setOrder(await cancelOrder(id));
    } catch (err) {
      setError(err?.message || "Не вдалося скасувати замовлення");
    } finally {
      setIsCancelling(false);
    }
  };

  if (loading) {
    return (
      <div className="min-h-[40vh] flex items-center justify-center font-[Halvar_Breitschrift] text-xs uppercase tracking-widest text-neutral-400">
        Завантаження замовлення...
      </div>
    );
  }

  if (!order) {
    return (
      <div className="min-h-[40vh] flex flex-col items-center justify-center font-[Halvar_Breitschrift] text-center">
        <p className="text-sm uppercase tracking-wider text-neutral-500 mb-4">
          {error || "Замовлення не знайдено"}
        </p>
        <Link
          to="/profile"
          className="text-xs uppercase tracking-widest underline"
        >
          До особистого кабінету
        </Link>
      </div>
    );
  }

  const isPending = order.status === "Pending";
  const isCancelled = order.status === "Cancelled";

  return (
    <div className="max-w-[900px] mx-auto px-6 py-12 font-[Halvar_Breitschrift]">
      <Link
        to="/profile"
        className="text-[11px] uppercase tracking-widest text-neutral-500 hover:text-black no-underline"
      >
        ← Мої замовлення
      </Link>

      <div className="flex flex-wrap items-center justify-between gap-3 mt-4 mb-8">
        <h1 className="text-2xl uppercase tracking-widest font-light text-neutral-900">
          Замовлення #{order.id}
        </h1>
        <span
          className={`text-[10px] uppercase tracking-widest px-3 py-1 border ${
            isCancelled
              ? "border-red-200 bg-red-50 text-red-600"
              : "border-neutral-300 bg-neutral-50 text-neutral-700"
          }`}
        >
          {getStatusLabel(order.status)}
        </span>
      </div>

      {error && (
        <div className="mb-6 p-3 text-[11px] text-red-600 bg-red-50 border border-red-200">
          {error}
        </div>
      )}

      <div className="space-y-3 mb-8">
        {order.items?.map((item) => (
          <div
            key={item.id}
            className="flex gap-4 p-4 border border-neutral-200 items-center bg-white"
          >
            <img
              src={
                item.product?.imageUrl ||
                "https://placehold.co/120x120?text=Bagllet"
              }
              alt={item.product?.name}
              className="w-20 h-20 object-contain bg-neutral-50 border border-neutral-100 p-1"
            />
            <div className="flex-1 min-w-0">
              <Link
                to={`/products/${item.productId}`}
                className="text-xs uppercase tracking-wider font-medium text-neutral-900 no-underline hover:opacity-60 block truncate"
              >
                {item.product?.name || `Товар #${item.productId}`}
              </Link>
              <p className="text-xs text-neutral-500 mt-1">
                {item.quantity} × ₴{Number(item.unitPrice).toLocaleString()}
              </p>
            </div>
            <div className="text-xs font-medium text-neutral-900">
              ₴{(item.quantity * item.unitPrice).toLocaleString()}
            </div>
          </div>
        ))}
      </div>

      <div className="grid grid-cols-1 sm:grid-cols-2 gap-6 p-6 border border-neutral-200 bg-neutral-50/60 text-xs text-neutral-700">
        <div className="space-y-1">
          <p className="uppercase tracking-widest text-[10px] text-neutral-500 mb-2">
            Доставка
          </p>
          <p>{order.customerName}</p>
          <p>{order.customerPhone}</p>
          <p>
            {order.deliveryCity}, {order.deliveryAddress}
          </p>
        </div>
        <div className="space-y-1 sm:text-right">
          <p className="uppercase tracking-widest text-[10px] text-neutral-500 mb-2">
            Оформлено
          </p>
          <p>{new Date(order.createdAt).toLocaleString("uk-UA")}</p>
          <p className="pt-3 uppercase tracking-wider text-neutral-900 text-sm font-medium">
            Разом: ₴{Number(order.totalAmount).toLocaleString()}
          </p>
        </div>
      </div>

      {isPending && (
        <button
          type="button"
          onClick={handleCancel}
          disabled={isCancelling}
          className="mt-8 border border-neutral-900 px-8 py-3 text-xs uppercase tracking-widest hover:bg-black hover:text-white disabled:opacity-50 transition-colors cursor-pointer"
        >
          {isCancelling ? "Скасовуємо..." : "Скасувати замовлення"}
        </button>
      )}
    </div>
  );
}
