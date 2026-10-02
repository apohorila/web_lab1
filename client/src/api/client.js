const API_BASE_URL = "http://localhost:5038/api";

export async function request(endpoint, options = {}) {
  const { body, ...customConfig } = options;

  const config = {
    method: customConfig.method || (body ? "POST" : "GET"),
    headers: {
      "Content-Type": "application/json",
      ...customConfig.headers,
    },
    ...customConfig,
  };

  if (body) {
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

  return response.json();
}
