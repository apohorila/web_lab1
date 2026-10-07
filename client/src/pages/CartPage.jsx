import React, { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useCart } from "../context/CartContext";
import { useAuth } from "../context/AuthContext";
import { createOrder } from "../api/orders";

export default function CartPage() {
  const navigate = useNavigate();
  const { cartItems, updateQuantity, removeFromCart, clearCart, totalPrice } = useCart();

 const { user } = useAuth();

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
        customerName: formData.customerName.trim(),
        customerPhone: formData.phone.trim(),
        customerEmail: user?.email || "",
        deliveryCity: formData.city.trim(),
        deliveryAddress: formData.deliveryAddress.trim(),
        items: cartItems.map((item) => ({
          productId: item.productId,
          quantity: item.quantity,
        })),
      };

      await createOrder(payload);

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
      <div className="max-w-[1240px] mx-auto px-[24px] py-[112px] text-center font-[Halvar_Breitschrift]">
        <h1 className="text-2xl uppercase tracking-widest font-light mb-[12px] text-neutral-900">
          Ваш кошик порожній
        </h1>
        <p className="text-xs text-neutral-500 mb-[32px]">
          Оберіть вироби в каталозі, щоб сформувати замовлення
        </p>
        <Link
          to="/"
          className="inline-block bg-black text-white px-[32px] py-[12px] text-xs tracking-widest uppercase hover:bg-neutral-800 transition-colors"
        >
          До покупок
        </Link>
      </div>
    );
  }

  return (
    <div className="max-w-[1240px] mx-auto px-[24px] py-[48px] font-[Halvar_Breitschrift]">
      <h1 className="text-2xl sm:text-3xl uppercase tracking-widest font-light mb-[40px] text-neutral-900">
        Кошик замовлення
      </h1>

      <div className="grid grid-cols-1 lg:grid-cols-12 gap-[48px] items-start">
        <div className="lg:col-span-7 space-y-4">
          {cartItems.map((item) => (
            <div
              key={item.id || item.productId}
              className="flex gap-[16px] p-[16px] border border-neutral-200 items-center justify-between bg-white"
            >
              <img
                src={item.imageUrl || item.image || "https://placehold.co/120x120?text=Bagllet"}
                alt={item.name}
                className="w-[80px] h-[80px] object-contain bg-neutral-50 border border-neutral-100 p-[4px]"
              />

              <div className="flex-1 min-w-0 px-[8px]">
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
                <span className="px-[12px] py-[4px] text-xs font-medium">{item.quantity}</span>
                <button
                  type="button"
                  onClick={() => updateQuantity(item.id || item.productId, 1)}
                  className="px-[10px] py-[4px] text-xs text-neutral-600 hover:bg-neutral-100 cursor-pointer"
                >
                  +
                </button>
              </div>

              <button
                type="button"
                onClick={() => removeFromCart(item.id || item.productId)}
                className="text-xs text-neutral-400 hover:text-black ml-[12px] px-[8px] py-[4px] cursor-pointer"
                title="Видалити"
              >
                ✕
              </button>
            </div>
          ))}
        </div>

        <div className="lg:col-span-5 p-[24px] border border-neutral-200 bg-neutral-50/60">
          <h2 className="text-xs uppercase tracking-[0.2em] font-medium mb-6 pb-[12px] border-b border-neutral-200 text-neutral-900">
            Оформлення доставки
          </h2>

          {error && (
            <div className="mb-[12px] p-[12px] text-[11px] text-red-600 bg-red-50 border border-red-200">
              {error}
            </div>
          )}

          <form onSubmit={handleCheckout} className="space-y-4">
            <div>
              <label className="text-[10px] tracking-wider uppercase text-neutral-500 block mb-[4px]">
                Одержувач (ПІБ) *
              </label>
              <input
                type="text"
                name="customerName"
                required
                value={formData.customerName}
                onChange={handleChange}
                placeholder="Ім'я та прізвище"
                className="w-full px-[12px] py-[8px] bg-[white] border border-neutral-300 text-xs outline-none focus:border-black"
              />
            </div>

            <div>
              <label className="text-[10px] tracking-wider uppercase text-neutral-500 block mb-[4px]">
                Номер телефону *
              </label>
              <input
                type="tel"
                name="phone"
                required
                value={formData.phone}
                onChange={handleChange}
                placeholder="+38 (099) 000-00-00"
                className="w-full px-[12px] py-[8px] bg-[white] border border-neutral-300 text-xs outline-none focus:border-black"
              />
            </div>

            <div>
              <label className="text-[10px] tracking-wider uppercase text-neutral-500 block mb-[4px]">
                Місто одержувача *
              </label>
              <input
                type="text"
                name="city"
                required
                value={formData.city}
                onChange={handleChange}
                placeholder="Київ"
                className="w-full px-[12px] py-[8px] bg-white border border-neutral-300 text-xs outline-none focus:border-black"
              />
            </div>

            <div>
              <label className="text-[10px] tracking-wider uppercase text-neutral-500 block mb-[4px]">
                Відділення Нової Пошти / Адреса *
              </label>
              <input
                type="text"
                name="deliveryAddress"
                required
                value={formData.deliveryAddress}
                onChange={handleChange}
                placeholder="Відділення №1 або адреса кур'єра"
                className="w-full px-[12px] py-[8px] bg-[white] border border-neutral-300 text-xs outline-none focus:border-black"
              />
            </div>

            <div className="pt-[16px] border-t border-neutral-200 flex justify-between items-center text-xs font-medium">
              <span className="uppercase tracking-wider text-neutral-700">Сума замовлення:</span>
              <span className="text-base text-neutral-900 font-normal">
                ₴{totalPrice.toLocaleString()}
              </span>
            </div>

            <button
              type="submit"
              disabled={loading}
              className="w-full mt-[16px] bg-[black] text-[white] py-[15px] text-xs tracking-widest uppercase hover:bg-neutral-800 disabled:opacity-50 transition-colors cursor-pointer"
            >
              {loading ? "Формуємо замовлення..." : "Підтвердити замовлення"}
            </button>
          </form>
        </div>
      </div>
    </div>
  );
}