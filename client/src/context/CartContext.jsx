import React, {
  createContext,
  useContext,
  useState,
  useEffect,
  useCallback,
} from "react";
import { request } from "../api/client";

const CartContext = createContext();

export function CartProvider({ children }) {
  const [cartItems, setCartItems] = useState([]);
  const [loading, setLoading] = useState(false);

  const storedUser = localStorage.getItem("user");
  const user = storedUser ? JSON.parse(storedUser) : null;
  const userId = user?.id;

  const fetchCart = useCallback(async () => {
    if (!userId) {
      setCartItems([]);
      return;
    }
    try {
      setLoading(true);
      const data = await request(`/cart`, { method: "GET" });
      setCartItems(data?.items || []);
    } catch (err) {
      console.error("Помилка завантаження кошика з БД:", err);
    } finally {
      setLoading(false);
    }
  }, [userId]);

  useEffect(() => {
    fetchCart();
  }, [fetchCart]);

  const addToCart = async (productId, quantity = 1) => {
    if (!userId) {
      alert("Будь ласка, увійдіть в акаунт, щоб додати товар у кошик");
      return;
    }
    try {
      const data = await request("/cart/items", {
        method: "POST",
        body: { userId, productId, quantity },
      });
      setCartItems(data?.items || []);
    } catch (err) {
      console.error("Не вдалося додати товар:", err);
    }
  };

  const updateQuantity = async (cartItemId, newQuantity) => {
    try {
      await request(`/cart/items/${cartItemId}`, {
        method: "PUT",
        body: { quantity: newQuantity },
      });
      await fetchCart();
    } catch (err) {
      console.error("Помилка зміни кількості:", err);
    }
  };

  const removeFromCart = async (cartItemId) => {
    try {
      await request(`/cart/items/${cartItemId}`, {
        method: "DELETE",
      });
      await fetchCart();
    } catch (err) {
      console.error("Помилка видалення:", err);
    }
  };

  const totalPrice = cartItems.reduce(
    (sum, item) => sum + (item.price || 0) * item.quantity,
    0,
  );

  const totalCount = cartItems.reduce((sum, item) => sum + item.quantity, 0);

  return (
    <CartContext.Provider
      value={{
        cartItems,
        loading,
        addToCart,
        updateQuantity,
        removeFromCart,
        refreshCart: fetchCart,
        totalPrice,
        totalCount,
      }}
    >
      {children}
    </CartContext.Provider>
  );
}

export const useCart = () => useContext(CartContext);
