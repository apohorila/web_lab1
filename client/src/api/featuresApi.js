import { request } from "./client";


export const getFeatures = () => request("/features");


export const getPremiumLounge = () => request("/premium/lounge");
