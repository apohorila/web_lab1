import React from "react";
import {
  BrowserRouter,
  Routes,
  Route,
  Navigate,
  Outlet,
} from "react-router-dom";
import Navbar from "./components/NavBar";
import Catalog from "./pages/Catalog";
import LoginAuthPage from "./pages/LoginAuthPage";
import ShowroomsPage from "./pages/ShowroomsPage";
import Profile from "./pages/ProfilePage";
import ProductDetailPage from "./pages/ProductDetailPage";
import CartPage from "./pages/CartPage";
import { useAuth } from "./context/AuthContext";

function ProtectedRoute() {
  const { user, loading } = useAuth();
  if (loading) return null;
  return user ? <Outlet /> : <Navigate to="/login" replace />;
}

function App() {
  return (
    <BrowserRouter>
      <div className="min-h-screen bg-white text-neutral-900">
        <Navbar />
        <main className="max-w-7xl mx-auto px-6 py-8">
          <Routes>
            <Route path="/" element={<Catalog />} />
            <Route path="/catalog" element={<Catalog />} />
            <Route path="/login" element={<LoginAuthPage />} />
            <Route path="/showrooms" element={<ShowroomsPage />} />
            <Route path="/products/:id" element={<ProductDetailPage />} />
            <Route path="/cart" element={<CartPage />} />
            <Route element={<ProtectedRoute />}>
              <Route path="/profile" element={<Profile />} />
            </Route>
            <Route path="*" element={<Navigate to="/" replace />} />
            {/* <Footer /> */}
          </Routes>
        </main>
      </div>
    </BrowserRouter>
  );
}

export default App;
