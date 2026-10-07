const API_BASE_URL = "http://localhost:5038/api";

export const TOKEN_KEY = "bagelle_token";

export async function request(endpoint, options = {}) {
  const { body, headers, ...rest } = options;
  const token = localStorage.getItem(TOKEN_KEY);

  const config = {
    method: rest.method || (body !== undefined ? "POST" : "GET"),
    ...rest,
    headers: {
      "Content-Type": "application/json",
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...headers,
    },
  };

  if (body !== undefined) {
    config.body = JSON.stringify(body);
  }

  const response = await fetch(`${API_BASE_URL}${endpoint}`, config);

  if (!response.ok) {
    const errorData = await response.json().catch(() => null);
    throw new Error(
      errorData?.message || `HTTP помилка! Статус: ${response.status}`,
    );
  }

  if (response.status === 204) {
    return null;
  }

  const text = await response.text();
  return text ? JSON.parse(text) : null;
}
