import React, {
  createContext,
  useContext,
  useState,
  useEffect,
  useCallback,
} from "react";
import { request } from "../api/client";
import { useAuth } from "./AuthContext";

const CartContext = createContext();

const normalizeCart = (cart) =>
  (cart?.items ?? []).map((item) => ({
    id: item.id, // id позиції кошика (CartItem) — саме його чекають PUT/DELETE
    productId: item.productId,
    quantity: item.quantity,
    name: item.product?.name ?? "",
    price: Number(item.product?.price ?? 0),
    imageUrl: item.product?.imageUrl ?? "",
  }));

export function CartProvider({ children }) {
  const { user } = useAuth();
  const userId = user?.id;
  const [cartItems, setCartItems] = useState([]);
  const [loading, setLoading] = useState(false);

  const fetchCart = useCallback(async () => {
    if (!userId) {
      setCartItems([]);
      return;
    }
    try {
      setLoading(true);
      const data = await request(`/cart`);
      setCartItems(normalizeCart(data));
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
    const data = await request("/cart/items", {
      method: "POST",
      body: { userId, productId, quantity },
    });
    setCartItems(normalizeCart(data));
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

  const clearCart = () => setCartItems([]);
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
        clearCart,
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
