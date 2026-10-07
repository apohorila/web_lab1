import { request } from "./client";

export const loginUser = (data) =>
  request("/auth/login", {
    method: "POST",
    body: data,
  });

export const registerUser = (data) =>
  request("/auth/register", {
    method: "POST",
    body: data,
  });

export const googleLogin = (idToken) =>
  request("/auth/google", {
    method: "POST",
    body: { idToken },
  });
