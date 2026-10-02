import React, { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useCart } from "../context/CartContext";
import { request } from "../api/client";

export default function CartPage() {
  const navigate = useNavigate();
  const { cartItems, updateQuantity, removeFromCart, clearCart, totalPrice } = useCart();

  const storedUser = localStorage.getItem("user");
  const user = storedUser ? JSON.parse(storedUser) : null;

  const [formData, setFormData] = useState({
    customerName: user?.fullName || user?.name || "",
    phone: "",
    city: "Київ",
    deliveryAddress: "",
    paymentMethod: "Накладений платіж",
  });

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");

  const handleChange = (e) => {
    setFormData((prev) => ({ ...prev, [e.target.name]: e.target.value }));
  };

  const handleCheckout = async (e) => {
    e.preventDefault();
    if (cartItems.length === 0) return;

    if (!formData.customerName || !formData.phone || !formData.deliveryAddress) {
      setError("Будь ласка, заповніть усі обов'язкові поля доставки");
      return;
    }

    try {
      setLoading(true);
      setError("");

      const payload = {
        userId: user?.id || null,
        customerName: formData.customerName,
        phone: formData.phone,
        city: formData.city,
        deliveryAddress: formData.deliveryAddress,
        paymentMethod: formData.paymentMethod,
        totalAmount: totalPrice,
        items: cartItems.map((item) => ({
          productId: item.id || item.productId,
          quantity: item.quantity,
          price: item.price,
        })),
      };

      await request("/orders", {
        method: "POST",
        body: payload,
      });

      clearCart();

      navigate("/profile");
    } catch (err) {
      setError(err?.message || "Не вдалося створити замовлення. Спробуйте пізніше.");
    } finally {
      setLoading(false);
    }
  };

  if (cartItems.length === 0) {
    return (
      <div className="max-w-[1240px] mx-auto px-6 py-28 text-center font-[Halvar_Breitschrift]">
        <h1 className="text-2xl uppercase tracking-widest font-light mb-3 text-neutral-900">
          Ваш кошик порожній
        </h1>
        <p className="text-xs text-neutral-500 mb-8">
          Оберіть вироби в каталозі, щоб сформувати замовлення
        </p>
        <Link
          to="/"
          className="inline-block bg-black text-white px-8 py-3 text-xs tracking-widest uppercase hover:bg-neutral-800 transition-colors"
        >
          До покупок
        </Link>
      </div>
    );
  }

  return (
    <div className="max-w-[1240px] mx-auto px-6 py-12 font-[Halvar_Breitschrift]">
      <h1 className="text-2xl sm:text-3xl uppercase tracking-widest font-light mb-10 text-neutral-900">
        Кошик замовлення
      </h1>

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-12 items-start">
        <div className="lg:col-span-7 space-y-4">
          {cartItems.map((item) => (
            <div
              key={item.id || item.productId}
              className="flex gap-4 p-4 border border-neutral-200 items-center justify-between bg-white"
            >
              <img
                src={item.imageUrl || item.image || "https://placehold.co/120x120?text=Bagllet"}
                alt={item.name}
                className="w-20 h-20 object-contain bg-neutral-50 border border-neutral-100 p-1"
              />

              <div className="flex-1 min-w-0 px-2">
                <h3 className="text-xs uppercase tracking-wider font-medium truncate text-neutral-900">
                  {item.name}
                </h3>
                <p className="text-xs text-neutral-500 mt-1">
                  ₴{Number(item.price).toLocaleString()}
                </p>
              </div>

              <div className="flex items-center border border-neutral-300">
                <button
                  type="button"
                  onClick={() => updateQuantity(item.id || item.productId, -1)}
                  className="px-2.5 py-1 text-xs text-neutral-600 hover:bg-neutral-100 cursor-pointer"
                >
                  -
                </button>
                <span className="px-3 py-1 text-xs font-medium">{item.quantity}</span>
                <button
                  type="button"
                  onClick={() => updateQuantity(item.id || item.productId, 1)}
                  className="px-2.5 py-1 text-xs text-neutral-600 hover:bg-neutral-100 cursor-pointer"
                >
                  +
                </button>
              </div>

              <button
                type="button"
                onClick={() => removeFromCart(item.id || item.productId)}
                className="text-xs text-neutral-400 hover:text-black ml-3 px-2 py-1 cursor-pointer"
                title="Видалити"
              >
                ✕
              </button>
            </div>
          ))}
        </div>

        <div className="lg:col-span-5 p-6 border border-neutral-200 bg-neutral-50/60">
          <h2 className="text-xs uppercase tracking-[0.2em] font-medium mb-6 pb-3 border-b border-neutral-200 text-neutral-900">
            Оформлення доставки
          </h2>

          {error && (
            <div className="mb-4 p-3 text-[11px] text-red-600 bg-red-50 border border-red-200">
              {error}
            </div>
          )}

          <form onSubmit={handleCheckout} className="space-y-4">
            <div>
              <label className="text-[10px] tracking-wider uppercase text-neutral-500 block mb-1">
                Одержувач (ПІБ) *
              </label>
              <input
                type="text"
                name="customerName"
                required
                value={formData.customerName}
                onChange={handleChange}
                placeholder="Ім'я та прізвище"
                className="w-full px-3 py-2 bg-white border border-neutral-300 text-xs outline-none focus:border-black"
              />
            </div>

            <div>
              <label className="text-[10px] tracking-wider uppercase text-neutral-500 block mb-1">
                Номер телефону *
              </label>
              <input
                type="tel"
                name="phone"
                required
                value={formData.phone}
                onChange={handleChange}
                placeholder="+38 (099) 000-00-00"
                className="w-full px-3 py-2 bg-white border border-neutral-300 text-xs outline-none focus:border-black"
              />
            </div>

            <div>
              <label className="text-[10px] tracking-wider uppercase text-neutral-500 block mb-1">
                Місто одержувача *
              </label>
              <input
                type="text"
                name="city"
                required
                value={formData.city}
                onChange={handleChange}
                placeholder="Київ"
                className="w-full px-3 py-2 bg-white border border-neutral-300 text-xs outline-none focus:border-black"
              />
            </div>

            <div>
              <label className="text-[10px] tracking-wider uppercase text-neutral-500 block mb-1">
                Відділення Нової Пошти / Адреса *
              </label>
              <input
                type="text"
                name="deliveryAddress"
                required
                value={formData.deliveryAddress}
                onChange={handleChange}
                placeholder="Відділення №1 або адреса кур'єра"
                className="w-full px-3 py-2 bg-white border border-neutral-300 text-xs outline-none focus:border-black"
              />
            </div>

            <div className="pt-4 border-t border-neutral-200 flex justify-between items-center text-xs font-medium">
              <span className="uppercase tracking-wider text-neutral-700">Сума замовлення:</span>
              <span className="text-base text-neutral-900 font-normal">
                ₴{totalPrice.toLocaleString()}
              </span>
            </div>

            <button
              type="submit"
              disabled={loading}
              className="w-full mt-4 bg-black text-white py-3.5 text-xs tracking-widest uppercase hover:bg-neutral-800 disabled:opacity-50 transition-colors cursor-pointer"
            >
              {loading ? "Формуємо замовлення..." : "Підтвердити замовлення"}
            </button>
          </form>
        </div>
      </div>
    </div>
  );
}