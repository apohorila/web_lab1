
import http from "node:http";

export const ADMIN = "admin-token";
export const CUSTOMER = "customer-token";

export function createFakeApi() {
  const categories = [{ id: 1, name: "Сумки" }, { id: 2, name: "Рюкзаки" }];
  const showrooms = [{ id: 1, name: "Bagelle Київ", address: "м. Київ, вул. Хрещатик, 10", phone: "+380441112233", workingHours: "10:00-21:00", latitude: 50.45, longitude: 30.52 }];
  const products = Array.from({ length: 25 }, (_, i) => ({
    id: i + 1,
    name: i % 2 ? `Рюкзак Trek ${i + 1}` : `Сумка Alma ${i + 1}`,
    price: 1000 + i * 100,
    description: i % 2 ? "Міський рюкзак зі шкіри" : "Класична шкіряна сумка",
    imageUrl: `https://cdn.example.com/${i + 1}.jpg`,
    categoryId: i % 2 ? 2 : 1,
    showroomId: 1,
  }));
  const orders = [
    { id: 1, status: "Pending", createdAt: "2026-10-01T10:00:00Z", totalAmount: 3600, customerName: "Іван", deliveryCity: "Київ", items: [{ productId: 1, quantity: 1, unitPrice: 3600 }] },
    { id: 2, status: "Cancelled", createdAt: "2026-10-02T10:00:00Z", totalAmount: 2900, customerName: "Олена", deliveryCity: "Львів", items: [] },
  ];
  const requests = []; 

  const out = (p) => ({ ...p, category: categories.find((c) => c.id === p.categoryId), showroom: showrooms[0] });

  const server = http.createServer((req, res) => {
    let raw = "";
    req.on("data", (c) => (raw += c));
    req.on("end", () => {
      const url = new URL(req.url, "http://x");
      const body = raw ? JSON.parse(raw) : undefined;
      const token = (req.headers.authorization || "").replace("Bearer ", "");
      requests.push({ method: req.method, path: url.pathname + url.search, token, body });

      const send = (code, data) => {
        res.writeHead(code, { "Content-Type": "application/json" });
        res.end(data === undefined ? "" : JSON.stringify(data));
      };
      const needAuth = () => { if (!token) { send(401); return false; } return true; };
      const needAdmin = () => { if (!needAuth()) return false; if (token !== ADMIN) { send(403); return false; } return true; };
      let m;

      if (url.pathname === "/api/categories") return send(200, categories);
      if (url.pathname === "/api/showrooms") return send(200, showrooms);
      if (url.pathname === "/api/stats/catalog") return send(200, { totalProducts: products.length, averagePrice: 2200, productsPerCategory: categories.map((c) => ({ name: c.name, count: products.filter((p) => p.categoryId === c.id).length })) });

      if (req.method === "GET" && url.pathname === "/api/products") {
        const cat = url.searchParams.get("categoryId");
        const list = products.filter((p) => !cat || p.categoryId === Number(cat));
        if (!url.searchParams.has("skip") && !url.searchParams.has("limit")) return send(200, list.map(out));
        const skip = Number(url.searchParams.get("skip") ?? 0), limit = Number(url.searchParams.get("limit") ?? 12);
        const next = skip + limit < list.length ? `http://x/api/products?skip=${skip + limit}&limit=${limit}` : null;
        return send(200, { items: list.slice(skip, skip + limit).map(out), total: list.length, skip, limit, nextLink: next });
      }
      if (req.method === "POST" && url.pathname === "/api/products") {
        if (!needAdmin()) return;
        const p = { ...body, id: products.length + 1 };
        products.push(p);
        return send(201, p);
      }
      if ((m = url.pathname.match(/^\/api\/products\/(\d+)$/))) {
        const p = products.find((x) => x.id === Number(m[1]));
        if (req.method === "GET") return p ? send(200, out(p)) : send(404);
        if (req.method === "PUT") {
          if (!needAdmin()) return;
          if (!p) return send(404);
          Object.assign(p, body);
          return send(200, p);
        }
      }
      if (req.method === "GET" && url.pathname === "/api/orders") { if (!needAuth()) return; return send(200, orders); }
      if (req.method === "POST" && (m = url.pathname.match(/^\/api\/orders\/(\d+)\/cancel$/))) {
        if (!needAuth()) return;
        const o = orders.find((x) => x.id === Number(m[1]));
        if (!o) return send(404, { message: "Замовлення не знайдено" });
        if (o.status !== "Pending") return send(400, { message: "Скасувати можна лише замовлення, що очікує обробки" });
        o.status = "Cancelled";
        return send(200, o);
      }
      send(404, { message: "not found" });
    });
  });

  return {
    products, orders, requests,
    start: () => new Promise((resolve) => server.listen(0, "127.0.0.1", () => resolve(`http://127.0.0.1:${server.address().port}/api`))),
    stop: () => new Promise((resolve) => server.close(resolve)),
  };
}
