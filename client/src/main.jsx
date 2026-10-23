import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { GoogleOAuthProvider } from "@react-oauth/google";
import { AuthProvider } from "./context/AuthContext";
import { CartProvider } from "./context/CartContext";
import { FeatureProvider } from "./context/FeatureContext";
import "./index.css";
import App from "./App.jsx";

console.log("GOOGLE CLIENT ID:", import.meta.env.VITE_GOOGLE_CLIENT_ID);
createRoot(document.getElementById("root")).render(
  <StrictMode>
    <GoogleOAuthProvider clientId={import.meta.env.VITE_GOOGLE_CLIENT_ID}>
      <AuthProvider>
        <FeatureProvider>
        <CartProvider>
          <App />
        </CartProvider>
        </FeatureProvider>
      </AuthProvider>
    </GoogleOAuthProvider>
  </StrictMode>,
);
