import React, { createContext, useContext, useState, useEffect } from "react";

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null);
  const [token, setToken] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const savedToken = localStorage.getItem("bagelle_token");
    const savedUser = localStorage.getItem("bagelle_user");

    if (savedToken && savedUser) {
      try {
        setToken(savedToken);
        setUser(JSON.parse(savedUser));
      } catch (e) {
        console.error("Не вдалося розпарсити збереженого користувача", e);
        localStorage.removeItem("bagelle_token");
        localStorage.removeItem("bagelle_user");
      }
    }
    setLoading(false);
  }, []);

  const login = (authToken, userData) => {
    setToken(authToken);
    setUser(userData);
    localStorage.setItem("bagelle_token", authToken);
    localStorage.setItem("bagelle_user", JSON.stringify(userData));
    console.log(userData.id);
  };

  const logout = () => {
    setToken(null);
    setUser(null);
    localStorage.removeItem("bagelle_token");
    localStorage.removeItem("bagelle_user");
  };

  const isAdmin = user?.role === "Admin";

  return (
    <AuthContext.Provider
      value={{ user, token, isAdmin, login, logout, loading }}
    >
      {!loading && children}
    </AuthContext.Provider>
  );
}

export const useAuth = () => useContext(AuthContext);
