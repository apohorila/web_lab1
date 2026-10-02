import React from "react";
import { useState } from "react";
import { GoogleLogin } from "@react-oauth/google";
import { useNavigate } from "react-router-dom";
import { request } from "../api/client";
import { useAuth } from "../context/AuthContext";

export default function LoginAuthPage() {
  const navigate = useNavigate();
  const { login } = useAuth();

  const [isRegister, setIsRegister] = useState(false);
  const [formData, setFormData] = useState({
    email: "",
    password: "",
    confirmPassword: "",
  });

  const [error, setError] = useState("");
  const [isLoading, setIsLoading] = useState(false);

  const handleChange = (e) => {
    setFormData((prev) => ({
      ...prev,
      [e.target.name]: e.target.value,
    }));
    setError("");
  };
  const handleAuthSuccess = (data) => {
    console.log("Отримані дані від бекенда:", data);

    const token = data?.token || data?.Token || data?.jwt;
    const user = data?.user || data?.User;

    if (token) {
      localStorage.setItem("token", token);
    }
    if (user) {
      localStorage.setItem("user", JSON.stringify(user));
    }

    if (login) {
      login(token, user);
    }

    if (user?.role === "Admin") {
      navigate("/admin");
    } else {
      navigate("/profile");
    }
  };

  const validateRegister = () => {
    if (formData.password.length < 8) {
      return "Пароль повинен містити щонайменше 8 символів";
    }
    if (!/\d/.test(formData.password)) {
      return "Пароль повинен містити хоча б одну цифру";
    }
    if (formData.password !== formData.confirmPassword) {
      return "Паролі не збігаються";
    }
    return null;
  };
  const handleSubmit = async (e) => {
    e.preventDefault();
    setError("");

    if (isRegister) {
      const validationError = validateRegister();
      if (validationError) {
        setError(validationError);
        return;
      }
    }

    setIsLoading(true);

    const endpoint = isRegister ? "/auth/register" : "/auth/login";
    const payload = isRegister
      ? { email: formData.email, password: formData.password }
      : { email: formData.email, password: formData.password };

    try {
      const data = await request(endpoint, {
        method: "POST",
        body: payload,
      });
      handleAuthSuccess(data);
    } catch (err) {
      setError(err?.message || "Помилка авторизації. Перевірте введені дані.");
    } finally {
      setIsLoading(false);
    }
  };

  const handleGoogleSuccess = async (credentialResponse) => {
    try {
      setError("");
      const data = await request("/auth/google", {
        method: "POST",
        body: { idToken: credentialResponse.credential },
      });
      handleAuthSuccess(data);
    } catch (err) {
      setError(err?.message || "Помилка входу через Google");
    }
  };

  return (
    <div className="min-h-[70vh] flex flex-col items-center justify-center px-[20px] py-[48px] font-[Halvar_Breitschrift]">
      <div className="w-full max-w-md p-[32px] border border-neutral-200 bg-[white]">
        <h1 className="text-xl tracking-widest uppercase mb-[24px] font-light font-[Halvar_Breitschrift] text-center">
          {isRegister ? "Реєстрація в Bagelle" : "Вхід в акаунт Bagelle"}
        </h1>

        {error && (
          <div className="mb-[24px] p-[12px] text-xs tracking-wide text-red-600 bg-red-50 border border-red-200">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit} className="flex flex-col gap-4">
          <div>
            <label className="block text-[15px] uppercase tracking-wider text-neutral-500 mb-[4px]">
              Email *
            </label>
            <input
              type="email"
              name="email"
              value={formData.email}
              onChange={handleChange}
              required
              className="w-full px-[12px] py-[8px] border border-neutral-300 focus:outline-none focus:border-black text-xs tracking-wider"
              placeholder="example@bagelle.ua"
            />
          </div>

          <div>
            <label className="block text-[15px] uppercase tracking-wider text-neutral-500 mb-[4px]">
              Пароль *
            </label>
            <input
              type="password"
              name="password"
              value={formData.password}
              onChange={handleChange}
              required
              className="w-full px-[12px] py-[8px] border border-neutral-300 focus:outline-none focus:border-black text-xs tracking-wider"
              placeholder="••••••••"
            />
            {isRegister && (
              <span className="text-[15px] text-neutral-400 mt-[4px] block">
                Мінімум 8 символів та 1 цифра
              </span>
            )}
          </div>

          {isRegister && (
            <div>
              <label className="block text-[15px] uppercase tracking-wider text-neutral-500 mb-[4px]">
                Підтвердження паролю *
              </label>
              <input
                type="password"
                name="confirmPassword"
                value={formData.confirmPassword}
                onChange={handleChange}
                required
                className="w-full px-[12px] py-[8px] border border-neutral-300 focus:outline-none focus:border-black text-xs tracking-wider"
                placeholder="••••••••"
              />
            </div>
          )}

          <button
            type="submit"
            disabled={isLoading}
            className="w-full mt-[8px] py-[12px] bg-[neutral-900] text-white text-xs uppercase tracking-widest hover:bg-neutral-800 transition-colors disabled:opacity-50 font-[Halvar_Breitschrift]"
          >
            {isLoading
              ? "Зачекайте..."
              : isRegister
                ? "Зареєструватися"
                : "Увійти"}
          </button>
        </form>

        {!isRegister && (
          <>
            <div className="relative my-[24px] flex items-center justify-center">
              <div className="absolute inset-0 flex items-center">
                <div className="w-full border-t border-neutral-200"></div>
              </div>
              <span className="relative bg-[white] px-[12px] text-[15px] uppercase tracking-wider text-neutral-400 font-[Halvar_Breitschrift]">
                або
              </span>
            </div>

            <div className="flex justify-center w-full font-[Halvar_Breitschrift]">
              <GoogleLogin
                onSuccess={handleGoogleSuccess}
                onError={() => setError("Не вдалося увійти через Google")}
                useOneTap={false}
              />
            </div>
          </>
        )}

        <div className="mt-[32px] text-center border-t border-neutral-100 pt-[24px] font-[Halvar_Breitschrift]">
          <button
            type="button"
            onClick={() => {
              setIsRegister(!isRegister);
              setError("");
              setFormData({ email: "", password: "", confirmPassword: "" });
            }}
            className="text-xs uppercase tracking-wider text-neutral-500 hover:text-black underline underline-offset-4 transition-colors font-[Halvar_Breitschrift] bg-[white]"
          >
            {isRegister
              ? "Вже маєте акаунт? Увійти"
              : "Ще не маєте акаунту? Зареєструватися"}
          </button>
        </div>
      </div>
    </div>
  );
}
