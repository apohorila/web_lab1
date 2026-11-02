import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { GoogleOAuthProvider } from "@react-oauth/google";
import { AuthProvider } from "./context/AuthContext";
import { CartProvider } from "./context/CartContext";
import { FeatureProvider } from "./context/FeatureContext";
import ReactGA from "react-ga4";
import "./index.css";
import App from "./App.jsx";

ReactGA.initialize("G-1234567890", {
  gaOptions: {
    debug_mode: true,
  },
});
ReactGA.send({ hitType: "pageview", page: window.location.pathname });

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
