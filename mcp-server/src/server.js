import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { ApiError } from "./api.js";

const READ_ONLY = { readOnlyHint: true, destructiveHint: false, idempotentHint: true, openWorldHint: false };
const WRITE = { readOnlyHint: false, destructiveHint: false, openWorldHint: false };


const shortProduct = (p) => ({
  id: p.id,
  name: p.name,
  price: p.price,
  category: p.category?.name ?? p.categoryId,
  showroom: p.showroom?.name ?? p.showroomId,
  imageUrl: p.imageUrl,
  description: p.description ? p.description.slice(0, 300) : null,
});

const shortOrder = (o) => ({
  id: o.id,
  status: o.status,
  createdAt: o.createdAt,
  totalAmount: o.totalAmount,
  customer: o.customerName,
  city: o.deliveryCity,
  items: o.items?.map((i) => ({ productId: i.productId, name: i.product?.name, quantity: i.quantity, unitPrice: i.unitPrice })),
});

const ok = (value) => ({ content: [{ type: "text", text: JSON.stringify(value, null, 2) }] });
const fail = (message) => ({ isError: true, content: [{ type: "text", text: message }] });

const guarded = (handler) => async (args) => {
  try {
    return await handler(args);
  } catch (error) {
    if (error instanceof ApiError) return fail(error.message);
    throw error;
  }
};

const requireToken = (api, action) => {
  if (!api.hasToken) {
    throw new ApiError(401, `Щоб ${action}, потрібен токен: вкажіть BAGELLE_API_TOKEN у налаштуваннях MCP-сервера.`);
  }
};

/**
 * 
 * @param {import("./api.js").BagelleApi} api
 * @param {{ readOnly?: boolean }} options  
 */
export function createServer(api, { readOnly = false } = {}) {
  const server = new McpServer({ name: "bagelle", version: "1.0.0" });


  server.registerTool(
    "list_products",
    {
      title: "Список товарів",
      description:
        "Повертає товари магазину посторінково (skip/limit). Можна обмежити категорією. " +
        "У відповіді total — скільки всього товарів і hasMore — чи є наступна сторінка.",
      inputSchema: {
        categoryId: z.number().int().positive().optional().describe("Id категорії (див. list_categories)"),
        limit: z.number().int().min(1).max(50).default(10).describe("Скільки товарів повернути (1-50)"),
        skip: z.number().int().min(0).default(0).describe("Скільки товарів пропустити (для наступних сторінок)"),
      },
      annotations: READ_ONLY,
    },
    guarded(async ({ categoryId, limit = 10, skip = 0 }) => {
      const params = new URLSearchParams({ skip, limit });
      if (categoryId) params.set("categoryId", categoryId);
      const page = await api.get(`/products?${params}`);
      return ok({
        total: page.total,
        skip: page.skip,
        limit: page.limit,
        hasMore: Boolean(page.nextLink),
        products: page.items.map(shortProduct),
      });
    }),
  );

  server.registerTool(
    "search_products",
    {
      title: "Пошук товарів",
      description: "Шукає товари за текстом у назві чи описі (без урахування регістру) та/або за максимальною ціною в гривнях.",
      inputSchema: {
        query: z.string().min(1).optional().describe("Текст для пошуку, напр. «рюкзак»"),
        maxPrice: z.number().positive().optional().describe("Максимальна ціна, ₴"),
        limit: z.number().int().min(1).max(50).default(10),
      },
      annotations: READ_ONLY,
    },
    guarded(async ({ query, maxPrice, limit = 10 }) => {
      const all = await api.get("/products");
      const needle = query?.toLowerCase();
      const found = all.filter(
        (p) =>
          (!needle || p.name.toLowerCase().includes(needle) || (p.description ?? "").toLowerCase().includes(needle)) &&
          (maxPrice === undefined || p.price <= maxPrice),
      );
      return ok({ found: found.length, shown: Math.min(found.length, limit), products: found.slice(0, limit).map(shortProduct) });
    }),
  );

  server.registerTool(
    "get_product",
    {
      title: "Деталі товару",
      description: "Повертає один товар за id: ціна, опис, категорія, шоурум, фото.",
      inputSchema: { id: z.number().int().positive().describe("Id товару") },
      annotations: READ_ONLY,
    },
    guarded(async ({ id }) => ok(shortProduct(await api.get(`/products/${id}`)))),
  );

  server.registerTool(
    "list_categories",
    { title: "Категорії", description: "Повертає всі категорії товарів (id та назва).", inputSchema: {}, annotations: READ_ONLY },
    guarded(async () => ok(await api.get("/categories"))),
  );

  server.registerTool(
    "list_showrooms",
    {
      title: "Шоуруми",
      description: "Повертає шоуруми: назва, адреса, телефон, графік роботи, координати.",
      inputSchema: {},
      annotations: READ_ONLY,
    },
    guarded(async () => ok(await api.get("/showrooms"))),
  );

  server.registerTool(
    "get_catalog_stats",
    {
      title: "Статистика каталогу",
      description: "Зведення по каталогу: кількість товарів, середня/мін./макс. ціна, товари за категоріями та шоурумами.",
      inputSchema: {},
      annotations: READ_ONLY,
    },
    guarded(async () => ok(await api.get("/stats/catalog"))),
  );

  server.registerTool(
    "list_orders",
    {
      title: "Замовлення",
      description:
        "Повертає замовлення (потрібен токен; адміністратору — усі, звичайному користувачу — його власні). " +
        "Можна відфільтрувати за статусом: Pending або Cancelled.",
      inputSchema: {
        status: z.enum(["Pending", "Cancelled"]).optional().describe("Статус замовлення"),
        limit: z.number().int().min(1).max(50).default(10),
      },
      annotations: READ_ONLY,
    },
    guarded(async ({ status, limit = 10 }) => {
      requireToken(api, "переглянути замовлення");
      const orders = (await api.get("/orders")).filter((o) => !status || o.status === status);
      return ok({ total: orders.length, shown: Math.min(orders.length, limit), orders: orders.slice(0, limit).map(shortOrder) });
    }),
  );

  if (readOnly) return server;

 

  server.registerTool(
    "create_product",
    {
      title: "Створити товар",
      description:
        "Додає новий товар до каталогу (лише адміністратор). Потрібні існуючі categoryId і showroomId — " +
        "дізнайтесь їх через list_categories та list_showrooms.",
      inputSchema: {
        name: z.string().trim().min(1).max(200).describe("Назва товару"),
        price: z.number().positive().describe("Ціна, ₴"),
        categoryId: z.number().int().positive().describe("Id категорії"),
        showroomId: z.number().int().positive().describe("Id шоуруму"),
        description: z.string().max(2000).optional().describe("Опис товару"),
        imageUrl: z.string().url().optional().describe("Посилання на фото"),
      },
      annotations: WRITE,
    },
    guarded(async ({ name, price, categoryId, showroomId, description, imageUrl }) => {
      requireToken(api, "створити товар");
      const created = await api.post("/products", {
        name,
        price,
        categoryId,
        showroomId,
        description: description ?? null,
        imageUrl: imageUrl ?? "",
      });
      return ok({ created: true, product: shortProduct(created) });
    }),
  );

  server.registerTool(
    "update_product_price",
    {
      title: "Змінити ціну товару",
      description: "Змінює ціну існуючого товару (лише адміністратор). Інші поля товару не змінюються.",
      inputSchema: {
        id: z.number().int().positive().describe("Id товару"),
        price: z.number().positive().describe("Нова ціна, ₴"),
      },
      annotations: { ...WRITE, idempotentHint: true },
    },
    guarded(async ({ id, price }) => {
      requireToken(api, "змінити ціну");
      const current = await api.get(`/products/${id}`);
      const updated = await api.put(`/products/${id}`, {
        id: current.id,
        name: current.name,
        price,
        description: current.description,
        imageUrl: current.imageUrl,
        categoryId: current.categoryId,
        showroomId: current.showroomId,
      });
      return ok({ updated: true, id, oldPrice: current.price, newPrice: updated?.price ?? price });
    }),
  );

  server.registerTool(
    "cancel_order",
    {
      title: "Скасувати замовлення",
      description:
        "Скасовує замовлення зі статусом Pending (власник замовлення або адміністратор). " +
        "Уже скасоване замовлення повторно скасувати не можна.",
      inputSchema: { id: z.number().int().positive().describe("Id замовлення") },
      annotations: { ...WRITE, destructiveHint: true },
    },
    guarded(async ({ id }) => {
      requireToken(api, "скасувати замовлення");
      const order = await api.post(`/orders/${id}/cancel`);
      return ok({ cancelled: true, order: shortOrder(order) });
    }),
  );

  return server;
}
