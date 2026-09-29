import React from "react";
import { BrowserRouter, Routes, Route } from "react-router-dom";
import Navbar from "./components/NavBar";
import Catalog from "./pages/Catalog";

function App() {
  // const [count, setCount] = useState(0)

  return (
    <BrowserRouter>
      <div className="min-h-screen bg-white text-neutral-900">
        <Navbar />
        <main className="max-w-7xl mx-auto px-6 py-8">
          <Routes>
            <Route path="/" element={<Catalog />} />
          </Routes>
        </main>
      </div>
    </BrowserRouter>
  );
}

export default App;
