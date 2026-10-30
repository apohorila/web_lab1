import { test, before, after, describe } from "node:test";
import assert from "node:assert/strict";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { InMemoryTransport } from "@modelcontextprotocol/sdk/inMemory.js";
import { StdioClientTransport } from "@modelcontextprotocol/sdk/client/stdio.js";
import { BagelleApi } from "../src/api.js";
import { createServer } from "../src/server.js";
import { createFakeApi, ADMIN, CUSTOMER } from "./fake-api.js";

const fake = createFakeApi();
let baseUrl;
const opened = [];

before(async () => { baseUrl = await fake.start(); });
after(async () => { for (const c of opened) await c.close(); await fake.stop(); });


async function connect({ token = "", readOnly = false } = {}) {
  const [clientSide, serverSide] = InMemoryTransport.createLinkedPair();
  await createServer(new BagelleApi({ baseUrl, token }), { readOnly }).connect(serverSide);
  const client = new Client({ name: "test", version: "1.0.0" });
  await client.connect(clientSide);
  opened.push(client);
  return client;
}
const json = (result) => JSON.parse(result.content[0].text);
const text = (result) => result.content[0].text;

describe("інструменти", () => {
  test("сервер публікує 6 читаючих і 3 записуючих інструменти з правильними підказками", async () => {
    const { tools } = await (await connect()).listTools();
    const byName = Object.fromEntries(tools.map((t) => [t.name, t]));

    const reading = ["list_products", "search_products", "get_product", "list_categories", "list_showrooms", "get_catalog_stats", "list_orders"];
    const writing = ["create_product", "update_product_price", "cancel_order"];
    assert.deepEqual(Object.keys(byName).sort(), [...reading, ...writing].sort());
    for (const n of reading) assert.equal(byName[n].annotations.readOnlyHint, true, `${n} має бути read-only`);
    for (const n of writing) assert.equal(byName[n].annotations.readOnlyHint, false, `${n} змінює дані`);
    assert.equal(byName.cancel_order.annotations.destructiveHint, true);
    for (const t of tools) assert.ok(t.description.length > 20, `${t.name}: потрібен опис для моделі`);
  });

  test("read-only режим не реєструє інструменти запису", async () => {
    const { tools } = await (await connect({ token: ADMIN, readOnly: true })).listTools();
    const names = tools.map((t) => t.name);

    assert.ok(names.includes("list_products"));
    for (const n of ["create_product", "update_product_price", "cancel_order"]) assert.ok(!names.includes(n), n);
  });
});

describe("читання (токен не потрібен)", () => {
  test("list_products: посторінково через skip/limit, фільтр категорії та hasMore", async () => {
    const client = await connect();
    fake.requests.length = 0;

    const first = json(await client.callTool({ name: "list_products", arguments: { limit: 10 } }));
    const last = json(await client.callTool({ name: "list_products", arguments: { limit: 10, skip: 20 } }));
    const cat = json(await client.callTool({ name: "list_products", arguments: { categoryId: 2, limit: 50 } }));

    assert.equal(first.total, 25);
    assert.equal(first.products.length, 10);
    assert.equal(first.hasMore, true);
    assert.equal(last.products.length, 5);
    assert.equal(last.hasMore, false);
    assert.ok(cat.products.every((p) => p.category === "Рюкзаки"));
    assert.equal(fake.requests[0].path, "/api/products?skip=0&limit=10");
  });

  test("search_products: за текстом (без регістру) і за максимальною ціною", async () => {
    const client = await connect();

    const byText = json(await client.callTool({ name: "search_products", arguments: { query: "РЮКЗАК", limit: 50 } }));
    const cheap = json(await client.callTool({ name: "search_products", arguments: { maxPrice: 1300 } }));

    assert.equal(byText.found, 12);
    assert.ok(byText.products.every((p) => p.name.startsWith("Рюкзак")));
    assert.equal(cheap.found, 4); // 1000, 1100, 1200, 1300
  });

  test("get_product: знайдений товар зі скороченими полями; неіснуючий — isError", async () => {
    const client = await connect();

    const found = json(await client.callTool({ name: "get_product", arguments: { id: 3 } }));
    const missing = await client.callTool({ name: "get_product", arguments: { id: 9999 } });

    assert.equal(found.id, 3);
    assert.equal(found.category, "Сумки");
    assert.equal(found.showroom, "Bagelle Київ");
    assert.equal(missing.isError, true);
    assert.match(text(missing), /Не знайдено/);
  });

  test("категорії, шоуруми та статистика каталогу", async () => {
    const client = await connect();

    assert.equal(json(await client.callTool({ name: "list_categories", arguments: {} })).length, 2);
    assert.equal(json(await client.callTool({ name: "list_showrooms", arguments: {} }))[0].name, "Bagelle Київ");
    assert.equal(json(await client.callTool({ name: "get_catalog_stats", arguments: {} })).totalProducts, 25);
  });

  test("недійсні аргументи відхиляються до звернення до API", async () => {
    const client = await connect();
    fake.requests.length = 0;

    const tooBig = await client.callTool({ name: "list_products", arguments: { limit: 1000 } });
    const badId = await client.callTool({ name: "get_product", arguments: { id: -1 } });

  
    for (const result of [tooBig, badId]) {
      assert.equal(result.isError, true);
      assert.match(text(result), /Input validation error/);
    }
    assert.equal(fake.requests.length, 0);
  });
});

describe("замовлення (потрібен токен)", () => {
  test("list_orders без токена — зрозуміла помилка, запиту до API немає", async () => {
    const client = await connect();
    fake.requests.length = 0;

    const result = await client.callTool({ name: "list_orders", arguments: {} });

    assert.equal(result.isError, true);
    assert.match(text(result), /BAGELLE_API_TOKEN/);
    assert.equal(fake.requests.length, 0);
  });

  test("list_orders з токеном: усі замовлення та фільтр за статусом", async () => {
    const client = await connect({ token: ADMIN });

    const all = json(await client.callTool({ name: "list_orders", arguments: {} }));
    const pending = json(await client.callTool({ name: "list_orders", arguments: { status: "Pending" } }));

    assert.equal(all.total, 2);
    assert.equal(pending.total, 1);
    assert.equal(pending.orders[0].customer, "Іван");
  });
});

describe("запис", () => {
  test("create_product надсилає токен і тіло та повертає створений товар", async () => {
    const client = await connect({ token: ADMIN });
    fake.requests.length = 0;

    const result = json(
      await client.callTool({
        name: "create_product",
        arguments: { name: "Гаманець Slim", price: 1200, categoryId: 1, showroomId: 1, description: "Тонкий гаманець" },
      }),
    );

    assert.equal(result.created, true);
    assert.equal(result.product.name, "Гаманець Slim");
    const sent = fake.requests.at(-1);
    assert.equal(sent.method, "POST");
    assert.equal(sent.token, ADMIN);
    assert.equal(sent.body.price, 1200);
    assert.equal(sent.body.categoryId, 1);
  });

  test("create_product: валідація (ціна > 0, непорожня назва) і відсутність токена", async () => {
    const admin = await connect({ token: ADMIN });
    fake.requests.length = 0;
    const negative = await admin.callTool({ name: "create_product", arguments: { name: "X", price: -5, categoryId: 1, showroomId: 1 } });
    const blank = await admin.callTool({ name: "create_product", arguments: { name: "  ", price: 5, categoryId: 1, showroomId: 1 } });
    assert.equal(negative.isError, true);
    assert.match(text(negative), /price/);
    assert.equal(blank.isError, true);
    assert.match(text(blank), /name/);
    assert.equal(fake.requests.length, 0); 

    const anonymous = await connect();
    const result = await anonymous.callTool({ name: "create_product", arguments: { name: "X", price: 5, categoryId: 1, showroomId: 1 } });
    assert.equal(result.isError, true);
    assert.match(text(result), /BAGELLE_API_TOKEN/);
  });

  test("звичайному користувачу API відмовляє (403) — це видно в помилці інструмента", async () => {
    const client = await connect({ token: CUSTOMER });

    const result = await client.callTool({ name: "create_product", arguments: { name: "X", price: 5, categoryId: 1, showroomId: 1 } });

    assert.equal(result.isError, true);
    assert.match(text(result), /адміністратора/);
  });

  test("update_product_price змінює лише ціну (інші поля зберігаються)", async () => {
    const client = await connect({ token: ADMIN });
    const before = { ...fake.products.find((p) => p.id === 2) };

    const result = json(await client.callTool({ name: "update_product_price", arguments: { id: 2, price: 4999 } }));

    assert.equal(result.oldPrice, before.price);
    assert.equal(result.newPrice, 4999);
    const after = fake.products.find((p) => p.id === 2);
    assert.equal(after.price, 4999);
    assert.equal(after.name, before.name);
    assert.equal(after.description, before.description);
    assert.equal(after.categoryId, before.categoryId);
  });

  test("update_product_price для неіснуючого товару — isError", async () => {
    const result = await (await connect({ token: ADMIN })).callTool({ name: "update_product_price", arguments: { id: 9999, price: 10 } });

    assert.equal(result.isError, true);
  });

  test("cancel_order: скасовує, а повторне скасування повертає зрозумілу помилку", async () => {
    const client = await connect({ token: ADMIN });

    const first = json(await client.callTool({ name: "cancel_order", arguments: { id: 1 } }));
    const second = await client.callTool({ name: "cancel_order", arguments: { id: 1 } });

    assert.equal(first.cancelled, true);
    assert.equal(first.order.status, "Cancelled");
    assert.equal(second.isError, true);
    assert.match(text(second), /очікує обробки/);
  });
});

describe("помилки з'єднання", () => {
  test("недоступне API — повідомлення, а не збій сервера", async () => {
    const [clientSide, serverSide] = InMemoryTransport.createLinkedPair();
    await createServer(new BagelleApi({ baseUrl: "http://127.0.0.1:9/api", timeoutMs: 1500 })).connect(serverSide);
    const client = new Client({ name: "test", version: "1.0.0" });
    await client.connect(clientSide);
    opened.push(client);

    const result = await client.callTool({ name: "list_categories", arguments: {} });

    assert.equal(result.isError, true);
    assert.match(text(result), /API недоступне/);
  });
});

describe("справжня точка входу (stdio)", () => {
  test("node src/index.js підключається за stdio, бачить інструменти й працює з API", async () => {
    const here = path.dirname(fileURLToPath(import.meta.url));
    const transport = new StdioClientTransport({
      command: process.execPath,
      args: [path.join(here, "../src/index.js")],
      env: { ...process.env, BAGELLE_API_URL: baseUrl, BAGELLE_API_TOKEN: ADMIN },
    });
    const client = new Client({ name: "stdio-test", version: "1.0.0" });
    await client.connect(transport);
    opened.push(client);

    const { tools } = await client.listTools();
    const stats = json(await client.callTool({ name: "get_catalog_stats", arguments: {} }));

    assert.equal(tools.length, 10);
    assert.ok(stats.totalProducts >= 25);
  });

  test("BAGELLE_READONLY=true: лише читаючі інструменти", async () => {
    const here = path.dirname(fileURLToPath(import.meta.url));
    const client = new Client({ name: "stdio-ro", version: "1.0.0" });
    await client.connect(
      new StdioClientTransport({
        command: process.execPath,
        args: [path.join(here, "../src/index.js")],
        env: { ...process.env, BAGELLE_API_URL: baseUrl, BAGELLE_READONLY: "true" },
      }),
    );
    opened.push(client);

    const { tools } = await client.listTools();

    assert.equal(tools.length, 7);
  });
});
