import React, { createContext, useContext, useEffect, useState, useCallback } from "react";
import { useAuth } from "./AuthContext";
import { getFeatures } from "../api/featuresApi";

const FeatureContext = createContext(null);

const EMPTY = { plan: null, spent: 0, threshold: 0, flags: {}, loaded: false };

/**
 * Завантажує з API тариф клієнта та стан feature flags:
 *   isAvailable(name) — прапорець увімкнено на сервері (можна показувати розділ);
 *   isEnabled(name)   — саме цьому клієнту функція доступна (його тариф її відкриває).
 * Оновлюється при вході/виході та після refresh() (наприклад, після нового замовлення).
 */
export function FeatureProvider({ children }) {
  const { user } = useAuth();
  const userId = user?.id;

  const [state, setState] = useState(EMPTY);
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    let cancelled = false;

    getFeatures()
      .then((data) => {
        if (!cancelled) setState({ ...data, flags: data.flags || {}, loaded: true });
      })
      .catch((err) => {
        console.error("Не вдалося завантажити feature flags:", err);
        if (!cancelled) setState({ ...EMPTY, loaded: true });
      });

    return () => {
      cancelled = true;
    };
  }, [userId, reloadKey]);

  const refresh = useCallback(() => setReloadKey((k) => k + 1), []);
  const isEnabled = useCallback((name) => Boolean(state.flags[name]?.access), [state.flags]);
  const isAvailable = useCallback((name) => Boolean(state.flags[name]?.enabled), [state.flags]);

  return (
    <FeatureContext.Provider
      value={{
        plan: state.plan,
        spent: state.spent,
        threshold: state.threshold,
        loaded: state.loaded,
        isEnabled,
        isAvailable,
        refresh,
      }}
    >
      {children}
    </FeatureContext.Provider>
  );
}

export const useFeatures = () => useContext(FeatureContext);
