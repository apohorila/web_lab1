

export class ApiError extends Error {
  constructor(status, message) {
    super(message);
    this.name = "ApiError";
    this.status = status;
  }
}

export class BagelleApi {
  /**
   * @param {{ baseUrl?: string, token?: string, timeoutMs?: number }} options
   *   
   */
  constructor({ baseUrl = "http://localhost:5038/api", token = "", timeoutMs = 10000 } = {}) {
    this.baseUrl = baseUrl.replace(/\/+$/, "");
    this.token = token;
    this.timeoutMs = timeoutMs;
  }

  get hasToken() {
    return Boolean(this.token);
  }

  async request(method, path, body) {
    let response;
    try {
      response = await fetch(`${this.baseUrl}${path}`, {
        method,
        headers: {
          "Content-Type": "application/json",
          ...(this.token ? { Authorization: `Bearer ${this.token}` } : {}),
        },
        body: body === undefined ? undefined : JSON.stringify(body),
        signal: AbortSignal.timeout(this.timeoutMs),
      });
    } catch (error) {
      throw new ApiError(0, `API недоступне (${this.baseUrl}): ${error.cause?.code ?? error.message}`);
    }

    const text = await response.text();
    let data = null;
    if (text) {
      try {
        data = JSON.parse(text);
      } catch {
        data = text;
      }
    }

    if (!response.ok) {
      throw new ApiError(response.status, describeError(response.status, data));
    }
    return data;
  }

  get = (path) => this.request("GET", path);
  post = (path, body) => this.request("POST", path, body);
  put = (path, body) => this.request("PUT", path, body);
}

function describeError(status, data) {
  const message = typeof data === "string" ? data : data?.message;
  switch (status) {
    case 401:
      return "Потрібна автентифікація: вкажіть BAGELLE_API_TOKEN (JWT) у налаштуваннях MCP-сервера.";
    case 403:
      return "Недостатньо прав: для цієї дії потрібен токен адміністратора.";
    case 404:
      return message ?? "Не знайдено.";
    default:
      return `HTTP ${status}${message ? `: ${message}` : ""}`;
  }
}
