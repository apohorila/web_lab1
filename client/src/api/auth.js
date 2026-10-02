import { request } from './client'; 

export const loginUser = (data) =>
  request('/auth/login', {
    method: 'POST',
    body: JSON.stringify(data),
  });

export const registerUser = (data) =>
  request('/auth/register', {
    method: 'POST',
    body: JSON.stringify(data),
  });

export const googleLogin = (idToken) =>
  request('/auth/google', {
    method: 'POST',
    body: JSON.stringify({ idToken }),
  });