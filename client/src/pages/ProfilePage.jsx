import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import {
  getCategories,
  getCategoryById,
  createCategory,
  deleteCategory,
} from "../api/categoriesApi";
import {
  getProducts,
  createProduct,
  updateProduct,
  deleteProduct,
} from "../api/productsApi";
import { getUserOrders } from "../api/orders";

export default function Profile() {
  const navigate = useNavigate();
  const [user, setUser] = useState(() => {
    try {
      return JSON.parse(localStorage.getItem("user")) || {};
    } catch {
      return {};
    }
  });

  const token = localStorage.getItem("token") || localStorage.getItem("jwt");

  useEffect(() => {
    if (!token || !user || (!user.id && !user.email)) {
      navigate("/login", { replace: true });
    }
  }, [user, token, navigate]);

  const isAdmin =
    user?.role === "Admin" || user?.email === "adminbagelle@gmail.com";
  if (!user) return null;

  const [categories, setCategories] = useState([]);
  const [products, setProducts] = useState([]);
  const [newCategoryName, setNewCategoryName] = useState("");
  const [isAddingCategory, setIsAddingCategory] = useState(false);

  const [productForm, setProductForm] = useState({
    name: "",
    price: "",
    imageUrl: "",
    description: "",
    categoryId: "",
    showroomId: 1,
  });

  const [orders, setOrders] = useState([]);
  const [loading, setLoading] = useState(true);
  const [msg, setMsg] = useState("");

  useEffect(() => {
    async function initData() {
      setLoading(true);
      try {
        if (isAdmin) {
          const [cats, prods] = await Promise.all([
            getCategories(),
            getProducts(),
          ]);
          setCategories(cats || []);
          setProducts(prods || []);
          if (cats?.length > 0) {
            setProductForm((prev) => ({ ...prev, categoryId: cats[0].id }));
          }
        } else {
          const myOrders = await getUserOrders();
          setOrders(myOrders || []);
        }
      } catch (err) {
        console.error("Помилка завантаження даних:", err);
      } finally {
        setLoading(false);
      }
    }
    initData();
  }, [isAdmin]);

  const handleAddCategory = async (e) => {
    e.preventDefault();
    if (!newCategoryName.trim()) return;
    try {
      const created = await createCategory(newCategoryName);
      setCategories([...categories, created]);
      setNewCategoryName("");
      setIsAddingCategory(false);
      setMsg("Категорію успішно додано");
    } catch {
      setMsg("Не вдалося додати категорію");
    }
  };

  const handleDeleteCategory = async (id) => {
    if (!window.confirm("Видалити категорію?")) return;
    try {
      await deleteCategory(id);
      setCategories(categories.filter((c) => c.id !== id));
    } catch {
      setMsg("Помилка видалення категорії (можливо, до неї прив’язані товари)");
    }
  };

  const handleAddProduct = async (e) => {
    e.preventDefault();
    if (!productForm.name || !productForm.price || !productForm.categoryId) {
      setMsg("Заповніть обов’язкові поля (назва, ціна, категорія)");
      return;
    }
    try {
      const created = await createProduct({
        ...productForm,
        price: parseFloat(productForm.price),
        categoryId: parseInt(productForm.categoryId, 10),
      });
      setProducts([...products, created]);
      setProductForm({
        name: "",
        price: "",
        imageUrl: "",
        description: "",
        categoryId: categories[0]?.id || "",
        showroomId: 1,
      });
      setMsg("Товар успішно додано до каталогу");
    } catch {
      setMsg("Не вдалося створити виріб");
    }
  };

  const handleDeleteProduct = async (id) => {
    if (!window.confirm("Ви дійсно хочете видалити цей виріб?")) return;
    try {
      await deleteProduct(id);
      setProducts(products.filter((p) => p.id !== id));
      setMsg("Товар видалено");
    } catch {
      setMsg("Помилка при видаленні виробу");
    }
  };

  const handleLogout = () => {
    localStorage.removeItem("token");
    localStorage.removeItem("jwt");
    localStorage.removeItem("user");
    window.location.href = "/";
  };

  if (loading) {
    return (
      <div className="text-center py-20 text-xs tracking-widest uppercase">
        Завантаження кабінету...
      </div>
    );
  }

  return (
    <div className="max-w-[1280px] mx-auto px-6 py-12 font-[Halvar_Breitschrift]">
      <div className="flex flex-col sm:flex-row justify-between items-start sm:items-center pb-8 border-b border-neutral-200 gap-4">
        <div>
          <span className="text-[11px] tracking-[0.25em] uppercase text-neutral-400 block mb-1">
            {isAdmin ? "Панель адміністратора" : "Особистий кабінет"}
          </span>
          <h1 className="text-2xl sm:text-3xl tracking-widest uppercase font-light">
            {user?.name || user?.email || "Користувач"}
          </h1>
          <p className="text-xs text-neutral-500 mt-1">{user?.email}</p>
        </div>
        <button
          onClick={handleLogout}
          className="text-xs  tracking-widest uppercase border border-neutral-300 px-[20px] py-[8px] hover:bg-neutral-900 hover:text-white transition-all bg-[white] mb-[20px] rounded-[50px] font-[Halvar_Breitschrift]"
        >
          Вийти
        </button>
      </div>

      {msg && (
        <div className="my-4 p-3 bg-neutral-100 text-xs tracking-wider uppercase text-neutral-700">
          {msg}
        </div>
      )}

      {isAdmin ? (
        <div className="mt-10 space-y-16">
          <section>
            <div className="flex justify-between items-center mb-6">
              <h2 className="text-sm tracking-[0.2em] uppercase font-medium">
                Категорії виробів
              </h2>
              {!isAddingCategory && (
                <button
                  onClick={() => setIsAddingCategory(true)}
                  className="text-xs tracking-wider uppercase underline underline-offset-4 hover:opacity-70 font-[Halvar_Breitschrift] bg-[white] px-[20px] py-[8px] no-underline rounded-[50px]"
                >
                  + Додати категорію
                </button>
              )}
            </div>

            {isAddingCategory && (
              <form
                onSubmit={handleAddCategory}
                className="flex gap-[12px] mb-[24px] max-w-md"
              >
                <input
                  type="text"
                  placeholder="Назва нової категорії..."
                  value={newCategoryName}
                  onChange={(e) => setNewCategoryName(e.target.value)}
                  className="border border-neutral-300 p-2 text-xs tracking-wider w-full outline-none focus:border-neutral-900"
                />
                <button
                  type="submit"
                  className="bg-neutral-900 text-white px-4 py-2 text-xs tracking-widest uppercase"
                >
                  Зберегти
                </button>
                <button
                  type="button"
                  onClick={() => setIsAddingCategory(false)}
                  className="border border-neutral-300 px-3 py-2 text-xs uppercase rounded-[80px]"
                >
                  ✕
                </button>
              </form>
            )}

            <div className="flex flex-wrap gap-[8px] mb-[20px]">
              {categories.map((cat) => (
                <span
                  key={cat.id}
                  className="inline-flex items-center gap-[8px] px-[12px] py-[6px] bg-neutral-100 text-xs tracking-wide uppercase border border-neutral-200"
                >
                  {cat.name}
                  <button
                    onClick={() => handleDeleteCategory(cat.id)}
                    className="text-neutral-400 hover:text-red-600 transition-colors ml-1 rounded-[80px] font-bold border-none"
                  >
                    ×
                  </button>
                </span>
              ))}
            </div>
          </section>

          <section className="bg-neutral-50 p-[24px] sm:p-8 border border-neutral-200">
            <h2 className="text-sm tracking-[0.2em] uppercase font-medium mb-[24px]">
              Створити новий виріб
            </h2>
            <form
              onSubmit={handleAddProduct}
              className="grid grid-cols-1 md:grid-cols-2 gap-[16px]"
            >
              <div>
                <label className="block text-[15px] tracking-widest uppercase text-neutral-500 mb-[4px]">
                  Назва *
                </label>
                <input
                  type="text"
                  required
                  placeholder="Наприклад: Шопер Bagelle Noir"
                  value={productForm.name}
                  onChange={(e) =>
                    setProductForm({ ...productForm, name: e.target.value })
                  }
                  className="w-full border border-neutral-300 bg-[white] p-[10px] text-xs outline-none focus:border-black font-[Halvar_Breitschrift]"
                />
              </div>

              <div>
                <label className="block text-[15px] tracking-widest uppercase text-neutral-500 mb-[4px]">
                  Ціна (₴) *
                </label>
                <input
                  type="number"
                  required
                  placeholder="3400"
                  value={productForm.price}
                  onChange={(e) =>
                    setProductForm({ ...productForm, price: e.target.value })
                  }
                  className="w-full border border-neutral-300 bg-[white] p-[10px] text-xs outline-none focus:border-black font-[Halvar_Breitschrift]"
                />
              </div>

              <div>
                <label className="block text-[15px] tracking-widest uppercase text-neutral-500 mb-[4px]">
                  Категорія *
                </label>
                <select
                  value={productForm.categoryId}
                  onChange={(e) =>
                    setProductForm({
                      ...productForm,
                      categoryId: e.target.value,
                    })
                  }
                  className="w-full border border-neutral-300 bg-[white] p-[10px] text-xs outline-none focus:border-black font-[Halvar_Breitschrift]"
                >
                  {categories.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.name}
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <label className="block text-[15px] tracking-widest uppercase text-neutral-500 mb-[4px]">
                  Посилання на фото (URL)
                </label>
                <input
                  type="text"
                  placeholder="https://..."
                  value={productForm.imageUrl}
                  onChange={(e) =>
                    setProductForm({ ...productForm, imageUrl: e.target.value })
                  }
                  className="w-full border border-neutral-300 bg-[white] p-[10px] text-xs outline-none focus:border-black font-[Halvar_Breitschrift]"
                />
              </div>

              <div className="md:col-span-2">
                <label className="block text-[15px] tracking-widest uppercase text-neutral-500 mb-[4px] font-[Halvar_Breitschrift]">
                  Опис виробу
                </label>
                <textarea
                  rows="3"
                  placeholder="Матеріали, розмір, фурнітура..."
                  value={productForm.description}
                  onChange={(e) =>
                    setProductForm({
                      ...productForm,
                      description: e.target.value,
                    })
                  }
                  className="w-full border border-neutral-300 bg-[white] p-[10px] text-xs outline-none focus:border-black resize-none font-[Halvar_Breitschrift]"
                />
              </div>

              <div className="md:col-span-2 mt-[8px]">
                <button
                  type="submit"
                  className="bg-[white] text-white text-xs tracking-[0.2em] uppercase px-[32px] py-[12px] hover:bg-neutral-800 transition-colors font-[Halvar_Breitschrift]"
                >
                  Додати виріб
                </button>
              </div>
            </form>
          </section>

          <section>
            <h2 className="text-sm tracking-[0.2em] uppercase font-medium mb-[24px]">
              Наявні товари в каталозі ({products.length})
            </h2>
            <div className="divide-y divide-neutral-200 border-t border-b border-neutral-200">
              {products.map((p) => (
                <div
                  key={p.id}
                  className="py-[16px] flex items-center justify-between gap-[16px]"
                >
                  <div className="flex items-center gap-[16px]">
                    {p.imageUrl && (
                      <img
                        src={p.imageUrl}
                        alt={p.name}
                        className="w-[48px] h-[48px] object-contain bg-neutral-100"
                      />
                    )}
                    <div>
                      <p className="text-xs uppercase font-medium tracking-wide">
                        {p.name}
                      </p>
                      <p className="text-[11px] text-neutral-500">
                        {p.price} ₴
                      </p>
                    </div>
                  </div>
                  <button
                    onClick={() => handleDeleteProduct(p.id)}
                    className="text-xs text-[red-500] hover:text-red-700 tracking-wider uppercase font-[Halvar_Breitschrift] text-[red] px-[20px] py-[8px] bg-[white]"
                  >
                    Видалити
                  </button>
                </div>
              ))}
            </div>
          </section>
        </div>
      ) : (
        <div className="mt-10 space-y-[32px]">
          <div>
            <h2 className="text-sm tracking-[0.2em] uppercase font-medium mb-[16px">
              Мої замовлення
            </h2>
            {orders.length === 0 ? (
              <p className="text-xs tracking-wider text-neutral-400 py-[32px]">
                У вас ще немає оформлених замовлень.
              </p>
            ) : (
              <div className="space-y-[16px]">
                {orders.map((order) => (
                  <div
                    key={order.id}
                    className="border border-neutral-200 p-[20px] bg-neutral-50/50"
                  >
                    <div className="flex justify-between items-center text-xs tracking-wider uppercase mb-[3px]">
                      <span className="font-semibold">
                        Замовлення #{order.id}
                      </span>
                      <span className="text-neutral-500">
                        {new Date(order.createdAt).toLocaleDateString()}
                      </span>
                    </div>
                    <p className="text-xs text-neutral-600 mb-[8px]">
                      Місто доставки: {order.deliveryCity},{" "}
                      {order.deliveryAddress}
                    </p>
                    <p className="text-xs font-medium uppercase tracking-wide">
                      Сума: {order.totalAmount} ₴
                    </p>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      )}
    </div>
  );
}
